"""Build THIRD-PARTY-NOTICES.txt for the NuGet packages and web assets shipped in a release."""

import hashlib
import json
import os
from pathlib import Path
import re
import xml.etree.ElementTree as ElementTree


ROOT = Path(__file__).resolve().parents[2]
LICENSE_TEMPLATES = ROOT / "tools/release/licenses"
SEPARATOR = "\n\n" + "=" * 80 + "\n\n"
LICENSE_FILE = re.compile(r"^licen[cs]e([.-].*)?$", re.IGNORECASE)
NOTICE_FILE = re.compile(r"^(third-?party-?notices|notice)([.-].*)?$", re.IGNORECASE)
SPDX_OPERATORS = re.compile(r"[\s()]+|\bAND\b|\bOR\b|\bWITH\b")


def nuget_root():
    configured = os.environ.get("NUGET_PACKAGES")
    return Path(configured) if configured else Path.home() / ".nuget/packages"


def shipped_packages(deps_files):
    """Packages that `dotnet publish` copied, as listed in the published *.deps.json files."""
    packages = {}
    for deps in deps_files:
        for library in json.loads(deps.read_text(encoding="utf-8-sig")).get("libraries", {}).values():
            if library["type"] == "package":
                packages[library["path"]] = library["path"]
    return sorted(packages)


def rtf_to_text(rtf):
    text = re.sub(r"\{\\\*[^{}]*\}|\{\\(fonttbl|colortbl)[^{}]*(\{[^{}]*\}[^{}]*)*\}", "", rtf)
    text = re.sub(r"\\par[d]?\b ?", "\n", text)
    text = re.sub(r"\\'([0-9a-f]{2})", lambda match: bytes.fromhex(match.group(1)).decode("cp1252"), text)
    text = re.sub(r"\\[a-z]+-?\d* ?|[{}]", "", text)
    return text.strip()


def read_text(path):
    content = path.read_text(encoding="utf-8-sig", errors="replace")
    return rtf_to_text(content) if path.suffix.lower() == ".rtf" else content.strip()


def spdx_texts(name, expression):
    identifiers = [identifier for identifier in SPDX_OPERATORS.split(expression or "") if identifier]
    missing = [identifier for identifier in identifiers if not (LICENSE_TEMPLATES / f"{identifier}.txt").is_file()]
    if not identifiers or missing:
        raise FileNotFoundError(
            f"{name} ships no license file and no SPDX template exists for '{', '.join(missing) or expression}'. "
            f"Add it to {LICENSE_TEMPLATES.relative_to(ROOT)} (see its README).")
    return "\n\n".join(read_text(LICENSE_TEMPLATES / f"{identifier}.txt") for identifier in identifiers)


def describe(folder, package):
    nuspec = next(folder.glob("*.nuspec"), None)
    if nuspec is None:
        raise FileNotFoundError(f"NuGet package {package} is not restored in {folder.parent.parent}")
    metadata = ElementTree.parse(nuspec).getroot().find("{*}metadata")
    field = lambda tag: (metadata.findtext(f"{{*}}{tag}") or "").strip()
    license_element = metadata.find("{*}license")
    license_kind = license_element.get("type") if license_element is not None else None
    license_value = (license_element.text or "").strip() if license_element is not None else ""

    files = sorted(path for path in folder.iterdir() if path.is_file())
    if license_kind == "file":
        license_text = read_text(folder / license_value)
    elif license_files := [path for path in files if LICENSE_FILE.match(path.name)]:
        license_text = "\n\n".join(read_text(path) for path in license_files)
    elif (override := LICENSE_TEMPLATES / "packages" / f"{field('id').lower()}.txt").is_file():
        license_text = read_text(override)
    else:
        license_text = spdx_texts(package, license_value)
    notices = [path for path in files if NOTICE_FILE.match(path.name)]
    notices += sorted((folder / "notices").glob("*")) if (folder / "notices").is_dir() else []

    header = [field("id") + " " + field("version"),
              f"License: {license_value if license_kind == 'expression' else 'see below'}"]
    header += [f"{label}: {value}" for label, value in
               (("Copyright", field("copyright")), ("Authors", field("authors")), ("Project", field("projectUrl")))
               if value]
    return "\n".join(header) + "\n\n" + license_text, [read_text(path) for path in notices]


def nuget_notices(deps_files, packages_root=None):
    """Render one section per shipped package, followed by de-duplicated vendor notice files."""
    packages_root = packages_root or nuget_root()
    sections, notices = [], {}
    for package in shipped_packages(deps_files):
        section, package_notices = describe(packages_root / package, package)
        sections.append(section)
        for notice in package_notices:
            notices.setdefault(hashlib.sha256(notice.encode()).hexdigest(), (notice, []))[1].append(package)
    appendix = [f"Additional notices shipped with {', '.join(owners)}\n\n{text}" for text, owners in notices.values()]
    return sections + appendix


def write_notices(output, deps_files, web_notices, packages_root=None):
    sections = ["Third-party software included in this BeatDash release",
                *nuget_notices(deps_files, packages_root)]
    if web_notices is not None:
        sections.append(web_notices.read_text(encoding="utf-8").strip())
    output.write_text(SEPARATOR.join(sections) + "\n", encoding="utf-8", newline="\n")
