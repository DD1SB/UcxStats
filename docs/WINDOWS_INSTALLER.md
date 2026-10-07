# Windows installer pipeline

The distribution is one `UcxStats-Setup-<version>-win-x64.exe`. The packaging
workflow is now standalone:

```text
Linux:   ./tools/publish-win-x64.sh
         -> artifacts/UcxStats-Packaging-<version>-win-x64.zip (+ .sha256)
Windows: extract ZIP anywhere -> package.ps1 -> output/Setup EXE
```

The extracted ZIP supplies every packaging input. Windows needs only PowerShell
5.1 (or PowerShell 7) and Inno Setup 6.7.3; no checkout, project source, .NET SDK,
Python, NuGet cache or repository structure is required. Run in a writable folder
as a normal user. Pop!_OS publishes the self-contained .NET 10 WPF payload.
Windows 10 22H2 (build 19045) or later, on an x64 OS, is required by this installer.
ARM64 and x86 are not installer targets.

## Inspected solution and assets

`UcxStats.Wpf` references `UcxStats.Core`, `UcxStats.Data`, `UcxStats.Geo` and
`UcxStats.Map`. All five assemblies are installed. The solution also has the
cross-platform data tests and synthetic parser smoke executable; neither test
project is distributed.

The payload includes:

- .NET and Windows Desktop/WPF runtime **10.0.12**, including native graphics
  libraries and satellite resource assemblies. No separate .NET installation is
  required. Trimming, single-file publishing and ReadyToRun are explicitly disabled.
- `Microsoft.Data.Sqlite` **10.0.11**, its managed dependencies and the x64
  `e_sqlite3.dll` from `SQLitePCLRaw` **2.1.12**.
- `Microsoft.Web.WebView2` **1.0.4078.44** assemblies and the x64
  `WebView2Loader.dll`. The separately serviced Evergreen Runtime is a prerequisite.
- Leaflet **1.9.4** JavaScript, CSS, source map, all five image assets and its BSD
  license under `Assets/Leaflet`. These are public application assets. HTML and
  QSO/DXCC payloads are generated at runtime rather than shipped as station data.

Maps load Leaflet from the local `https://ucxstats.local` origin. Only background
tiles require the configured network provider; offline tiles depend on the
provider/cache. There is no Leaflet CDN dependency. DXCC geometry is optional,
external and never included in a publish or installer.

## Versioning and reproducibility

`Directory.Build.props` contains the explicit four-component `Version`, initially
**0.12.3.3**, taken from the pre-existing UI/README baseline. This is not a new
release claim. Assembly/file versions, the window title, map User-Agent, manifest,
installer metadata and installer filename derive from it. Linux resolves this
version into `manifest.json` (schema 2); the standalone Windows script reads it
there and verifies the published dependency version, without consulting project
files. The manifest also records the resolved SDK, runtime and Inno compiler
versions, payload hashes and packaging-resource hashes. To release a new
version, update this one property to the real version (components 0–65535), refresh
the project-reference lock entries and review them. Scripts refuse to overwrite
an existing staging bundle, packaging ZIP/checksum or installer. Use new paths
for a rebuild; the optional second publish argument selects a new archive path.

`global.json` pins SDK **10.0.112** without roll-forward.
`packaging/windows-publish.props` pins the Windows runtime and sets deterministic
Release compilation, x64, self-contained publishing, no symbols, and a source
path map. `packages.lock.json` locks normal builds; `packages.win-x64.lock.json`
locks the Windows publish graph separately. Publish restore uses `--locked-mode`.
`packaging/toolchain.json` pins Inno Setup **6.7.3**. Update pinned toolchain/runtime
versions deliberately, including security updates, and revalidate before release.

The ZIP has sorted entries, fixed timestamps and permissions. The generated Inno
entries use `ignoreversion touch` with fixed UTC timestamps. There are no build
dates, absolute build paths or source-revision suffixes in the release manifest.
Compare clean payload/ZIP hashes for the same source and toolchain; also compile
twice on Windows and compare unsigned installer hashes before asserting full
installer reproducibility. Authenticode signing/timestamping is not configured
and changes installer bytes. A SHA-256 sidecar detects transfer damage but is not
publisher authentication.

## Build on Pop!_OS

Install the pinned .NET SDK and Python **3.11+** (3.12 is used on the build server).
Bash, GNU `realpath`/`mv`, and access to NuGet on the first restore are required.
The project already sets `EnableWindowsTargeting=true`; Windows targeting/runtime
packs are acquired through restore. Inno Setup and Wine are not required here.

From the repository root:

```bash
dotnet restore UcxStats.sln --locked-mode --artifacts-path artifacts/build/linux
dotnet build UcxStats.sln -c Release --no-restore --artifacts-path artifacts/build/linux
dotnet test UcxStats.sln -c Release --no-build --no-restore --artifacts-path artifacts/build/linux
dotnet artifacts/build/linux/bin/UcxStats.SmokeTest/release/UcxStats.SmokeTest.dll

./tools/publish-win-x64.sh
python3 tools/validate-windows-publish.py artifacts/packaging/win-x64/0.12.3.3
python3 -m unittest discover -s tests/packaging -v
```

Use the current project version in paths after changing it. The publish script
reads it automatically. An optional destination must be new:

```bash
./tools/publish-win-x64.sh /tmp/ucxstats-release-check /tmp/ucxstats-packaging-check.zip
```

The actual restore/publish commands are:

```bash
dotnet restore src/UcxStats.Wpf/UcxStats.Wpf.csproj \
  -p:UcxStatsWindowsPublish=true --locked-mode \
  --artifacts-path artifacts/build/win-x64 -m:1
dotnet publish src/UcxStats.Wpf/UcxStats.Wpf.csproj \
  -c Release -r win-x64 --self-contained true --no-restore \
  -p:UcxStatsWindowsPublish=true -m:1 \
  --artifacts-path artifacts/build/win-x64 -o <fresh-staging-directory>/payload
```

The script validates staging before moving it into the final directory. Failed
staging is retained for inspection. Serial MSBuild avoids the parallel publish
failure observed on this build server. Normal solution outputs stay under
`artifacts/build/linux`; Windows publish intermediates stay under
`artifacts/build/win-x64`. Use `artifacts/build/windows` for ordinary Windows
solution builds. Build/installer/transfer outputs are ignored by Git.

Artifacts:

```text
artifacts/UcxStats-Packaging-<version>-win-x64.zip
artifacts/UcxStats-Packaging-<version>-win-x64.zip.sha256
artifacts/packaging/win-x64/<version>/                  # validated staging bundle
```

For a clean comparison, run `dotnet clean` on the WPF project with the same
Release/RID/property/artifacts arguments, publish into another new directory,
then compare manifests and ZIP SHA-256. Avoid simultaneous publishes against the
same intermediate directory.

When dependencies or the project version change, refresh locks intentionally:

```bash
dotnet restore UcxStats.sln --force-evaluate --artifacts-path artifacts/build/linux
dotnet restore src/UcxStats.Wpf/UcxStats.Wpf.csproj --force-evaluate \
  -p:UcxStatsWindowsPublish=true --artifacts-path artifacts/build/win-x64 -m:1
```

Review all lock changes. Restore with `--force-evaluate` is a maintenance operation,
not part of a release build. Refresh `payload-policy.json` when approved runtime,
satellite or vendored asset inventories change; never relax it to accept private
data. The map asset SHA-256 pins are recorded there. Leaflet source is from the
[official 1.9.4 tag](https://github.com/Leaflet/Leaflet/tree/v1.9.4); built JS and
its source map are from the [official release archive](https://github.com/Leaflet/Leaflet/releases/tag/v1.9.4).

## Standalone archive and Windows packaging

Copy only `UcxStats-Packaging-<version>-win-x64.zip` and its checksum to Windows.
Verify the checksum against a trusted transfer channel, then extract into any
writable directory, including a path with spaces. The archive root contains:

```text
package.ps1             # standalone entry point; uses $PSScriptRoot
UcxStats.iss             # stable installer identity and behavior
manifest.json           # schema 2, resolved versions, payload/resource hashes
README.txt              # instructions rendered with resolved version/toolchain
payload-policy.json     # required assets and approved runtime/asset inventory
installer-files.iss     # explicit [Files] entries; no wildcard copy
payload/                # complete self-contained application
```

The policy and explicit file list are included to preserve the existing validation
rules and reviewed Inno inputs. Source files, project files, `global.json`, NuGet
packages and build intermediates are not included. `README.txt` documents the full
workflow inside the ZIP so the repository documentation is not needed on Windows.

Install Inno Setup **6.7.3** from the
[official release](https://github.com/jrsoftware/issrc/releases/tag/is-6_7_3), verifying
its signature; see [verification instructions](https://jrsoftware.org/isdl-verify.php).
The script locates `ISCC.exe` through PATH, standard machine/current-user Inno
Setup 6 paths, or an explicit `-IsccPath`. Compiler version must match the manifest.
Building does not request elevation.

Compiler detection runs a minimal temporary `.iss` through ISCC. ISPP reports
`DecodeVer(Ver, 4)` with a fixed marker; the script requires exactly **6.7.3**.
Windows `FileVersion`/`ProductVersion` resources may be `0.0.0.0` and are not used
for this decision. The probe has `Output=no` and empty output/manifest paths,
creates no installer, and is cleaned up from its unique directory under `output/`.
Probe failure, missing/invalid version output, missing ISPP or another compiler
version stops packaging before the real installer build. `-ValidateOnly` and
`-Preview` still validate/preview only the payload and do not run a compiler probe.
See [ISPP Ver](https://jrsoftware.org/ishelp/topic_predefinedvars.htm) and
[DecodeVer](https://jrsoftware.org/ishelp/topic_decodever.htm).

From any Windows working directory, run these commands with your extraction path:

```powershell
# Validate without compiling:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Build Folder\package.ps1' -ValidateOnly

# Build:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Build Folder\package.ps1'

# Custom compiler location:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Build Folder\package.ps1' `
  -IsccPath 'C:\Inno Setup 6\ISCC.exe'

# Preview the verified version, compiler arguments and output path:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Build Folder\package.ps1' -Preview
```

For PowerShell 7 substitute `pwsh.exe`. `-ExecutionPolicy Bypass` applies only to
that new process and does not change a permanent machine or user setting. Review
the script before using it. Organization Group Policy can still override this
setting; follow the organization's policy. Do not change `LocalMachine` execution
policy or run an elevated terminal merely to build this installer. With an execution
policy that already permits the script, `& 'C:\Build Folder\package.ps1'` is enough.

Before compilation the script validates manifest schema/version fields, all
required files, the complete dependency/asset allowlist, self-contained .NET/WPF
versions, win-x64 target, native PE architecture, asset hashes, safe unique paths,
every payload length/SHA-256, every packaging-resource length/SHA-256, and the
exact explicit Inno file list. Missing, modified and unexpected payload files,
private data and links/reparse points fail; verification cannot be skipped.
The manifest has no hash of itself: compare the ZIP checksum through a trusted
channel before trusting its metadata/scripts. Checksums do not authenticate a
publisher.

The compiler receives an argument array (so spaces in paths remain intact):

```text
ISCC.exe /DAppVersion=<manifest.version>
         /DPayloadDir=<bundle>\payload
         /DFileList=<bundle>\installer-files.iss
         /DInstallerOutputDir=<bundle>\output
         <bundle>\UcxStats.iss
```

All inputs are inside the extracted directory except the installed Inno toolchain
and its standard language resources. Output is fixed inside the bundle:

```text
output/UcxStats-Setup-<version>-win-x64.exe
output/UcxStats-Setup-<version>-win-x64.exe.sha256
output/UcxStats-Setup-<version>-win-x64.exe.manifest.json
```

Compilation failures retain a non-zero exit code and report the compiler failure;
validation failures report `Packaging failed: ...` and exit 1. An existing Setup
EXE is not overwritten; extract into a new directory for another build. The
`output/` directory is permitted during repeated validation, but is never an input
to the compiler or part of the Linux ZIP. The `.exe` is the only end-user installer.
No prerequisite executable is embedded or downloaded by Setup.

The optional repository wrapper now requires `-BundlePath` and forwards to the
bundle's `package.ps1`. It is not shipped or needed on Windows.

## Packaging regression checks

After publishing, run:

```bash
python3 -m unittest discover -s tests/packaging -v
# To exercise the standalone PowerShell script on Linux when pwsh is installed:
UCXSTATS_PWSH=/path/to/pwsh python3 -m unittest discover -s tests/packaging -v
```

Tests extract the ZIP outside the repository into a folder containing spaces,
change to an unrelated working directory, check the schema/version, exact archive
inventory, resource/payload hashes, required files, absent source/development/private
files, deterministic ZIP bytes/checksum, compiler argument construction and stable
AppId. PowerShell execution tests additionally verify success and rejection of
missing/modified/unexpected payloads, tampered packaging resources, invalid JSON
and mismatched versions. They are explicitly skipped if PowerShell is unavailable.
Linux `-ValidateOnly`/`-Preview` tests never execute Inno Setup or the application.
Compiler-probe regressions use a mock compiler on Linux, including zeroed Windows
version metadata, incompatible versions, failed/malformed probes, path quoting,
stderr output and temporary-directory cleanup. Actual ISCC compilation still
requires Windows; these Linux tests do not perform it. The user-reported successful
clean-Windows packaging, installation and Live Map test is recorded in
[DONE](../DONE.md); additional Windows acceptance checks remain in [TODO](../TODO.md).

## WebView2 prerequisite

Setup checks the Evergreen `pv` registry version in the machine's 32-bit EdgeUpdate
view and both current-user views. Missing, empty, invalid and zero versions fail.
If absent, Setup stops before copying files, with Microsoft's download URL; silent
installation also fails and records the reason in its `/LOG` output. Existing
Edge browser installations alone do not satisfy the prerequisite.

Use the [Microsoft WebView2 download page](https://developer.microsoft.com/microsoft-edge/webview2/)
to obtain the **Evergreen Bootstrapper** for online machines, or the **x64 Evergreen
Standalone Installer** for offline machines. Verify a valid Microsoft Authenticode
signature (Properties → Digital Signatures, or `Get-AuthenticodeSignature`) before
running either. Transfer the verified standalone installer separately for offline
deployment; it is not a UcxStats asset.

Run the prerequisite as the normal intended user from a non-elevated shell:

```powershell
# Microsoft bootstrapper, only after verifying its signature:
.\MicrosoftEdgeWebview2Setup.exe /silent /install
# Or use the verified x64 standalone installer with /silent /install.
# Wait for it to finish successfully, then run UcxStats Setup again.
```

Microsoft supports per-user installation when invoked without elevation; an
existing machine Edge Updater can service it as a machine installation. Managed
device policies may require IT to provision the runtime. The application's
installer always uses `PrivilegesRequired=lowest` and does not request elevation.
Keep Evergreen updated and restart UcxStats after runtime updates. See
[Microsoft's distribution and detection guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).
Registry detection is an installation check, not proof that a damaged or
policy-blocked runtime can initialize; map launch must be tested on Windows.

## Installation, upgrades and uninstall

Run Setup as the intended normal user. It installs into
`%LOCALAPPDATA%\Programs\UcxStats`, creates a current-user Start Menu shortcut,
offers an unchecked desktop shortcut, and registers a current-user uninstall
entry. Close UcxStats/maps before upgrading; Setup uses Windows Restart Manager
to detect in-use program binaries and does not automatically restart them.

Install the same or a higher actual version over the existing installation. A
fixed Inno AppId and previous installation directory/settings identify the upgrade.
Known program files are replaced even when their assembly versions match. Setup
does not initialize, migrate, replace or delete user databases/configuration.
The application performs its existing schema upgrades on launch. A lower version
is blocked because schema downgrades have not been demonstrated.

Back up `%LOCALAPPDATA%\UcxStats` with the application closed before an upgrade;
also keep the original UCX/DCL sources and external geometry safe. For portable
build users, the database/settings already use this same default location, so the
installer uses them without copying personal data into program files. The old
portable WebView2 profile, if present next to its executable, is left in place;
installed maps use the new shared user-data profile.

| Data | Existing/runtime location | Installer behavior |
| --- | --- | --- |
| SQLite QSOs, profiles, source metadata, confirmations | `%LOCALAPPDATA%\UcxStats\ucxstats.db` (+ WAL/SHM while open) | Never bundled, overwritten or removed |
| Tile provider/configuration, including any private URLs/tokens | `%LOCALAPPDATA%\UcxStats\map-tiles.json` | Preserved; the application creates defaults only if absent |
| DXCC external path and default visibility | `%LOCALAPPDATA%\UcxStats\map-dxcc.json` | Preserved; absolute geometry path remains unchanged |
| Browser profile/cache | `%LOCALAPPDATA%\UcxStats\WebView2` | Outside program files; preserved |
| Generated map HTML/normalized geometry | `%LOCALAPPDATA%\UcxStats\MapWeb\<window-id>` | Runtime temporary data; map-window cleanup remains unchanged |
| Original UCX logs, DCL exports, saved SQL/configuration and audit/log files | User-selected paths | Never gathered, bundled, moved or removed |
| External `dxcc.geojson` | User-selected absolute path | Never bundled; only read/staged by the existing map workflow |

No global file logger or separate general user-settings file was found in the
current application. Any files users keep under the data directory remain outside
the installer lifecycle, including future configuration/log files. Installation
is per user; it does not move another Windows user's data.

Uninstall through Windows Installed Apps or the registered uninstaller. It removes
tracked program files, shortcuts and its uninstall entry. There is no user-data
deletion action, recursive directory wipe, or WebView2 Runtime removal. Data remains
available on reinstall. To intentionally erase personal data, close the application,
back it up and manually remove only the desired user files; uninstall never does
this by default.

## Windows release smoke-test checklist

Record OS/build, normal-user account, UcxStats version, installer hash, WebView2
version and each outcome. Use disposable/synthetic logs and a user-supplied,
licensed GeoJSON for validation; do not add them to the bundle.

- [ ] **Manifest:** extract into a folder unrelated to any checkout, change to another
  working directory, and run the extracted `package.ps1 -ValidateOnly`;
  confirm every installed payload file appears in the manifest and no private
  UCX/DCL/database/GeoJSON/settings/secrets are included.
- [ ] **Compiler/reproducibility:** compile twice with Inno 6.7.3 to separate
  output folders; compare unsigned EXE SHA-256 (use separate extracted folders). Check version metadata, x64
  restriction and single-EXE output. Record any differences before claiming
  installer byte reproducibility.
- [ ] **Missing prerequisite:** on a VM without WebView2, run interactive and
  silent (`/VERYSILENT /SUPPRESSMSGBOXES /LOG=...`) Setup. Expect failure before
  program files/shortcuts are installed. Test empty/zero registry `pv` as absent.
  Provision the signed Evergreen prerequisite as a normal user and retry; also
  test the offline standalone workflow. Check both per-user and machine runtimes.
- [ ] **Fresh install:** install under a standard account without UAC elevation;
  inspect the program directory and uninstall entry. Verify Start Menu launch,
  unchecked desktop default and opt-in desktop creation. Cancelled setup should
  not damage an existing installation.
- [ ] **First launch:** on a machine without .NET SDK/runtime installed, launch
  the app, verify displayed version and database creation at the documented path.
  Import a synthetic log, set a station profile, close/reopen and verify persistence.
  Check that no database/settings/profile are written into program files.
- [ ] **Maps:** open ordinary and live maps; verify Leaflet JS/CSS/icons load from
  the local origin, tiles/attribution, markers, paths, band controls, synchronization
  and F11. Block CDN access and confirm the map UI still initializes; offline
  uncached network tiles may be unavailable. Verify the WebView2 profile location.
- [ ] **DXCC:** first test without geometry (main app/maps work, overlay unavailable).
  Select an external absolute GeoJSON path, enable the overlay, inspect polygons,
  status colors/band filtering and repeat after restart. Test missing/invalid
  geometry gracefully. Verify the source GeoJSON file remains unchanged.
- [ ] **Upgrade:** back up the data directory with the app closed; note database
  QSO/profile/confirmation counts and hashes of settings, logs, source files and
  external geometry. Use a prior real version and a higher actual project version,
  regenerate locks/payload and install over it (also test same-version reinstall).
  Confirm one uninstall entry, consistent shortcuts, retained tile provider and
  DXCC path, and no lost data. Database bytes may change through existing schema
  migrations; verify content rather than requiring an identical database hash.
- [ ] **Running app/downgrade:** exercise upgrade with maps open and check the
  in-use-file prompt/cancel path. Verify lower-version Setup is blocked.
- [ ] **Uninstall/data preservation:** close the app, uninstall as the same user,
  confirm program files/shortcuts/entry are removed and all databases, settings,
  logs, browser profile and external files remain. Compare closed-app data hashes
  across uninstall. Reinstall and confirm the retained database/configuration is
  usable; WebView2 remains installed for other applications.

Linux compilation, tests, native PE inspection, standalone script validation/preview
and ZIP comparison do not execute WPF, Windows Setup or WebView2. Complete the Windows
checklist before distributing a release; do not report these runtime checks as
performed on Linux.
