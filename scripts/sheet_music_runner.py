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
    from music21 import converter, stream, note, chord, environment  # type: ignore
    import music21  # type: ignore
    # 외부 프로그램(MuseScore, LilyPond 등) 자동 실행 완전 차단
    _env = environment.Environment()
    for _key in ("musescorePath", "musicxmlPath", "lilypondPath",
                 "midiPath", "graphicsPath"):
        try:
            _env[_key] = ""
        except Exception:
            pass
    _env["autoDownload"] = "deny"
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

def _find_oemer() -> list[str]:
    """
    oemer 실행 커맨드를 반환합니다.
    oemer는 __main__.py가 없으므로 `python -m oemer`가 동작하지 않습니다.
    우선순위:
      1. sys.executable과 같은 Scripts 폴더의 oemer(.exe) 실행 파일
      2. PATH에서 shutil.which("oemer")로 탐색
      3. 폴백: oemer.main() 직접 호출을 위해 빈 리스트 반환
    """
    import shutil

    scripts_dir = os.path.dirname(sys.executable)
    for name in ("oemer.exe", "oemer"):
        candidate = os.path.join(scripts_dir, name)
        if os.path.isfile(candidate):
            return [candidate]

    found = shutil.which("oemer")
    if found:
        return [found]

    return []   # 폴백: 직접 import로 실행


def run_omr(image_path: str, output_dir: str) -> str:
    """
    oemer를 서브프로세스로 실행하여 MusicXML 파일을 생성합니다.
    oemer는 입력 이미지 이름과 동일한 .musicxml을 output_dir에 저장합니다.
    """
    print("LOG:OMR 실행 중 (oemer)...", flush=True)

    oemer_cmd = _find_oemer()
    if not oemer_cmd:
        print("LOG:OEMER_MISSING — oemer 실행 파일을 찾을 수 없습니다. pip install oemer", flush=True)
        sys.exit(1)

    result = subprocess.run(
        oemer_cmd + [image_path, "-o", output_dir, "--without-deskew"],
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

def _write_midi_via_mido(score: "music21.stream.Score", midi_path: str) -> None:
    """
    music21 Score → MIDI 변환을 mido로 직접 수행합니다.
    music21의 streamToMidiFile()을 완전히 우회하므로
    외부 프로그램 호출이나 I/O 블로킹이 발생하지 않습니다.
    """
    import mido  # type: ignore

    ticks_per_beat = 480
    tempo = 500_000  # 기본 120 BPM

    # 악보에서 템포 추출
    try:
        from music21 import tempo as m21tempo  # type: ignore
        t = score.flat.getElementsByClass(m21tempo.MetronomeMark).first()
        if t and t.number:
            tempo = int(60_000_000 / t.number)
    except Exception:
        pass

    mid = mido.MidiFile(ticks_per_beat=ticks_per_beat)

    # 트랙 0: 템포
    meta_track = mido.MidiTrack()
    mid.tracks.append(meta_track)
    meta_track.append(mido.MetaMessage("set_tempo", tempo=tempo, time=0))
    meta_track.append(mido.MetaMessage("end_of_track", time=0))

    # 파트별 MIDI 트랙
    parts = score.parts if score.parts else [score]
    for ch_idx, part in enumerate(parts):
        track = mido.MidiTrack()
        mid.tracks.append(track)

        # (절대 tick, 'note_on'|'note_off', pitch, velocity) 이벤트 수집
        events: list[tuple[int, str, int, int]] = []
        for el in part.flat.notesAndRests:
            offset_ticks = int(float(el.offset) * ticks_per_beat)
            dur_ticks    = max(1, int(float(el.duration.quarterLength) * ticks_per_beat))

            if hasattr(el, "pitches"):          # Note 또는 Chord
                vel = 64
                try:
                    if el.volume and el.volume.velocity:
                        vel = int(el.volume.velocity)
                except Exception:
                    pass
                for p in el.pitches:
                    events.append((offset_ticks,              "note_on",  p.midi, vel))
                    events.append((offset_ticks + dur_ticks,  "note_off", p.midi, 0))

        events.sort(key=lambda x: x[0])

        prev_tick = 0
        for abs_tick, msg_type, pitch, vel in events:
            delta = abs_tick - prev_tick
            track.append(mido.Message(msg_type, channel=ch_idx % 16,
                                      note=pitch, velocity=vel, time=delta))
            prev_tick = abs_tick

        track.append(mido.MetaMessage("end_of_track", time=0))

    mid.save(midi_path)


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
    # music21의 streamToMidiFile()은 CPU 0% 상태로 무한 대기하는 경우가 있어
    # (외부 프로그램 호출 또는 I/O 블로킹) mido로 직접 변환합니다.
    print("PROGRESS:75", flush=True)
    print("LOG:MIDI 생성 중...", flush=True)
    midi_path = os.path.join(args.output_dir, "score.mid")
    try:
        _write_midi_via_mido(score, midi_path)
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
