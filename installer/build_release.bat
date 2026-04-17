@echo off
chcp 65001 >nul
setlocal

echo ============================================================
echo  Selah — 릴리즈 빌드 및 설치 파일 생성
echo ============================================================
echo.

set SOLUTION_DIR=%~dp0..
set PUBLISH_DIR=%SOLUTION_DIR%\src\Selah.App\bin\Release\net8.0-windows\win-x64\publish
set ISS_FILE=%~dp0Selah.iss

:: ── 1. dotnet publish ─────────────────────────────────────────
echo [1/2] dotnet publish (self-contained, win-x64)...
dotnet publish "%SOLUTION_DIR%\src\Selah.App\Selah.App.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=false ^
    -p:PublishReadyToRun=true

if errorlevel 1 (
    echo.
    echo [오류] dotnet publish 실패. 빌드 오류를 확인하세요.
    pause
    exit /b 1
)
echo.

:: ── 2. Inno Setup 컴파일 ──────────────────────────────────────
echo [2/2] Inno Setup 컴파일...

:: 일반적인 Inno Setup 설치 경로 탐색
set ISCC=
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe
if exist "C:\Program Files\Inno Setup 6\ISCC.exe"       set ISCC=C:\Program Files\Inno Setup 6\ISCC.exe

if "%ISCC%"=="" (
    echo.
    echo [경고] Inno Setup을 찾을 수 없습니다.
    echo   https://jrsoftware.org/isinfo.php 에서 설치 후 다시 실행하세요.
    echo.
    echo   또는 Inno Setup을 열고 다음 파일을 직접 컴파일하세요:
    echo   %ISS_FILE%
    pause
    exit /b 1
)

"%ISCC%" "%ISS_FILE%"

if errorlevel 1 (
    echo.
    echo [오류] Inno Setup 컴파일 실패.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo  완료!
echo  출력 파일: installer\Selah-1.0.0-Setup.exe
echo ============================================================
echo.
pause
endlocal
