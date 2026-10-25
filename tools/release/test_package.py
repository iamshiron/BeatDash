"""Check release contents and prevent accidental redistribution of secrets/game DLLs."""

import hashlib
from pathlib import Path
import tempfile
import unittest
import zipfile

from package import package_mod, package_server


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(
            dir="/tmp/opencode" if Path("/tmp/opencode").is_dir() else None
        )
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / "source"
        self.source.mkdir()
        self.output = self.root / "release.zip"

    def write(self, name, content=b"test binary"):
        path = self.source / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)

    def test_mod_contains_only_our_dlls_and_install_instructions(self):
        self.write("Shiron.BeatDash.Mod.dll")
        self.write("Shiron.BeatDash.Data.dll")
        self.write("Main.dll")
        self.write("Newtonsoft.Json.dll")
        package_mod(self.source, self.output)
        with zipfile.ZipFile(self.output) as zipped:
            self.assertEqual({"Plugins/Shiron.BeatDash.Mod.dll", "Libs/Shiron.BeatDash.Data.dll", "README.md",
                              "LICENSE", "NOTICE.md"}, set(zipped.namelist()))
        digest = hashlib.sha256(self.output.read_bytes()).hexdigest()
        self.assertEqual(f"{digest}  release.zip\n", self.output.with_suffix(".zip.sha256").read_text())

    def test_missing_shared_mod_dll_fails_instead_of_shipping_broken_zip(self):
        self.write("Shiron.BeatDash.Mod.dll")
        with self.assertRaises(FileNotFoundError):
            package_mod(self.source, self.output)

    def prepare_server(self):
        self.write("Shiron.BeatDash.API.dll")
        self.write("Shiron.BeatDash.API.runtimeconfig.json", b"{}")
        self.write("Shiron.BeatDash.API.deps.json", b"{}")
        self.write("wwwroot/index.html", b"<html></html>")
        self.write("wwwroot/assets/app.css", b"body{}")
        self.write("Shiron.BeatDash.API.pdb")
        self.cli = self.root / "cli"
        self.cli.mkdir()
        (self.cli / "beatdash.dll").write_bytes(b"cli binary")
        (self.cli / "beatdash.runtimeconfig.json").write_bytes(b"{}")
        (self.cli / "beatdash.deps.json").write_bytes(b"{}")

    def test_server_zip_is_complete_with_commented_configuration(self):
        self.prepare_server()
        package_server(self.source, self.cli, self.output)
        with zipfile.ZipFile(self.output) as zipped:
            for name in ("Shiron.BeatDash.API.dll", "wwwroot/index.html", "wwwroot/assets/app.css",
                         "cli/beatdash.dll", "appsettings.example.json", "README.md", "BACKUPS.md",
                         "LICENSE", "NOTICE.md"):
                self.assertIn(name, zipped.namelist())
            self.assertIn(b"//", zipped.read("appsettings.example.json"))
            self.assertIn(b'"DisableSignup": true', zipped.read("appsettings.example.json"))
            self.assertNotIn("Shiron.BeatDash.API.pdb", zipped.namelist())
            self.assertIn(b"GNU AFFERO GENERAL PUBLIC LICENSE", zipped.read("LICENSE"))

    def test_local_credentials_are_never_released(self):
        self.prepare_server()
        self.write("appsettings.json", b'{"password":"private"}')
        with self.assertRaises(ValueError):
            package_server(self.source, self.cli, self.output)


if __name__ == "__main__":
    unittest.main()
