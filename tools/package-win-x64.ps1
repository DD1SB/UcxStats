# Optional repository convenience wrapper. The extracted package.ps1 is standalone.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $BundlePath,
    [string] $IsccPath,
    [switch] $ValidateOnly,
    [switch] $Preview
)
$ErrorActionPreference = 'Stop'
try {
    $script = Join-Path (Resolve-Path -LiteralPath $BundlePath).Path 'package.ps1'
    $options = @{ ValidateOnly = $ValidateOnly; Preview = $Preview }
    if ($IsccPath) { $options.IsccPath = $IsccPath }
    & $script @options
} catch {
    [Console]::Error.WriteLine("Packaging failed: $($_.Exception.Message)")
    exit 1
}
