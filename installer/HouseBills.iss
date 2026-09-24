; Inno Setup script for HouseBills. Build with installer\build-installer.ps1 (it publishes the app first),
; or directly: ISCC /DAppVersion=1.0.0 installer\HouseBills.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#define AppExe "HouseBills.Wpf.exe"

[Setup]
; Keep AppId stable forever: it is how upgrades and uninstall find the installed app.
; (Deliberately different from the SQL Server/LocalDB edition's AppId, so the two never overwrite each other.)
AppId={{5BAEDDC1-7C81-4EBC-BC9A-22CDA6CCB3AA}
AppName=HouseBills
AppVersion={#AppVersion}
AppVerName=HouseBills {#AppVersion}
AppPublisher=HouseBills
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\HouseBills
DisableProgramGroupPage=yes
; No prerequisites (SQLite ships inside the app), so install per user: no UAC prompt, {autopf} = %LOCALAPPDATA%\Programs.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 1809 or later, as required by .NET 10.
MinVersion=10.0.17763
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
OutputBaseFilename=HouseBills-Setup-{#AppVersion}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\src\HouseBills.Wpf\Assets\HouseBills.ico
UninstallDisplayName=HouseBills
; Close a running HouseBills before upgrading its files.
CloseApplications=yes
InfoBeforeFile=InstallInfo.txt

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\HouseBills"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\HouseBills"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,HouseBills}"; Flags: nowait postinstall skipifsilent
