; ============================================================
;  Selah 1.0.0 — Inno Setup 설치 스크립트
;
;  사전 준비:
;    dotnet publish -c Release -r win-x64 --self-contained true ^
;      -p:PublishSingleFile=false ^
;      src/Selah.App/Selah.App.csproj
;
;  출력 폴더:
;    src\Selah.App\bin\Release\net8.0-windows\win-x64\publish\
;
;  Inno Setup 6.x 이상 필요: https://jrsoftware.org/isinfo.php
; ============================================================

#define AppName      "Selah"
#define AppVersion   "1.0.0"
#define AppPublisher "HANDTECH 노진문(Noh JinMoon)"
#define AppURL       "https://github.com/elrang3843/Selah"
#define AppExeName   "Selah.exe"
#define PublishDir   "src\Selah.App\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
LicenseFile=LICENSE
OutputDir=installer
OutputBaseFilename=Selah-{#AppVersion}-Setup
; SetupIconFile=src\Selah.App\Resources\selah.ico  ; .ico 파일 준비 후 주석 해제
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
UninstallDisplayIcon={app}\{#AppExeName}
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} Setup
VersionInfoCopyright=Copyright (C) 2026 HANDTECH Noh JinMoon

[Languages]
Name: "korean";  MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon";     Description: "{cm:CreateDesktopIcon}";     GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
; ── 앱 본체 (self-contained publish 폴더 전체) ──────────────
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── Python 환경 설치 스크립트 ────────────────────────────────
Source: "setup_env.bat";            DestDir: "{app}"; Flags: ignoreversion
Source: "setup_env.ko.bat";         DestDir: "{app}"; Flags: ignoreversion
Source: "setup_env.en.bat";         DestDir: "{app}"; Flags: ignoreversion
Source: "setup_env.zh.bat";         DestDir: "{app}"; Flags: ignoreversion

; ── requirements 파일 ───────────────────────────────────────
Source: "requirements.txt";               DestDir: "{app}"; Flags: ignoreversion
Source: "requirements-stem.txt";          DestDir: "{app}"; Flags: ignoreversion
Source: "requirements-sheet-music.txt";   DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";                   Filename: "{app}\{#AppExeName}"
Name: "{group}\Python 환경 설치 (한국어)";    Filename: "{app}\setup_env.ko.bat"
Name: "{group}\Python Setup (English)";       Filename: "{app}\setup_env.en.bat"
Name: "{group}\Python 安装 (中文)";           Filename: "{app}\setup_env.zh.bat"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";             Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; 설치 직후 Python 환경 설치 스크립트 실행 여부 선택
Filename: "{app}\setup_env.bat"; \
  Description: "Python 패키지 설치 (스템 분리 / 악보 인식에 필요)"; \
  Flags: postinstall nowait skipifsilent unchecked

; 설치 완료 후 앱 바로 실행
Filename: "{app}\{#AppExeName}"; \
  Description: "{cm:LaunchProgram,{#AppName}}"; \
  Flags: postinstall nowait skipifsilent

[UninstallDelete]
; 프로젝트 파일(.slh)은 사용자 데이터이므로 삭제하지 않음
; 앱이 생성한 캐시 폴더만 정리
Type: filesandordirs; Name: "{localappdata}\Selah\ModelCache"
