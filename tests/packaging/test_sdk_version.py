"""SDK policy and publish-script checks; no publish bundle or installed SDK needed."""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("dotnet_sdk", REPO / "tools/dotnet_sdk.py")
sdk = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sdk)


class SdkVersionTests(unittest.TestCase):
    policy = {"version": "10.0.112", "rollForward": "latestPatch", "allowPrerelease": False}

    def test_same_feature_band_patches_are_accepted(self):
        for version in ("10.0.112", "10.0.113", "10.0.199"):
            with self.subTest(version=version):
                self.assertEqual(sdk.validate_sdk_version(version, self.policy), version)

    def test_incompatible_and_malformed_versions_are_rejected(self):
        for version in ("10.0.111", "10.0.100", "10.0.200", "10.1.113", "11.0.113",
                        "9.0.113", "10.0.113-preview.1", "10.0", "10.0.0113", "garbage", None):
            with self.subTest(version=version), self.assertRaisesRegex(ValueError, "incompatible"):
                sdk.validate_sdk_version(version, self.policy)

    def test_disabled_rollforward_remains_exact(self):
        policy = dict(self.policy, rollForward="disable")
        self.assertEqual(sdk.validate_sdk_version("10.0.112", policy), "10.0.112")
        with self.assertRaises(ValueError):
            sdk.validate_sdk_version("10.0.113", policy)

    def test_broader_rollforward_is_not_silently_accepted(self):
        for policy in ("feature", "latestFeature", "minor", "major", "latestMajor", "unknown"):
            with self.subTest(policy=policy), self.assertRaisesRegex(ValueError, "patch-only"):
                sdk.validate_sdk_version("10.0.113", dict(self.policy, rollForward=policy))

    def test_sdk_feature_band_is_not_a_decimal_prefix(self):
        policy = dict(self.policy, version="10.0.199")
        self.assertEqual(sdk.validate_sdk_version("10.0.199", policy), "10.0.199")
        for version in ("10.0.200", "10.0.1100"):
            with self.subTest(version=version), self.assertRaises(ValueError):
                sdk.validate_sdk_version(version, policy)

    def test_repository_uses_stable_patch_rollforward(self):
        configured = json.loads((REPO / "global.json").read_text())["sdk"]
        self.assertEqual(configured, self.policy)

    def test_publish_checks_sdk_before_restore_or_output_creation(self):
        with tempfile.TemporaryDirectory(prefix="ucxstats-sdk-test-") as temporary:
            root = Path(temporary)
            fake_dotnet = root / "dotnet"
            fake_dotnet.write_text(
                '#!/bin/bash\n'
                'if [[ "$1" == "--version" ]]; then echo "$UCXSTATS_FAKE_SDK"; exit 0; fi\n'
                'echo "RESTORE_REACHED" >&2\nexit 42\n')
            fake_dotnet.chmod(0o755)
            for version, accepted in (("10.0.112", True), ("10.0.113", True), ("10.0.199", True),
                                      ("10.0.111", False), ("10.0.200", False), ("11.0.113", False),
                                      ("10.0.113-preview.1", False)):
                with self.subTest(version=version):
                    parent = root / version
                    env = dict(os.environ, PATH=str(root) + os.pathsep + os.environ["PATH"],
                               UCXSTATS_FAKE_SDK=version)
                    result = subprocess.run(["bash", str(REPO / "tools/publish-win-x64.sh"),
                                             str(parent / "bundle"), str(parent / "bundle.zip")],
                                            cwd=root, env=env, text=True, capture_output=True, timeout=15)
                    self.assertEqual(result.returncode, 42 if accepted else 1, result.stdout + result.stderr)
                    self.assertEqual("RESTORE_REACHED" in result.stderr, accepted)
                    self.assertEqual(parent.exists(), accepted)


if __name__ == "__main__":
    unittest.main()
