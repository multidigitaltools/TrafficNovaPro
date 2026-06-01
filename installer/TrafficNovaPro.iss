; ──────────────────────────────────────────────────────────────────────────────
; TrafficNova Pro — Inno Setup 6 Installer Script
; Step 118 — Build: iscc TrafficNovaPro.iss
; ──────────────────────────────────────────────────────────────────────────────

#define AppName       "TrafficNova Pro"
#define AppVersion    "1.0.0"
#define AppPublisher  "MultiDigitalTools"
#define AppURL        "https://multidigitaltools.com/trafficnova"
#define AppExeName    "TrafficNova.exe"
#define PublishDir    "..\publish\win-x64"

[Setup]
AppId={{B3A7E1F2-9C4D-4E8A-B1F3-0D2E5A7C9B1D}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/support
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE.txt
OutputDir=..\dist
OutputBaseFilename=TrafficNovaPro-Setup-{#AppVersion}
SetupIconFile=..\TrafficNova\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
CloseApplications=yes
RestartApplications=no

; Require .NET 10 Desktop Runtime on Windows 10+
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";   Description: "{cm:CreateDesktopIcon}";   GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenufolder"; Description: "Create Start Menu folder"; GroupDescription: "{cm:AdditionalIcons}"
Name: "launchapp";     Description: "Launch {#AppName} after install"; GroupDescription: "After installation:"

[Files]
; Main publish output
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";          Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent; Tasks: launchapp

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// ── Pre-req: check for .NET 10 Desktop Runtime ────────────────────────────
function DotNetRuntimeInstalled: Boolean;
var
  Key: String;
begin
  Key := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  Result := RegKeyExists(HKLM, Key) or RegKeyExists(HKCU, Key);
end;

function InitializeSetup: Boolean;
var
  MsgResult: Integer;
begin
  Result := True;
  if not DotNetRuntimeInstalled then
  begin
    MsgResult := MsgBox(
      '.NET 10 Desktop Runtime is required but not detected.' + #13#10 +
      'Please download and install it from:' + #13#10 +
      'https://dotnet.microsoft.com/en-us/download/dotnet/10.0' + #13#10#13#10 +
      'Click OK to continue setup anyway, or Cancel to abort.',
      mbConfirmation, MB_OKCANCEL);
    if MsgResult = IDCANCEL then
      Result := False;
  end;
end;
