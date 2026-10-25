"""Check release contents and prevent accidental redistribution of secrets/game DLLs."""

import hashlib
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest import mock
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
        self.nuget = self.root / "nuget"
        environment = mock.patch.dict(os.environ, {"NUGET_PACKAGES": str(self.nuget)})
        environment.start()
        self.addCleanup(environment.stop)

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

    def restore_package(self, package, license_expression, files=None):
        folder = self.nuget / package.lower()
        folder.mkdir(parents=True)
        name, version = package.split("/")
        (folder / f"{name.lower()}.nuspec").write_text(
            '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata>'
            f'<id>{name}</id><version>{version}</version><copyright>(c) {name} authors</copyright>'
            f'<license type="expression">{license_expression}</license></metadata></package>')
        for file_name, content in (files or {}).items():
            (folder / file_name).write_text(content)
        return package.lower()

    def write_deps(self, name, *packages):
        libraries = {"Shiron.BeatDash.Data/1.0.0": {"type": "project"}}
        libraries |= {package: {"type": "package", "path": package} for package in packages}
        self.write(name, json.dumps({"libraries": libraries}).encode())

    def prepare_server(self):
        self.write("Shiron.BeatDash.API.dll")
        self.write("Shiron.BeatDash.API.runtimeconfig.json", b"{}")
        self.write("Shiron.BeatDash.API.deps.json", b"{}")
        self.write("wwwroot/index.html", b"<html></html>")
        self.write("wwwroot/third-party-notices.txt", b"web-package 1.0.0\nLicense: MIT")
        self.write("wwwroot/assets/app.css", b"body{}")
        self.write("Shiron.BeatDash.API.pdb")
        self.cli = self.root / "cli"
        self.cli.mkdir()
        (self.cli / "beatdash.dll").write_bytes(b"cli binary")
        (self.cli / "beatdash.runtimeconfig.json").write_bytes(b"{}")
        (self.cli / "beatdash.deps.json").write_bytes(b"{}")

    def test_server_notices_cover_shipped_packages_and_web_bundle(self):
        self.prepare_server()
        vendor = {"THIRD-PARTY-NOTICES.TXT": "Vendored zlib notice"}
        packages = (self.restore_package("Template.Package/1.0.0", "MIT"),
                    self.restore_package("Vendor.One/2.0.0", "Apache-2.0", {"LICENSE.txt": "Vendor license", **vendor}),
                    self.restore_package("Vendor.Two/2.0.0", "MIT", vendor))
        self.write_deps("Shiron.BeatDash.API.deps.json", *packages)
        package_server(self.source, self.cli, self.output)
        with zipfile.ZipFile(self.output) as zipped:
            notices = zipped.read("THIRD-PARTY-NOTICES.txt").decode()
        self.assertIn("Template.Package 1.0.0\nLicense: MIT\nCopyright: (c) Template.Package authors", notices)
        self.assertIn("Permission is hereby granted", notices)
        self.assertIn("Vendor license", notices)
        self.assertEqual(1, notices.count("Vendored zlib notice"))
        self.assertIn("web-package 1.0.0", notices)
        self.assertNotIn("Shiron.BeatDash.Data", notices)

    def test_package_without_license_file_or_template_fails(self):
        self.prepare_server()
        self.write_deps("Shiron.BeatDash.API.deps.json", self.restore_package("Odd.Package/1.0.0", "WTFPL"))
        with self.assertRaisesRegex(FileNotFoundError, "WTFPL"):
            package_server(self.source, self.cli, self.output)

    def test_unrestored_package_fails(self):
        self.prepare_server()
        self.write_deps("Shiron.BeatDash.API.deps.json", "missing.package/1.0.0")
        with self.assertRaisesRegex(FileNotFoundError, "not restored"):
            package_server(self.source, self.cli, self.output)

    def test_server_zip_is_complete_with_commented_configuration(self):
        self.prepare_server()
        package_server(self.source, self.cli, self.output)
        with zipfile.ZipFile(self.output) as zipped:
            for name in ("Shiron.BeatDash.API.dll", "wwwroot/index.html", "wwwroot/assets/app.css",
                         "cli/beatdash.dll", "appsettings.example.json", "README.md", "BACKUPS.md",
                         "LICENSE", "NOTICE.md", "THIRD-PARTY-NOTICES.txt", "wwwroot/third-party-notices.txt"):
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
