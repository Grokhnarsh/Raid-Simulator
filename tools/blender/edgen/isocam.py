"""The isometric camera, and the arithmetic that makes it match the tile grid.

The single most important number in this file is the camera's elevation:
**30 degrees**, not the 35.264 degrees of "true" isometric.

Derivation, because it is worth being able to check:

    A 1x1 ground square, viewed with the camera yawed 45 degrees, projects to a
    horizontal extent of its diagonal, sqrt(2). Its vertical extent is
    sqrt(2) * sin(elevation). Pixel-art isometric wants those in a 2:1 ratio:

        sqrt(2) / (sqrt(2) * sin(elevation)) = 2
                                 sin(elevation) = 0.5
                                      elevation = 30 degrees

    At 35.264 degrees you get sqrt(3):1 instead, which is correct for
    engineering isometric and wrong for a 64x32 tile — everything renders
    slightly too tall and props stop sitting flush on the floor.
"""

from __future__ import annotations

import math

import bpy
from mathutils import Vector

#: Camera elevation above the horizon. See the module docstring before changing.
ELEVATION_DEGREES = 30.0

#: Yaw. 45 degrees is what turns the grid's axes into the screen's diagonals.
YAW_DEGREES = 45.0

#: Must match IsoGrid.TileWidthPx on the Unity side.
TILE_WIDTH_PX = 64

#: Horizontal screen extent of one 1x1 world cell, in world units.
CELL_SCREEN_WIDTH = math.sqrt(2.0)

#: Rendered pixels per world unit, so that one cell is exactly TILE_WIDTH_PX wide.
PIXELS_PER_WORLD_UNIT = TILE_WIDTH_PX / CELL_SCREEN_WIDTH

#: Breathing room above and below a prop, in pixels. Enough that a leaning slab
#: or a plume of flame is not clipped, and little enough that the sprite is
#: mostly prop rather than mostly nothing.
VERTICAL_PADDING_PX = 12

#: Same, horizontally.
HORIZONTAL_PADDING_PX = 16


def ortho_scale_for(render_width_px: int, render_height_px: int) -> float:
    """Blender's ortho_scale spans the LARGER render dimension, so pick that one."""
    longest = max(render_width_px, render_height_px)
    return longest / PIXELS_PER_WORLD_UNIT


def setup(
    render_width_px: int,
    render_height_px: int,
    target: Vector | tuple[float, float, float] = (0.0, 0.0, 0.0),
    distance: float = 30.0,
) -> bpy.types.Object:
    """Creates (or reuses) the isometric camera and makes it the scene camera.

    ``target`` is the point that lands in the centre of the frame. For a prop
    standing on the floor that should be somewhere around its waist, not its
    origin, or half the sprite is empty space.
    """
    scene = bpy.context.scene

    camera_data = bpy.data.cameras.get("EDGEN_IsoCam")
    if camera_data is None:
        camera_data = bpy.data.cameras.new("EDGEN_IsoCam")

    camera_data.type = "ORTHO"
    camera_data.ortho_scale = ortho_scale_for(render_width_px, render_height_px)
    # Ortho has no perspective, so the clip range only has to contain the scene.
    camera_data.clip_start = 0.1
    camera_data.clip_end = distance * 4.0

    camera = bpy.data.objects.get("EDGEN_IsoCam")
    if camera is None:
        camera = bpy.data.objects.new("EDGEN_IsoCam", camera_data)
        scene.collection.objects.link(camera)
    camera.data = camera_data

    pitch = math.radians(90.0 - ELEVATION_DEGREES)
    yaw = math.radians(YAW_DEGREES)
    camera.rotation_euler = (pitch, 0.0, yaw)

    # Walk backwards along the view direction from the target.
    elevation = math.radians(ELEVATION_DEGREES)
    offset = Vector(
        (
            math.sin(yaw) * math.cos(elevation),
            -math.cos(yaw) * math.cos(elevation),
            math.sin(elevation),
        )
    ) * distance

    camera.location = Vector(target) + offset
    scene.camera = camera
    return camera


def frame_height_px_for(footprint_cells: float, height_cells: float) -> int:
    """Render height that fits a prop of the given footprint and height.

    A prop occupying ``footprint_cells`` tiles across needs half a tile of
    vertical room for its base diamond, plus its own height. Rounded up to an
    even number so the sprite's centre lands on a pixel boundary.
    """
    # Base diamond: a footprint of N cells is N * TILE_HEIGHT_PX tall on screen.
    base = footprint_cells * TILE_WIDTH_PX * 0.5

    # A vertical world unit projects to cos(elevation) screen units — the
    # camera is tilted down, so height foreshortens rather than lengthens.
    body = height_cells * math.cos(math.radians(ELEVATION_DEGREES)) * PIXELS_PER_WORLD_UNIT

    total = int(math.ceil((base + body + VERTICAL_PADDING_PX) / 2.0) * 2)
    return max(32, total)
