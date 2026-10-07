#!/usr/bin/env python3
"""Validate the publish allowlist and generate/check the exact Inno file manifest."""

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import struct
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED

REPO = Path(__file__).resolve().parents[1]
BUNDLE_RESOURCES = {
    "package.ps1": "packaging/package.ps1",
    "UcxStats.iss": "packaging/UcxStats.iss",
    "payload-policy.json": "packaging/payload-policy.json",
}
BUNDLE_FILES = tuple(sorted((*BUNDLE_RESOURCES, "README.txt", "installer-files.iss")))


def property_value(path, name):
    values = ET.parse(path).getroot().findall(f".//{name}")
    if len(values) != 1 or not values[0].text:
        raise ValueError(f"Expected one explicit {name} in {path}")
    return values[0].text.strip()


def project_version():
    version = property_value(REPO / "Directory.Build.props", "Version")
    if not re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", version):
        raise ValueError("Version must have four numeric components for Windows file versions")
    if any(int(v) > 65535 for v in version.split(".")):
        raise ValueError("Windows version components must be <= 65535")
    return version


def safe_path(name):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*", name):
        raise ValueError(f"Unsafe payload path: {name}")
    if any(p in (".", "..") or p.startswith(".") for p in name.split("/")):
        raise ValueError(f"Unsafe payload path: {name}")
    return name


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def file_list(entries):
    lines = ["; Generated from the validated publish manifest. Do not edit."]
    for entry in entries:
        name = safe_path(entry["path"]).replace("/", "\\")
        parent = str(PurePosixPath(entry["path"]).parent)
        dest = "{app}" if parent == "." else "{app}\\" + parent.replace("/", "\\")
        lines.append(f'Source: "{{#PayloadDir}}\\{name}"; DestDir: "{dest}"; Flags: ignoreversion touch')
    return "\n".join(lines) + "\n"


def validate_payload(payload):
    if payload.is_symlink() or not payload.is_dir():
        raise ValueError("Payload must be a real directory")
    policy = json.loads((REPO / "packaging/payload-policy.json").read_text())
    version = project_version()
    runtime_version = property_value(REPO / "packaging/windows-publish.props", "RuntimeFrameworkVersion")
    files = {}
    folded_paths = set()
    for path in sorted(payload.rglob("*")):
        if path.is_symlink():
            raise ValueError(f"Links are not permitted: {path}")
        if path.is_file():
            name = safe_path(path.relative_to(payload).as_posix())
            if name.lower() in folded_paths:
                raise ValueError(f"Windows case collision: {name}")
            folded_paths.add(name.lower())
            if path.stat().st_size == 0:
                raise ValueError(f"Empty payload file: {name}")
            files[name] = path
    missing = set(policy["requiredFiles"]) - files.keys()
    if missing:
        raise ValueError(f"Missing required publish assets: {sorted(missing)}")
    config = json.loads(files["UcxStats.Wpf.runtimeconfig.json"].read_text())['runtimeOptions']
    expected_frameworks = [
        {"name": "Microsoft.NETCore.App", "version": runtime_version},
        {"name": "Microsoft.WindowsDesktop.App", "version": runtime_version},
    ]
    if config.get("includedFrameworks") != expected_frameworks or "frameworks" in config or "framework" in config:
        raise ValueError("Publish is not self-contained with the pinned .NET/WPF runtimes")
    deps = json.loads(files["UcxStats.Wpf.deps.json"].read_text())
    if deps["runtimeTarget"]["name"] != ".NETCoreApp,Version=v10.0/win-x64":
        raise ValueError("Publish dependency target must be win-x64 / .NET 10")
    libraries = deps["targets"][deps["runtimeTarget"]["name"]]
    if f"UcxStats.Wpf/{version}" not in libraries:
        raise ValueError("Published application version does not match Directory.Build.props")
    expected = set(policy["additionalFiles"]) | set(policy["assetHashes"])
    for library in libraries.values():
        for kind in ("runtime", "native", "resources"):
            for asset in library.get(kind, {}):
                if asset.endswith("/_._") or asset == "_._":
                    continue
                # .NET flattens package lib/native paths; WebView2 also retains its RID path.
                name = safe_path(asset if asset in files else PurePosixPath(asset).name)
                if not name.endswith(".dll") and name != "createdump.exe":
                    raise ValueError(f"Unexpected dependency asset: {name}")
                expected.add(name)
    for culture in policy["satelliteCultures"]:
        for assembly in policy["satelliteAssemblies"]:
            expected.add(f"{culture}/{assembly}.resources.dll")
    if set(files) != expected:
        raise ValueError(f"Payload differs from dependency/asset allowlist. "
                         f"Missing: {sorted(expected - files.keys())}; "
                         f"Unexpected (including private data): {sorted(files.keys() - expected)}")
    for name, sha in policy["assetHashes"].items():
        if digest(files[name]) != sha:
            raise ValueError(f"Vendored map asset hash mismatch: {name}")
    for name in policy["x64Files"]:
        with files[name].open("rb") as stream:
            if stream.read(2) != b"MZ":
                raise ValueError(f"Not a Windows PE file: {name}")
            stream.seek(0x3C)
            offset = struct.unpack("<I", stream.read(4))[0]
            stream.seek(offset)
            if stream.read(6) != b"PE\0\0\x64\x86":
                raise ValueError(f"Not an AMD64 Windows PE file: {name}")
    entries = [{"path": name, "size": path.stat().st_size, "sha256": digest(path)}
               for name, path in sorted(files.items())]
    return {
        "schemaVersion": 2,
        "version": version,
        "runtimeIdentifier": "win-x64",
        "selfContained": True,
        "sdkVersion": json.loads((REPO / "global.json").read_text())["sdk"]["version"],
        "runtimeVersion": runtime_version,
        "innoSetupVersion": json.loads((REPO / "packaging/toolchain.json").read_text())["innoSetupVersion"],
        "files": entries,
    }


def validate_bundle(bundle, create=False):
    if bundle.is_symlink():
        raise ValueError("Bundle must not be a symbolic link")
    manifest = validate_payload(bundle / "payload")
    text = file_list(manifest["files"])
    readme = (REPO / "packaging/README.txt").read_text(encoding="utf-8")
    readme = readme.replace("@VERSION@", manifest["version"]).replace("@INNO_VERSION@", manifest["innoSetupVersion"])
    if create:
        for name, source in BUNDLE_RESOURCES.items():
            shutil.copyfile(REPO / source, bundle / name)
        (bundle / "README.txt").write_text(readme, encoding="utf-8")
        (bundle / "installer-files.iss").write_text(text, encoding="utf-8")
    for name in (*BUNDLE_FILES, "manifest.json"):
        if (bundle / name).is_symlink():
            raise ValueError(f"Bundle metadata must not be a symbolic link: {name}")
    for name, source in BUNDLE_RESOURCES.items():
        if (bundle / name).read_bytes() != (REPO / source).read_bytes():
            raise ValueError(f"Packaging resource differs from its reviewed source: {name}")
    if (bundle / "README.txt").read_text(encoding="utf-8") != readme:
        raise ValueError("Packaging README metadata mismatch")
    if (bundle / "installer-files.iss").read_text() != text:
        raise ValueError("Inno file manifest differs from validated payload")
    manifest["bundleFiles"] = [{"path": name, "size": (bundle / name).stat().st_size,
                              "sha256": digest(bundle / name)} for name in BUNDLE_FILES]
    if create:
        (bundle / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    if json.loads((bundle / "manifest.json").read_text()) != manifest:
        raise ValueError("Manifest metadata, hashes or file inventory mismatch")
    if {p.name for p in bundle.iterdir()} != {"payload", "manifest.json", *BUNDLE_FILES}:
        raise ValueError("Unexpected files at bundle root")
    return manifest


def archive_bundle(bundle, archive_path):
    manifest = validate_bundle(bundle)
    # Stable order, permissions and ZIP timestamps; never archive the checkout.
    with ZipFile(archive_path, "x", compression=ZIP_DEFLATED, compresslevel=9) as archive:
        names = ["manifest.json", *BUNDLE_FILES] + ["payload/" + f["path"] for f in manifest["files"]]
        for name in sorted(names):
            info = ZipInfo(name, date_time=(2000, 1, 1, 0, 0, 0))
            info.compress_type = ZIP_DEFLATED
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            archive.writestr(info, (bundle / name).read_bytes(), compresslevel=9)
    sha = digest(archive_path)
    Path(str(archive_path) + ".sha256").write_text(f"{sha}  {archive_path.name}\n", encoding="ascii")
    return sha


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("bundle", nargs="?", type=Path)
    parser.add_argument("--version", action="store_true")
    parser.add_argument("--create", action="store_true")
    parser.add_argument("--archive", type=Path)
    args = parser.parse_args()
    if args.version:
        print(project_version())
        return
    if args.bundle is None:
        parser.error("bundle is required")
    manifest = validate_bundle(args.bundle, args.create)
    if args.archive:
        print(f"Archive SHA-256: {archive_bundle(args.bundle, args.archive)}")
    print(f"Validated {len(manifest['files'])} files; UcxStats {manifest['version']}; "
          f"self-contained .NET {manifest['runtimeVersion']} win-x64")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, KeyError, ET.ParseError, struct.error) as error:
        print(f"Publish validation failed: {error}", file=sys.stderr)
        sys.exit(1)
