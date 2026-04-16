#!/usr/bin/env python3
"""
Selah sheet_music_runner.py — 악보 이미지 OMR + ScoreProfile 추출

파이프라인:
  1. 이미지 전처리  — PIL/scipy: 그레이스케일, 노이즈 감소, 적응형 이진화
  2. OMR 실행       — oemer 서브프로세스로 MusicXML 생성
  3. MIDI 내보내기  — xml.etree.ElementTree + mido: score.mid 생성 (blocking 없음)
  4. ScoreProfile   — xml.etree.ElementTree: 보표·음자리표·음표 구조 분석
  5. 프로파일 출력  — ScoreProfile JSON을 stdout에 기록

의존 패키지:
  pip install oemer mido Pillow scipy

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
    Popen + communicate()를 사용하여 stdout/stderr 파이프 버퍼 교착(deadlock)을 방지하고,
    15초마다 진행 상태 메시지를 출력하여 사용자에게 oemer가 실행 중임을 알립니다.
    """
    import threading
    import time

    print("LOG:OMR 실행 중 (oemer)...", flush=True)

    oemer_cmd = _find_oemer()
    if not oemer_cmd:
        print("LOG:OEMER_MISSING — oemer 실행 파일을 찾을 수 없습니다. pip install oemer", flush=True)
        sys.exit(1)

    proc_oemer = subprocess.Popen(
        oemer_cmd + [image_path, "-o", output_dir, "--without-deskew"],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )

    # 15초마다 경과 시간을 출력 — C#이 수신하여 UI 상태 메시지를 갱신함
    def _heartbeat() -> None:
        elapsed = 0
        while proc_oemer.poll() is None:
            time.sleep(15)
            elapsed += 15
            if proc_oemer.poll() is None:
                print(f"LOG:OMR 실행 중 (oemer)... ({elapsed}초 경과)", flush=True)

    hb = threading.Thread(target=_heartbeat, daemon=True)
    hb.start()

    stdout_data, stderr_data = proc_oemer.communicate()

    if proc_oemer.returncode != 0:
        # oemer는 오류를 stderr 대신 stdout에 출력하는 경우가 있음
        stderr_text = (stderr_data or "").strip()
        stdout_text = (stdout_data or "").strip()
        # stderr 우선, 없으면 stdout 마지막 10줄 사용
        output_for_error = stderr_text or stdout_text
        err_tail = "\n".join(output_for_error.splitlines()[-10:]) if output_for_error else "(출력 없음)"
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

def _musicxml_to_midi(xml_path: str, midi_path: str) -> None:
    """
    MusicXML → MIDI 변환.
    music21 Score 객체를 전혀 사용하지 않습니다.
    stdlib xml.etree.ElementTree 로 XML을 직접 파싱하고
    mido 로 MIDI 파일을 생성합니다.
    외부 프로그램 호출·네트워크 접근·I/O 블로킹이 없습니다.
    """
    import xml.etree.ElementTree as ET
    import mido  # type: ignore

    tree = ET.parse(xml_path)
    root = tree.getroot()

    # 네임스페이스 처리 ({http://...}tag 형식)
    ns = ""
    if root.tag.startswith("{"):
        ns = root.tag.split("}")[0] + "}"

    def t(name: str) -> str:
        return f"{ns}{name}"

    ticks_per_beat = 480
    tempo = 500_000  # 기본 120 BPM

    # 최초 <sound tempo="…"> 에서 템포 추출
    for sound in root.iter(t("sound")):
        val = sound.get("tempo")
        if val:
            try:
                tempo = int(60_000_000 / float(val))
            except ValueError:
                pass
            break

    mid = mido.MidiFile(ticks_per_beat=ticks_per_beat)

    meta_track = mido.MidiTrack()
    mid.tracks.append(meta_track)
    meta_track.append(mido.MetaMessage("set_tempo", tempo=tempo, time=0))
    meta_track.append(mido.MetaMessage("end_of_track", time=0))

    # MIDI 음번호: step + octave + alter
    _STEP = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}

    def pitch_midi(pitch_el) -> int | None:
        step  = pitch_el.find(t("step"))
        oct_  = pitch_el.find(t("octave"))
        alter = pitch_el.find(t("alter"))
        if step is None or oct_ is None:
            return None
        num = (int(oct_.text) + 1) * 12 + _STEP.get(step.text, 0)
        if alter is not None:
            num += round(float(alter.text))
        return max(0, min(127, num))

    # 파트별 처리
    for part_idx, part in enumerate(root.findall(t("part"))):
        track = mido.MidiTrack()
        mid.tracks.append(track)

        events: list[tuple[int, str, int, int]] = []
        divisions = 1      # MusicXML <divisions>: 4분음표당 XML 틱 수
        cursor    = 0      # 현재 절대 MIDI 틱

        for measure in part.findall(t("measure")):
            attrs = measure.find(t("attributes"))
            if attrs is not None:
                div_el = attrs.find(t("divisions"))
                if div_el is not None:
                    divisions = max(1, int(div_el.text))

            measure_start = cursor
            advance       = 0   # 이 마디 내 최대 전진량
            note_pos      = 0   # 현재 음표 위치 (마디 기준)

            for child in measure:
                local = child.tag.replace(ns, "")

                if local == "note":
                    is_chord = child.find(t("chord")) is not None
                    is_rest  = child.find(t("rest"))  is not None

                    dur_el  = child.find(t("duration"))
                    dur_xml = int(dur_el.text) if dur_el is not None else divisions
                    dur_tck = max(1, round(dur_xml * ticks_per_beat / divisions))

                    if not is_chord:
                        note_pos = advance

                    if not is_rest:
                        pitch_el = child.find(t("pitch"))
                        if pitch_el is not None:
                            midi_num = pitch_midi(pitch_el)
                            if midi_num is not None:
                                vel = 64
                                abs_on  = measure_start + note_pos
                                abs_off = abs_on + dur_tck
                                events.append((abs_on,  "note_on",  midi_num, vel))
                                events.append((abs_off, "note_off", midi_num, 0))

                    if not is_chord:
                        advance = note_pos + dur_tck

                elif local == "backup":
                    dur_el = child.find(t("duration"))
                    if dur_el is not None:
                        advance -= round(int(dur_el.text) * ticks_per_beat / divisions)

                elif local == "forward":
                    dur_el = child.find(t("duration"))
                    if dur_el is not None:
                        advance += round(int(dur_el.text) * ticks_per_beat / divisions)

            cursor = measure_start + advance

        events.sort(key=lambda x: x[0])

        prev = 0
        for abs_t, msg, pitch, vel in events:
            track.append(mido.Message(msg, channel=part_idx % 16,
                                      note=pitch, velocity=vel,
                                      time=abs_t - prev))
            prev = abs_t

        track.append(mido.MetaMessage("end_of_track", time=0))

    mid.save(midi_path)
    print(f"LOG:MIDI 저장 완료 ({len(mid.tracks) - 1}개 트랙)", flush=True)


def _build_profile_from_xml(xml_path: str, midi_path: str) -> dict:
    """
    MusicXML 파일에서 ScoreProfile을 추출합니다.
    music21을 전혀 사용하지 않습니다.
    stdlib xml.etree.ElementTree로 XML을 직접 파싱하고,
    이미 생성된 MIDI 파일에서 재생 길이를 읽습니다.
    """
    import xml.etree.ElementTree as ET
    import mido  # type: ignore

    tree = ET.parse(xml_path)
    root = tree.getroot()

    ns = ""
    if root.tag.startswith("{"):
        ns = root.tag.split("}")[0] + "}"

    def t(name: str) -> str:
        return f"{ns}{name}"

    _STEP = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}

    def pitch_to_midi(pitch_el) -> int | None:
        step  = pitch_el.find(t("step"))
        oct_  = pitch_el.find(t("octave"))
        alter = pitch_el.find(t("alter"))
        if step is None or oct_ is None:
            return None
        num = (int(oct_.text) + 1) * 12 + _STEP.get(step.text, 0)
        if alter is not None:
            num += round(float(alter.text))
        return max(0, min(127, num))

    parts          = root.findall(t("part"))
    staff_count    = max(len(parts), 1)
    clef_types: list[str] = []
    has_perc_clef  = False
    is_polyphonic  = False
    chord_count    = 0
    total_notes    = 0
    pitch_min      = 127
    pitch_max      = 0
    note_count     = 0

    for part in parts:
        for measure in part.findall(t("measure")):
            attrs = measure.find(t("attributes"))
            if attrs is not None:
                for clef_el in attrs.findall(t("clef")):
                    sign_el = clef_el.find(t("sign"))
                    if sign_el is not None and sign_el.text:
                        sign = sign_el.text.strip().lower()
                        clef_map = {"g": "treble", "f": "bass", "c": "alto",
                                    "percussion": "percussion"}
                        clef_name = clef_map.get(sign, sign)
                        if clef_name not in clef_types:
                            clef_types.append(clef_name)
                        if sign == "percussion":
                            has_perc_clef = True

            for child in measure:
                if child.tag.replace(ns, "") != "note":
                    continue
                is_chord = child.find(t("chord")) is not None
                is_rest  = child.find(t("rest"))  is not None
                total_notes += 1
                if is_chord:
                    is_polyphonic = True
                    chord_count  += 1
                if not is_rest:
                    pitch_el = child.find(t("pitch"))
                    if pitch_el is not None:
                        midi_num = pitch_to_midi(pitch_el)
                        if midi_num is not None:
                            note_count += 1
                            pitch_min   = min(pitch_min, midi_num)
                            pitch_max   = max(pitch_max, midi_num)

    chord_density = round(chord_count / total_notes, 3) if total_notes > 0 else 0.0
    if pitch_min > pitch_max:
        pitch_min, pitch_max = 0, 0

    # 재생 길이: 이미 생성된 MIDI 파일에서 읽음 (mido.MidiFile.length → 초)
    try:
        mid = mido.MidiFile(midi_path)
        duration_sec = round(mid.length, 2)
    except Exception:
        duration_sec = 0.0

    return {
        "StaffCount":           staff_count,
        "ClefTypes":            clef_types if clef_types else ["treble"],
        "IsPolyphonic":         is_polyphonic,
        "HasPercussionClef":    has_perc_clef,
        "ChordDensity":         chord_density,
        "PitchRangeMin":        pitch_min,
        "PitchRangeMax":        pitch_max,
        "NoteCount":            note_count,
        "DurationSeconds":      duration_sec,
        "MidiPath":             midi_path,
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

    # Step 2: OMR (오래 걸림 — 15초마다 진행 상태 출력)
    print("PROGRESS:15", flush=True)
    xml_path = run_omr(preprocessed, args.output_dir)
    print(f"LOG:MusicXML 생성: {os.path.basename(xml_path)}", flush=True)

    # Step 3: MIDI 내보내기 (직접 XML 파싱 — music21 불필요, blocking 없음)
    print("PROGRESS:75", flush=True)
    print("LOG:MIDI 생성 중...", flush=True)
    midi_path = os.path.join(args.output_dir, "score.mid")
    try:
        _musicxml_to_midi(xml_path, midi_path)
    except Exception as exc:
        print(f"LOG:MIDI 변환 실패: {exc}", flush=True)
        sys.exit(1)

    # Step 4: ScoreProfile 추출 (직접 XML 파싱 — music21 불필요, blocking 없음)
    print("PROGRESS:90", flush=True)
    print("LOG:악보 분석 중...", flush=True)
    try:
        profile = _build_profile_from_xml(xml_path, midi_path)
    except Exception as exc:
        print(f"LOG:악보 분석 실패: {exc}", flush=True)
        sys.exit(1)

    print("PROGRESS:100", flush=True)
    print(f"LOG:인식 완료 — {profile['NoteCount']}개 음표, {profile['DurationSeconds']}초", flush=True)
    print(f"PROFILE:{json.dumps(profile, ensure_ascii=False)}", flush=True)


if __name__ == "__main__":
    main()
