#!/usr/bin/env python3
"""
Selah sheet_music_runner.py — 악보 이미지 OMR + ScoreProfile 추출

파이프라인:
  1. 이미지 전처리  — PIL/scipy: 그레이스케일, 노이즈 감소, 적응형 이진화
  2. OMR 실행       — oemer 서브모듈로 MusicXML 생성
  3. 악보 분석      — music21: 보표·음자리표·음표 구조 분석
  4. MIDI 내보내기  — music21: score.mid 생성
  5. 프로파일 출력  — ScoreProfile JSON을 stdout에 기록

의존 패키지:
  pip install oemer music21 Pillow scipy

사용법:
  python sheet_music_runner.py --input <image> --output-dir <dir>

stdout 프로토콜:
  PROGRESS:<0-100>   진행률
  LOG:<message>      상태/오류 메시지
  PROFILE:<json>     ScoreProfile JSON
"""

import sys
import os
import argparse
import json
import subprocess

# ── 의존 패키지 확인 ──────────────────────────────────────────────────────────

try:
    from PIL import Image
    import numpy as np
except ImportError:
    print("LOG:PILLOW_MISSING — pip install Pillow", flush=True)
    sys.exit(1)

try:
    from scipy.ndimage import uniform_filter
    _SCIPY_OK = True
except ImportError:
    _SCIPY_OK = False

try:
    import oemer  # noqa: F401 — 존재 여부만 확인
except ImportError:
    print("LOG:OEMER_MISSING — pip install oemer", flush=True)
    sys.exit(1)

try:
    from music21 import converter, stream, note, chord  # type: ignore
    import music21  # type: ignore
except ImportError:
    print("LOG:MUSIC21_MISSING — pip install music21", flush=True)
    sys.exit(1)


# ── 이미지 전처리 ─────────────────────────────────────────────────────────────

def preprocess_image(input_path: str, output_path: str) -> None:
    """
    악보 이미지 노이즈 감소 및 이진화 전처리.
    그레이스케일 변환 → 균일 필터(블러) → 로컬 임계값 이진화.
    """
    print("LOG:이미지 전처리 중...", flush=True)

    img = Image.open(input_path).convert("L")   # 그레이스케일
    arr = np.array(img, dtype=np.float32)

    # 블러로 고주파 노이즈 감소 (scipy 없으면 생략)
    if _SCIPY_OK:
        arr_smooth = uniform_filter(arr, size=3)
    else:
        arr_smooth = arr

    # 로컬 평균 기반 적응형 이진화 (블록 25px, 오프셋 -10)
    from numpy.lib.stride_tricks import sliding_window_view  # numpy 1.20+
    try:
        block = 25
        # 패딩하여 전체 크기 유지
        pad = block // 2
        padded = np.pad(arr_smooth, pad, mode="reflect")
        local_mean = uniform_filter(padded, size=block)[pad:-pad, pad:-pad] if _SCIPY_OK \
            else arr_smooth.mean()
        binary = ((arr_smooth > local_mean - 10).astype(np.uint8)) * 255
    except Exception:
        # 폴백: 전역 Otsu-style 임계값
        thresh = arr_smooth.mean()
        binary = (arr_smooth > thresh).astype(np.uint8) * 255

    Image.fromarray(binary).save(output_path, format="PNG")


# ── OMR 실행 ─────────────────────────────────────────────────────────────────

def run_omr(image_path: str, output_dir: str) -> str:
    """
    oemer를 서브프로세스로 실행하여 MusicXML 파일을 생성합니다.
    oemer는 입력 이미지 이름과 동일한 .musicxml을 output_dir에 저장합니다.
    """
    print("LOG:OMR 실행 중 (oemer)...", flush=True)

    result = subprocess.run(
        [sys.executable, "-m", "oemer", image_path, "-o", output_dir,
         "--without-deskew"],   # 이미 전처리에서 처리됨
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        # stderr의 마지막 몇 줄만 로그 출력 (파이프 버퍼 초과 방지)
        err_tail = "\n".join(result.stderr.strip().splitlines()[-10:])
        print(f"LOG:OMR 실패: {err_tail}", flush=True)
        sys.exit(1)

    # oemer 출력 파일 탐색 (.musicxml 또는 .xml)
    base = os.path.splitext(os.path.basename(image_path))[0]
    for ext in (".musicxml", ".xml"):
        candidate = os.path.join(output_dir, base + ext)
        if os.path.exists(candidate):
            return candidate

    # 폴더 전체에서 첫 번째 .musicxml 탐색 (버전별 차이 대응)
    for fname in os.listdir(output_dir):
        if fname.endswith((".musicxml", ".xml")):
            return os.path.join(output_dir, fname)

    print("LOG:OMR이 MusicXML 파일을 생성하지 못했습니다.", flush=True)
    sys.exit(1)


# ── ScoreProfile 빌드 ─────────────────────────────────────────────────────────

def build_score_profile(score: "music21.stream.Score", midi_path: str) -> dict:
    """
    music21 Score 객체에서 ScoreProfile 딕셔너리를 추출합니다.
    C# ScoreProfile 모델과 필드 이름을 일치시킵니다.
    """
    clef_types: list[str] = []
    staff_count    = max(len(score.parts), 1)
    is_polyphonic  = False
    has_perc_clef  = False
    chord_beats    = 0
    total_beats    = 0
    pitch_min      = 127
    pitch_max      = 0
    note_count     = 0

    for part in score.parts:
        # 음자리표 수집
        for clef_obj in part.flat.getElementsByClass("Clef"):
            clef_name = type(clef_obj).__name__.replace("Clef", "").lower()
            if not clef_name:
                clef_name = "treble"
            if clef_name not in clef_types:
                clef_types.append(clef_name)
            if "percussion" in clef_name:
                has_perc_clef = True

        # 음표/화음 순회
        for el in part.flat.notesAndRests:
            total_beats += 1
            if isinstance(el, chord.Chord):
                is_polyphonic = True
                chord_beats  += 1
                note_count   += len(el.pitches)
                for p in el.pitches:
                    pitch_min = min(pitch_min, p.midi)
                    pitch_max = max(pitch_max, p.midi)
            elif isinstance(el, note.Note):
                note_count += 1
                pitch_min   = min(pitch_min, el.pitch.midi)
                pitch_max   = max(pitch_max, el.pitch.midi)

    chord_density = round(chord_beats / total_beats, 3) if total_beats > 0 else 0.0

    # 음표가 없으면 MIDI 범위 초기화
    if pitch_min > pitch_max:
        pitch_min, pitch_max = 0, 0

    # 재생 길이: score.seconds 실패 시 quarterLength × 0.5s (120 BPM 추정)
    try:
        duration_sec = round(float(score.seconds), 2)
    except Exception:
        try:
            duration_sec = round(score.duration.quarterLength * 0.5, 2)
        except Exception:
            duration_sec = 0.0

    return {
        "StaffCount":        staff_count,
        "ClefTypes":         clef_types if clef_types else ["treble"],
        "IsPolyphonic":      is_polyphonic,
        "HasPercussionClef": has_perc_clef,
        "ChordDensity":      chord_density,
        "PitchRangeMin":     pitch_min,
        "PitchRangeMax":     pitch_max,
        "NoteCount":         note_count,
        "DurationSeconds":   duration_sec,
        "MidiPath":          midi_path,
        "SuggestedInstruments": [],   # C# SheetMusicService.SuggestInstruments()가 채웁니다
    }


# ── 메인 ─────────────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description="Selah OMR runner")
    parser.add_argument("--input",      required=True, help="입력 이미지 경로")
    parser.add_argument("--output-dir", required=True, help="출력 디렉터리")
    args = parser.parse_args()

    os.makedirs(args.output_dir, exist_ok=True)

    # Step 1: 이미지 전처리
    print("PROGRESS:5", flush=True)
    preprocessed = os.path.join(args.output_dir, "preprocessed.png")
    try:
        preprocess_image(args.input, preprocessed)
    except Exception as exc:
        print(f"LOG:전처리 실패 — 원본 이미지 사용: {exc}", flush=True)
        preprocessed = args.input

    # Step 2: OMR
    print("PROGRESS:15", flush=True)
    xml_path = run_omr(preprocessed, args.output_dir)
    print(f"LOG:MusicXML 생성: {os.path.basename(xml_path)}", flush=True)

    # Step 3: music21로 분석
    print("PROGRESS:60", flush=True)
    print("LOG:악보 분석 중...", flush=True)
    try:
        score = converter.parse(xml_path)
    except Exception as exc:
        print(f"LOG:MusicXML 파싱 실패: {exc}", flush=True)
        sys.exit(1)

    # Step 4: MIDI 내보내기
    print("PROGRESS:75", flush=True)
    print("LOG:MIDI 생성 중...", flush=True)
    midi_path = os.path.join(args.output_dir, "score.mid")
    try:
        result_path = score.write("midi", fp=midi_path)
        midi_path = str(result_path)
    except Exception as exc:
        print(f"LOG:MIDI 변환 실패: {exc}", flush=True)
        sys.exit(1)

    # Step 5: ScoreProfile 빌드 및 출력
    print("PROGRESS:90", flush=True)
    profile = build_score_profile(score, midi_path)

    print("PROGRESS:100", flush=True)
    print(f"LOG:인식 완료 — {profile['NoteCount']}개 음표, {profile['DurationSeconds']}초", flush=True)
    print(f"PROFILE:{json.dumps(profile, ensure_ascii=False)}", flush=True)


if __name__ == "__main__":
    main()
