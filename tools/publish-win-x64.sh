#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
command -v dotnet >/dev/null
command -v python3 >/dev/null
if (( $# > 2 )); then
    echo "Usage: $0 [new-bundle-directory [new-archive-path]]" >&2
    exit 2
fi
version="$(python3 tools/validate-windows-publish.py --version)"
release_dir="${1:-$repo_root/artifacts/packaging/win-x64/$version}"
release_dir="$(realpath -m -- "$release_dir")"
archive_path="${2:-$repo_root/artifacts/UcxStats-Packaging-$version-win-x64.zip}"
archive_path="$(realpath -m -- "$archive_path")"
if [[ -e "$release_dir" ]]; then
    echo "Refusing to overwrite $release_dir; use a new release directory." >&2
    exit 1
fi
if [[ -e "$archive_path" || -e "${archive_path}.sha256" ]]; then
    echo "Refusing to overwrite $archive_path or its checksum; use a new archive path." >&2
    exit 1
fi
sdk_version="$(python3 -c 'import json; print(json.load(open("global.json"))["sdk"]["version"])')"
if [[ "$(dotnet --version)" != "$sdk_version" ]]; then
    echo "Install the SDK pinned in global.json ($sdk_version)." >&2
    exit 1
fi
mkdir -p -- "$(dirname -- "$release_dir")"
mkdir -p -- "$(dirname -- "$archive_path")"
stage_dir="$(mktemp -d "$(dirname -- "$release_dir")/.ucxstats-publish.XXXXXXXX")"
# A failed staging directory is left for inspection; only a validated bundle is promoted.
dotnet restore src/UcxStats.Wpf/UcxStats.Wpf.csproj \
    -p:UcxStatsWindowsPublish=true --locked-mode \
    --artifacts-path "$repo_root/artifacts/build/win-x64" -m:1
dotnet publish src/UcxStats.Wpf/UcxStats.Wpf.csproj \
    -c Release -r win-x64 --self-contained true --no-restore \
    -p:UcxStatsWindowsPublish=true -m:1 \
    --artifacts-path "$repo_root/artifacts/build/win-x64" -o "$stage_dir/payload"
python3 tools/validate-windows-publish.py "$stage_dir" --create
mv -T -- "$stage_dir" "$release_dir"
python3 tools/validate-windows-publish.py "$release_dir" --archive "$archive_path"
echo "Validated release: $release_dir"
echo "Transfer bundle: $archive_path"
echo "Windows: extract ZIP, then run package.ps1; installer is written to output/."
