#define MyAppName "Qlip"
#define MyAppExeName "Qlip.exe"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Qlip"

[Setup]
AppId={{7A1A2C6D-5A2B-4B78-AF10-8F0C3A5A5B77}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=dist
OutputBaseFilename=QlipSetup_{#MyAppVersion}_win-x64_offline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

; EULA / License acceptance
LicenseFile=EULA.txt
; Show third-party notices before install
InfoBeforeFile=THIRD-PARTY-NOTICES.txt

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにアイコンを作成"; GroupDescription: "追加タスク"; Flags: unchecked

[Files]
; Build output is staged under installer\publish\
Source: "publish\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"
Name: "{autodesktop}\\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\\{#MyAppExeName}"; Description: "{#MyAppName} を起動"; Flags: nowait postinstall skipifsilent
