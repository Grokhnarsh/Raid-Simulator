#!/usr/bin/env python3
"""Generate Unity .meta files with stable, reproducible GUIDs.

Unity identifies every asset by the GUID in its .meta file, and every reference between assets is
stored as that GUID. A project authored outside the editor therefore needs .meta files up front, or
Unity invents fresh GUIDs on first import and every hand-written reference breaks.

GUIDs here are derived from the asset's project-relative path, so:

  * the same path always produces the same GUID, on any machine and in any checkout;
  * a file that references another can compute its target's GUID without a lookup table;
  * re-running this script never changes an existing asset's identity.

The script is idempotent: an asset that already has a .meta is left completely alone, so it is safe
to run after the editor has taken over maintenance of these files.

Usage:
    python3 Tools/Unity/generate_meta.py [--check] [--root <project root>]

    --check   Report what is missing and exit non-zero instead of writing.
"""

from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path

# Folders Unity never imports, and which therefore must not receive .meta files.
SKIPPED_DIR_NAMES = {".git", "Library", "Temp", "obj", "bin", "Logs", "UserSettings", "build"}

# Unity treats a file ending in ~ or starting with . as hidden; it imports neither.
def is_hidden(path: Path) -> bool:
    return path.name.startswith(".") or path.name.endswith("~")


def guid_for(relative_path: str) -> str:
    """Stable 32-character hex GUID derived from the project-relative path."""
    digest = hashlib.md5(f"raidsim::{relative_path}".encode("utf-8")).hexdigest()
    return digest[:32]


FOLDER_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:{trailing}
  assetBundleName:{trailing}
  assetBundleVariant:{trailing}
"""

SCRIPT_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:{trailing}
  assetBundleName:{trailing}
  assetBundleVariant:{trailing}
"""

NATIVE_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:{trailing}
  assetBundleName:{trailing}
  assetBundleVariant:{trailing}
"""

ASMDEF_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:{trailing}
  assetBundleName:{trailing}
  assetBundleVariant:{trailing}
"""

TEXT_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:{trailing}
  assetBundleName:{trailing}
  assetBundleVariant:{trailing}
"""

DEFAULT_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData:{trailing}
  assetBundleName:{trailing}
  assetBundleVariant:{trailing}
"""

# Unity writes a trailing space after these keys. Kept identical so the editor does not rewrite
# every file the first time it opens the project.
TRAILING = " "

TEMPLATES_BY_SUFFIX = {
    ".cs": SCRIPT_TEMPLATE,
    ".asset": NATIVE_TEMPLATE,
    ".mat": NATIVE_TEMPLATE,
    ".physicMaterial": NATIVE_TEMPLATE,
    ".asmdef": ASMDEF_TEMPLATE,
    ".asmref": ASMDEF_TEMPLATE,
    ".json": TEXT_TEMPLATE,
    ".txt": TEXT_TEMPLATE,
    ".md": TEXT_TEMPLATE,
}


def template_for(path: Path) -> str:
    """Importer block Unity uses for this file type.

    Unknown types fall back to DefaultImporter. Unity rewrites the importer block to the correct one
    on first import while preserving the GUID, so an imperfect guess costs a reserialise, never a
    broken reference.
    """
    return TEMPLATES_BY_SUFFIX.get(path.suffix.lower(), DEFAULT_TEMPLATE)


def iter_assets(assets_root: Path):
    """Yield every folder and file under Assets/ that Unity would import."""
    for path in sorted(assets_root.rglob("*")):
        if path.suffix == ".meta" or is_hidden(path):
            continue
        if any(part in SKIPPED_DIR_NAMES or is_hidden(Path(part)) for part in path.relative_to(assets_root).parts):
            continue
        yield path


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", default=None, help="Project root. Defaults to the repository root.")
    parser.add_argument("--check", action="store_true", help="Report missing .meta files without writing any.")
    args = parser.parse_args()

    root = Path(args.root) if args.root else Path(__file__).resolve().parents[2]
    assets_root = root / "Assets"
    if not assets_root.is_dir():
        print(f"No Assets folder at {assets_root}", file=sys.stderr)
        return 2

    written, missing = 0, []
    for path in iter_assets(assets_root):
        meta_path = path.with_name(path.name + ".meta")
        if meta_path.exists():
            continue

        relative = path.relative_to(root).as_posix()
        template = FOLDER_TEMPLATE if path.is_dir() else template_for(path)
        content = template.format(guid=guid_for(relative), trailing=TRAILING)

        if args.check:
            missing.append(relative)
            continue

        meta_path.write_text(content, encoding="utf-8")
        written += 1

    if args.check:
        for relative in missing:
            print(f"missing .meta: {relative}")
        print(f"{len(missing)} asset(s) without a .meta file.")
        return 1 if missing else 0

    print(f"Wrote {written} .meta file(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
