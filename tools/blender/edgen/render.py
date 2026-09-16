"""Renders props to eight-direction sprite sets.

The render settings here are not defaults with a few tweaks — almost every one
of them is chosen against Blender's default, because Blender's defaults are
tuned for photographic output and this is the opposite of that.

    view_transform = Standard   AgX/Filmic desaturate everything. The palette
                                was authored in sRGB and must arrive in sRGB.
    filter_size    = 0.01       Reconstruction filtering is anti-aliasing by
                                another name; at 64px it is just blur.
    film_transparent            Sprites need alpha, not a sky.
    samples        = low        There is nothing to converge on: flat shading,
                                no ray tracing, and the output gets quantised.
"""

from __future__ import annotations

import math
import os

import bpy

from . import isocam, pixelize
from .props import Prop

#: Direction names and their yaw offsets, matching IsoDirection on the C# side.
#: Rotating the prop rather than the camera keeps the lighting fixed in world
#: space, which is what makes a turning object look lit rather than painted.
DIRECTIONS: list[tuple[str, float]] = [
    ("south", 0.0),
    ("south-east", 45.0),
    ("east", 90.0),
    ("north-east", 135.0),
    ("north", 180.0),
    ("north-west", 225.0),
    ("west", 270.0),
    ("south-west", 315.0),
]


def configure_scene(width: int, height: int) -> None:
    scene = bpy.context.scene
    render = scene.render

    render.engine = _pick_engine()
    render.resolution_x = width
    render.resolution_y = height
    render.resolution_percentage = 100
    render.film_transparent = True
    render.filter_size = 0.01

    render.image_settings.file_format = "PNG"
    render.image_settings.color_mode = "RGBA"
    render.image_settings.color_depth = "8"
    render.image_settings.compression = 100

    # Straight sRGB out. Anything else and the palette snap has to fight the
    # colour management instead of just working.
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0

    _set_samples(scene, 16)


def _pick_engine() -> str:
    """Blender renamed EEVEE across versions; take whichever this build has."""
    available = {
        item.identifier
        for item in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items
    }

    for preferred in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
        if preferred in available:
            return preferred

    return next(iter(available))


def _set_samples(scene, samples: int) -> None:
    eevee = getattr(scene, "eevee", None)
    if eevee is not None and hasattr(eevee, "taa_render_samples"):
        eevee.taa_render_samples = samples

    cycles = getattr(scene, "cycles", None)
    if cycles is not None:
        cycles.samples = samples
        cycles.use_denoising = False


def render_prop(prop: Prop, out_dir: str, directions=None) -> dict[str, str]:
    """Renders one prop to ``out_dir/<direction>.png``. Returns the written paths."""
    directions = directions or DIRECTIONS
    os.makedirs(out_dir, exist_ok=True)

    # One cell is TILE_WIDTH_PX across, plus padding for overhang. Rounded up to
    # an even number so the sprite's horizontal centre lands on a pixel boundary
    # and the Unity importer's computed pivot is exact.
    width = prop.footprint_cells * isocam.TILE_WIDTH_PX + isocam.HORIZONTAL_PADDING_PX * 2
    width = int(math.ceil(width / 2.0) * 2)
    height = isocam.frame_height_px_for(prop.footprint_cells, prop.height_cells)

    configure_scene(width, height)

    # Frame on the prop's vertical middle, not its origin at the floor, or the
    # top half of a tall prop falls out of shot.
    focus = (0.0, 0.0, prop.height_cells * 0.5)
    isocam.setup(width, height, target=focus)

    written: dict[str, str] = {}

    for name, yaw in directions:
        prop.pivot.rotation_euler = (0.0, 0.0, math.radians(yaw))
        bpy.context.view_layer.update()

        path = os.path.join(out_dir, f"{name}.png")
        bpy.context.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)

        opaque = pixelize.quantise_file(path)
        if opaque == 0:
            print(f"  !! {prop.name}/{name}: rendered empty — check framing")

        written[name] = path

    prop.pivot.rotation_euler = (0.0, 0.0, 0.0)
    return written


def clear_scene() -> None:
    """Removes everything except the camera rig, so props cannot contaminate each other."""
    keep = {"EDGEN_IsoCam", "EDGEN_Key", "EDGEN_Fill", "EDGEN_Bounce"}

    for obj in list(bpy.data.objects):
        if obj.name in keep:
            continue
        bpy.data.objects.remove(obj, do_unlink=True)

    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
