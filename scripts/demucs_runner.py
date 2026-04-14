#!/usr/bin/env python3
"""
demucs_runner.py  —  셀라(Selah) MR 추출 스크립트

Demucs CLI를 래핑하여 C# 애플리케이션에서 호출합니다.
진행률은 stdout에 "PROGRESS:<0~100>" 형식으로 출력됩니다.

사용법:
    python demucs_runner.py \
        --input  <입력 WAV 파일 경로> \
        --output <출력 폴더 경로> \
        --model  <모델 ID: htdemucs | htdemucs_ft | mdx_extra> \
        --stems  <2 | 4>

출력:
    --stems 2 : vocals.wav, no_vocals.wav
    --stems 4 : vocals.wav, drums.wav, bass.wav, other.wav

라이선스:
    이 스크립트 자체는 GPLv3 (셀라 프로젝트 전체 라이선스 준수).
    Demucs 모델은 Meta AI / MIT License.
    처리 대상 음원의 저작권은 사용자가 별도로 확인해야 합니다.
"""

import argparse
import os
import sys
import shutil
import subprocess
import tempfile
from pathlib import Path


def log_progress(value: float) -> None:
    """C# 호출자에게 진행률 전송 (0.0 ~ 100.0)."""
    print(f"PROGRESS:{value:.1f}", flush=True)


def run_demucs(input_path: str, output_dir: str, model: str, stems: int) -> int:
    """
    demucs CLI를 실행합니다.
    두 가지 stem(2-stem)이면 --two-stems vocals 옵션을 사용합니다.
    """
    os.makedirs(output_dir, exist_ok=True)

    cmd = [
        sys.executable, "-m", "demucs",
        "--name", model,
        "--out", output_dir,
        "--float32",        # float32 WAV 출력
        input_path,
    ]

    if stems == 2:
        cmd += ["--two-stems", "vocals"]

    log_progress(5.0)
    print(f"[Selah] 실행 명령: {' '.join(cmd)}", file=sys.stderr)

    try:
        proc = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
        )

        for line in proc.stdout:  # type: ignore[union-attr]
            line = line.rstrip()
            print(f"[demucs] {line}", file=sys.stderr, flush=True)

            # Demucs 출력에서 진행률 파싱 (예: "  0%|          |")
            if "%" in line:
                try:
                    pct_str = line.split("%")[0].split()[-1]
                    pct = float(pct_str)
                    log_progress(5.0 + pct * 0.9)  # 5~95% 구간 매핑
                except (ValueError, IndexError):
                    pass

        proc.wait()
        exit_code = proc.returncode

    except FileNotFoundError:
        print("[Selah] 오류: demucs 명령을 찾을 수 없습니다. pip install demucs 를 실행하세요.",
              file=sys.stderr)
        return 1

    if exit_code != 0:
        print(f"[Selah] Demucs 오류 (종료 코드 {exit_code})", file=sys.stderr)
        return exit_code

    # Demucs는 output_dir/<model>/<track_name>/ 구조로 저장
    # 셀라가 기대하는 output_dir/vocals.wav, output_dir/no_vocals.wav 로 이동
    input_name = Path(input_path).stem
    demucs_out = Path(output_dir) / model / input_name

    if not demucs_out.exists():
        # htdemucs_ft 는 폴더 이름이 다를 수 있음 — 첫 번째 매칭 폴더 사용
        parent = Path(output_dir) / model
        if parent.exists():
            candidates = list(parent.iterdir())
            if candidates:
                demucs_out = candidates[0]

    if not demucs_out.exists():
        print(f"[Selah] 출력 폴더를 찾을 수 없습니다: {demucs_out}", file=sys.stderr)
        return 2

    log_progress(97.0)

    stem_map = {
        "vocals.wav": "vocals.wav",
        "no_vocals.wav": "no_vocals.wav",   # 2-stem
        "bass.wav": "bass.wav",
        "drums.wav": "drums.wav",
        "other.wav": "other.wav",
    }

    moved = 0
    for src_name, dst_name in stem_map.items():
        src = demucs_out / src_name
        if src.exists():
            dst = Path(output_dir) / dst_name
            shutil.move(str(src), str(dst))
            print(f"[Selah] 저장됨: {dst}", file=sys.stderr)
            # Notify C# that this stem WAV is ready for immediate clip creation.
            # Format: STEM:<key>=<absolute-path>  (written to stdout)
            stem_key = src_name.replace(".wav", "")
            print(f"STEM:{stem_key}={dst}", flush=True)
            moved += 1

    if moved == 0:
        print(f"[Selah] 경고: 출력 stem 파일을 찾을 수 없습니다.", file=sys.stderr)
        return 3

    log_progress(100.0)
    print(f"[Selah] 완료. {moved}개 스템 저장됨.", file=sys.stderr)
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(
        description="셀라(Selah) Demucs MR 추출 스크립트"
    )
    parser.add_argument("--input",  required=True, help="입력 WAV 파일 경로")
    parser.add_argument("--output", required=True, help="출력 폴더 경로")
    parser.add_argument("--model",  default="htdemucs",
                        choices=["htdemucs", "htdemucs_ft", "mdx_extra"],
                        help="사용할 Demucs 모델")
    parser.add_argument("--stems",  type=int, default=4, choices=[2, 4],
                        help="분리할 스템 수 (2=보컬/반주, 4=드럼/베이스/기타/보컬)")
    args = parser.parse_args()

    if not os.path.isfile(args.input):
        print(f"[Selah] 오류: 입력 파일이 없습니다: {args.input}", file=sys.stderr)
        return 1

    return run_demucs(args.input, args.output, args.model, args.stems)


if __name__ == "__main__":
    sys.exit(main())
