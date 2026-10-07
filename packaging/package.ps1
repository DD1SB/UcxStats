# Standalone packaging: Windows PowerShell 5.1 or PowerShell 7 and Inno Setup only.
[CmdletBinding()]
param(
    [string] $IsccPath,
    [switch] $ValidateOnly,
    [switch] $Preview
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-SafePath([string] $Name) {
    if ($Name -cnotmatch '^[A-Za-z0-9_.-]+(/[A-Za-z0-9_.-]+)*$' -or
        @($Name.Split('/') | Where-Object { $_.StartsWith('.') }).Count -ne 0) {
        throw "Unsafe payload path: $Name"
    }
}

function Get-Properties($Object) {
    if ($null -ne $Object) { return @($Object.PSObject.Properties) }
    return @()
}

function Get-OptionalProperty($Object, [string] $Name) {
    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property) { return $property.Value }
    return $null
}

function Assert-X64Pe([string] $Path) {
    $stream = [IO.File]::OpenRead($Path)
    $reader = New-Object IO.BinaryReader($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw "Not a Windows PE: $Path" }
        $stream.Position = 0x3C
        $offset = $reader.ReadUInt32()
        $stream.Position = $offset
        if ($reader.ReadUInt32() -ne 0x00004550 -or $reader.ReadUInt16() -ne 0x8664) {
            throw "Not an AMD64 Windows PE: $Path"
        }
    } finally { $reader.Dispose() }
}

function Get-InnoCompilerVersion([string] $CompilerPath, [string] $ProbeRoot) {
    # ISCC's Windows version resources can be 0.0.0.0. Ask its preprocessor instead.
    $probeDirectory = Join-Path $ProbeRoot ('.compiler-probe-' + [Guid]::NewGuid().ToString('N'))
    $process = New-Object Diagnostics.Process
    try {
        [IO.Directory]::CreateDirectory($probeDirectory) | Out-Null
        $probePath = Join-Path $probeDirectory 'version-probe.iss'
        $probeScript = @'
#pragma message "UCXSTATS_ISCC_VERSION=" + DecodeVer(Ver, 4)
[Setup]
AppName=UcxStats compiler version probe
AppVersion=0
DefaultDirName={tmp}\UcxStatsCompilerProbe
CreateAppDir=no
Uninstallable=no
Output=no
OutputDir=
OutputManifestFile=
'@
        [IO.File]::WriteAllText($probePath, $probeScript, (New-Object Text.UTF8Encoding($false)))
        $process.StartInfo.FileName = $CompilerPath
        # One absolute script path; Windows filenames cannot contain a double quote.
        $process.StartInfo.Arguments = '"' + $probePath + '"'
        $process.StartInfo.WorkingDirectory = $probeDirectory
        $process.StartInfo.UseShellExecute = $false
        $process.StartInfo.CreateNoWindow = $true
        $process.StartInfo.RedirectStandardOutput = $true
        $process.StartInfo.RedirectStandardError = $true
        if (!$process.Start()) { throw 'Could not start the Inno Setup compiler version probe' }
        # Read both pipes concurrently, including diagnostics, without PowerShell 5.1
        # interpreting native stderr as a terminating error.
        $stdoutRead = $process.StandardOutput.ReadToEndAsync()
        $stderrRead = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(30000)) {
            $process.Kill()
            $process.WaitForExit()
            throw 'Inno Setup compiler version probe timed out after 30 seconds'
        }
        $probeOutput = $stdoutRead.GetAwaiter().GetResult() + "`n" + $stderrRead.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw "Inno Setup compiler version probe failed (exit code $($process.ExitCode)). $($probeOutput.Trim())"
        }
        $reported = [regex]::Matches($probeOutput,
            '(?m)UCXSTATS_ISCC_VERSION=([0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)[ \t]*\r?$')
        if ($reported.Count -ne 1) {
            throw "Inno Setup compiler version probe did not report one valid ISPP version. Ensure ISPP is installed. $($probeOutput.Trim())"
        }
        return $reported[0].Groups[1].Value
    } finally {
        $process.Dispose()
        # Delete only this invocation's temporary probe, never installer/user files.
        if ([IO.Directory]::Exists($probeDirectory)) { [IO.Directory]::Delete($probeDirectory, $true) }
    }
}

function Assert-InnoCompilerVersion([string] $CompilerPath, [string] $RequiredVersion, [string] $ProbeRoot) {
    $detectedVersion = Get-InnoCompilerVersion $CompilerPath $ProbeRoot
    if ($detectedVersion -cne $RequiredVersion) {
        throw "Install Inno Setup $RequiredVersion; compiler reported $detectedVersion through ISPP"
    }
    return $detectedVersion
}

try {
$bundle = [IO.Path]::GetFullPath($PSScriptRoot)
$payload = Join-Path $bundle 'payload'
$resourceNames = @('README.txt', 'UcxStats.iss', 'installer-files.iss', 'package.ps1', 'payload-policy.json')
foreach ($name in @('', 'payload', 'manifest.json') + $resourceNames) {
    $path = $bundle
    if ($name) { $path = Join-Path $bundle $name }
    $item = Get-Item -LiteralPath $path -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Links/reparse points are forbidden: $path" }
    if ($item.PSIsContainer -ne ($name -in @('', 'payload'))) { throw "Incorrect bundle entry type: $path" }
}
$rootNames = @(Get-ChildItem -LiteralPath $bundle -Force | ForEach-Object { $_.Name })
if (@($rootNames | Where-Object { $_ -notin (@('payload','manifest.json','output') + $resourceNames) }).Count) {
    throw 'Unexpected bundle root entries'
}
$output = Join-Path $bundle 'output'
if (Test-Path -LiteralPath $output) {
    $item = Get-Item -LiteralPath $output -Force
    if (!$item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'output must be a real directory inside the bundle'
    }
}
$manifest = Get-Content -LiteralPath (Join-Path $bundle 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 2 -or $manifest.runtimeIdentifier -cne 'win-x64' -or
    $manifest.selfContained -isnot [bool] -or !$manifest.selfContained) {
    throw 'Unsupported standalone manifest schema or runtime target'
}
$version = $manifest.version
if ($version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' -or
    @($version.Split('.') | Where-Object { [int] $_ -gt 65535 }).Count -ne 0) {
    throw 'Version must have four numeric components <= 65535'
}
$runtimeVersion = $manifest.runtimeVersion
$innoVersion = $manifest.innoSetupVersion
if ($runtimeVersion -cnotmatch '^10\.0\.[0-9]+$' -or $manifest.sdkVersion -cnotmatch '^10\.0\.[0-9]+$' -or
    $innoVersion -cnotmatch '^[0-9]+\.[0-9]+\.[0-9]+$') { throw 'Invalid runtime/SDK/compiler version metadata' }
$resources = @{}
if ($manifest.bundleFiles.Count -ne $resourceNames.Count) { throw 'Packaging resource manifest count mismatch' }
foreach ($entry in $manifest.bundleFiles) {
    if ($entry.path -cnotin $resourceNames -or $resources.ContainsKey($entry.path)) { throw 'Unexpected/duplicate packaging resource' }
    $item = Get-Item -LiteralPath (Join-Path $bundle $entry.path)
    if ($item.Length -ne $entry.size -or $entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
        (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.sha256) {
        throw "Packaging resource hash/size mismatch: $($entry.path)"
    }
    $resources[$entry.path] = $true
}
$policy = Get-Content -LiteralPath (Join-Path $bundle 'payload-policy.json') -Raw | ConvertFrom-Json
$actual = @{}
foreach ($item in Get-ChildItem -LiteralPath $payload -Recurse -Force) {
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Link/reparse point: $($item.FullName)" }
    if ($item.PSIsContainer) { continue }
    $relative = $item.FullName.Substring($payload.Length + 1).Replace('\', '/')
    Assert-SafePath $relative
    if ($actual.ContainsKey($relative) -or $item.Length -eq 0) { throw "Duplicate/empty payload file: $relative" }
    $actual[$relative] = $item
}
foreach ($name in $policy.requiredFiles) {
    if (!$actual.ContainsKey($name)) { throw "Missing required asset: $name" }
}
$config = (Get-Content -LiteralPath (Join-Path $payload 'UcxStats.Wpf.runtimeconfig.json') -Raw | ConvertFrom-Json).runtimeOptions
if ($null -ne (Get-OptionalProperty $config 'framework') -or $null -ne (Get-OptionalProperty $config 'frameworks')) {
    throw 'Framework-dependent publish is forbidden'
}
$frameworks = @(Get-OptionalProperty $config 'includedFrameworks')
if ($frameworks.Count -ne 2 -or $frameworks[0].name -cne 'Microsoft.NETCore.App' -or
    $frameworks[1].name -cne 'Microsoft.WindowsDesktop.App' -or
    @($frameworks | Where-Object { $_.version -cne $runtimeVersion }).Count) {
    throw 'Self-contained .NET/WPF runtime versions do not match the pinned runtime'
}
$deps = Get-Content -LiteralPath (Join-Path $payload 'UcxStats.Wpf.deps.json') -Raw | ConvertFrom-Json
if ($deps.runtimeTarget.name -cne '.NETCoreApp,Version=v10.0/win-x64') { throw 'Incorrect dependency runtime target' }
$libraries = $deps.targets.PSObject.Properties[$deps.runtimeTarget.name].Value
if ($null -eq $libraries.PSObject.Properties["UcxStats.Wpf/$version"]) { throw 'Published version mismatch' }
$expected = @{}
foreach ($name in $policy.additionalFiles) { $expected[$name] = $true }
foreach ($asset in Get-Properties $policy.assetHashes) { $expected[$asset.Name] = $true }
foreach ($library in Get-Properties $libraries) {
    foreach ($kind in @('runtime','native','resources')) {
        foreach ($asset in Get-Properties (Get-OptionalProperty $library.Value $kind)) {
            if ($asset.Name -eq '_._' -or $asset.Name.EndsWith('/_._')) { continue }
            $name = $asset.Name
            if (!$actual.ContainsKey($name)) { $name = ($name.Split('/'))[-1] }
            Assert-SafePath $name
            if (!$name.EndsWith('.dll') -and $name -cne 'createdump.exe') { throw "Unexpected dependency asset: $name" }
            $expected[$name] = $true
        }
    }
}
foreach ($culture in $policy.satelliteCultures) {
    foreach ($assembly in $policy.satelliteAssemblies) { $expected["$culture/$assembly.resources.dll"] = $true }
}
if ($actual.Count -ne $expected.Count -or @($actual.Keys | Where-Object { !$expected.ContainsKey($_) }).Count) {
    throw 'Payload differs from dependency/asset allowlist (possible private data or missing assets)'
}
foreach ($asset in Get-Properties $policy.assetHashes) {
    if ((Get-FileHash -LiteralPath $actual[$asset.Name].FullName -Algorithm SHA256).Hash.ToLowerInvariant() -cne $asset.Value) {
        throw "Vendored map asset hash mismatch: $($asset.Name)"
    }
}
foreach ($name in $policy.x64Files) { Assert-X64Pe $actual[$name].FullName }
$listed = @{}
[string[]] $names = @($actual.Keys)
[Array]::Sort($names, [StringComparer]::Ordinal)
$lines = New-Object 'Collections.Generic.List[string]'
$lines.Add('; Generated from the validated publish manifest. Do not edit.')
if ($manifest.files.Count -ne $actual.Count) { throw 'Manifest file count mismatch' }
for ($i = 0; $i -lt $manifest.files.Count; $i++) {
    $entry = $manifest.files[$i]
    Assert-SafePath $entry.path
    if ($entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or $entry.size -le 0) { throw 'Invalid payload hash/size metadata' }
    if ($entry.path -cne $names[$i] -or $listed.ContainsKey($entry.path)) { throw 'Manifest paths must be unique and sorted' }
    $listed[$entry.path] = $true
    $item = $actual[$entry.path]
    if ($item.Length -ne $entry.size -or
        (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.sha256) {
        throw "Payload hash/size mismatch: $($entry.path)"
    }
    $name = $entry.path.Replace('/', '\')
    $parent = $name.LastIndexOf('\')
    $dest = '{app}'
    if ($parent -ge 0) { $dest += '\' + $name.Substring(0, $parent) }
    $lines.Add(('Source: "{{#PayloadDir}}\{0}"; DestDir: "{1}"; Flags: ignoreversion touch' -f $name, $dest))
}
$expectedList = ($lines -join "`n") + "`n"
$fileList = Join-Path $bundle 'installer-files.iss'
if ((Get-Content -LiteralPath $fileList -Raw).Replace("`r`n", "`n") -cne $expectedList) {
    throw 'Inno file manifest differs from the validated payload'
}
Write-Host "Validated $($actual.Count) files; UcxStats $version; self-contained .NET $runtimeVersion win-x64"
$installer = Join-Path $output "UcxStats-Setup-$version-win-x64.exe"
$compilerArguments = @(
    "/DAppVersion=$version",
    "/DPayloadDir=$payload",
    "/DFileList=$fileList",
    "/DInstallerOutputDir=$output",
    (Join-Path $bundle 'UcxStats.iss')
)
if ($Preview) {
    [pscustomobject] @{ Installer = $installer; InnoSetupVersion = $innoVersion; Arguments = $compilerArguments } | ConvertTo-Json
    return
}
if ($ValidateOnly) { return }
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Inno packaging must run on Windows' }
if (!$IsccPath) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $IsccPath = $command.Source }
    else {
        foreach ($base in @([Environment]::GetEnvironmentVariable('ProgramFiles(x86)'),
                             [Environment]::GetEnvironmentVariable('ProgramFiles'),
                             [Environment]::GetEnvironmentVariable('LOCALAPPDATA'))) {
            if (!$base) { continue }
            foreach ($relative in @('Inno Setup 6/ISCC.exe', 'Programs/Inno Setup 6/ISCC.exe')) {
                $candidate = Join-Path $base $relative
                if (Test-Path -LiteralPath $candidate -PathType Leaf) { $IsccPath = $candidate; break }
            }
            if ($IsccPath) { break }
        }
    }
}
if (!$IsccPath) { throw "ISCC.exe was not found. Install Inno Setup $innoVersion or pass -IsccPath." }
$iscc = (Resolve-Path -LiteralPath $IsccPath).Path
[IO.Directory]::CreateDirectory($output) | Out-Null
if (Test-Path -LiteralPath $installer) { throw "Refusing to overwrite $installer; extract the ZIP into a new directory." }
$compilerVersion = Assert-InnoCompilerVersion $iscc $innoVersion $output
Write-Host "Inno Setup compiler $compilerVersion verified through ISPP"
# Only the explicit, verified list is consumed by Inno. TouchDate/TouchTime normalize timestamps.
& $iscc @compilerArguments
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("Inno Setup failed with exit code $LASTEXITCODE")
    exit $LASTEXITCODE
}
if (!(Test-Path -LiteralPath $installer) -or (Get-Item -LiteralPath $installer).Length -eq 0) {
    throw 'Inno Setup did not produce the expected installer'
}
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$installer.sha256", "$hash  $([IO.Path]::GetFileName($installer))`n", (New-Object Text.UTF8Encoding($false)))
Copy-Item -LiteralPath (Join-Path $bundle 'manifest.json') -Destination "$installer.manifest.json"
Write-Host "Installer: $installer"
Write-Host "SHA-256: $hash"
} catch {
    [Console]::Error.WriteLine("Packaging failed: $($_.Exception.Message)")
    exit 1
}
