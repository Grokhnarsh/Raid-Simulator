"""Materials and lighting, built so the render quantises cleanly.

The renders get snapped to a fixed palette afterwards, which changes what good
lighting means. Smooth photographic gradients are actively harmful: they land
between palette entries and come out as dithered noise. What works is a small
number of clearly separated tones per surface, which is what this rig produces
— one hard key light, one cool fill, and a warm bounce standing in for the lava
underfoot.
"""

from __future__ import annotations

import math

import bpy

from . import palette


def _principled(material: bpy.types.Material) -> bpy.types.ShaderNode:
    return material.node_tree.nodes["Principled BSDF"]


def solid(name: str, hex_colour: str, roughness: float = 0.85) -> bpy.types.Material:
    """A plain matte material in a palette colour."""
    material = bpy.data.materials.get(name)
    if material is None:
        material = bpy.data.materials.new(name)
    material.use_nodes = True

    bsdf = _principled(material)
    r, g, b = palette.hex_to_linear(hex_colour)
    bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = 0.0
    return material


def emissive(name: str, hex_colour: str, strength: float = 1.0) -> bpy.types.Material:
    """A glowing material for lava, coals and runes.

    Strength is kept low on purpose. Anything above about 5 clips to pure white\r
    before quantisation ever runs, so the palette snap receives a flat blob and\r
    the lava ramp's hues are lost. Low emission plus a hot palette colour reads\r
    far better than high emission plus a clipped one.
    """
    material = bpy.data.materials.get(name)
    if material is None:
        material = bpy.data.materials.new(name)
    material.use_nodes = True

    bsdf = _principled(material)
    r, g, b = palette.hex_to_linear(hex_colour)
    bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
    bsdf.inputs["Roughness"].default_value = 1.0
    bsdf.inputs["Emission Color"].default_value = (r, g, b, 1.0)
    bsdf.inputs["Emission Strength"].default_value = strength
    return material


#: Named materials the prop builders draw from. Keeping them in one dict means a
#: palette change repaints every prop at once.
def library() -> dict[str, bpy.types.Material]:
    return {
        "rock_dark": solid("EDGEN_RockDark", palette.ROCK[1]),
        "rock_mid": solid("EDGEN_RockMid", palette.ROCK[3]),
        "rock_light": solid("EDGEN_RockLight", palette.ROCK[5]),
        "obsidian": solid("EDGEN_Obsidian", palette.OBSIDIAN[1], roughness=0.35),
        "ash": solid("EDGEN_Ash", palette.ASH[2]),
        "bone": solid("EDGEN_Bone", palette.BONE[3]),
        "steel": solid("EDGEN_Steel", palette.STEEL[3], roughness=0.5),
        "lava": emissive("EDGEN_Lava", palette.LAVA[5], strength=1.0),
        "lava_hot": emissive("EDGEN_LavaHot", palette.LAVA[7], strength=1.3),
        "ember": emissive("EDGEN_Ember", palette.EMBER_GLOW, strength=1.0),
        "crystal": emissive("EDGEN_Crystal", palette.CRYSTAL[2], strength=1.0),
    }


def setup_lighting() -> None:
    """Three lights, no shadows softer than they need to be.

    Positions are fixed relative to the world rather than to the camera, so a
    prop rendered at eight rotations is lit from eight different angles — that
    is what makes the rotations read as one object turning rather than as eight
    flat stickers.
    """
    scene = bpy.context.scene

    for name in ("EDGEN_Key", "EDGEN_Fill", "EDGEN_Bounce"):
        existing = bpy.data.objects.get(name)
        if existing is not None:
            bpy.data.objects.remove(existing, do_unlink=True)

    # Key: from the upper front-left, hard, slightly warm.
    key_data = bpy.data.lights.new("EDGEN_KeyData", type="SUN")
    key_data.energy = 2.4
    key_data.angle = math.radians(2.0)
    key_data.color = palette.hex_to_rgb("#fff2e0")
    key = bpy.data.objects.new("EDGEN_Key", key_data)
    key.rotation_euler = (math.radians(50.0), 0.0, math.radians(-35.0))
    scene.collection.objects.link(key)

    # Fill: cool and weak, so shadowed faces stay in the palette's cool ramp
    # instead of collapsing to black.
    fill_data = bpy.data.lights.new("EDGEN_FillData", type="SUN")
    fill_data.energy = 0.8
    fill_data.color = palette.hex_to_rgb("#8fb4c8")
    fill = bpy.data.objects.new("EDGEN_Fill", fill_data)
    fill.rotation_euler = (math.radians(65.0), 0.0, math.radians(150.0))
    scene.collection.objects.link(fill)

    # Bounce: the lava underfoot. Comes from below, which is what sells the
    # biome more than any amount of texture detail.
    bounce_data = bpy.data.lights.new("EDGEN_BounceData", type="SUN")
    bounce_data.energy = 1.2
    bounce_data.color = palette.hex_to_rgb(palette.EMBER_GLOW)
    bounce = bpy.data.objects.new("EDGEN_Bounce", bounce_data)
    bounce.rotation_euler = (math.radians(-70.0), 0.0, math.radians(20.0))
    scene.collection.objects.link(bounce)

    world = scene.world
    if world is None:
        world = bpy.data.worlds.new("EDGEN_World")
        scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        r, g, b = palette.hex_to_linear(palette.ROCK[0])
        background.inputs["Color"].default_value = (r, g, b, 1.0)
        background.inputs["Strength"].default_value = 0.35
