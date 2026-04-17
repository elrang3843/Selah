
---

# `README.ko.md`

```md
# Selah

**Selah**는 예배 음악 준비, 프로젝트 기반 오디오 편집, AI 기반 스템 분리 기능을 지원하기 위해 개발 중인 **Windows 데스크톱 애플리케이션**입니다.

교회, 선교지, 소규모 찬양팀이 예배 녹음물이나 미디어로부터 반주 중심의 오디오 자료를 보다 실용적으로 준비할 수 있도록 돕는 것을 목표로 합니다.

> 현재 상태: 초기 프로토타입 / 구조와 기능을 계속 정리·확장 중

---

## 프로젝트 개요

Selah는 다음 세 가지 흐름을 하나의 워크플로우로 묶는 것을 목표로 합니다.

- **프로젝트 기반 예배 오디오 준비**
- **타임라인 기반 클립 / 트랙 편집**
- **AI 기반 스템 분리**

현재 애플리케이션은 다음과 같은 구조로 구현되어 있습니다.

- **WPF 데스크톱 앱** (`Selah.App`)
- **공통 코어 라이브러리** (`Selah.Core`)
- **Python + Demucs 기반 분리 프로토타입**

---

## 주요 목표

Selah는 다음과 같은 환경을 지원하기 위해 개발되고 있습니다.

- 소형 교회 및 개척교회 예배 준비
- 선교 및 사역 중심 오디오 작업
- 예배 녹음물로부터 반주 자료 준비
- 분리된 스템을 활용한 프로젝트 기반 재구성
- 클라우드 의존 없이 로컬 환경에서 수행 가능한 작업 흐름

이 프로젝트는 **비상업적 사역 지향의 무료 오픈소스 도구**를 목표로 합니다.

---

## 현재 포함된 기능

현재 저장소에는 다음 요소들이 포함되어 있습니다.

- WPF 데스크톱 애플리케이션 (`Selah.App`)
- 공통 코어 라이브러리 (`Selah.Core`)
- 프로젝트 / 트랙 / 클립 데이터 모델
- 타임라인 관련 UI 및 ViewModel
- 오디오 엔진 및 믹서 관련 구성 요소
- 웨이브폼 캐시 지원
- FFmpeg / FFprobe 래퍼 서비스
- 하드웨어 감지 서비스
- 모델 관리 서비스
- 스템 분리 서비스 (audio-separator, ONNX Runtime, Demucs 엔진)
- 노이즈 제거 서비스
- **악보 인식** — 스캔·촬영한 악보 이미지 → 악기별 오디오 트랙 (oemer OMR + FluidSynth 합성)
- 다국어 리소스 (한국어 / 영어 / 중국어)
- 테마 리소스 (라이트 / 다크)

---

## 설치 및 의존 도구

전체 설치 안내는 **[docs/SETUP.ko.md](docs/SETUP.ko.md)** 를 참조하세요.

- .NET 8 런타임 및 Python 3.10+
- FFmpeg (오디오 가져오기/내보내기)
- 스템 분리·노이즈 제거·악보 인식용 Python 패키지
- MIDI 합성을 위한 FluidSynth 및 SoundFont(.sf2/.sf3) — 권장: **GeneralUser GS** (~29 MB, 무료) 또는 **MuseScore_General.sf3** (~50 MB, MIT, 최고 품질)

---

## 저장소 구조

```text
Selah.sln
├─ src/
│  ├─ Selah.App/               # WPF 애플리케이션
│  └─ Selah.Core/              # 오디오 엔진, 모델, 서비스
├─ scripts/
│  ├─ sheet_music_runner.py    # OMR 파이프라인 (oemer + music21)
│  ├─ midi_synthesizer.py      # MIDI → WAV 합성 (FluidSynth)
│  ├─ demucs_runner.py         # Demucs 스템 분리
│  ├─ onnx_runner.py           # ONNX 스템 분리
│  ├─ audio_separator_runner.py
│  └─ noise_reducer.py
├─ docs/
│  ├─ SETUP.md                 # 의존 도구 설치 안내 (영어)
│  ├─ SETUP.ko.md              # 의존 도구 설치 안내 (한국어)
│  ├─ ETHICS.md
│  ├─ TRADEMARK.md
│  ├─ ETHICS.ko.md
│  └─ TRADEMARK.ko.md
├─ README.md
├─ README.ko.md
├─ LICENSE
└─ THIRD_PARTY_NOTICES.md
