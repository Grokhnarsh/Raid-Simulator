"""Snaps already-generated PNGs to the EmberDepths palette.

Used on the PixelLab terrain tiles. The Blender props are quantised as part of
their own render, but the tiles arrive from an external generator and have to
be brought into the same colour space afterwards — otherwise floor and prop sit
next to each other looking like two different games.

It also fixes a specific, recurring problem: generated "volcanic rock" tends to
come back with a cool blue-grey cast. Snapping terrain to
``palette.ENVIRONMENT_PALETTE`` — which deliberately excludes the cool STEEL and
PLAYER ramps — forces it warm, and keeps the cool half of the palette reserved
for the party.

No Blender required; this runs on any Python 3.

    python tools/blender/quantise.py Assets/_Project/Art/Tiles
    python tools/blender/quantise.py --full Assets/_Project/Art/Props
"""

from __future__ import annotations

import argparse
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

from edgen import palette, pixelize  # noqa: E402


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(prog="quantise")
    parser.add_argument("paths", nargs="+", help="PNG files or directories to process")
    parser.add_argument(
        "--full",
        action="store_true",
        help="allow the whole palette instead of the environment-only subset",
    )
    parser.add_argument("--dry-run", action="store_true", help="report without writing")
    args = parser.parse_args(argv)

    entries = palette.FULL_PALETTE if args.full else palette.ENVIRONMENT_PALETTE
    print(f"palette: {'full' if args.full else 'environment'} ({len(entries)} colours)")

    files: list[str] = []
    for path in args.paths:
        if os.path.isdir(path):
            for root, _dirs, names in os.walk(path):
                files.extend(os.path.join(root, n) for n in names if n.lower().endswith(".png"))
        elif path.lower().endswith(".png"):
            files.append(path)

    if not files:
        print("nothing to do", file=sys.stderr)
        return 1

    # pixelize snaps against FULL_PALETTE by default; swap the list it reads so
    # the restriction applies without duplicating the quantiser.
    original = palette.FULL_PALETTE
    palette.FULL_PALETTE = entries

    try:
        for path in sorted(files):
            before = pixelize.unique_colour_count(path)

            if args.dry_run:
                print(f"  {os.path.basename(path):<24} {before:>4} colours (dry run)")
                continue

            pixelize.quantise_file(path)
            after = pixelize.unique_colour_count(path)
            print(f"  {os.path.basename(path):<24} {before:>4} -> {after:>3} colours")
    finally:
        palette.FULL_PALETTE = original

    print(f"\ndone: {len(files)} file(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
