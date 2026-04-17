@echo off
chcp 65001 >nul
setlocal

echo ============================================================
echo  Selah — Environment Setup
echo  MR Editor for Worship Ministry v1.0.0
echo ============================================================
echo.

:: ── Detect Python ────────────────────────────────────────────
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python not found.
    echo   Install Python 3.10 or later from python.org and
    echo   check "Add Python to PATH" during setup, then run this script again.
    echo   https://www.python.org/downloads/
    pause
    exit /b 1
)

for /f "tokens=2" %%v in ('python --version 2^>^&1') do set PY_VER=%%v
echo [OK] Python %PY_VER% detected
echo.

:: ── Select install mode ───────────────────────────────────────
echo Select the features to install:
echo   [1] Full install  (stem separation + noise reduction + sheet music)
echo   [2] Stem separation only  (audio-separator + ONNX + Demucs)
echo   [3] Sheet music recognition only  (OMR + FluidSynth)
echo   [4] Noise reduction only
echo   [5] Cancel
echo.
set /p CHOICE="Enter number (default: 1): "
if "%CHOICE%"=="" set CHOICE=1

if "%CHOICE%"=="1" goto INSTALL_ALL
if "%CHOICE%"=="2" goto INSTALL_STEM
if "%CHOICE%"=="3" goto INSTALL_SHEET
if "%CHOICE%"=="4" goto INSTALL_NOISE
if "%CHOICE%"=="5" goto END
echo [ERROR] Invalid input.
goto END

:: ── Full install ──────────────────────────────────────────────
:INSTALL_ALL
echo.
echo [1/3] Installing stem separation packages...
pip install "audio-separator[cpu]" onnxruntime demucs soundfile numpy scipy
if errorlevel 1 ( echo [WARNING] Some stem separation packages failed to install. & goto CONTINUE_ALL )

:CONTINUE_ALL
echo.
echo [2/3] Installing noise reduction packages...
pip install noisereduce soundfile numpy
if errorlevel 1 echo [WARNING] Some noise reduction packages failed to install.

echo.
echo [3/3] Installing sheet music packages...
pip install oemer music21 mido Pillow scipy fluidsynth
if errorlevel 1 echo [WARNING] Some sheet music packages failed to install.
goto POST_INSTALL

:: ── Stem separation only ──────────────────────────────────────
:INSTALL_STEM
echo.
echo Installing stem separation packages...
pip install "audio-separator[cpu]" onnxruntime demucs soundfile numpy scipy
if errorlevel 1 echo [WARNING] Some packages failed to install.
goto POST_INSTALL

:: ── Sheet music only ──────────────────────────────────────────
:INSTALL_SHEET
echo.
echo Installing sheet music packages...
pip install oemer music21 mido Pillow scipy fluidsynth numpy
if errorlevel 1 echo [WARNING] Some packages failed to install.
goto POST_INSTALL

:: ── Noise reduction only ──────────────────────────────────────
:INSTALL_NOISE
echo.
echo Installing noise reduction packages...
pip install noisereduce soundfile numpy
if errorlevel 1 echo [WARNING] Some packages failed to install.
goto POST_INSTALL

:: ── Post-install notes ────────────────────────────────────────
:POST_INSTALL
echo.
echo ============================================================
echo  Python packages installed successfully.
echo ============================================================
echo.
echo The following items require separate installation:
echo.
echo  [FFmpeg]
echo    Download from https://ffmpeg.org/download.html
echo    Add the bin\ folder to your system PATH.
echo    Verify: ffmpeg -version
echo.

if "%CHOICE%"=="3" goto FLUIDSYNTH_NOTICE
if "%CHOICE%"=="1" goto FLUIDSYNTH_NOTICE
goto END

:FLUIDSYNTH_NOTICE
echo  [FluidSynth native DLL]
echo    Run the Windows installer from https://www.fluidsynth.org/
echo    (provides libfluidsynth-3.dll — pip alone is not enough)
echo.
echo  [SoundFont (.sf2 / .sf3)]
echo    Download one of the following and place it in:
echo      %%AppData%%\Selah\soundfonts\
echo.
echo    Recommended: GeneralUser GS (~29 MB, free)
echo      https://schristiancollins.com/generaluser.php
echo    Best quality: MuseScore_General.sf3 (~50 MB, MIT)
echo      MuseScore path: C:\Program Files\MuseScore 4\sound\
echo.

:END
echo.
pause
endlocal
