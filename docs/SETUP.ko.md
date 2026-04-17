# Selah — 설치 및 의존 도구 안내

이 문서는 Selah의 모든 기능을 사용하기 위해 필요한 외부 도구와 Python 패키지를 설명합니다.
기본 재생 및 편집 기능은 아래 의존 요소 없이도 동작하며, 각 항목은 특정 기능 그룹을 활성화합니다.

---

## 1. 필수 런타임

### .NET 8 데스크톱 런타임 (필수)

Selah는 .NET 8 기반 WPF 애플리케이션입니다.

- 다운로드: <https://dotnet.microsoft.com/download/dotnet/8.0>
- **".NET Desktop Runtime 8.x"** (Windows x64) 를 선택하세요.

### Python 3.10 이상 (AI 기능 필수)

스템 분리, 노이즈 제거, 악보 인식에 필요합니다.

- 다운로드: <https://www.python.org/downloads/>
- 설치 중 **"Add Python to PATH"** 옵션을 반드시 체크하세요.
- 확인: 터미널에서 `python --version` 실행.

---

## 2. FFmpeg (오디오 가져오기 / 내보내기)

WAV 이외의 형식 가져오기 및 오디오 내보내기에 사용됩니다.

### 설치 방법

1. <https://ffmpeg.org/download.html> 에서 Windows 빌드를 다운로드합니다
   (예: "gyan.dev full" 또는 "BtbN" 릴리스).
2. 압축을 풀고 `ffmpeg.exe`와 `ffprobe.exe`가 있는 `bin\` 폴더 경로를 확인합니다.
3. 해당 경로를 시스템 `PATH`에 추가합니다
   (*시스템 속성 → 환경 변수 → Path → 새로 만들기*).
4. 확인: `ffmpeg -version`

---

## 3. 스템 분리

두 가지 스템 분리 엔진을 지원합니다. 하나 또는 모두 설치하세요.

### 옵션 A — audio-separator (권장)

```
pip install "audio-separator[cpu]"
```

GPU 가속 (선택, NVIDIA GPU + CUDA 필요):

```
pip install "audio-separator[gpu]"
```

모델은 처음 사용 시 자동으로 다운로드됩니다 (각 100–400 MB).

### 옵션 B — ONNX Runtime (경량)

```
pip install onnxruntime numpy scipy
```

모델은 Selah 모델 관리자에서 첫 사용 전에 다운로드합니다.

---

## 4. 노이즈 제거

```
pip install noisereduce soundfile numpy
```

추가 시스템 도구는 필요하지 않습니다.

---

## 5. 악보 인식 (OMR)

악보 가져오기 기능(파일 → 악보 이미지 가져오기)은 스캔하거나 촬영한 악보 이미지를
악기별 오디오 트랙으로 변환합니다. 세 단계로 처리됩니다:
이미지 전처리 → OMR 인식 → MIDI 합성.

### 5.1 OMR 및 분석 패키지

```
pip install oemer music21 mido Pillow scipy
```

| 패키지   | 역할                                      |
|----------|-------------------------------------------|
| oemer    | 광학 음악 인식(OMR) 엔진                  |
| music21  | MusicXML / MIDI 분석 및 내보내기          |
| mido     | MIDI 파일 조작 및 GM 패치 교체            |
| Pillow   | 이미지 전처리 (그레이스케일, 이진화)      |
| scipy    | 악보 이미지 노이즈 제거 필터              |

### 5.2 FluidSynth — MIDI → WAV 합성

FluidSynth는 인식된 MIDI를 SoundFont 악기 뱅크를 사용하여 오디오로 변환합니다.
두 가지 구성 요소가 필요합니다: **네이티브 라이브러리**와 **Python 래퍼**.

#### Step 1 — FluidSynth 네이티브 라이브러리 설치

<https://www.fluidsynth.org/> 에서 Windows 설치 프로그램을 다운로드하여 실행합니다.

> 설치 프로그램은 `libfluidsynth-3.dll` (및 `fluidsynth.exe` 실행 파일)을
> `C:\Program Files\FluidSynth\bin\` 에 설치합니다.
> Selah가 DLL을 자동으로 찾으므로 PATH 설정은 필요하지 않습니다.

또는 Chocolatey로 설치:

```
choco install fluidsynth
```

#### Step 2 — Python 래퍼 설치

```
pip install fluidsynth
```

이 명령은 Selah의 Python 스크립트가 네이티브 라이브러리를 직접 호출할 수 있도록
하는 ctypes 바인딩을 설치합니다. `fluidsynth.exe`를 별도로 실행할 필요가 없습니다.

> **두 단계가 모두 필요한 이유:** `pip install fluidsynth`는 Python 래퍼만 설치합니다.
> 래퍼는 실제로 작동하기 위해 네이티브 설치 프로그램이 제공하는
> `libfluidsynth-3.dll`이 필요합니다.

#### 동작 확인

```python
python -c "import fluidsynth; print('fluidsynth OK')"
```

`fluidsynth OK`가 출력되면 합성 준비가 된 것입니다
(SoundFont도 설치되어 있어야 합니다 — §5.3 참조).

---

### 5.3 SoundFont (.sf2)

SoundFont는 FluidSynth가 MIDI 음표를 실제 악기 음색으로 렌더링하는 데 사용하는
악기 샘플 뱅크입니다. 파일 크기 문제로 Selah에 포함되어 있지 않습니다.

#### 무료 SoundFont 비교

SF2와 SF3 형식 모두 지원합니다. SF3는 OGG Vorbis로 샘플을 압축한 포맷으로, 동일한 음질에 훨씬 작은 파일 크기를 제공합니다 (FluidSynth 1.1.7+ 필요).

| SoundFont | 형식 | 크기 | 음질 | 라이선스 | 비고 |
|-----------|------|------|------|----------|------|
| **GeneralUser GS** | SF2 | ~29 MB | ★★★★☆ | 무료 (상업 사용 가능) | 크기 대비 품질 최고, **권장** |
| **MuseScore_General.sf3** | SF3 | ~50 MB | ★★★★★ | MIT | 최고 음질 + 압축으로 소형화 |
| **MuseScore_General.sf2** | SF2 | ~206 MB | ★★★★★ | MIT | 최고 음질 (비압축) |
| **FluidR3_GM.sf2** | SF2 | ~141 MB | ★★★☆☆ | MIT | 널리 배포되지만 관악기 품질 낮음 |

---

**GeneralUser GS** — 권장 (대부분의 경우)

파일이 가장 작으면서도 음질이 균형 잡혀 있습니다. 피아노·스트링·목관악기(색소폰 포함)에서 좋은 성능을 보입니다.

- 다운로드: `schristiancollins.com/generaluser.php`
- 압축 해제 후 `GeneralUser GS.sf2` 파일을 사용합니다.

**MuseScore_General.sf3** — 최고 음질 + 소형 (권장 대용량 옵션)

MuseScore_General.sf2와 동일한 샘플을 OGG Vorbis로 압축한 SF3 파일입니다. 206 MB → ~50 MB로 크기가 크게 줄어들면서 음질은 동일합니다.

- MuseScore 설치 경로: `C:\Program Files\MuseScore 4\sound\MuseScore_General.sf3`
- MuseScore 공식 웹사이트에서도 별도 다운로드 가능합니다.

**MuseScore_General.sf2** — 최고 음질 (비압축, SF3 대안)

SF3 지원 여부가 불확실한 구형 FluidSynth 환경에서의 대안. 동일한 위치에서 구할 수 있습니다.

**FluidR3_GM.sf2** — 대안 (낮은 품질)

Linux 배포판과 일부 음악 소프트웨어에 기본 포함되어 있어 구하기 쉽지만, 금관·목관악기 샘플 품질이 낮습니다(색소폰 음색이 특히 좋지 않음). 다른 옵션을 구하기 어려울 때 대안으로 사용합니다.

> **예배 음악 용도:** GeneralUser GS를 권장합니다. 피아노·스트링·어쿠스틱 기타·목관악기 패치가 찬송·예배 악보 재생에 적합합니다.

#### .sf2 / .sf3 파일 배치 위치

아래 위치 중 **하나**에 `.sf2` 또는 `.sf3` 파일을 넣으면 됩니다 (우선순위 순으로 탐색):

| 우선순위 | 경로 |
|----------|------|
| 1 (최우선) | `%AppData%\Selah\soundfonts\` |
| 2 | `<앱 폴더>\soundfonts\` |
| 3 | `C:\Program Files\FluidSynth\` |
| 4 | `C:\soundfonts\` |

권장 위치는 `%AppData%\Selah\soundfonts\`입니다 (Selah가 첫 실행 시 자동 생성).
탐색기 주소 표시줄에 해당 경로를 직접 입력하면 폴더를 열 수 있습니다.

---

## 6. 기능별 요약

| 기능                       | 필요한 패키지 / 도구 |
|----------------------------|----------------------|
| 기본 재생 및 편집           | .NET 8 데스크톱 런타임 |
| 오디오 가져오기 / 내보내기  | PATH에 FFmpeg + FFprobe |
| 스템 분리 (audio-separator) | Python 3.10+ · `audio-separator[cpu]` |
| 스템 분리 (ONNX)            | Python 3.10+ · `onnxruntime numpy scipy` |
| 노이즈 제거                 | Python 3.10+ · `noisereduce soundfile numpy` |
| 악보 인식                   | Python 3.10+ · `oemer music21 mido Pillow scipy` |
| 악보 합성                   | FluidSynth 설치 프로그램 · `pip install fluidsynth` · SoundFont (.sf2) |

---

## 7. 전체 기능 빠른 설치

```bat
:: 모든 Python 패키지를 한 번에 설치합니다
pip install oemer music21 mido Pillow scipy fluidsynth noisereduce soundfile numpy "audio-separator[cpu]"
```

이후:
1. <https://www.fluidsynth.org/> 에서 FluidSynth를 설치합니다 (합성용 네이티브 DLL 제공).
2. SoundFont를 다운로드하여 `%AppData%\Selah\soundfonts\`에 넣습니다.
3. FFmpeg를 설치하고 `bin\` 폴더를 PATH에 추가합니다.

---

## 8. 문제 해결

### "FluidSynth를 찾을 수 없습니다"

- `pip install fluidsynth`를 실행했는지 확인하세요.
- FluidSynth **네이티브 설치 프로그램**을 실행했는지 확인하세요 (`libfluidsynth-3.dll` 제공).
- DLL이 비표준 경로에 있는 경우, 해당 폴더를 시스템 `PATH`에 추가하세요.

### "SoundFont(.sf2)를 찾을 수 없습니다"

- `.sf2` 파일을 `%AppData%\Selah\soundfonts\`에 넣고 Selah를 재시작하세요.

### "oemer를 찾을 수 없음" / OMR 실패

- `pip install oemer`를 실행하고 Python 3.10 이상이 PATH에 있는지 확인하세요.
- oemer는 첫 실행 시 자체 모델 가중치를 다운로드하기 위해 인터넷 연결이 필요합니다.

### Python이 감지되지 않음

- Python을 재설치하면서 설치 중 **"Add Python to PATH"**를 체크하세요.
- Windows 스토어 버전의 Python은 백그라운드 프로세스 환경에서 감지되지 않을 수 있습니다.
  python.org의 설치 프로그램을 사용하세요.
