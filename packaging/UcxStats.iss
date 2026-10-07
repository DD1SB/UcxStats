; Compile through the adjacent package.ps1 after validating the standalone bundle.
#ifndef AppVersion
  #error AppVersion must be supplied by the packaging script
#endif
#ifndef PayloadDir
  #error PayloadDir must be supplied by the packaging script
#endif
#ifndef FileList
  #error FileList must be supplied by the packaging script
#endif
#ifndef InstallerOutputDir
  #error InstallerOutputDir must be supplied by the packaging script
#endif

[Setup]
; Keep this ID stable across all versions to upgrade the same per-user installation.
AppId={{BCBA7EA1-315B-4D90-91DA-71248703BF73}
AppName=UcxStats
AppVersion={#AppVersion}
AppVerName=UcxStats {#AppVersion}
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\UcxStats
DefaultGroupName=UcxStats
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19045
UsePreviousAppDir=yes
UsePreviousTasks=yes
UninstallDisplayIcon={app}\UcxStats.Wpf.exe
UninstallDisplayName=UcxStats
OutputDir={#InstallerOutputDir}
OutputBaseFilename=UcxStats-Setup-{#AppVersion}-win-x64
Compression=lzma2/max
SolidCompression=yes
DiskSpanning=no
TimeStampsInUTC=yes
TouchDate=2000-01-01
TouchTime=00:00
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
; Each entry is explicit. No wildcard can pick up a database, export or local setting.
#include FileList

[Icons]
Name: "{userprograms}\UcxStats\UcxStats"; Filename: "{app}\UcxStats.Wpf.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\UcxStats"; Filename: "{app}\UcxStats.Wpf.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\UcxStats.Wpf.exe"; Description: "Launch UcxStats"; Flags: nowait postinstall skipifsilent

; Deliberately no InstallDelete/UninstallDelete or user-data registry entries.
; Inno uninstalls its tracked program files; LOCALAPPDATA\UcxStats is never touched.
[Code]
const
  WebView2Key = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{BCBA7EA1-315B-4D90-91DA-71248703BF73}_is1';

function RuntimeInRegistry(Root: Integer): Boolean;
var
  Value: String;
  Version: Int64;
begin
  Result := False;
  if RegQueryStringValue(Root, WebView2Key, 'pv', Value) then
    if StrToVersion(Value, Version) then
      Result := Version > 0;
end;

function InitializeSetup(): Boolean;
var
  InstalledVersion: String;
  InstalledPacked, NewPacked: Int64;
begin
  Result := False;
  if RegQueryStringValue(HKCU64, UninstallKey, 'DisplayVersion', InstalledVersion) then
    if StrToVersion(InstalledVersion, InstalledPacked) and
       StrToVersion('{#AppVersion}', NewPacked) then
      if ComparePackedVersion(InstalledPacked, NewPacked) > 0 then
      begin
        Log('Refusing to downgrade UcxStats ' + InstalledVersion + ' to {#AppVersion}.');
        SuppressibleMsgBox('A newer UcxStats version is installed. Downgrades are blocked to protect the database. Install the same or a newer version.', mbError, MB_OK, IDOK);
        Exit;
      end;

  // Microsoft documents HKLM's 32-bit registry view and HKCU for Evergreen detection.
  if not (RuntimeInRegistry(HKLM32) or RuntimeInRegistry(HKCU32) or RuntimeInRegistry(HKCU64)) then
  begin
    Log('Microsoft Edge WebView2 Evergreen Runtime is missing. Installation cancelled before copying program files.');
    SuppressibleMsgBox('UcxStats maps require Microsoft Edge WebView2 Evergreen Runtime.' + #13#10 + #13#10 +
      'Download the Evergreen Bootstrapper (online) or x64 Standalone Installer (offline) from:' + #13#10 +
      'https://developer.microsoft.com/microsoft-edge/webview2/' + #13#10 + #13#10 +
      'Verify the Microsoft digital signature, run it as your normal user, then run UcxStats Setup again.' + #13#10 +
      'Setup does not download or execute prerequisites automatically.', mbError, MB_OK, IDOK);
    Exit;
  end;
  Result := True;
end;
