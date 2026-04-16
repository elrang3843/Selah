#!/usr/bin/env python3
"""
onnx_runner.py  —  Selah 음원 분리 (ONNX Runtime + FFmpeg)

MrCitron/demucs-v4-onnx 에서 배포된 htdemucs ONNX 모델을
ONNX Runtime으로 추론하고, 오디오 I/O는 FFmpeg로 처리합니다.

사용법:
    python onnx_runner.py \\
        --input  <입력 파일 경로> \\
        --output <출력 폴더 경로> \\
        --model  <htdemucs | htdemucs_ft> \\
        --stems  <2 | 4>

출력 (stdout):
    PROGRESS:<0~100>      진행률
    STEM:<key>=<경로>     스템 WAV 완료
    LOG:<메시지>          상태/오류

──────────────────────────────────────────────────────────────
라이선스 고지 (License Notices)
──────────────────────────────────────────────────────────────
본 스크립트 자체:
    GNU General Public License v2 (GPL v2)
    Copyright (C) Selah 프로젝트

사용 라이브러리:
    onnxruntime   MIT License — Copyright (C) Microsoft Corporation
    numpy         BSD 3-Clause License — Copyright (C) NumPy Developers
    scipy         BSD 3-Clause License — Copyright (C) SciPy Developers
    FFmpeg        GNU Lesser General Public License v2.1+ (LGPL v2.1+)
                  <https://ffmpeg.org/legal.html>

모델 가중치 (htdemucs / htdemucs_ft):
    MIT License — Copyright (C) Meta AI Research (Alexandre Défossez 외)
    원본 코드: <https://github.com/facebookresearch/demucs>
    ONNX 내보내기: MrCitron (HuggingFace: MrCitron/demucs-v4-onnx)
    내보내기 형식 자체는 별도 라이선스 표기 없음;
    가중치의 MIT 라이선스가 적용되는 것으로 간주합니다.

주의:
    처리 대상 음원의 저작권은 사용자가 직접 확인해야 합니다.
    CCL 여부, 원저작자 허락 여부를 먼저 확인하십시오.
──────────────────────────────────────────────────────────────
"""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
from pathlib import Path

try:
    import numpy as np
except ImportError:
    print("LOG:ONNX_RUNTIME_MISSING", flush=True)
    sys.exit(1)

# ── htdemucs 기본 파라미터 ────────────────────────────────────
SAMPLE_RATE   = 44_100
N_FFT         = 4_096
HOP_LENGTH    = N_FFT // 4                        # 1024
N_BINS        = N_FFT // 2                        # 2048 (마지막 bin 제거)
CHUNK_SAMPLES = int(7.8 * SAMPLE_RATE)            # 343 980  (~7.8 초)
OVERLAP       = 0.25                              # 25 % 겹침
HOP_SAMPLES   = int(CHUNK_SAMPLES * (1 - OVERLAP))  # 257 985

# 4-stem 순서 (htdemucs 기본값)
STEMS_4 = ["drums", "bass", "other", "vocals"]
STEMS_2 = ["vocals", "no_vocals"]


# ─────────────────────────────────────────────────────────────
# 진행률 / 로그
# ─────────────────────────────────────────────────────────────

def log_progress(value: float) -> None:
    print(f"PROGRESS:{value:.1f}", flush=True)


def log(msg: str) -> None:
    print(f"LOG:{msg}", flush=True)


# ─────────────────────────────────────────────────────────────
# FFmpeg 오디오 I/O
# ─────────────────────────────────────────────────────────────

def read_audio(path: str) -> np.ndarray:
    """FFmpeg로 오디오 파일을 float32 스테레오 [2, samples]로 디코딩."""
    cmd = [
        "ffmpeg", "-i", path,
        "-f", "f32le",
        "-ar", str(SAMPLE_RATE),
        "-ac", "2",
        "-vn",           # 비디오 스트림 무시
        "pipe:1",
        "-loglevel", "error",
    ]
    proc = subprocess.run(cmd, capture_output=True)
    if proc.returncode != 0:
        raise RuntimeError(
            f"FFmpeg 디코딩 실패 (코드 {proc.returncode}):\n"
            f"{proc.stderr.decode(errors='replace')}"
        )
    if not proc.stdout:
        raise RuntimeError("FFmpeg가 오디오 데이터를 출력하지 않았습니다.")
    audio = np.frombuffer(proc.stdout, dtype=np.float32)
    return np.ascontiguousarray(audio.reshape(-1, 2).T)  # [2, samples]


def write_audio(path: str, audio: np.ndarray) -> None:
    """float32 스테레오 [2, samples] → 16-bit PCM WAV (FFmpeg)."""
    pcm = np.ascontiguousarray(audio.T, dtype=np.float32).tobytes()
    cmd = [
        "ffmpeg",
        "-f", "f32le",
        "-ar", str(SAMPLE_RATE),
        "-ac", "2",
        "-i", "pipe:0",
        "-c:a", "pcm_s16le",
        "-y", path,
        "-loglevel", "error",
    ]
    proc = subprocess.run(cmd, input=pcm, capture_output=True)
    if proc.returncode != 0:
        raise RuntimeError(
            f"FFmpeg 인코딩 실패 (코드 {proc.returncode}):\n"
            f"{proc.stderr.decode(errors='replace')}"
        )


# ─────────────────────────────────────────────────────────────
# STFT  (torch.stft 재현 — htdemucs _spec() 와 동일)
# ─────────────────────────────────────────────────────────────

def _stft_channel(x: np.ndarray) -> np.ndarray:
    """
    단일 채널 STFT.
    torch.stft(normalized=True, center=True, pad_mode='reflect') 와 일치.

    x       : [samples] float32
    반환값  : [N_BINS, T] complex64   (N_BINS = N_FFT//2, 마지막 bin 제거)
    """
    window = np.hanning(N_FFT).astype(np.float32)
    norm   = float(np.sqrt(np.sum(window ** 2)))   # torch normalized=True 계수

    # center=True : 양쪽에 N_FFT//2 샘플 reflect 패딩
    x_pad = np.pad(x, N_FFT // 2, mode="reflect")

    n_frames = (len(x_pad) - N_FFT) // HOP_LENGTH + 1
    idx      = (
        np.arange(N_FFT)[np.newaxis, :]
        + np.arange(n_frames)[:, np.newaxis] * HOP_LENGTH
    )  # [n_frames, N_FFT]
    frames = x_pad[idx] * window[np.newaxis, :]    # [n_frames, N_FFT]

    Z = np.fft.rfft(frames, n=N_FFT, axis=-1).T    # [N_FFT//2+1, n_frames]
    Z = (Z / norm).astype(np.complex64)
    return Z[:N_BINS, :]                            # [N_BINS, T]  마지막 bin 제거


def compute_stft(chunk: np.ndarray) -> np.ndarray:
    """
    스테레오 STFT.
    chunk   : [2, CHUNK_SAMPLES] float32
    반환값  : [1, 4, N_BINS, T] float32
              채널 순서: [ch0_real, ch0_imag, ch1_real, ch1_imag]
    """
    parts: list[np.ndarray] = []
    for ch in range(2):
        Z = _stft_channel(chunk[ch])
        parts.append(Z.real.astype(np.float32))
        parts.append(Z.imag.astype(np.float32))
    return np.stack(parts, axis=0)[np.newaxis]      # [1, 4, N_BINS, T]


# ─────────────────────────────────────────────────────────────
# ISTFT  (htdemucs _ispec() 재현 — 모델이 스펙트로그램 출력 시 사용)
# ─────────────────────────────────────────────────────────────

def _istft_channel(Z: np.ndarray, length: int) -> np.ndarray:
    """
    단일 채널 ISTFT.
    Z      : [N_BINS, T] complex64
    length : 원본 샘플 수
    반환값 : [length] float32
    """
    window = np.hanning(N_FFT).astype(np.float32)
    norm   = float(np.sqrt(np.sum(window ** 2)))

    # 마지막 bin 복원 (0으로 채움)
    Z_full = np.concatenate(
        [Z * norm, np.zeros((1, Z.shape[1]), dtype=Z.dtype)], axis=0
    )  # [N_FFT//2+1, T]

    frames = np.fft.irfft(Z_full.T, n=N_FFT, axis=-1) * window[np.newaxis, :]  # [T, N_FFT]

    n_frames = frames.shape[0]
    total    = (n_frames - 1) * HOP_LENGTH + N_FFT
    out      = np.zeros(total, dtype=np.float32)
    wsum     = np.zeros(total, dtype=np.float32)
    w2       = window ** 2

    for i in range(n_frames):
        s = i * HOP_LENGTH
        out[s: s + N_FFT]  += frames[i]
        wsum[s: s + N_FFT] += w2

    pad = N_FFT // 2
    out  = out[pad: pad + length]
    wsum = wsum[pad: pad + length]
    return (out / np.maximum(wsum, 1e-8)).astype(np.float32)


# ─────────────────────────────────────────────────────────────
# ONNX 추론
# ─────────────────────────────────────────────────────────────

def _prepare_feeds(
    session,             # ort.InferenceSession
    input_names: list[str],
    chunk: np.ndarray,
) -> dict[str, np.ndarray]:
    """
    모델 입력 수에 따라 feed dict 구성.
      - 1-input 모델: STFT 내장 → waveform만
      - 2-input 모델: STFT 외부 → waveform + 스펙트로그램
    """
    mix_batch = chunk[np.newaxis].astype(np.float32)   # [1, 2, CHUNK_SAMPLES]
    if len(input_names) == 1:
        return {input_names[0]: mix_batch}
    spec = compute_stft(chunk)                          # [1, 4, N_BINS, T]
    return {input_names[0]: mix_batch, input_names[1]: spec}


def run_chunk(
    session,
    input_names: list[str],
    chunk: np.ndarray,
) -> np.ndarray:
    """
    청크 1개 추론.
    반환값: [n_stems, 2, CHUNK_SAMPLES] float32  (time-domain)
            또는 [n_stems, 4, N_BINS, T] float32  (스펙트로그램 출력 시)
    """
    feeds = _prepare_feeds(session, input_names, chunk)
    raw   = session.run(None, feeds)[0]    # [1, ...]
    return raw[0]                          # [n_stems, ...]


def _to_timedomain(
    out: np.ndarray,
    chunk_samples: int,
) -> np.ndarray:
    """
    출력 텐서를 time-domain [n_stems, 2, chunk_samples]로 변환.
    time-domain이면 그대로, 스펙트로그램이면 ISTFT 적용.
    """
    # [n_stems, 2, chunk_samples] — time-domain 직접 출력
    if out.ndim == 3 and out.shape[1] == 2 and out.shape[2] == chunk_samples:
        return out

    # [n_stems, 4, N_BINS, T] — complex-as-channels 스펙트로그램 출력
    if out.ndim == 4 and out.shape[2] == N_BINS:
        n_stems = out.shape[0]
        result  = np.zeros((n_stems, 2, chunk_samples), dtype=np.float32)
        for s in range(n_stems):
            for ch in range(2):
                real = out[s, ch * 2,     :, :]
                imag = out[s, ch * 2 + 1, :, :]
                Z    = (real + 1j * imag).astype(np.complex64)
                result[s, ch] = _istft_channel(Z, chunk_samples)
        return result

    # 알 수 없는 형식 — 그대로 반환 (진단 로그로 형식 확인 가능)
    return out


# ─────────────────────────────────────────────────────────────
# Overlap-Add 재구성
# ─────────────────────────────────────────────────────────────

def overlap_add(
    chunks_out: list[np.ndarray],
    total_samples: int,
) -> np.ndarray:
    """
    Hann 크로스페이드를 적용한 Overlap-Add.
    선형 페이드 대비 청크 경계에서 더 부드러운 전환을 제공합니다.
    chunks_out : list of [n_stems, 2, CHUNK_SAMPLES]
    반환값     : [n_stems, 2, total_samples]
    """
    n_stems  = chunks_out[0].shape[0]
    out      = np.zeros((n_stems, 2, total_samples), dtype=np.float32)
    norm     = np.zeros(total_samples, dtype=np.float32)

    # Hann 페이드: 0→1 (fade-in), 1→0 (fade-out) — 반코사인 곡선
    fade_len = CHUNK_SAMPLES - HOP_SAMPLES
    t        = np.arange(fade_len, dtype=np.float32)
    fade_in  = 0.5 * (1.0 - np.cos(np.pi * t / fade_len))
    fade_out = fade_in[::-1].copy()

    win = np.ones(CHUNK_SAMPLES, dtype=np.float32)
    win[:fade_len]                 = fade_in
    win[CHUNK_SAMPLES - fade_len:] = fade_out

    for i, chunk in enumerate(chunks_out):
        start = i * HOP_SAMPLES
        end   = min(start + CHUNK_SAMPLES, total_samples)
        n     = end - start
        w     = win[:n]
        out[:, :, start:end] += chunk[:, :, :n] * w[np.newaxis, np.newaxis, :]
        norm[start:end]       += w

    return out / np.maximum(norm, 1e-8)[np.newaxis, np.newaxis, :]


# ─────────────────────────────────────────────────────────────
# 메인 파이프라인
# ─────────────────────────────────────────────────────────────

def separate(
    input_path: str,
    output_dir: str,
    model_path: str,
    n_stems: int,
) -> int:
    try:
        import onnxruntime as ort
    except ImportError:
        log("ONNX_RUNTIME_MISSING")
        return 1

    # ── 모델 로드 ──────────────────────────────────────────────
    log("ONNX 모델 로딩 중...")
    log_progress(2.0)

    sess_opts = ort.SessionOptions()
    sess_opts.log_severity_level = 3          # WARNING 이상만 출력
    try:
        session = ort.InferenceSession(
            model_path,
            sess_options=sess_opts,
            providers=ort.get_available_providers(),
        )
    except Exception as e:
        log(f"모델 로드 실패: {e}")
        return 1

    input_names  = [i.name for i in session.get_inputs()]
    output_names = [o.name for o in session.get_outputs()]

    # 진단: 입출력 형식 로그 (처음 실행 시 유용)
    for inp in session.get_inputs():
        log(f"[DIAG] 입력  '{inp.name}' : {inp.shape} {inp.type}")
    for out in session.get_outputs():
        log(f"[DIAG] 출력  '{out.name}' : {out.shape} {out.type}")

    log_progress(5.0)

    # ── 오디오 디코딩 ──────────────────────────────────────────
    log("오디오 디코딩 중...")
    try:
        audio = read_audio(input_path)      # [2, total_samples]
    except RuntimeError as e:
        log(str(e))
        return 1

    total_samples = audio.shape[1]
    log_progress(10.0)

    # ── 청크 분할 ──────────────────────────────────────────────
    chunks_in: list[np.ndarray] = []
    pos = 0
    while pos < total_samples:
        end   = min(pos + CHUNK_SAMPLES, total_samples)
        chunk = audio[:, pos:end]
        if chunk.shape[1] < CHUNK_SAMPLES:           # 마지막 청크 제로패딩
            chunk = np.pad(chunk, ((0, 0), (0, CHUNK_SAMPLES - chunk.shape[1])))
        chunks_in.append(np.ascontiguousarray(chunk))
        pos += HOP_SAMPLES

    # ── 청크별 추론 ────────────────────────────────────────────
    log("스템 분리 중...")
    chunks_out: list[np.ndarray] = []

    for i, chunk in enumerate(chunks_in):
        try:
            # 청크 RMS 정규화: 모델을 훈련 범위 내에서 동작시켜 분리 품질 향상
            rms = float(np.sqrt(np.mean(chunk ** 2)))
            if rms > 1e-6:
                chunk_norm = chunk / rms
            else:
                chunk_norm = chunk
                rms = 1.0

            raw = run_chunk(session, input_names, chunk_norm)
            td  = _to_timedomain(raw, CHUNK_SAMPLES)
            # 정규화 해제: 원래 음량으로 복원
            chunks_out.append(td * rms)
        except Exception as e:
            log(f"청크 {i} 추론 오류: {e}")
            return 1

        pct = 10.0 + (i + 1) / len(chunks_in) * 82.0
        log_progress(pct)

    # ── Overlap-Add 재구성 ────────────────────────────────────
    log("스템 합성 중...")
    stems_audio = overlap_add(chunks_out, total_samples)
    # stems_audio: [n_stems, 2, total_samples]

    log_progress(95.0)

    # ── 스템 저장 ──────────────────────────────────────────────
    stem_names = STEMS_4 if n_stems == 4 else STEMS_2

    # 2-stem: 보컬 + (나머지 합계 = no_vocals)
    if n_stems == 2 and stems_audio.shape[0] == 4:
        vocals_idx = STEMS_4.index("vocals")
        vocals     = stems_audio[vocals_idx]
        no_vocals  = sum(
            stems_audio[i] for i in range(4) if i != vocals_idx
        )
        stems_to_write = [("vocals", vocals), ("no_vocals", no_vocals)]
    else:
        stems_to_write = [
            (name, stems_audio[idx])
            for idx, name in enumerate(stem_names)
            if idx < stems_audio.shape[0]
        ]

    for name, stem_audio in stems_to_write:
        path = str(Path(output_dir) / f"{name}.wav")
        try:
            write_audio(path, stem_audio)
        except RuntimeError as e:
            log(str(e))
            return 1
        print(f"STEM:{name}={path}", flush=True)

    log_progress(100.0)
    return 0


# ─────────────────────────────────────────────────────────────
# 진입점
# ─────────────────────────────────────────────────────────────

def main() -> int:
    parser = argparse.ArgumentParser(
        description="Selah ONNX 음원 분리기 (htdemucs / htdemucs_ft)"
    )
    parser.add_argument("--input",  required=True, help="입력 오디오 파일 경로")
    parser.add_argument("--output", required=True, help="출력 폴더 경로")
    parser.add_argument(
        "--model", required=True,
        choices=["htdemucs", "htdemucs_ft"],
        help="모델 ID",
    )
    parser.add_argument(
        "--stems", type=int, default=4, choices=[2, 4],
        help="출력 스템 수 (2=보컬/반주, 4=드럼/베이스/기타/보컬)",
    )
    args = parser.parse_args()

    if not os.path.isfile(args.input):
        log(f"입력 파일을 찾을 수 없습니다: {args.input}")
        return 1

    models_dir = os.environ.get(
        "SELAH_MODELS_DIR",
        os.path.join(
            os.environ.get("APPDATA", os.path.expanduser("~")),
            "Selah", "models",
        ),
    )
    model_path = os.path.join(models_dir, f"{args.model}.onnx")

    if not os.path.isfile(model_path):
        log(f"ONNX_MODEL_MISSING:{args.model}")
        return 1

    os.makedirs(args.output, exist_ok=True)

    try:
        return separate(args.input, args.output, model_path, args.stems)
    except Exception as e:
        log(str(e))
        return 1


if __name__ == "__main__":
    sys.exit(main())
