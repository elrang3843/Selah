# Selah

**찬양사역용 MR편집기** · MR Editor for Worship Ministry

**Selah**는 예배 음악 준비, 프로젝트 기반 오디오 편집, AI 기반 스템 분리를 위한 **Windows 데스크톱 애플리케이션**입니다.  
**Selah** is a Windows desktop application for worship music preparation, project-based audio editing, and AI-assisted stem separation.

> 버전 / Version 1.0.0 · GPLv3 · Copyright (C) 2026 HANDTECH 노진문(Noh JinMoon)

**한국어** | [English](README.en.md)

---

## 스크린샷 / Screenshots

| 시작 화면 / Welcome | 새 프로젝트 / New Project |
|:-------------------:|:-------------------------:|
| ![시작 화면](docs/screenshots/Screen001.jpg) | ![새 프로젝트](docs/screenshots/Screen002.jpg) |

| 메인 편집 화면 / Main Editor |
|:----------------------------:|
| ![메인 편집 화면](docs/screenshots/Screen013.jpg) |

| 파일 메뉴 / File Menu | 편집 메뉴 / Edit Menu |
|:---------------------:|:---------------------:|
| ![파일 메뉴](docs/screenshots/Screen004.jpg) | ![편집 메뉴](docs/screenshots/Screen005.jpg) |

| 악보 인식 / Sheet Music Recognition | 모델 관리자 / Model Manager |
|:------------------------------------:|:---------------------------:|
| ![악보 인식](docs/screenshots/Screen016.jpg) | ![모델 관리자](docs/screenshots/Screen019.jpg) |

---

## 개요 / Overview

Selah는 세 가지 흐름을 하나의 워크플로우로 묶습니다.  
Selah combines three workflows into one application.

- **프로젝트 기반 예배 오디오 준비** / Project-based worship audio preparation
- **타임라인 기반 클립·트랙 편집** / Timeline-oriented clip and track arrangement
- **AI 기반 스템 분리** / AI-assisted stem separation

---

## 주요 목표 / Goals

소형 교회, 선교지, 소규모 찬양팀이 예배 녹음물이나 미디어로부터 반주 중심 오디오 자료를 실용적으로 준비할 수 있도록 돕습니다.  
Designed to help small churches, missionaries, and worship teams prepare accompaniment-oriented audio from recordings or worship media.

- 소형 교회 및 개척교회 예배 준비 / Small and pioneer church worship preparation
- 선교 및 사역 중심 오디오 작업 / Missionary and ministry-oriented audio workflows
- 예배 녹음물로부터 반주 자료 준비 / Accompaniment preparation from worship recordings
- 클라우드 없이 로컬에서 실행 / Simple local workflows without cloud services

이 프로젝트는 **비상업적 사역 지향의 무료 오픈소스 도구**입니다.  
This project is a **free and open-source tool** for non-commercial ministry-oriented use.

---

## 주요 기능 / Features

- **비파괴 클립 편집** — 복사·잘라내기·붙여넣기·분할·합치기  
  **Non-destructive clip editing** — copy / cut / paste / split / merge
- **다중 클립 선택** — Ctrl+클릭(토글), Shift+클릭(범위 선택)  
  **Multi-clip selection** — Ctrl+click toggle, Shift+click range
- **클립 위치 이동** — 앞 클립 뒤로(Ctrl+J) / 플레이헤드로(Ctrl+G) / 트랙 시작(Ctrl+H)  
  **Clip positioning** — after previous (Ctrl+J) / to playhead (Ctrl+G) / to track start (Ctrl+H)
- **AI 스템 분리** — audio-separator(MDX-Net) · ONNX Runtime · Demucs  
  **AI stem separation** — audio-separator / ONNX Runtime / Demucs
- **노이즈 제거** / Noise reduction
- **악보 인식** — 스캔 악보 이미지 → 악기별 오디오 트랙 (oemer OMR + FluidSynth)  
  **Sheet music recognition** — scanned score → per-instrument audio tracks
- **다국어** — 한국어 / English / 中文(简体)  
  **Localization** — Korean / English / Chinese
- **라이트·다크 테마** / Light and dark themes

---

## 단축키 / Keyboard Shortcuts

### 트랜스포트 / Transport

| 키 / Key | 동작 / Action |
|----------|---------------|
| `Space` | 재생 / 정지 · Play / Stop |
| `Shift+Space` | 정지 + 처음으로 · Stop + Return to Start |
| `Home` | 처음으로 · Return to Start |
| `M` | 메트로놈 · Metronome |
| `N` | 스냅 · Snap |

### 편집 / Editing

| 키 / Key | 동작 / Action |
|----------|---------------|
| `S` | 클립 분할 · Split clip |
| `Del` | 선택 삭제 · Delete |
| `Ctrl+C` | 복사 · Copy |
| `Ctrl+X` | 잘라내기 · Cut |
| `Ctrl+V` | 붙여넣기 · Paste |
| `Ctrl+M` | 클립 합치기 · Merge clips |
| `Ctrl+J` | 앞 클립 뒤로 이동 · Move after previous clip |
| `Ctrl+G` | 플레이헤드 위치로 이동 · Move to playhead |
| `Ctrl+H` | 트랙 시작으로 이동 · Move to track start |

### 마우스 / Mouse

| 동작 / Action | 결과 / Result |
|---------------|---------------|
| 클립 클릭 / Click clip | 선택 · Select |
| Ctrl+클릭 / Ctrl+click | 선택 토글 · Toggle selection |
| Shift+클릭 / Shift+click | 범위 선택 · Range select |
| 클립 드래그 / Drag clip | 이동 · Move |
| 눈금자 클릭 / Click ruler | 플레이헤드 이동 · Seek |
| Ctrl+스크롤 / Ctrl+scroll | 확대/축소 · Zoom |

---

## 사용자 매뉴얼 / User Manual

- 한국어: **[docs/MANUAL.ko.md](docs/MANUAL.ko.md)**

---

## 설치 / Installation

### 1단계 — 설치 파일 다운로드 / Step 1 — Download installer

**[▶ Releases 페이지에서 다운로드](https://github.com/elrang3843/Selah/releases/latest)**

`Selah-1.0.0-Setup.exe` 파일을 클릭해 다운로드합니다.  
Click `Selah-1.0.0-Setup.exe` on the Releases page to download.

> Windows Defender SmartScreen이 나타나면 **"추가 정보" → "실행"** 을 클릭하세요.  
> If Windows Defender SmartScreen appears, click **"More info" → "Run anyway"**.

---

### 2단계 — 설치 마법사 실행 / Step 2 — Run the installer

1. 다운로드한 `Selah-1.0.0-Setup.exe`를 실행합니다.  
   Run the downloaded `Selah-1.0.0-Setup.exe`.
2. 설치 언어를 선택합니다 (한국어 / English).  
   Select installation language.
3. 설치 경로를 확인하고 **설치** 를 클릭합니다.  
   Confirm the installation path and click **Install**.
4. 설치 완료 후 **"Python 패키지 설치"** 체크박스가 나타납니다.  
   When finished, a **"Install Python packages"** checkbox appears.  
   - AI 기능(스템 분리·악보 인식)을 사용할 예정이면 체크하고 **마침** 을 클릭하세요.  
     Check it if you plan to use AI features (stem separation / sheet music), then click **Finish**.

> **.NET 런타임은 별도 설치 불필요** — 설치 파일에 포함되어 있습니다.  
> **.NET runtime is included** in the installer — no separate download needed.

---

### 3단계 — Python 패키지 설치 (AI 기능) / Step 3 — Install Python packages (AI features)

스템 분리·노이즈 제거·악보 인식을 사용하려면 Python 환경이 필요합니다.  
Python is required for stem separation, noise reduction, and sheet music recognition.

#### Python 설치 / Install Python

Python이 없으면 먼저 설치합니다.  
If Python is not installed:

1. <https://www.python.org/downloads/> 에서 **Python 3.10 이상** 다운로드  
   Download **Python 3.10+** from <https://www.python.org/downloads/>
2. 설치 중 **"Add Python to PATH"** 반드시 체크  
   Check **"Add Python to PATH"** during installation
3. 설치 확인: 명령 프롬프트에서 `python --version` 실행  
   Verify: run `python --version` in Command Prompt

#### 패키지 설치 / Install packages

설치 폴더(기본: `C:\Program Files\Selah\`)에서 `setup_env.bat`을 실행합니다.  
Run `setup_env.bat` from the install folder (default: `C:\Program Files\Selah\`).

```
C:\Program Files\Selah\setup_env.bat
```

또는 기능별로 선택 설치 / Or install by feature:

| 파일 / File | 대상 기능 / Feature |
|-------------|---------------------|
| `setup_env.ko.bat` | 한국어 안내로 전체 선택 설치 |
| `setup_env.en.bat` | English guided install |
| `requirements.txt` | `pip install -r requirements.txt` (전체) |
| `requirements-stem.txt` | 스템 분리만 · Stem separation only |
| `requirements-sheet-music.txt` | 악보 인식만 · Sheet music only |

---

### 4단계 — FFmpeg 설치 (WAV 외 형식 지원) / Step 4 — FFmpeg (non-WAV formats)

MP3·MP4·FLAC 등을 가져오거나 내보내려면 FFmpeg가 필요합니다.  
Required to import/export MP3, MP4, FLAC, and other non-WAV formats.

1. <https://ffmpeg.org/download.html> 에서 Windows 빌드 다운로드  
   Download a Windows build from <https://ffmpeg.org/download.html>  
   (권장 / recommended: **gyan.dev full build** 또는 / or **BtbN release**)
2. 압축 해제 후 `bin\` 폴더 경로 확인 (`ffmpeg.exe`, `ffprobe.exe` 위치)  
   Extract and note the `bin\` folder containing `ffmpeg.exe` and `ffprobe.exe`
3. 시스템 PATH에 `bin\` 폴더 추가  
   Add the `bin\` folder to system PATH:  
   *시스템 속성 → 환경 변수 → Path → 새로 만들기 / System Properties → Environment Variables → Path → New*
4. 확인 / Verify: `ffmpeg -version`

---

### 5단계 — FluidSynth + SoundFont (악보 인식 전용) / Step 5 — FluidSynth + SoundFont (sheet music only)

악보 인식 기능을 사용하지 않는다면 이 단계는 건너뛰세요.  
Skip this step if you do not use the sheet music recognition feature.

#### FluidSynth 네이티브 DLL / Native DLL

1. <https://www.fluidsynth.org/> 에서 Windows 설치 프로그램 다운로드 후 실행  
   Download and run the Windows installer from <https://www.fluidsynth.org/>
2. 설치 후 `C:\Program Files\FluidSynth\bin\libfluidsynth-3.dll` 확인  
   After install, verify `C:\Program Files\FluidSynth\bin\libfluidsynth-3.dll` exists

#### SoundFont 파일 배치 / Place a SoundFont file

아래 경로 중 하나에 `.sf2` 또는 `.sf3` 파일을 넣습니다.  
Place a `.sf2` or `.sf3` file in any of these locations:

| 우선순위 / Priority | 경로 / Path |
|---------------------|-------------|
| 1 (최우선 / highest) | `%AppData%\Selah\soundfonts\` |
| 2 | `C:\Program Files\Selah\soundfonts\` |
| 3 | `C:\Program Files\FluidSynth\` |
| 4 | `C:\soundfonts\` |

**권장 SoundFont / Recommended SoundFont:**

| SoundFont | 크기 / Size | 품질 / Quality | 라이선스 / License |
|-----------|-------------|----------------|-------------------|
| **GeneralUser GS** ★ 권장 | ~29 MB | ★★★★☆ | 무료 / Free |
| MuseScore_General.sf3 | ~50 MB | ★★★★★ | MIT |
| MuseScore_General.sf2 | ~206 MB | ★★★★★ | MIT |

- GeneralUser GS 다운로드 / Download: `schristiancollins.com/generaluser.php`
- MuseScore_General.sf3: MuseScore 4 설치 경로 / install path  
  `C:\Program Files\MuseScore 4\sound\MuseScore_General.sf3`

---

### 설치 요약 / Installation Summary

| 기능 / Feature | 필요 항목 / Requires |
|----------------|----------------------|
| 기본 재생·편집 / Basic playback & editing | 설치 파일만 / Installer only |
| MP3·MP4 가져오기 / Import MP3·MP4 | FFmpeg |
| 스템 분리 / Stem separation | Python 3.10+ · `audio-separator` 또는 `onnxruntime` |
| 노이즈 제거 / Noise reduction | Python 3.10+ · `noisereduce` |
| 악보 인식 / Sheet music recognition | Python 3.10+ · `oemer` · FluidSynth · SoundFont |

전체 설치 안내: **[docs/SETUP.ko.md](docs/SETUP.ko.md)** · **[docs/SETUP.md](docs/SETUP.md)**

---

## 버전 히스토리 / Release History

- 한국어: **[HISTORY.ko.md](HISTORY.ko.md)**
- English: **[HISTORY.md](HISTORY.md)**

---

## 저장소 구조 / Repository Structure

```text
Selah.sln
├─ src/
│  ├─ Selah.App/               # WPF 애플리케이션 / WPF application
│  └─ Selah.Core/              # 오디오 엔진, 모델, 서비스 / audio engine, models, services
├─ scripts/
│  ├─ sheet_music_runner.py    # OMR 파이프라인 / OMR pipeline
│  ├─ midi_synthesizer.py      # MIDI → WAV (FluidSynth)
│  ├─ demucs_runner.py         # Demucs 스템 분리 / stem separation
│  ├─ onnx_runner.py           # ONNX 스템 분리 / ONNX stem separation
│  ├─ audio_separator_runner.py
│  └─ noise_reducer.py
├─ docs/
│  ├─ MANUAL.ko.md             # 사용자 매뉴얼 (한국어)
│  ├─ SETUP.md / SETUP.ko.md   # 설치 안내 / Setup guide
│  ├─ ETHICS.md / ETHICS.ko.md
│  └─ TRADEMARK.md / TRADEMARK.ko.md
├─ installer/
│  └─ build_release.bat        # 릴리즈 빌드 스크립트 / Release build script
├─ Selah.iss                   # Inno Setup 설치 스크립트 / installer script
├─ requirements.txt            # 전체 Python 패키지 / All Python packages
├─ setup_env.bat               # 환경 설치 (언어 선택) / Setup wizard
├─ README.md                   # 이 파일 (한/영 합본) / This file (KO+EN)
├─ README.en.md                # English only
├─ README.ko.md                # 한국어 전용
├─ LICENSE
└─ THIRD_PARTY_NOTICES.md
```

---

## 라이선스 / License

GNU General Public License v3.0 — [LICENSE](LICENSE)  
Copyright (C) 2026 HANDTECH 노진문(Noh JinMoon)

*처리 대상 음원의 저작권은 사용자가 직접 확인하세요.*  
*Users are responsible for verifying copyright of any audio processed.*
