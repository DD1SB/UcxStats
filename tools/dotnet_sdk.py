#!/usr/bin/env python3
"""Resolve and validate the stable SDK patch policy used by Windows publishing."""

import json
from pathlib import Path
import re
import subprocess
import sys

REPO = Path(__file__).resolve().parents[1]


def validate_sdk_version(version, sdk=None):
    if sdk is None:
        sdk = json.loads((REPO / "global.json").read_text())["sdk"]
    pattern = r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.([1-9][0-9]{2,})"
    required = re.fullmatch(pattern, sdk["version"])
    actual = re.fullmatch(pattern, version) if isinstance(version, str) else None
    if required is None:
        raise ValueError("global.json must specify a stable, full SDK version")
    policy = sdk.get("rollForward", "patch")
    if policy not in ("disable", "patch", "latestPatch"):
        raise ValueError("Windows publishing requires a patch-only SDK rollForward policy")
    minimum = tuple(map(int, required.groups()))
    detected = tuple(map(int, actual.groups())) if actual else None
    if (detected is None or detected[:2] != minimum[:2]
            or detected[2] // 100 != minimum[2] // 100 or detected < minimum
            or (policy == "disable" and detected != minimum)):
        raise ValueError(f"SDK {version!r} is incompatible with global.json "
                         f"({sdk['version']}, {policy}); install a stable compatible SDK patch")
    return version


def resolve_sdk_version():
    # Run from the checkout so dotnet itself applies this repository's global.json.
    version = subprocess.check_output(["dotnet", "--version"], cwd=REPO, text=True).strip()
    return validate_sdk_version(version)


if __name__ == "__main__":
    try:
        print(resolve_sdk_version())
    except (ValueError, OSError, KeyError, subprocess.CalledProcessError) as error:
        print(f"SDK validation failed: {error}", file=sys.stderr)
        sys.exit(1)
