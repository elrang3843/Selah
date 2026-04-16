#!/usr/bin/env python3
"""
audio_separator_runner.py — Selah 보컬 분리 스크립트 (MDX-Net)

audio-separator 패키지를 래핑합니다.
UVR MDX-NET-Voc_FT 등의 MDX-Net 모델을 사용하여 보컬/반주를 분리합니다.
Demucs보다 음역대·음색에 관계없이 모든 목소리를 더 정확하게 인식합니다.

출력 프로토콜 (stdout):
    PROGRESS:<0~100>        — 진행률
    STEM:<key>=<abs_path>   — 스템 파일 준비 완료
    LOG:<message>           — 진단 / 오류 메시지

오류 센티넬:
    LOG:AUDIO_SEPARATOR_MISSING  — audio-separator 패키지 미설치

사용법:
    python audio_separator_runner.py \\
        --input  <입력 WAV 경로> \\
        --output <출력 폴더> \\
        --model  <모델 파일명>  (기본값: UVR-MDX-NET-Voc_FT.onnx)

라이선스:
    이 스크립트 자체는 GPLv3 (셀라 프로젝트 전체 라이선스 준수).
    audio-separator 패키지: MIT License (nomadkaraoke).
    UVR MDX-Net 모델 가중치: MIT License (Anjok07 / Ultimate Vocal Remover).
"""

import argparse
import os
import sys
from pathlib import Path


def log(msg: str) -> None:
    print(f"LOG:{msg}", flush=True)


def report_progress(pct: float) -> None:
    print(f"PROGRESS:{pct:.1f}", flush=True)


def find_stem_files(
    output_dir: Path,
    candidates: list,
) -> tuple["Path | None", "Path | None"]:
    """
    출력 파일 목록에서 vocals 파일과 instrumental 파일을 찾습니다.
    audio-separator 버전에 따라 출력 파일명이 다를 수 있으므로
    여러 패턴을 시도합니다.
    """
    vocals_src: "Path | None" = None
    no_vocals_src: "Path | None" = None

    def classify(p: Path) -> str:
        nl = p.name.lower()
        # '(instrumental)' or '(no vocals)' → no_vocals
        if "(instrumental)" in nl or "(no vocals)" in nl:
            return "no_vocals"
        # '(vocals)' without instrumental qualifier → vocals
        if "(vocals)" in nl:
            return "vocals"
        return ""

    for item in candidates:
        p = Path(item) if os.path.isabs(str(item)) else output_dir / item
        kind = classify(p)
        if kind == "vocals" and vocals_src is None:
            vocals_src = p
        elif kind == "no_vocals" and no_vocals_src is None:
            no_vocals_src = p

    # Fallback: scan output directory
    if vocals_src is None or no_vocals_src is None:
        for wav in sorted(output_dir.glob("*.wav")):
            kind = classify(wav)
            if kind == "vocals" and vocals_src is None:
                vocals_src = wav
            elif kind == "no_vocals" and no_vocals_src is None:
                no_vocals_src = wav

    return vocals_src, no_vocals_src


def main() -> int:
    parser = argparse.ArgumentParser(description="Selah audio-separator MDX-Net 보컬 분리")
    parser.add_argument("--input",  required=True, help="입력 WAV 파일 경로")
    parser.add_argument("--output", required=True, help="출력 폴더 경로")
    parser.add_argument(
        "--model",
        default="UVR-MDX-NET-Voc_FT.onnx",
        help="모델 파일명 (audio-separator 캐시 폴더 내 파일명)",
    )
    args = parser.parse_args()

    if not os.path.isfile(args.input):
        log(f"입력 파일 없음: {args.input}")
        return 1

    # audio_separator 설치 확인
    try:
        from audio_separator.separator import Separator  # noqa: F401
    except ImportError:
        log("AUDIO_SEPARATOR_MISSING")
        return 1

    os.makedirs(args.output, exist_ok=True)
    output_dir = Path(args.output)

    report_progress(5.0)
    log(f"모델 로드 중: {args.model}")

    try:
        from audio_separator.separator import Separator

        sep = Separator(
            output_dir=str(output_dir),
            output_format="WAV",
        )

        report_progress(10.0)
        sep.load_model(model_filename=args.model)
        report_progress(20.0)

        log(f"분리 시작: {Path(args.input).name}")
        output_files = sep.separate(args.input)
        report_progress(90.0)

    except Exception as exc:
        log(f"오류: {exc}")
        return 2

    # --- 출력 파일 매핑 ---
    vocals_src, no_vocals_src = find_stem_files(output_dir, output_files or [])

    if vocals_src is None or no_vocals_src is None:
        existing = [f.name for f in output_dir.glob("*.wav")]
        log(f"출력 파일 매핑 실패. 생성된 파일: {existing}")
        return 3

    # 표준 파일명으로 이동
    vocals_out    = output_dir / "vocals.wav"
    no_vocals_out = output_dir / "no_vocals.wav"

    if vocals_src.resolve() != vocals_out.resolve():
        vocals_src.rename(vocals_out)
    if no_vocals_src.resolve() != no_vocals_out.resolve():
        # no_vocals_src may have been renamed to vocals_out above if they were
        # the same path — guard against that edge case
        src = no_vocals_src if no_vocals_src.exists() else Path(
            str(no_vocals_src).replace(vocals_src.name, no_vocals_src.name)
        )
        if src.exists():
            src.rename(no_vocals_out)

    # 남은 중간 파일 정리
    for wav in list(output_dir.glob("*.wav")):
        try:
            if wav.resolve() not in (vocals_out.resolve(), no_vocals_out.resolve()):
                wav.unlink()
        except OSError:
            pass

    if not vocals_out.exists() or not no_vocals_out.exists():
        log("파일 이동 후 출력 파일을 확인할 수 없습니다.")
        return 4

    print(f"STEM:vocals={vocals_out}", flush=True)
    print(f"STEM:no_vocals={no_vocals_out}", flush=True)
    report_progress(100.0)
    log("분리 완료: vocals.wav + no_vocals.wav")
    return 0


if __name__ == "__main__":
    sys.exit(main())
