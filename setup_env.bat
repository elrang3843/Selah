@echo off
chcp 65001 >nul
setlocal

echo ============================================================
echo  Selah — Setup / 설치 / 安装
echo ============================================================
echo.
echo  [1] 한국어
echo  [2] English
echo  [3] 中文 (简体)
echo.
set /p LANG="Select / 선택 / 选择 (1/2/3, default: 1): "
if "%LANG%"=="" set LANG=1

if "%LANG%"=="1" ( call "%~dp0setup_env.ko.bat" & goto END )
if "%LANG%"=="2" ( call "%~dp0setup_env.en.bat" & goto END )
if "%LANG%"=="3" ( call "%~dp0setup_env.zh.bat" & goto END )

echo Invalid input. / 잘못된 입력. / 输入无效。

:END
endlocal
