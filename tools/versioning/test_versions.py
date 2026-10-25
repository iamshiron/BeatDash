"""Exercise release streams against real, disposable Git histories."""

import json
import os
from datetime import datetime, timedelta, timezone
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]


class VersioningTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(
            prefix="beatdash-versioning-",
            dir="/tmp/opencode" if Path("/tmp/opencode").is_dir() else None,
        )
        self.addCleanup(self.directory.cleanup)
        self.repo = Path(self.directory.name)
        self.commit_count = 0
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Version tests")
        self.git("config", "user.email", "version-tests@example.invalid")
        for name in ("GitVersion.yml", "GitVersion.Mod.yml"):
            shutil.copyfile(ROOT / name, self.repo / name)
        self.commit("chore: Initial commit", "src/BeatDash.Data/Contract.cs")
        self.git("tag", "v1.2.3")
        self.git("tag", "beatdash-mod/v4.5.6")

    def git(self, *args):
        return subprocess.run(
            ["git", *args], cwd=self.repo, check=True, capture_output=True, text=True
        ).stdout.strip()

    def commit(self, message, *paths):
        for name in paths:
            path = self.repo / name
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open("a") as file:
                file.write(message + "\n")
        self.git("add", "-A")
        self.commit_count += 1
        date = (datetime(2026, 1, 1, tzinfo=timezone.utc)
                + timedelta(minutes=self.commit_count)).isoformat()
        subprocess.run(
            ["git", "commit", "-m", message], cwd=self.repo, check=True,
            capture_output=True, text=True,
            env={**os.environ, "GIT_AUTHOR_DATE": date, "GIT_COMMITTER_DATE": date},
        )

    def version(self, mod=False):
        return self.gitversion(self.repo, mod, {}, ["/nonormalize"])

    def tag_build_version(self, tag, mod=False):
        """Version a fresh clone checked out at `tag`, as GitHub Actions does on a tag push."""
        checkout = tempfile.TemporaryDirectory(prefix="beatdash-checkout-", dir=self.repo.parent)
        self.addCleanup(checkout.cleanup)
        subprocess.run(["git", "clone", "-q", str(self.repo), checkout.name],
                       check=True, capture_output=True, text=True)
        subprocess.run(["git", "checkout", "-q", "--detach", tag], cwd=checkout.name,
                       check=True, capture_output=True, text=True)
        env = {"GITHUB_ACTIONS": "true", "GITHUB_REF": f"refs/tags/{tag}", "GITHUB_REF_NAME": tag}
        return self.gitversion(Path(checkout.name), mod, env, [])

    def gitversion(self, repo, mod, ci_env, arguments):
        config = self.repo / ("GitVersion.Mod.yml" if mod else "GitVersion.yml")
        env = {
            key: value for key, value in os.environ.items()
            if not key.startswith(("GITHUB_", "GIT_", "BUILD_")) and key != "CI"
        }
        result = subprocess.run(
            ["dotnet", "tool", "run", "dotnet-gitversion", str(repo),
             "/config", str(config), "/nofetch", *arguments, "/output", "json"],
            cwd=ROOT, env={**env, **ci_env}, check=True, capture_output=True, text=True,
        )
        return json.loads(result.stdout)

    def test_tags_are_independent(self):
        self.assertEqual("1.2.3", self.version()["SemVer"])
        self.assertEqual("4.5.6", self.version(mod=True)["SemVer"])

    def test_mod_breaking_change_does_not_bump_backend(self):
        self.commit("feat!: Changed mod", "src/BeatDash.Mod/Plugin.cs")
        self.assertEqual("1.2.3", self.version()["SemVer"])
        self.assertEqual("5.0.0", self.version(mod=True)["MajorMinorPatch"])

    def test_backend_and_web_breaking_changes_do_not_bump_mod(self):
        for path in ("src/BeatDash.API/Program.cs", "apps/beatdash-web/src/main.tsx",
                     "packages/ui", "src/BeatDash.DB/Schema.cs"):
            with self.subTest(path=path):
                self.commit("feat!: Changed server", path)
                self.assertEqual("2.0.0", self.version()["MajorMinorPatch"])
                self.assertEqual("4.5.6", self.version(mod=True)["SemVer"])

    def test_shared_changes_bump_both(self):
        self.commit("feat: Added contract", "src/BeatDash.Data/Contract.cs")
        self.assertEqual("1.3.0", self.version()["MajorMinorPatch"])
        self.assertEqual("4.6.0", self.version(mod=True)["MajorMinorPatch"])
        self.commit("feat!: Changed shared library", "external/lib")
        self.assertEqual("2.0.0", self.version()["MajorMinorPatch"])
        self.assertEqual("5.0.0", self.version(mod=True)["MajorMinorPatch"])

    def test_mixed_commit_affects_both(self):
        self.commit("feat!: Changed protocol", "src/BeatDash.Mod/Plugin.cs",
                    "src/BeatDash.Data/Contract.cs")
        self.assertEqual("2.0.0", self.version()["MajorMinorPatch"])
        self.assertEqual("5.0.0", self.version(mod=True)["MajorMinorPatch"])

    def test_fix_and_perf_bump_patch(self):
        self.commit("perf(data): Improved protocol", "src/BeatDash.Data/Contract.cs")
        self.assertEqual("1.2.4", self.version()["MajorMinorPatch"])
        self.assertEqual("4.5.7", self.version(mod=True)["MajorMinorPatch"])

    def test_other_stream_tag_is_not_a_version_source(self):
        self.commit("feat: Added mod feature", "src/BeatDash.Mod/Plugin.cs")
        self.git("tag", "beatdash-mod/v9.0.0")
        self.assertEqual("1.2.3", self.version()["SemVer"])
        self.assertEqual("9.0.0", self.version(mod=True)["SemVer"])
        self.commit("fix: Fixed backend", "src/BeatDash.API/Program.cs")
        self.git("tag", "v2.0.0")
        self.assertEqual("2.0.0", self.version()["SemVer"])
        self.assertEqual("9.0.0", self.version(mod=True)["SemVer"])

    def test_feature_and_release_labels(self):
        self.git("checkout", "-b", "feat/versioning")
        self.commit("feat: Added contract", "src/BeatDash.Data/Contract.cs")
        self.assertEqual("versioning", self.version()["PreReleaseLabel"])
        self.assertEqual("versioning", self.version(mod=True)["PreReleaseLabel"])
        self.git("checkout", "main")
        self.git("checkout", "-b", "rel/1.3.0-rc")
        self.commit("fix: Fixed contract", "src/BeatDash.Data/Contract.cs")
        self.assertEqual("rc", self.version()["PreReleaseLabel"])
        self.assertEqual("rc", self.version(mod=True)["PreReleaseLabel"])

    def test_ci_tag_checkout_uses_tag_version(self):
        self.git("checkout", "-b", "rel/1.3.0-alpha")
        self.commit("feat: Added contract", "src/BeatDash.Data/Contract.cs")
        self.git("tag", "-a", "v1.3.0-alpha", "-m", "Release")
        self.git("tag", "-a", "beatdash-mod/v4.6.0-alpha.2", "-m", "Release")
        self.assertEqual("1.3.0-alpha", self.version()["SemVer"])
        self.assertEqual("1.3.0-alpha", self.tag_build_version("v1.3.0-alpha")["SemVer"])
        self.assertEqual("4.6.0-alpha.2",
                         self.tag_build_version("beatdash-mod/v4.6.0-alpha.2", mod=True)["SemVer"])
        self.git("tag", "v1.3.0")
        self.assertEqual("1.3.0", self.tag_build_version("v1.3.0")["SemVer"])
        self.commit("fix: Fixed backend", "src/BeatDash.API/Program.cs")
        self.git("tag", "-a", "v1.3.1-rc.2", "-m", "Release")
        self.assertEqual("1.3.1-rc.2", self.tag_build_version("v1.3.1-rc.2")["SemVer"])


if __name__ == "__main__":
    unittest.main()
