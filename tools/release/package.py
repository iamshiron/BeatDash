"""Create downloadable release ZIPs without including local config or game DLLs."""

import argparse
import hashlib
from pathlib import Path
import tempfile
import shutil
import zipfile

from notices import write_notices


ROOT = Path(__file__).resolve().parents[2]
TEMP_DIRECTORY = "/tmp/opencode" if Path("/tmp/opencode").is_dir() else None
LEGAL_FILES = ("LICENSE", "NOTICE.md")


def require_file(path):
    if not path.is_file():
        raise FileNotFoundError(f"Required release asset is missing: {path}")


def copy_legal_files(stage):
    for name in LEGAL_FILES:
        require_file(ROOT / name)
        shutil.copyfile(ROOT / name, stage / name)


def archive(source, output):
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as zipped:
        for path in sorted(source.rglob("*")):
            if path.is_file():
                if path.suffix == ".pdb":
                    continue
                local_config = path.name.startswith("appsettings") and path.name != "appsettings.example.json"
                if local_config or path.name.startswith(".env") or path.suffix == ".user":
                    raise ValueError(f"Refusing to release local configuration: {path}")
                zipped.write(path, path.relative_to(source))
    digest = hashlib.sha256(output.read_bytes()).hexdigest()
    output.with_suffix(output.suffix + ".sha256").write_text(f"{digest}  {output.name}\n", encoding="utf-8", newline="\n")


def package_server(source, cli, output):
    for path in (source / "Shiron.BeatDash.API.dll", source / "wwwroot/index.html",
                 source / "Shiron.BeatDash.API.runtimeconfig.json", source / "Shiron.BeatDash.API.deps.json",
                 source / "wwwroot/third-party-notices.txt",
                 cli / "beatdash.dll", cli / "beatdash.runtimeconfig.json", cli / "beatdash.deps.json"):
        require_file(path)
    with tempfile.TemporaryDirectory(prefix="beatdash-server-", dir=TEMP_DIRECTORY) as temporary:
        stage = Path(temporary)
        shutil.copytree(source, stage, dirs_exist_ok=True)
        shutil.copytree(cli, stage / "cli")
        shutil.copyfile(ROOT / "tools/release/appsettings.example.json", stage / "appsettings.example.json")
        shutil.copyfile(ROOT / "tools/release/SERVER-README.md", stage / "README.md")
        shutil.copyfile(ROOT / "docs/BACKUPS.md", stage / "BACKUPS.md")
        copy_legal_files(stage)
        write_notices(stage / "THIRD-PARTY-NOTICES.txt",
                      [source / "Shiron.BeatDash.API.deps.json", cli / "beatdash.deps.json"],
                      source / "wwwroot/third-party-notices.txt")
        archive(stage, output)


def package_mod(source, output):
    with tempfile.TemporaryDirectory(prefix="beatdash-mod-", dir=TEMP_DIRECTORY) as temporary:
        stage = Path(temporary)
        for name, directory in (("Shiron.BeatDash.Mod.dll", "Plugins"),
                                ("Shiron.BeatDash.Data.dll", "Libs")):
            require_file(source / name)
            target = stage / directory / name
            target.parent.mkdir(exist_ok=True)
            shutil.copyfile(source / name, target)
        shutil.copyfile(ROOT / "tools/release/MOD-README.md", stage / "README.md")
        copy_legal_files(stage)
        archive(stage, output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("kind", choices=("server", "mod"))
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--cli", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if args.kind == "server":
        if args.cli is None:
            parser.error("--cli is required for a server release")
        package_server(args.source, args.cli, args.output)
    else:
        package_mod(args.source, args.output)
