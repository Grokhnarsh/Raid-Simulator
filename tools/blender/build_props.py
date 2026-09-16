"""Batch-builds every dungeon prop and renders it to eight isometric sprites.

Run from the project root with Blender's own Python:

    & "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" `
        --background --factory-startup `
        --python tools/blender/build_props.py

Optional arguments go after a bare ``--``:

    -- --only brazier,rune_stone      build a subset
    -- --out  path/to/dir             write somewhere other than Art/Props
    -- --list                         print the catalogue and exit

Output lands in ``Assets/_Project/Art/Props/<prop>/<direction>.png`` plus a
``props.json`` manifest recording the footprint and blocking flag of each, which
is what the Unity side needs in order to place them on the grid correctly.

``--factory-startup`` is not optional: add-ons and user preferences change
colour management and render defaults, and this pipeline depends on those being
exactly what :mod:`edgen.render` sets.
"""

from __future__ import annotations

import argparse
import json
import os
import sys

import bpy

# Blender runs this file directly, so the package directory is not importable
# until we put it on the path ourselves.
_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

from edgen import GENERATOR_VERSION, mats, props, render  # noqa: E402

DEFAULT_OUT = os.path.normpath(
    os.path.join(_HERE, "..", "..", "Assets", "_Project", "Art", "Props")
)


def parse_args(argv: list[str]) -> argparse.Namespace:
    # Everything before "--" belongs to Blender.
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = []

    parser = argparse.ArgumentParser(prog="build_props", add_help=True)
    parser.add_argument("--only", default="", help="comma-separated prop names")
    parser.add_argument("--out", default=DEFAULT_OUT, help="output directory")
    parser.add_argument("--list", action="store_true", help="print the catalogue and exit")
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args(list(sys.argv))

    if args.list:
        print("Props in the catalogue:")
        for name in sorted(props.CATALOGUE):
            print(f"  {name}")
        return 0

    wanted = [n.strip() for n in args.only.split(",") if n.strip()] or sorted(props.CATALOGUE)

    unknown = [n for n in wanted if n not in props.CATALOGUE]
    if unknown:
        print(f"error: unknown prop(s): {', '.join(unknown)}", file=sys.stderr)
        print(f"       available: {', '.join(sorted(props.CATALOGUE))}", file=sys.stderr)
        return 2

    os.makedirs(args.out, exist_ok=True)

    print(f"EmberDepths prop build v{GENERATOR_VERSION}")
    print(f"  blender : {bpy.app.version_string}")
    print(f"  output  : {args.out}")
    print(f"  props   : {len(wanted)}")
    print()

    render.clear_scene()
    mats.setup_lighting()

    manifest = {
        "generator_version": GENERATOR_VERSION,
        "blender_version": bpy.app.version_string,
        "props": {},
    }

    for index, name in enumerate(wanted, start=1):
        print(f"[{index}/{len(wanted)}] {name}")

        prop = props.CATALOGUE[name](seed=abs(hash(name)) % 10_000)
        out_dir = os.path.join(args.out, name)

        try:
            written = render.render_prop(prop, out_dir)
        finally:
            prop.dispose()

        manifest["props"][name] = {
            "footprint_cells": prop.footprint_cells,
            "height_cells": round(prop.height_cells, 3),
            "blocking": prop.blocking,
            "directions": sorted(written),
        }

        print(f"      -> {len(written)} sprites in {out_dir}")

    manifest_path = os.path.join(args.out, "props.json")
    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2, sort_keys=True)

    print()
    print(f"Wrote manifest: {manifest_path}")
    print("Now switch to Unity and run EmberDepths/Art/Import Everything.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
