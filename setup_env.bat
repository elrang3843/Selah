@echo off
chcp 65001 >nul
setlocal

echo ============================================================
echo  Selah — 환경 설치 스크립트
echo  찬양사역용 MR편집기 v1.0.0
echo ============================================================
echo.

:: ── Python 감지 ──────────────────────────────────────────────
python --version >nul 2>&1
if errorlevel 1 (
    echo [오류] Python을 찾을 수 없습니다.
    echo   python.org 에서 Python 3.10 이상을 설치하고
    echo   "Add Python to PATH" 옵션을 체크한 뒤 다시 실행하세요.
    echo   https://www.python.org/downloads/
    pause
    exit /b 1
)

for /f "tokens=2" %%v in ('python --version 2^>^&1') do set PY_VER=%%v
echo [확인] Python %PY_VER% 감지됨
echo.

:: ── 설치 모드 선택 ────────────────────────────────────────────
echo 설치할 기능을 선택하세요:
echo   [1] 전체 설치  (스템 분리 + 노이즈 제거 + 악보 인식)
echo   [2] 스템 분리만  (audio-separator + ONNX + Demucs)
echo   [3] 악보 인식만  (OMR + FluidSynth)
echo   [4] 노이즈 제거만
echo   [5] 취소
echo.
set /p CHOICE="번호 입력 (기본값: 1): "
if "%CHOICE%"=="" set CHOICE=1

if "%CHOICE%"=="1" goto INSTALL_ALL
if "%CHOICE%"=="2" goto INSTALL_STEM
if "%CHOICE%"=="3" goto INSTALL_SHEET
if "%CHOICE%"=="4" goto INSTALL_NOISE
if "%CHOICE%"=="5" goto END
echo [오류] 잘못된 입력입니다.
goto END

:: ── 전체 설치 ─────────────────────────────────────────────────
:INSTALL_ALL
echo.
echo [1/3] 스템 분리 패키지 설치 중...
pip install "audio-separator[cpu]" onnxruntime demucs soundfile numpy scipy
if errorlevel 1 ( echo [경고] 스템 분리 패키지 설치 중 오류가 발생했습니다. & goto CONTINUE_ALL )

:CONTINUE_ALL
echo.
echo [2/3] 노이즈 제거 패키지 설치 중...
pip install noisereduce soundfile numpy
if errorlevel 1 echo [경고] 노이즈 제거 패키지 설치 중 오류가 발생했습니다.

echo.
echo [3/3] 악보 인식 패키지 설치 중...
pip install oemer music21 mido Pillow scipy fluidsynth
if errorlevel 1 echo [경고] 악보 인식 패키지 설치 중 오류가 발생했습니다.
goto POST_INSTALL

:: ── 스템 분리만 ───────────────────────────────────────────────
:INSTALL_STEM
echo.
echo 스템 분리 패키지 설치 중...
pip install "audio-separator[cpu]" onnxruntime demucs soundfile numpy scipy
if errorlevel 1 echo [경고] 일부 패키지 설치 중 오류가 발생했습니다.
goto POST_INSTALL

:: ── 악보 인식만 ───────────────────────────────────────────────
:INSTALL_SHEET
echo.
echo 악보 인식 패키지 설치 중...
pip install oemer music21 mido Pillow scipy fluidsynth numpy
if errorlevel 1 echo [경고] 일부 패키지 설치 중 오류가 발생했습니다.
goto POST_INSTALL

:: ── 노이즈 제거만 ─────────────────────────────────────────────
:INSTALL_NOISE
echo.
echo 노이즈 제거 패키지 설치 중...
pip install noisereduce soundfile numpy
if errorlevel 1 echo [경고] 일부 패키지 설치 중 오류가 발생했습니다.
goto POST_INSTALL

:: ── 설치 완료 안내 ────────────────────────────────────────────
:POST_INSTALL
echo.
echo ============================================================
echo  Python 패키지 설치가 완료되었습니다.
echo ============================================================
echo.
echo 다음 항목은 별도로 설치해야 합니다:
echo.
echo  [FFmpeg]
echo    https://ffmpeg.org/download.html 에서 다운로드 후
echo    bin\ 폴더를 시스템 PATH에 추가하세요.
echo    확인: ffmpeg -version
echo.

if "%CHOICE%"=="3" goto FLUIDSYNTH_NOTICE
if "%CHOICE%"=="1" goto FLUIDSYNTH_NOTICE
goto END

:FLUIDSYNTH_NOTICE
echo  [FluidSynth 네이티브 DLL]
echo    https://www.fluidsynth.org/ 에서 Windows 설치 프로그램 실행
echo    (libfluidsynth-3.dll 제공 — pip만으로는 부족합니다)
echo.
echo  [SoundFont (.sf2 / .sf3)]
echo    아래 중 하나를 다운로드하여:
echo      %%AppData%%\Selah\soundfonts\  에 넣으세요.
echo.
echo    권장: GeneralUser GS (~29 MB, 무료)
echo      https://schristiancollins.com/generaluser.php
echo    최고품질: MuseScore_General.sf3 (~50 MB, MIT)
echo      MuseScore 설치 경로: C:\Program Files\MuseScore 4\sound\
echo.

:END
echo.
pause
endlocal
