#!/usr/bin/env python3
"""
Selah midi_synthesizer.py — MIDI + 악기 패치 → WAV (FluidSynth 경유)

파이프라인:
  1. mido로 MIDI 읽기
  2. 대상 악기 GM 프로그램 번호로 패치 교체
     - 드럼(patch=-1): 모든 노트를 채널 9(GM 드럼)로 리매핑
     - 일반 악기: 기존 program_change를 대상 패치로 교체
  3. FluidSynth 서브프로세스로 WAV 합성

의존 패키지:
  pip install mido
  PATH에 fluidsynth 실행 파일 필요

사용법:
  python midi_synthesizer.py \
      --midi <path> \
      --soundfont <sf2> \
      --fluidsynth <exe> \
      --instrument <name> \
      --patch <gm_program_number|−1_for_drums> \
      --output <wav> \
      --sample-rate <sr>

stdout 프로토콜:
  PROGRESS:<0-100>
  LOG:<message>
"""

import sys
import os
import argparse
import subprocess

# ── 의존 패키지 확인 ──────────────────────────────────────────────────────────

try:
    import mido  # type: ignore
except ImportError:
    print("LOG:MIDO_MISSING — pip install mido", flush=True)
    sys.exit(1)

# MIDI 채널 9 (0-indexed) = GM 드럼 채널
_DRUMS_CHANNEL = 9


# ── MIDI 패치 교체 ────────────────────────────────────────────────────────────

def patch_midi(input_path: str, output_path: str, patch: int, is_drums: bool) -> None:
    """
    MIDI 파일의 모든 채널 program_change를 대상 패치로 교체합니다.

    is_drums=True 일 때:
      - 모든 note_on / note_off 메시지를 채널 9로 리매핑
      - 드럼 채널 이외의 program_change 제거
    is_drums=False 일 때:
      - 드럼 채널(9)을 제외한 모든 채널의 program_change를 patch로 교체
      - program_change 메시지가 없으면 각 트랙 시작에 삽입
    """
    mid = mido.MidiFile(input_path)
    new_mid = mido.MidiFile(type=mid.type, ticks_per_beat=mid.ticks_per_beat)

    for track in mid.tracks:
        new_track = mido.MidiTrack()
        has_program_change = False

        if is_drums:
            for msg in track:
                if msg.type in ("note_on", "note_off"):
                    new_track.append(msg.copy(channel=_DRUMS_CHANNEL))
                elif msg.type == "program_change" and msg.channel == _DRUMS_CHANNEL:
                    # 드럼 채널 program_change는 유지 (표준 MIDI는 ch9에 program_change 없음)
                    new_track.append(msg)
                elif msg.type == "program_change":
                    pass  # 일반 채널 program_change 제거
                else:
                    new_track.append(msg)
        else:
            for msg in track:
                if msg.type == "program_change" and msg.channel != _DRUMS_CHANNEL:
                    new_track.append(msg.copy(program=patch))
                    has_program_change = True
                elif msg.type in ("note_on", "note_off") and msg.channel == _DRUMS_CHANNEL:
                    # 드럼 채널 노트를 채널 0으로 옮겨 드럼 사운드 방지
                    new_track.append(msg.copy(channel=0))
                else:
                    new_track.append(msg)

            # program_change가 없는 트랙이라면 첫 번째 위치에 삽입
            if not has_program_change and any(
                m.type in ("note_on", "note_off") for m in new_track
            ):
                new_track.insert(0, mido.Message(
                    "program_change", channel=0, program=patch, time=0
                ))

        new_mid.tracks.append(new_track)

    new_mid.save(output_path)


# ── FluidSynth 합성 ───────────────────────────────────────────────────────────

def synthesize(fluidsynth_exe: str, soundfont: str,
               midi_path: str, output_wav: str, sample_rate: int) -> None:
    """FluidSynth 서브프로세스로 MIDI를 WAV로 합성합니다."""
    cmd = [
        fluidsynth_exe,
        "-ni",                  # non-interactive, 오디오 드라이버 없음
        "-F", output_wav,       # WAV 출력 파일
        "-r", str(sample_rate), # 샘플레이트
        soundfont,
        midi_path,
    ]
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        err = result.stderr.strip().splitlines()
        tail = "\n".join(err[-10:]) if err else "(오류 메시지 없음)"
        print(f"LOG:FluidSynth 오류: {tail}", flush=True)
        sys.exit(1)


# ── 메인 ─────────────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description="Selah MIDI synthesizer")
    parser.add_argument("--midi",        required=True,            help="입력 MIDI 경로")
    parser.add_argument("--soundfont",   required=True,            help="SoundFont(.sf2) 경로")
    parser.add_argument("--fluidsynth",  required=True,            help="fluidsynth 실행 파일 경로")
    parser.add_argument("--instrument",  required=True,            help="악기 이름 (로그용)")
    parser.add_argument("--patch",       type=int,   default=0,    help="GM 프로그램 번호 (-1=드럼)")
    parser.add_argument("--output",      required=True,            help="출력 WAV 경로")
    parser.add_argument("--sample-rate", type=int,   default=44100, help="샘플레이트")
    args = parser.parse_args()

    is_drums = (args.patch == -1)

    print(f"LOG:{args.instrument} 합성 준비 중...", flush=True)
    print("PROGRESS:10", flush=True)

    # 패치된 MIDI 임시 파일 (출력 WAV와 같은 디렉터리에 생성)
    os.makedirs(os.path.dirname(os.path.abspath(args.output)), exist_ok=True)
    patched_midi = args.output + ".patched.mid"

    try:
        patch_midi(args.midi, patched_midi, args.patch, is_drums)
    except Exception as exc:
        print(f"LOG:MIDI 패치 실패: {exc}", flush=True)
        sys.exit(1)

    print("PROGRESS:30", flush=True)
    print(f"LOG:FluidSynth로 {args.instrument} 합성 중...", flush=True)

    try:
        synthesize(args.fluidsynth, args.soundfont,
                   patched_midi, args.output, args.sample_rate)
    finally:
        # 임시 MIDI 정리
        try:
            os.remove(patched_midi)
        except OSError:
            pass

    print("PROGRESS:100", flush=True)
    print(f"LOG:합성 완료: {os.path.basename(args.output)}", flush=True)


if __name__ == "__main__":
    main()
