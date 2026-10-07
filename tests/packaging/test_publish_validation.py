"""Safety regression tests against an actual self-contained publish bundle.

Run after publish: python3 -m unittest discover -s tests/packaging -v
Set UCXSTATS_TEST_BUNDLE to validate a different release directory.
All mutations operate on a temporary copy, never the distributable or user data.
"""

import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from zipfile import ZipFile

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("publish_validator", REPO / "tools/validate-windows-publish.py")
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)


class PublishValidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        source = Path(os.environ.get("UCXSTATS_TEST_BUNDLE", REPO / "artifacts/packaging/win-x64" / validator.project_version()))
        validator.validate_bundle(source)
        cls.temp = tempfile.TemporaryDirectory(prefix="ucxstats-packaging-tests-")
        cls.bundle = Path(cls.temp.name) / "bundle"
        shutil.copytree(source, cls.bundle)

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def test_valid_publish_and_installer_manifest(self):
        manifest = validator.validate_bundle(self.bundle)
        self.assertGreater(len(manifest["files"]), 200)

    def test_private_data_is_rejected_even_when_added_to_manifest(self):
        names = ["private.UCX", "DCL.adi", "DCL.adif", "ucxstats.db", "cache.sqlite3",
                 "runtime.log", "audit.csv", "map-tiles.json", "map-dxcc.json", "dxcc.geojson", ".env", "secret.pfx"]
        manifest_path = self.bundle / "manifest.json"
        original = manifest_path.read_bytes()
        for name in names:
            with self.subTest(name=name):
                path = self.bundle / "payload" / name
                path.write_bytes(b"synthetic private-data sentinel")
                try:
                    manifest = json.loads(original)
                    manifest["files"].append({"path": name, "size": path.stat().st_size, "sha256": validator.digest(path)})
                    manifest_path.write_text(json.dumps(manifest))
                    with self.assertRaises(ValueError):
                        validator.validate_bundle(self.bundle)
                finally:
                    path.unlink()
                    manifest_path.write_bytes(original)

    def test_missing_native_dependency_is_rejected(self):
        path = self.bundle / "payload" / "e_sqlite3.dll"
        hidden = self.bundle / "sqlite-backup"
        path.rename(hidden)
        try:
            with self.assertRaisesRegex(ValueError, "Missing required"):
                validator.validate_bundle(self.bundle)
        finally:
            hidden.rename(path)

    def test_changed_assembly_hash_is_rejected(self):
        path = self.bundle / "payload" / "UcxStats.Core.dll"
        original = path.read_bytes()
        try:
            path.write_bytes(original + b"tamper")
            with self.assertRaisesRegex(ValueError, "hashes or file inventory"):
                validator.validate_bundle(self.bundle)
        finally:
            path.write_bytes(original)

    def test_wrong_architecture_is_rejected(self):
        path = self.bundle / "payload" / "WebView2Loader.dll"
        original = path.read_bytes()
        data = bytearray(original)
        offset = int.from_bytes(data[0x3C:0x40], "little")
        data[offset + 4:offset + 6] = b"\x4c\x01"  # i386
        try:
            path.write_bytes(data)
            with self.assertRaisesRegex(ValueError, "AMD64"):
                validator.validate_bundle(self.bundle)
        finally:
            path.write_bytes(original)

    def test_framework_dependent_publish_is_rejected(self):
        path = self.bundle / "payload" / "UcxStats.Wpf.runtimeconfig.json"
        original = path.read_bytes()
        try:
            config = json.loads(original)
            config["runtimeOptions"]["frameworks"] = config["runtimeOptions"].pop("includedFrameworks")
            path.write_text(json.dumps(config))
            with self.assertRaisesRegex(ValueError, "self-contained"):
                validator.validate_bundle(self.bundle)
        finally:
            path.write_bytes(original)

    def test_wildcard_installer_manifest_is_rejected(self):
        path = self.bundle / "installer-files.iss"
        original = path.read_bytes()
        try:
            path.write_text('Source: "{#PayloadDir}\\*"; DestDir: "{app}"\n')
            with self.assertRaisesRegex(ValueError, "Inno file manifest"):
                validator.validate_bundle(self.bundle)
        finally:
            path.write_bytes(original)

    def test_path_traversal_is_rejected(self):
        for name in ["../secrets", "C:/data/file", "Assets/../dxcc.geojson", 'bad".dll']:
            with self.subTest(name=name), self.assertRaises(ValueError):
                validator.safe_path(name)

    def test_symlink_is_rejected(self):
        link = self.bundle / "payload" / "linked.dll"
        link.symlink_to(self.bundle / "payload" / "UcxStats.Core.dll")
        try:
            with self.assertRaisesRegex(ValueError, "Links"):
                validator.validate_bundle(self.bundle)
        finally:
            link.unlink()


class StandaloneArchiveTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.archive_path = Path(os.environ.get("UCXSTATS_TEST_ARCHIVE",
            REPO / "artifacts" / f"UcxStats-Packaging-{validator.project_version()}-win-x64.zip"))
        cls.temp = tempfile.TemporaryDirectory(prefix="ucxstats-standalone-tests-")
        cls.root = Path(cls.temp.name)
        cls.bundle = cls.root / "Extracted Bundle With Spaces"
        cls.cwd = cls.root / "unrelated-working-directory"
        cls.cwd.mkdir()
        with ZipFile(cls.archive_path) as archive:
            cls.members = archive.namelist()
            cls.manifest = json.loads(archive.read("manifest.json"))
            archive.extractall(cls.bundle)
        cls.pwsh = os.environ.get("UCXSTATS_PWSH") or shutil.which("pwsh") or shutil.which("powershell.exe")

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def run_package(self, *options):
        if not self.pwsh:
            self.skipTest("PowerShell is required for standalone script execution tests")
        env = os.environ.copy()
        env["XDG_CACHE_HOME"] = str(self.root / "powershell-cache")
        env["XDG_CONFIG_HOME"] = str(self.root / "powershell-config")
        return subprocess.run([self.pwsh, "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", str(self.bundle / "package.ps1"), *options], cwd=self.cwd, env=env,
            text=True, capture_output=True, timeout=60)

    def test_archive_structure_required_resources_and_privacy(self):
        expected = {"manifest.json", *validator.BUNDLE_FILES} | {
            "payload/" + entry["path"] for entry in self.manifest["files"]}
        self.assertEqual(set(self.members), expected)
        self.assertEqual(len(self.members), len(expected))
        with ZipFile(self.archive_path) as archive:
            self.assertIsNone(archive.testzip())
            self.assertTrue(all(info.date_time == (2000, 1, 1, 0, 0, 0) for info in archive.infolist()))
        for name in self.members:
            parts = Path(name).parts
            self.assertNotIn("bin", parts)
            self.assertNotIn("obj", parts)
            self.assertNotIn(".git", parts)
            self.assertNotIn(Path(name).suffix.lower(), (".cs", ".csproj", ".props", ".ucx", ".adi", ".adif",
                                                         ".geojson", ".csv", ".db", ".sqlite", ".key", ".pfx"))

    def test_explicit_version_metadata_and_packaging_resource_hashes(self):
        self.assertEqual(self.manifest["schemaVersion"], 2)
        self.assertEqual(self.manifest["version"], validator.project_version())
        self.assertEqual(self.manifest["runtimeIdentifier"], "win-x64")
        self.assertTrue(self.manifest["selfContained"])
        self.assertEqual(self.manifest["innoSetupVersion"], json.loads((REPO / "packaging/toolchain.json").read_text())["innoSetupVersion"])
        for entry in self.manifest["bundleFiles"]:
            path = self.bundle / entry["path"]
            self.assertEqual(path.stat().st_size, entry["size"])
            self.assertEqual(validator.digest(path), entry["sha256"])
        self.assertEqual(validator.validate_bundle(self.bundle), self.manifest)

    def test_no_repository_or_source_dependencies(self):
        script = (self.bundle / "package.ps1").read_text()
        for dependency in ("Directory.Build.props", "global.json", ".csproj", "$repoRoot",
                           "Read-ProjectProperty", "Split-Path -Parent $PSScriptRoot", "dotnet "):
            self.assertNotIn(dependency, script)
        self.assertIn("$PSScriptRoot", script)
        self.assertNotIn("packaging/", (self.bundle / "UcxStats.iss").read_text())
        self.assertFalse(self.bundle.is_relative_to(REPO))

    def test_stable_installer_identity_and_data_preservation(self):
        script = (self.bundle / "UcxStats.iss").read_text()
        self.assertIn("AppId={{BCBA7EA1-315B-4D90-91DA-71248703BF73}", script)
        self.assertIn("{BCBA7EA1-315B-4D90-91DA-71248703BF73}_is1", script)
        self.assertIn("PrivilegesRequired=lowest", script)
        self.assertIn("DefaultDirName={localappdata}\\Programs\\UcxStats", script)
        self.assertIn("{userprograms}\\UcxStats\\UcxStats", script)
        self.assertIn("Tasks: desktopicon", script)
        self.assertIn("WebView2Key", script)
        self.assertNotIn("[InstallDelete]", script)
        self.assertNotIn("[UninstallDelete]", script)

    def test_standalone_validation_from_unrelated_directory(self):
        result = self.run_package("-ValidateOnly")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn(f"UcxStats {self.manifest['version']}", result.stdout)
        self.assertFalse((self.bundle / "output").exists())

    def test_correct_inno_arguments_and_relative_output(self):
        result = self.run_package("-Preview")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        preview = json.loads(result.stdout[result.stdout.index("{"):])
        self.assertEqual(preview["Arguments"], [
            f"/DAppVersion={self.manifest['version']}",
            f"/DPayloadDir={self.bundle / 'payload'}",
            f"/DFileList={self.bundle / 'installer-files.iss'}",
            f"/DInstallerOutputDir={self.bundle / 'output'}",
            str(self.bundle / "UcxStats.iss"),
        ])
        self.assertEqual(preview["Installer"], str(self.bundle / "output" /
            f"UcxStats-Setup-{self.manifest['version']}-win-x64.exe"))
        self.assertEqual(preview["InnoSetupVersion"], self.manifest["innoSetupVersion"])

    def test_windows_script_rejects_modified_payload_hash(self):
        path = self.bundle / "payload/UcxStats.Core.dll"
        original = path.read_bytes()
        try:
            # Same length, so only hash verification detects the change.
            path.write_bytes(original[:-1] + bytes([original[-1] ^ 1]))
            result = self.run_package("-ValidateOnly")
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Payload hash/size mismatch", result.stderr)
        finally:
            path.write_bytes(original)

    def test_windows_script_rejects_missing_required_payload(self):
        path = self.bundle / "payload/e_sqlite3.dll"
        backup = self.root / "sqlite-backup"
        path.rename(backup)
        try:
            result = self.run_package("-ValidateOnly")
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Missing required asset", result.stderr)
        finally:
            backup.rename(path)

    def test_windows_script_rejects_unexpected_private_payload(self):
        path = self.bundle / "payload/map-tiles.json"
        path.write_bytes(b"synthetic configuration")
        try:
            result = self.run_package("-ValidateOnly")
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("allowlist", result.stderr)
        finally:
            path.unlink()

    def test_windows_script_rejects_changed_resource(self):
        path = self.bundle / "UcxStats.iss"
        original = path.read_bytes()
        try:
            path.write_bytes(original + b"\n; tampered\n")
            result = self.run_package("-ValidateOnly")
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Packaging resource hash/size mismatch", result.stderr)
        finally:
            path.write_bytes(original)

    def test_windows_script_rejects_inconsistent_version_metadata(self):
        path = self.bundle / "manifest.json"
        original = path.read_bytes()
        try:
            manifest = json.loads(original)
            manifest["version"] = "1.2.3.4"
            path.write_text(json.dumps(manifest))
            result = self.run_package("-ValidateOnly")
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Published version mismatch", result.stderr)
        finally:
            path.write_bytes(original)

    def test_windows_script_rejects_invalid_manifest(self):
        path = self.bundle / "manifest.json"
        original = path.read_bytes()
        try:
            path.write_text("{invalid JSON")
            result = self.run_package("-ValidateOnly")
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Packaging failed:", result.stderr)
        finally:
            path.write_bytes(original)

    def test_deterministic_zip_and_checksum(self):
        rebuilt = self.root / "rebuilt.zip"
        sha = validator.archive_bundle(self.bundle, rebuilt)
        self.assertEqual(sha, validator.digest(self.archive_path))
        self.assertEqual(Path(str(rebuilt) + ".sha256").read_text(), f"{sha}  rebuilt.zip\n")


if __name__ == "__main__":
    unittest.main()
