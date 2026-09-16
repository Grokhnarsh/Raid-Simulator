"""Procedural dungeon props.

Each builder returns a :class:`Prop`: an empty acting as the pivot, with the
geometry parented to it, plus the footprint and height the renderer needs to
frame it.

Everything is built from cones and cubes on purpose. They are the two bmesh
primitives whose signatures have been stable across Blender versions, and at
64 pixels per tile nobody can tell a six-sided cone from a sculpted rock. Shape
reads at this resolution; detail does not.

Randomness is seeded per prop so a given name always produces the same mesh —
the renders are committed art, and art that changes when you re-run the
generator is art nobody trusts.
"""

from __future__ import annotations

import math
import random
from dataclasses import dataclass, field

import bmesh
import bpy
from mathutils import Matrix, Vector

from . import mats


@dataclass
class Prop:
    """A built prop, ready to render."""

    name: str
    pivot: bpy.types.Object

    #: Tiles the prop occupies on the floor. Drives the render width.
    footprint_cells: float = 1.0

    #: Height in tile-widths. Drives the render height.
    height_cells: float = 1.0

    #: Blocks movement when placed in the dungeon.
    blocking: bool = True

    parts: list[bpy.types.Object] = field(default_factory=list)

    def dispose(self) -> None:
        for part in self.parts:
            bpy.data.objects.remove(part, do_unlink=True)
        bpy.data.objects.remove(self.pivot, do_unlink=True)


# ---------------------------------------------------------------------------
# mesh helpers
# ---------------------------------------------------------------------------


def _part(name: str, material: bpy.types.Material) -> tuple[bpy.types.Object, bmesh.types.BMesh]:
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj, bmesh.new()


def _finish(obj: bpy.types.Object, bm: bmesh.types.BMesh, shade_flat: bool = True) -> bpy.types.Object:
    bm.to_mesh(obj.data)
    bm.free()
    if shade_flat:
        # Flat shading keeps each facet a single tone, which survives palette
        # quantisation. Smooth shading turns into banding.
        for polygon in obj.data.polygons:
            polygon.use_smooth = False
    return obj


def _cone(bm, segments, r1, r2, depth, at=(0.0, 0.0, 0.0), rot=(0.0, 0.0, 0.0)):
    matrix = Matrix.Translation(Vector(at)) @ _euler_matrix(rot)
    bmesh.ops.create_cone(
        bm,
        cap_ends=True,
        cap_tris=False,
        segments=segments,
        radius1=r1,
        radius2=r2,
        depth=depth,
        matrix=matrix,
    )


def _cube(bm, size, at=(0.0, 0.0, 0.0), rot=(0.0, 0.0, 0.0), scale=(1.0, 1.0, 1.0)):
    matrix = (
        Matrix.Translation(Vector(at))
        @ _euler_matrix(rot)
        @ Matrix.Diagonal(Vector(scale) .to_4d())
    )
    bmesh.ops.create_cube(bm, size=size, matrix=matrix)


def _euler_matrix(rot) -> Matrix:
    rx, ry, rz = rot
    return (
        Matrix.Rotation(rx, 4, "X")
        @ Matrix.Rotation(ry, 4, "Y")
        @ Matrix.Rotation(rz, 4, "Z")
    )


def _pivot(name: str) -> bpy.types.Object:
    empty = bpy.data.objects.new(f"{name}_pivot", None)
    empty.empty_display_size = 0.5
    bpy.context.scene.collection.objects.link(empty)
    return empty


def _assemble(name: str, parts: list[bpy.types.Object], footprint: float, height: float,
              blocking: bool = True) -> Prop:
    pivot = _pivot(name)
    for part in parts:
        part.parent = pivot
    return Prop(name=name, pivot=pivot, footprint_cells=footprint,
                height_cells=height, blocking=blocking, parts=parts)


# ---------------------------------------------------------------------------
# props
# ---------------------------------------------------------------------------


def obsidian_pillar(seed: int = 1) -> Prop:
    """A snapped basalt column with lava bleeding through its fractures."""
    rng = random.Random(seed)
    m = mats.library()

    body, bm = _part("pillar_body", m["obsidian"])
    height = rng.uniform(1.6, 2.1)
    # Three stacked segments, each slightly narrower and rotated, so the
    # silhouette breaks up instead of reading as a smooth cylinder.
    z = 0.0
    radius = 0.42
    for i in range(3):
        segment = height / 3.0 * rng.uniform(0.85, 1.15)
        _cone(bm, 6, radius, radius * 0.9, segment,
              at=(rng.uniform(-0.04, 0.04), rng.uniform(-0.04, 0.04), z + segment / 2.0),
              rot=(0.0, 0.0, rng.uniform(0.0, math.pi)))
        z += segment
        radius *= 0.9
    _finish(body, bm)

    cracks, bm = _part("pillar_cracks", m["lava"])
    for _ in range(4):
        angle = rng.uniform(0.0, math.tau)
        h = rng.uniform(0.25, 0.75) * z
        _cube(bm, 1.0,
              at=(math.cos(angle) * 0.36, math.sin(angle) * 0.36, h),
              rot=(0.0, rng.uniform(-0.4, 0.4), angle),
              scale=(0.05, 0.16, rng.uniform(0.25, 0.5)))
    _finish(cracks, bm)

    return _assemble("obsidian_pillar", [body, cracks], footprint=1.0, height=z + 0.1)


def brazier(seed: int = 2) -> Prop:
    """A standing bowl of coals. The room's light source, and its landmark."""
    rng = random.Random(seed)
    m = mats.library()

    stand, bm = _part("brazier_stand", m["steel"])
    _cone(bm, 8, 0.30, 0.10, 0.12, at=(0, 0, 0.06))          # foot
    _cone(bm, 8, 0.07, 0.07, 0.70, at=(0, 0, 0.45))          # shaft
    _cone(bm, 10, 0.16, 0.34, 0.26, at=(0, 0, 0.93))         # bowl
    _finish(stand, bm)

    coals, bm = _part("brazier_coals", m["lava_hot"])
    _cone(bm, 10, 0.30, 0.26, 0.06, at=(0, 0, 1.05))
    for _ in range(5):
        a = rng.uniform(0.0, math.tau)
        r = rng.uniform(0.0, 0.2)
        _cube(bm, 1.0, at=(math.cos(a) * r, math.sin(a) * r, 1.10),
              rot=(0, 0, a), scale=(0.09, 0.09, 0.07))
    _finish(coals, bm)

    return _assemble("brazier", [stand, coals], footprint=1.0, height=1.25)


def lava_geyser(seed: int = 3) -> Prop:
    """A crater venting fire. Pairs with a hazard so it reads as dangerous."""
    rng = random.Random(seed)
    m = mats.library()

    crust, bm = _part("geyser_crust", m["rock_dark"])
    _cone(bm, 12, 0.85, 0.50, 0.30, at=(0, 0, 0.15))
    for _ in range(6):
        a = rng.uniform(0.0, math.tau)
        _cone(bm, 5, rng.uniform(0.10, 0.18), 0.02, rng.uniform(0.20, 0.40),
              at=(math.cos(a) * 0.7, math.sin(a) * 0.7, 0.18),
              rot=(rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 0.0))
    _finish(crust, bm)

    core, bm = _part("geyser_core", m["lava_hot"])
    _cone(bm, 12, 0.46, 0.30, 0.10, at=(0, 0, 0.30))
    _cone(bm, 8, 0.22, 0.04, 0.55, at=(0, 0, 0.60))
    _finish(core, bm)

    return _assemble("lava_geyser", [crust, core], footprint=1.6, height=0.95)


def stalagmite(seed: int = 4) -> Prop:
    """A cluster of rock spikes. The cheap, plentiful piece of set dressing."""
    rng = random.Random(seed)
    m = mats.library()

    rock, bm = _part("stalagmite", m["rock_mid"])
    tallest = 0.0
    for i in range(3):
        h = rng.uniform(0.45, 1.15)
        a = rng.uniform(0.0, math.tau)
        r = 0.0 if i == 0 else rng.uniform(0.15, 0.30)
        _cone(bm, 5, rng.uniform(0.14, 0.24), 0.015, h,
              at=(math.cos(a) * r, math.sin(a) * r, h / 2.0),
              rot=(rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), a))
        tallest = max(tallest, h)
    _finish(rock, bm)

    return _assemble("stalagmite", [rock], footprint=1.0, height=tallest + 0.05)


def bone_pile(seed: int = 5) -> Prop:
    """Somebody else's failed run. Walkable, unlike most props."""
    rng = random.Random(seed)
    m = mats.library()

    bones, bm = _part("bone_pile", m["bone"])
    for _ in range(9):
        a = rng.uniform(0.0, math.tau)
        r = rng.uniform(0.0, 0.34)
        _cone(bm, 5, 0.035, 0.035, rng.uniform(0.22, 0.42),
              at=(math.cos(a) * r, math.sin(a) * r, rng.uniform(0.03, 0.10)),
              rot=(math.radians(90.0) + rng.uniform(-0.3, 0.3), 0.0, a))
    _cone(bm, 7, 0.12, 0.10, 0.16, at=(rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), 0.09))
    _finish(bones, bm)

    return _assemble("bone_pile", [bones], footprint=1.0, height=0.3, blocking=False)


def ember_chest(seed: int = 6) -> Prop:
    """The treasure room's payoff."""
    m = mats.library()

    wood, bm = _part("chest_body", m["rock_light"])
    _cube(bm, 1.0, at=(0, 0, 0.20), scale=(0.62, 0.42, 0.40))
    _cone(bm, 12, 0.21, 0.21, 0.62, at=(0, 0, 0.42), rot=(0.0, math.radians(90.0), 0.0))
    _finish(wood, bm)

    bands, bm = _part("chest_bands", m["steel"])
    for x in (-0.22, 0.22):
        _cube(bm, 1.0, at=(x, 0, 0.22), scale=(0.06, 0.45, 0.46))
    _cube(bm, 1.0, at=(0, -0.22, 0.30), scale=(0.14, 0.05, 0.14))
    _finish(bands, bm)

    glow, bm = _part("chest_glow", m["ember"])
    _cube(bm, 1.0, at=(0, -0.215, 0.36), scale=(0.40, 0.02, 0.05))
    _finish(glow, bm)

    return _assemble("ember_chest", [wood, bands, glow], footprint=1.0, height=0.62)


def rune_stone(seed: int = 7) -> Prop:
    """A leaning marker slab. Used to signpost the boss door."""
    rng = random.Random(seed)
    m = mats.library()

    slab, bm = _part("rune_slab", m["rock_dark"])
    tilt = rng.uniform(-0.12, 0.12)
    _cube(bm, 1.0, at=(0, 0, 0.75), rot=(tilt, 0.0, rng.uniform(0.0, 0.6)),
          scale=(0.46, 0.16, 1.5))
    _cone(bm, 7, 0.40, 0.30, 0.16, at=(0, 0, 0.08))
    _finish(slab, bm)

    runes, bm = _part("rune_glyphs", m["crystal"])
    for i in range(3):
        _cube(bm, 1.0, at=(0.0, -0.085, 0.55 + i * 0.32), rot=(tilt, 0.0, 0.0),
              scale=(0.22, 0.02, 0.05))
    _finish(runes, bm)

    return _assemble("rune_stone", [slab, runes], footprint=1.0, height=1.6)


def gate_arch(seed: int = 8) -> Prop:
    """A two-tile doorway frame. Marks the transition into the boss arena."""
    m = mats.library()

    stone, bm = _part("gate_stone", m["obsidian"])
    for x in (-0.85, 0.85):
        _cone(bm, 6, 0.30, 0.26, 2.0, at=(x, 0, 1.0))
    _cube(bm, 1.0, at=(0, 0, 2.05), scale=(2.2, 0.42, 0.30))
    _finish(stone, bm)

    glow, bm = _part("gate_glow", m["lava"])
    _cube(bm, 1.0, at=(0, 0, 1.88), scale=(1.9, 0.10, 0.06))
    _finish(glow, bm)

    return _assemble("gate_arch", [stone, glow], footprint=2.0, height=2.25)


#: Everything the batch build produces. Add an entry and it renders next run.
CATALOGUE = {
    "obsidian_pillar": obsidian_pillar,
    "brazier": brazier,
    "lava_geyser": lava_geyser,
    "stalagmite": stalagmite,
    "bone_pile": bone_pile,
    "ember_chest": ember_chest,
    "rune_stone": rune_stone,
    "gate_arch": gate_arch,
}
