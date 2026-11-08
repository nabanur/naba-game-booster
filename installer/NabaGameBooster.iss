; Naba Game Booster - Inno Setup installer
; Version GitHub Actions se aata hai:  iscc /DMyAppVersion=1.2.3 NabaGameBooster.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "Naba Game Booster"
#define MyAppExe  "NabaGameBooster.exe"

[Setup]
AppId={{6E1B7C2A-5D3F-4B8E-9A61-3C5D7E9F1A2B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher=Naba
AppPublisherURL=https://github.com/nabanur/naba-game-booster
AppUpdatesURL=https://github.com/nabanur/naba-game-booster/releases
DefaultDirName={autopf}\Naba Game Booster
DefaultGroupName=Naba Game Booster
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=NabaGameBooster-Setup
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop pe icon banao"; GroupDescription: "Shortcut:"

[Files]
Source: "..\build\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
; Update (silent) ke baad bhi app apne aap khul jaye
Filename: "{app}\{#MyAppExe}"; Description: "Naba Game Booster kholo"; Flags: nowait postinstall shellexec

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\NabaGameBooster"
