; =============================================================================
;  Snapboard installer — Inno Setup 6 script
;
;  Built by the GitHub Actions release workflow (.github/workflows/release.yml):
;      iscc /DAppVersion=x.y.z /DSourceDir=<publish-output> installer\Snapboard.iss
;
;  Inputs (override via /D on the command line):
;      AppVersion   — full SemVer, e.g. 0.1.0. Defaults to 0.0.0 for local dev.
;      SourceDir    — folder containing the published single-file Snapboard.exe.
;      OutputDir    — where to drop the finished setup .exe. Defaults to .\dist.
; =============================================================================

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\Snapboard\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#endif

#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

#define AppName        "Snapboard"
#define AppPublisher   "FlowDesk"
#define AppURL         "https://flowdesk.tech"
#define AppExeName     "Snapboard.exe"
#define AppId          "{{B3E7F3E2-5D58-4B6A-9F7E-7C17C6B3C7A1}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
VersionInfoVersion={#AppVersion}

; Install per-user by default so no UAC prompt is required. Users who want
; a machine-wide install can pass /ALLUSERS on the command line.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline dialog
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto

OutputDir={#OutputDir}
OutputBaseFilename=Snapboard-{#AppVersion}-Setup
SetupIconFile=..\Snapboard\Assets\snapboard.ico
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";   Description: "Create a &desktop shortcut";         GroupDescription: "Additional shortcuts:"
Name: "startmenuicon"; Description: "Create a Start &Menu shortcut";      GroupDescription: "Additional shortcuts:"; Flags: checkedonce
Name: "runonstartup";  Description: "Launch {#AppName} when I sign in to Windows"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; Self-contained single-file publish produces just Snapboard.exe (+ a handful
; of resource side-files we also copy if they exist). The wildcard grabs every
; file dotnet publish drops in SourceDir so we stay robust to future output
; layout changes (e.g. when single-file extraction is disabled).
Source: "{#SourceDir}\*";   DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: startmenuicon
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Optional Run-at-logon entry. Snapboard also manages this key itself from
; its own Settings → Startup toggle; picking "Launch at sign-in" here just
; pre-seeds it for first launch.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "Snapboard"; \
    ValueData: """{app}\{#AppExeName}"" --autostart"; \
    Flags: uninsdeletevalue; Tasks: runonstartup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; Best-effort: stop a running instance before removing files so the uninstall
; isn't blocked by locked handles. /F = force, /IM = by image name.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExeName}"; Flags: runhidden; RunOnceId: "StopSnapboard"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Snapboard\logs"
