#!/usr/bin/env python3
"""Check that every asset reference in the project points at a file that exists.

Unity stores references as GUIDs. A reference whose GUID has no owning .meta shows up in the editor
as a silently empty field — the classic failure mode of a data-driven project, and one that costs
far more to diagnose at runtime than to catch here.

This runs without Unity, so it works in continuous integration and in an agent session.

Usage:
    python3 Tools/Unity/verify_references.py [--verbose]
"""

from __future__ import annotations

import argparse
import collections
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GUID_IN_META = re.compile(r"^guid: ([0-9a-f]{32})$", re.MULTILINE)
GUID_REFERENCE = re.compile(r"guid: ([0-9a-f]{32})")

# Unity's built-in resources (default material, skybox, ...) use GUIDs beginning with a long run of
# zeroes. They live inside the editor, not in the project, so they are expected not to resolve here.
BUILTIN_GUID = re.compile(r"^0{8}")

SEARCHED_GLOBS = ("Assets/**/*.asset", "Assets/**/*.unity", "Assets/**/*.prefab",
                  "Assets/**/*.mat", "ProjectSettings/*.asset")


def collect_owned_guids() -> dict[str, Path]:
    """Map every GUID the project owns to the asset it identifies."""
    owned: dict[str, Path] = {}
    for meta in ROOT.glob("Assets/**/*.meta"):
        match = GUID_IN_META.search(meta.read_text(encoding="utf-8"))
        if match:
            owned[match.group(1)] = meta.with_suffix("")
    return owned


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--verbose", action="store_true", help="Print every resolved reference.")
    args = parser.parse_args()

    owned = collect_owned_guids()
    unresolved: dict[Path, list[str]] = collections.defaultdict(list)
    checked = 0

    for pattern in SEARCHED_GLOBS:
        for asset in sorted(ROOT.glob(pattern)):
            text = asset.read_text(encoding="utf-8", errors="ignore")
            resolved = []
            for guid in GUID_REFERENCE.findall(text):
                if BUILTIN_GUID.match(guid):
                    continue
                checked += 1
                if guid in owned:
                    resolved.append(owned[guid].relative_to(ROOT).as_posix())
                else:
                    unresolved[asset].append(guid)

            if args.verbose and resolved:
                print(asset.relative_to(ROOT).as_posix())
                for target in dict.fromkeys(resolved):
                    print(f"   -> {target}")

    print(f"{len(owned)} asset(s) carry a GUID; {checked} project-internal reference(s) checked.")

    if not unresolved:
        print("All references resolve.")
        return 0

    for asset, guids in unresolved.items():
        for guid in guids:
            print(f"UNRESOLVED {guid} referenced by {asset.relative_to(ROOT).as_posix()}", file=sys.stderr)
    print(f"{sum(len(v) for v in unresolved.values())} unresolved reference(s).", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
