#!/usr/bin/env python3
"""
Selah midi_synthesizer.py — MIDI + 악기 패치 → WAV

합성 경로 (우선순위 순):
  1. Python `fluidsynth` 패키지 + MIDI 플레이어 API  (pip install fluidsynth)
  2. Python `fluidsynth` 패키지 + 수동 get_samples 렌더링
  3. FluidSynth 실행 파일 서브프로세스                (exe 경로 전달 시)

1, 2 모두 fluidsynth 패키지가 libfluidsynth 네이티브 라이브러리를 로드해야 합니다.
  - Windows: FluidSynth 설치 시 함께 제공되는 libfluidsynth-3.dll / libfluidsynth.dll
  - fluidsynth.org → 인스톨러, 또는 Chocolatey: choco install fluidsynth

의존 패키지:
  pip install fluidsynth mido

사용법:
  python midi_synthesizer.py
      --midi <path>
      --soundfont <sf2>
      --instrument <name>
      --patch <gm_program|−1_for_drums>
      --output <wav>
      --sample-rate <sr>
      [--fluidsynth <exe>]   # 선택적 — exe 경로 없으면 Python API 사용

stdout 프로토콜:
  PROGRESS:<0-100>
  LOG:<message>
"""

import sys
import os
import argparse
import subprocess
import wave

# ── 의존 패키지 확인 ──────────────────────────────────────────────────────────

try:
    import mido  # type: ignore
    _MIDO_OK = True
except ImportError:
    print("LOG:MIDO_MISSING: pip install mido", flush=True)
    _MIDO_OK = False

# ── FluidSynth DLL 경로 사전 등록 ────────────────────────────────────────────
# pyfluidsynth은 import 시점에 libfluidsynth DLL을 ctypes로 로드하고,
# 로드에 실패하면 Synth 클래스를 정의하지 않습니다.
# os.add_dll_directory()는 import 전에 호출해야 적용됩니다.
def _register_dll_paths() -> None:
    if sys.platform != "win32":
        return
    import ctypes as _ctypes
    import shutil as _shutil

    choco_root = os.path.join(os.environ.get("PROGRAMDATA", r"C:\ProgramData"), "chocolatey")
    choco_lib  = os.path.join(choco_root, "lib", "fluidsynth", "tools")

    candidates: list[str] = [
        os.path.join(os.environ.get("PROGRAMFILES",      r"C:\Program Files"),
                     "FluidSynth", "bin"),
        os.path.join(os.environ.get("PROGRAMFILES",      r"C:\Program Files"),
                     "FluidSynth", "lib"),
        os.path.join(os.environ.get("PROGRAMFILES(X86)", r"C:\Program Files (x86)"),
                     "FluidSynth", "bin"),
        os.path.join(choco_lib, "bin"),
        choco_lib,
        r"C:\tools\fluidsynth\bin",   # Chocolatey C:\tools\ 레이아웃
        r"C:\tools\fluidsynth\lib",
        r"C:\FluidSynth\bin",
        r"C:\FluidSynth\lib",
    ]

    # Chocolatey shim 파일에서 실제 exe 경로 읽기
    # C:\ProgramData\chocolatey\bin\fluidsynth.exe.shim 에 "path = <실제경로>" 형식으로 저장됨
    shim_file = os.path.join(choco_root, "bin", "fluidsynth.exe.shim")
    if os.path.isfile(shim_file):
        try:
            with open(shim_file, "r", encoding="utf-8", errors="ignore") as _f:
                for _line in _f:
                    if "=" in _line:
                        _actual = _line.split("=", 1)[1].strip().strip('"')
                        if _actual and os.path.isfile(_actual):
                            candidates.insert(0, os.path.dirname(_actual))
                        break
        except Exception:
            pass

    # fluidsynth.exe 가 PATH에 있으면 그 디렉터리도 후보에 추가
    exe = _shutil.which("fluidsynth") or _shutil.which("fluidsynth.exe")
    if exe:
        candidates.append(os.path.dirname(exe))

    # Chocolatey lib 하위 재귀 탐색: 버전별 하위폴더(예: tools\fluidsynth-2.4.7-win10-x64\bin)에 대응
    choco_lib_root = os.path.join(choco_root, "lib", "fluidsynth")
    if os.path.isdir(choco_lib_root):
        try:
            _dll_names = {"libfluidsynth-3.dll", "libfluidsynth.dll", "libfluidsynth-2.dll"}
            for _root, _dirs, _files in os.walk(choco_lib_root):
                if _dll_names.intersection(_files):
                    candidates.insert(0, _root)   # 찾은 경로를 최우선 후보로 삽입
                    break
        except Exception:
            pass

    # ① os.add_dll_directory + ② PATH 등록
    for path in candidates:
        if not os.path.isdir(path):
            continue
        try:
            os.add_dll_directory(path)
        except (AttributeError, OSError):
            pass
        if path not in os.environ.get("PATH", ""):
            os.environ["PATH"] = path + os.pathsep + os.environ.get("PATH", "")

    # ③ DLL 직접 선로드: ctypes.WinDLL 로 DLL을 프로세스에 올려두면
    #    이후 pyfluidsynth의 이름 기반 CDLL() 호출이 캐시에서 해결됨
    for path in candidates:
        if not os.path.isdir(path):
            continue
        for dll_name in ("libfluidsynth-3.dll", "libfluidsynth.dll", "libfluidsynth-2.dll"):
            dll_path = os.path.join(path, dll_name)
            if os.path.isfile(dll_path):
                try:
                    _ctypes.WinDLL(dll_path)
                    return   # 로드 성공
                except OSError:
                    pass

_register_dll_paths()

try:
    import fluidsynth as _pyfs  # type: ignore
    # DLL 로드 실패 시 pyfluidsynth은 import는 성공하지만 Synth 클래스를 정의하지 않음
    _PYFLUIDSYNTH_OK = hasattr(_pyfs, "Synth")
except ImportError:
    _pyfs = None             # type: ignore
    _PYFLUIDSYNTH_OK = False

_DRUMS_CHANNEL = 9   # MIDI 채널 9 (0-indexed) = GM 드럼


# ── MIDI 패치 교체 ────────────────────────────────────────────────────────────

def patch_midi(input_path: str, output_path: str, patch: int, is_drums: bool) -> None:
    """대상 악기 GM 패치로 MIDI program_change를 교체합니다."""
    mid = mido.MidiFile(input_path)
    new_mid = mido.MidiFile(type=mid.type, ticks_per_beat=mid.ticks_per_beat)

    for track in mid.tracks:
        new_track = mido.MidiTrack()
        has_pc = False

        if is_drums:
            for msg in track:
                if msg.type in ("note_on", "note_off"):
                    new_track.append(msg.copy(channel=_DRUMS_CHANNEL))
                elif msg.type == "program_change" and msg.channel == _DRUMS_CHANNEL:
                    new_track.append(msg)
                elif msg.type != "program_change":
                    new_track.append(msg)
        else:
            for msg in track:
                if msg.type == "program_change" and msg.channel != _DRUMS_CHANNEL:
                    new_track.append(msg.copy(program=patch))
                    has_pc = True
                elif msg.type in ("note_on", "note_off") and msg.channel == _DRUMS_CHANNEL:
                    new_track.append(msg.copy(channel=0))
                else:
                    new_track.append(msg)

            if not has_pc and any(m.type in ("note_on", "note_off") for m in new_track):
                new_track.insert(0, mido.Message(
                    "program_change", channel=0, program=patch, time=0))

        new_mid.tracks.append(new_track)

    new_mid.save(output_path)


# ── Python API 합성 ───────────────────────────────────────────────────────────

def synthesize_python_player(soundfont: str, midi_path: str, output_wav: str,
                              sample_rate: int, patch: int, is_drums: bool) -> None:
    """
    fluidsynth Python 패키지의 MIDI 플레이어 API를 사용합니다.
    fluid_player_add → fluid_player_play → fluid_player_join 순서로 동기 렌더링.
    오디오 드라이버를 'file'로 설정하면 실시간 재생 없이 WAV로 직접 씁니다.
    """
    import fluidsynth  # type: ignore
    fs = fluidsynth.Synth(samplerate=float(sample_rate))

    # 파일 출력 드라이버 설정 (start() 전에 설정해야 합니다)
    fs.setting("audio.file.name", output_wav)
    fs.setting("audio.file.type", "wav")
    fs.start(driver="file")

    sfid = fs.sfload(soundfont)
    if sfid < 0:
        fs.delete()
        raise RuntimeError(f"SoundFont 로드 실패: {soundfont}")

    if is_drums:
        fs.program_select(_DRUMS_CHANNEL, sfid, 128, 0)
    else:
        fs.program_select(0, sfid, 0, patch)

    # MIDI 플레이어로 파일 재생
    fs.player_add(midi_path)
    fs.player_play()
    fs.player_join()   # 재생 완료까지 블로킹
    fs.delete()


def synthesize_python_manual(soundfont: str, midi_path: str, output_wav: str,
                              sample_rate: int, patch: int, is_drums: bool) -> None:
    """
    MIDI 플레이어 API를 쓸 수 없을 때의 폴백.
    mido로 이벤트를 파싱하고 get_samples()로 수동 렌더링합니다.
    """
    if not _MIDO_OK:
        raise RuntimeError("mido 미설치: pip install mido")

    import fluidsynth  # type: ignore
    fs = fluidsynth.Synth(samplerate=float(sample_rate))
    sfid = fs.sfload(soundfont)
    if sfid < 0:
        fs.delete()
        raise RuntimeError(f"SoundFont 로드 실패: {soundfont}")

    ch = _DRUMS_CHANNEL if is_drums else 0
    fs.program_select(ch, sfid, 128 if is_drums else 0, 0 if is_drums else patch)

    # MIDI 이벤트를 샘플 시각으로 변환
    mid = mido.MidiFile(midi_path)
    tempo = 500_000       # 기본 120 BPM
    tpb   = mid.ticks_per_beat
    events: list[tuple[int, object]] = []

    for track in mid.tracks:
        tick = 0
        for msg in track:
            tick += msg.time
            secs = mido.tick2second(tick, tpb, tempo)
            events.append((int(secs * sample_rate), msg))

    if not events:
        fs.delete()
        return

    events.sort(key=lambda x: x[0])
    decay_samples = sample_rate * 2   # 2초 감쇠
    total_samples = events[-1][0] + decay_samples
    block_size    = 1024

    wav_frames = bytearray()
    pos = 0
    ei  = 0

    while pos < total_samples:
        # 현재 위치까지의 이벤트 처리
        while ei < len(events) and events[ei][0] <= pos:
            msg = events[ei][1]
            if msg.type == "note_on":
                target_ch = ch if is_drums else (
                    _DRUMS_CHANNEL if msg.channel == _DRUMS_CHANNEL else ch)
                fs.noteon(target_ch, msg.note, msg.velocity)
            elif msg.type == "note_off":
                target_ch = ch if is_drums else (
                    _DRUMS_CHANNEL if msg.channel == _DRUMS_CHANNEL else ch)
                fs.noteoff(target_ch, msg.note)
            elif msg.type == "set_tempo":
                tempo = msg.tempo
            ei += 1

        chunk = fs.get_samples(block_size)  # int16 스테레오 인터리브드
        buf = chunk.tobytes() if hasattr(chunk, "tobytes") else bytes(chunk)
        wav_frames.extend(buf)
        pos += block_size

    fs.delete()

    # WAV 파일 저장 (FluidSynth get_samples는 스테레오 int16 출력)
    with wave.open(output_wav, "wb") as wf:
        wf.setnchannels(2)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        wf.writeframes(bytes(wav_frames))


def synthesize_python_api(soundfont: str, midi_path: str, output_wav: str,
                           sample_rate: int, patch: int, is_drums: bool) -> None:
    """플레이어 API → 수동 렌더링 순으로 시도합니다."""
    try:
        synthesize_python_player(soundfont, midi_path, output_wav,
                                 sample_rate, patch, is_drums)
        return
    except (AttributeError, Exception) as player_err:
        print(f"LOG:플레이어 API 실패, 수동 렌더링으로 전환: {player_err}", flush=True)

    synthesize_python_manual(soundfont, midi_path, output_wav,
                             sample_rate, patch, is_drums)


# ── 실행 파일 합성 (폴백) ─────────────────────────────────────────────────────

def synthesize_exe(exe: str, soundfont: str, midi_path: str,
                   output_wav: str, sample_rate: int) -> None:
    """FluidSynth 실행 파일로 MIDI → WAV 합성합니다."""
    cmd = [exe, "-ni", "-F", output_wav, "-r", str(sample_rate), soundfont, midi_path]
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode != 0:
        lines = res.stderr.strip().splitlines()
        raise RuntimeError("\n".join(lines[-10:]) if lines else "FluidSynth 오류")


# ── 메인 ─────────────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description="Selah MIDI synthesizer")
    parser.add_argument("--midi",        required=True)
    parser.add_argument("--soundfont",   required=True)
    parser.add_argument("--instrument",  required=True)
    parser.add_argument("--patch",       type=int,  default=0)
    parser.add_argument("--output",      required=True)
    parser.add_argument("--sample-rate", type=int,  default=44100)
    # exe 경로는 선택적 — 없거나 비어 있으면 Python API 경로를 사용합니다
    parser.add_argument("--fluidsynth",  default="", help="(선택) fluidsynth 실행 파일 경로")
    args = parser.parse_args()

    is_drums = (args.patch == -1)

    print(f"LOG:{args.instrument} 합성 준비 중...", flush=True)
    print("PROGRESS:10", flush=True)

    os.makedirs(os.path.dirname(os.path.abspath(args.output)), exist_ok=True)

    # MIDI 패치 교체
    patched_midi = args.output + ".patched.mid"
    if _MIDO_OK:
        try:
            patch_midi(args.midi, patched_midi, args.patch, is_drums)
            midi_to_use = patched_midi
        except Exception as e:
            print(f"LOG:MIDI 패치 실패 (원본 사용): {e}", flush=True)
            midi_to_use = args.midi
    else:
        midi_to_use = args.midi

    print("PROGRESS:30", flush=True)
    print(f"LOG:{args.instrument} 합성 중...", flush=True)

    errors: list[str] = []
    success = False

    # ① Python API 시도 (pip install fluidsynth + native DLL)
    if _PYFLUIDSYNTH_OK:
        try:
            synthesize_python_api(args.soundfont, midi_to_use, args.output,
                                  args.sample_rate, args.patch, is_drums)
            success = True
        except Exception as exc:
            errors.append(f"Python fluidsynth API: {exc}")
            print(f"LOG:{errors[-1]}", flush=True)

    # ② 실행 파일 폴백 (exe 경로가 전달된 경우)
    if not success and args.fluidsynth and os.path.isfile(args.fluidsynth):
        try:
            synthesize_exe(args.fluidsynth, args.soundfont, midi_to_use,
                           args.output, args.sample_rate)
            success = True
        except Exception as exc:
            errors.append(f"FluidSynth exe: {exc}")
            print(f"LOG:{errors[-1]}", flush=True)

    # 정리
    try:
        if os.path.exists(patched_midi):
            os.remove(patched_midi)
    except OSError:
        pass

    if not success:
        if not _PYFLUIDSYNTH_OK:
            # DLL 없이 import만 성공한 경우와 완전 미설치 구분
            import importlib.util
            if importlib.util.find_spec("fluidsynth") is not None:
                print("LOG:FLUIDSYNTH_DLL_MISSING: libfluidsynth.dll을 찾을 수 없습니다. "
                      "fluidsynth.org 에서 FluidSynth 를 설치하거나 PATH 에 bin 폴더를 추가하세요.",
                      flush=True)
            else:
                print("LOG:FLUIDSYNTH_MISSING: pip install fluidsynth", flush=True)
        else:
            print(f"LOG:SYNTHESIS_FAILED: {' | '.join(errors)}", flush=True)
        sys.exit(1)

    print("PROGRESS:100", flush=True)
    print(f"LOG:합성 완료: {os.path.basename(args.output)}", flush=True)


if __name__ == "__main__":
    main()
