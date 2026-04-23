@echo off
chcp 65001 >nul
setlocal

echo ============================================================
echo  Selah — 环境安装向导
echo  敬拜伴奏编辑器 v1.0.0
echo ============================================================
echo.

:: ── 检测 Python ──────────────────────────────────────────────
python --version >nul 2>&1
if errorlevel 1 (
    echo [错误] 未找到 Python。
    echo   请从 python.org 安装 Python 3.10 或更高版本，
    echo   安装时务必勾选 "Add Python to PATH"，然后重新运行此脚本。
    echo   https://www.python.org/downloads/
    pause
    exit /b 1
)

for /f "tokens=2" %%v in ('python --version 2^>^&1') do set PY_VER=%%v
echo [确认] 检测到 Python %PY_VER%
echo.

:: ── 选择安装模式 ──────────────────────────────────────────────
echo 请选择要安装的功能：
echo   [1] 完整安装  （人声分离 + 降噪 + 乐谱识别）
echo   [2] 仅人声分离  （audio-separator + ONNX + Demucs）
echo   [3] 仅乐谱识别  （OMR + FluidSynth）
echo   [4] 仅降噪
echo   [5] 取消
echo.
set /p CHOICE="请输入编号（默认：1）："
if "%CHOICE%"=="" set CHOICE=1

if "%CHOICE%"=="1" goto INSTALL_ALL
if "%CHOICE%"=="2" goto INSTALL_STEM
if "%CHOICE%"=="3" goto INSTALL_SHEET
if "%CHOICE%"=="4" goto INSTALL_NOISE
if "%CHOICE%"=="5" goto END
echo [错误] 输入无效。
goto END

:: ── 完整安装 ──────────────────────────────────────────────────
:INSTALL_ALL
echo.
echo [1/3] 正在安装人声分离软件包...
pip install "audio-separator[cpu]" onnxruntime demucs soundfile numpy scipy
if errorlevel 1 ( echo [警告] 部分人声分离软件包安装失败。 & goto CONTINUE_ALL )

:CONTINUE_ALL
echo.
echo [2/3] 正在安装降噪软件包...
pip install noisereduce soundfile numpy
if errorlevel 1 echo [警告] 部分降噪软件包安装失败。

echo.
echo [3/3] 正在安装乐谱识别软件包...
pip install oemer music21 mido Pillow scipy fluidsynth
if errorlevel 1 echo [警告] 部分乐谱识别软件包安装失败。
goto POST_INSTALL

:: ── 仅人声分离 ────────────────────────────────────────────────
:INSTALL_STEM
echo.
echo 正在安装人声分离软件包...
pip install "audio-separator[cpu]" onnxruntime demucs soundfile numpy scipy
if errorlevel 1 echo [警告] 部分软件包安装失败。
goto POST_INSTALL

:: ── 仅乐谱识别 ────────────────────────────────────────────────
:INSTALL_SHEET
echo.
echo 正在安装乐谱识别软件包...
pip install oemer music21 mido Pillow scipy fluidsynth numpy
if errorlevel 1 echo [警告] 部分软件包安装失败。
goto POST_INSTALL

:: ── 仅降噪 ────────────────────────────────────────────────────
:INSTALL_NOISE
echo.
echo 正在安装降噪软件包...
pip install noisereduce soundfile numpy
if errorlevel 1 echo [警告] 部分软件包安装失败。
goto POST_INSTALL

:: ── 安装完成提示 ──────────────────────────────────────────────
:POST_INSTALL
echo.
echo ============================================================
echo  Python 软件包安装完成。
echo ============================================================
echo.
echo 以下项目需要单独安装：
echo.
echo  [FFmpeg]
echo    从 https://ffmpeg.org/download.html 下载
echo    将 bin\ 文件夹添加到系统 PATH 环境变量。
echo    验证：ffmpeg -version
echo.

if "%CHOICE%"=="3" goto FLUIDSYNTH_NOTICE
if "%CHOICE%"=="1" goto FLUIDSYNTH_NOTICE
goto END

:FLUIDSYNTH_NOTICE
echo  [FluidSynth 原生 DLL]
echo    从 https://www.fluidsynth.org/ 运行 Windows 安装程序
echo    （提供 libfluidsynth-3.dll — 仅 pip 安装不够）
echo.
echo  [SoundFont (.sf2 / .sf3)]
echo    下载以下之一并放置到：
echo      %%AppData%%\Selah\soundfonts\
echo.
echo    推荐：GeneralUser GS（约 29 MB，免费）
echo      https://schristiancollins.com/generaluser.php
echo    最佳音质：MuseScore_General.sf3（约 50 MB，MIT 协议）
echo      MuseScore 路径：C:\Program Files\MuseScore 4\sound\
echo.

:END
echo.
pause
endlocal
