UcxStats @VERSION@ - standalone Windows installer packaging bundle

Workflow: Linux publish-win-x64.sh -> copy ZIP -> extract -> package.ps1 -> Setup EXE

This directory contains all packaging inputs. No Git checkout, project sources,
.NET SDK, Python, NuGet cache or repository layout is needed on Windows.
Use Windows PowerShell 5.1 or PowerShell 7 and Inno Setup @INNO_VERSION@.
Build as a normal user in a writable directory; administrator privileges are
not required to compile the installer.

1. Check the ZIP SHA-256 against the separately transferred trusted checksum.
2. Extract every file into a writable folder (paths containing spaces work).
3. Review package.ps1. In a PowerShell terminal run:

   powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Your Folder\package.ps1"

   With PowerShell 7, substitute pwsh.exe for powershell.exe.
   Bypass applies to this process only; no permanent machine policy is changed.
   Organization Group Policy may still apply. Follow your organization's policy;
   do not change LocalMachine execution policy or run an elevated shell.

   To validate without compiling:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Your Folder\package.ps1" -ValidateOnly

   To select a custom Inno Setup installation:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Your Folder\package.ps1" -IsccPath "C:\Inno Setup 6\ISCC.exe"

The script uses its own directory regardless of your current working directory.
It reads version/runtime/compiler metadata from manifest.json and verifies all
payload files and packaging resources before compiling. Missing, modified,
unexpected or private payload files, unsafe paths and links fail with exit code 1.
Compilation errors produce a non-zero exit code. Existing installers are not
overwritten. Output is always inside this extracted directory:

   output\UcxStats-Setup-@VERSION@-win-x64.exe
   output\UcxStats-Setup-@VERSION@-win-x64.exe.sha256
   output\UcxStats-Setup-@VERSION@-win-x64.exe.manifest.json

Inno Setup @INNO_VERSION@: https://github.com/jrsoftware/issrc/releases/tag/is-6_7_3
Verify the compiler download's signature. ISCC.exe is located through PATH,
standard Inno Setup 6 installations, or -IsccPath. See the Inno Setup license.
The actual compiler version is detected using a temporary ISPP script reporting
DecodeVer(Ver, 4); zeroed FileVersion/ProductVersion resources are ignored.
The probe uses Output=no, creates no installer, and is removed after checking.
Missing ISPP, a failed/invalid probe or a version different from @INNO_VERSION@
stops packaging. Payload-only -ValidateOnly/-Preview do not run this probe.

Installation behavior: per-user program files, Start Menu shortcut, optional
desktop shortcut, uninstall entry, stable upgrade identity and downgrade guard.
SQLite databases, logs, map/user settings and external DXCC paths stay outside
the installation directory and are preserved across upgrades and uninstall.
No personal data, secrets, local geometry or development outputs are bundled.

End-user prerequisite: Microsoft Edge WebView2 Evergreen Runtime. Setup checks
both user and machine installations and stops before installing if absent.
Use Microsoft's signed Evergreen Bootstrapper online, or x64 Standalone Installer
offline, as the intended normal user. Setup does not download prerequisites.
https://developer.microsoft.com/microsoft-edge/webview2/
.NET/WPF runtimes and Leaflet assets are included in the payload.

Licensing: payload/LICENSE is the MIT license for the UcxStats application.
Dependencies retain their own licenses and copyrights. See
payload/THIRD-PARTY-NOTICES.txt, payload/ThirdPartyNotices/ and
payload/Assets/Leaflet/LICENSE. These files are also installed with the app.
The application license grants no rights to AmazingProducer's external
dxcc.geojson; obtain the dataset and permissions separately. It is excluded
from this ZIP and the installer, as are local user configuration and logs.

Before release, verify on Windows: compiler success, installer reproducibility,
missing/present WebView2, standard-user install and shortcuts, first launch with
no .NET installation, ordinary/live maps, optional external DXCC overlay, upgrade,
downgrade rejection, uninstall and retained databases/settings/logs/geometry.
Linux manifest/ZIP tests do not test Windows installation or runtime behavior.

Checksums establish integrity relative to this manifest, not publisher identity.
Trust the transferred ZIP/checksum and reviewed scripts before running them.
