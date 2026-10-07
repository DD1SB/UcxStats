"""Exercise the bundled PowerShell compiler probe with a mock native process.

These are Linux protocol regressions, not Inno Setup compilation or Windows
runtime tests. The actual standalone ZIP is extracted outside the repository.
"""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from zipfile import ZipFile

from test_publish_validation import REPO, validator


@unittest.skipIf(os.name == "nt", "POSIX mock compiler; real ISCC retest runs on Windows")
class CompilerVersionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pwsh = os.environ.get("UCXSTATS_PWSH") or shutil.which("pwsh")
        if not cls.pwsh:
            raise unittest.SkipTest("PowerShell is required for compiler-probe regressions")
        cls.temp = tempfile.TemporaryDirectory(prefix="ucxstats-compiler-tests-")
        cls.root = Path(cls.temp.name)
        cls.bundle = cls.root / "Extracted bundle with spaces"
        cls.cwd = cls.root / "Unrelated working directory"
        cls.cwd.mkdir()
        archive = Path(os.environ.get("UCXSTATS_TEST_ARCHIVE", REPO / "artifacts" /
            f"UcxStats-Packaging-{validator.project_version()}-win-x64.zip"))
        with ZipFile(archive) as zip_file:
            zip_file.extractall(cls.bundle)
        cls.compiler = cls.cwd / "Mock compiler with spaces" / "ISCC.exe"
        cls.compiler.parent.mkdir()
        cls.compiler.write_text(f"#!{sys.executable}\n" + r'''
import json
import os
from pathlib import Path
import sys

assert len(sys.argv) == 2, "Probe must receive one script path"
probe = Path(sys.argv[1])
assert probe.is_absolute(), "Probe must use an absolute path"
script = probe.read_text(encoding="utf-8")
Path(os.environ["UCXSTATS_MOCK_CAPTURE"]).write_text(json.dumps({
    "script": script, "path": str(probe), "cwd": os.getcwd(),
}))
assert '#pragma message "UCXSTATS_ISCC_VERSION=" + DecodeVer(Ver, 4)' in script
assert "Output=no" in script.splitlines(), "Probe must not create an installer"
assert "OutputDir=" in script.splitlines(), "Probe must not clean an output directory"
assert "OutputManifestFile=" in script.splitlines()
sys.stdout.write(os.environ["UCXSTATS_MOCK_STDOUT"])
sys.stderr.write(os.environ["UCXSTATS_MOCK_STDERR"])
sys.exit(int(os.environ["UCXSTATS_MOCK_EXIT_CODE"]))
''', encoding="utf-8")
        cls.compiler.chmod(0o755)
        cls.harness = cls.cwd / "probe-test.ps1"
        cls.harness.write_text(r'''
$ErrorActionPreference = 'Stop'
. (Join-Path $env:UCXSTATS_MOCK_BUNDLE 'package.ps1') -ValidateOnly
# Simulate the Windows version-resource bug independently of the ISPP result.
function Get-Item {
    param([string] $LiteralPath)
    if ($LiteralPath -cne $env:UCXSTATS_MOCK_COMPILER) { throw 'Unexpected metadata lookup' }
    return [pscustomobject] @{ VersionInfo = [pscustomobject] @{
        FileVersion = $env:UCXSTATS_MOCK_METADATA
        ProductVersion = $env:UCXSTATS_MOCK_METADATA
    } }
}
$metadata = (Get-Item -LiteralPath $env:UCXSTATS_MOCK_COMPILER).VersionInfo
Write-Host "Metadata fixture: $($metadata.FileVersion)/$($metadata.ProductVersion)"
try {
    $reported = Assert-InnoCompilerVersion $env:UCXSTATS_MOCK_COMPILER '6.7.3' (
        Join-Path $env:UCXSTATS_MOCK_BUNDLE 'output')
    Write-Host "Verified compiler: $reported"
} catch {
    [Console]::Error.WriteLine("Compiler probe rejected: $($_.Exception.Message)")
    exit 1
}
''', encoding="utf-8")

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def run_probe(self, stdout="UCXSTATS_ISCC_VERSION=6.7.3\n", stderr="", exit_code=0,
                  metadata="0.0.0.0"):
        env = os.environ.copy()
        capture = self.cwd / "probe-capture.json"
        if capture.exists():
            capture.unlink()
        env.update({
            "XDG_CACHE_HOME": str(self.root / "powershell-cache"),
            "XDG_CONFIG_HOME": str(self.root / "powershell-config"),
            "UCXSTATS_MOCK_BUNDLE": str(self.bundle),
            "UCXSTATS_MOCK_COMPILER": str(self.compiler),
            "UCXSTATS_MOCK_CAPTURE": str(capture),
            "UCXSTATS_MOCK_STDOUT": stdout,
            "UCXSTATS_MOCK_STDERR": stderr,
            "UCXSTATS_MOCK_EXIT_CODE": str(exit_code),
            "UCXSTATS_MOCK_METADATA": metadata,
        })
        result = subprocess.run([self.pwsh, "-NoLogo", "-NoProfile", "-File", str(self.harness)],
            cwd=self.cwd, env=env, text=True, capture_output=True, timeout=45)
        self.assertTrue(capture.exists(), result.stdout + result.stderr)
        observed = json.loads(capture.read_text())
        probe = Path(observed["path"])
        self.assertEqual(Path(observed["cwd"]), probe.parent)
        self.assertEqual(probe.parent.parent, self.bundle / "output")
        self.assertFalse(probe.parent.exists(), "Temporary probe must be removed on success/failure")
        self.assertEqual(list((self.bundle / "output").iterdir()), [])
        return result

    def test_zeroed_file_and_product_metadata_accept_actual_pinned_version(self):
        result = self.run_probe(stdout="Compiler messages:\r\n  UCXSTATS_ISCC_VERSION=6.7.3\r\n")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Metadata fixture: 0.0.0.0/0.0.0.0", result.stdout)
        self.assertIn("Verified compiler: 6.7.3", result.stdout)

    def test_incompatible_ispp_versions_rejected_despite_matching_metadata(self):
        for version in ("6.7.2", "6.7.4", "7.0.0", "6.7.3.1", "0.0.0.0"):
            with self.subTest(version=version):
                result = self.run_probe(stdout=f"UCXSTATS_ISCC_VERSION={version}\n", metadata="6.7.3")
                self.assertNotEqual(result.returncode, 0)
                self.assertIn(f"Install Inno Setup 6.7.3; compiler reported {version} through ISPP", result.stderr)

    def test_help_banner_without_ispp_marker_is_not_version_evidence(self):
        result = self.run_probe(stdout="Inno Setup 6.7.3 Compiler\nUsage: ISCC [options] script.iss\n")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("did not report one valid ISPP version", result.stderr)

    def test_malformed_or_duplicate_version_markers_are_rejected(self):
        for reported in ("6.7", "6.7.3-beta", "6.7.3.1.2", "not-a-version",
                         "6.7.3\nUCXSTATS_ISCC_VERSION=6.7.3"):
            with self.subTest(reported=reported):
                result = self.run_probe(stdout=f"UCXSTATS_ISCC_VERSION={reported}\n")
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("did not report one valid ISPP version", result.stderr)

    def test_probe_compile_failure_rejects_even_a_matching_marker(self):
        result = self.run_probe(stderr="Synthetic ISPP compile error", exit_code=2)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("version probe failed (exit code 2)", result.stderr)
        self.assertIn("Synthetic ISPP compile error", result.stderr)

    def test_stderr_marker_is_captured_without_native_stderr_failure(self):
        result = self.run_probe(stdout="Compiler diagnostics\n", stderr="UCXSTATS_ISCC_VERSION=6.7.3\n")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Verified compiler: 6.7.3", result.stdout)


if __name__ == "__main__":
    unittest.main()
