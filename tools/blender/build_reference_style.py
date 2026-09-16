"""Render the screenshot-inspired comic biomes and enemies with Blender 5.2.

The four user-supplied screenshots are visual references only. No screenshot
pixels or user-interface elements are copied into these game assets.
"""

import math
import random
import sys
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets" / "_Project" / "Art"
SOURCE_DIR = Path(__file__).resolve().parent / "reference_style_sources"
OUTLINE = "#2D2424"

PALETTES = {
    "legion_hold": {
        "base": "#BA6B51", "light": "#D38562", "shadow": "#92503E",
        "crack": "#784536", "accent": "#D9AD69", "speck": "#8C4A37",
    },
    "marsh_maze": {
        "base": "#4B8893", "light": "#6EAAB0", "shadow": "#2C6875",
        "crack": "#286273", "accent": "#36B8D5", "speck": "#80A85C",
    },
    "inferno_wall": {
        "base": "#8C7271", "light": "#AB8F8B", "shadow": "#655456",
        "crack": "#514548", "accent": "#F47737", "speck": "#B8998F",
    },
}

DIRECTIONS = [
    "south", "south-east", "east", "north-east", "north",
    "north-west", "west", "south-west",
]


def rgba(hex_color, alpha=1.0):
    value = hex_color.lstrip("#")
    srgb = [int(value[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    rgb = [v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
           for v in srgb]
    return (*rgb, alpha)


def material(name, color):
    key = f"comic_{name}_{color}"
    mat = bpy.data.materials.get(key)
    if mat is None:
        mat = bpy.data.materials.new(key)
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        nodes.clear()
        emission = nodes.new("ShaderNodeEmission")
        emission.inputs["Color"].default_value = rgba(color)
        emission.inputs["Strength"].default_value = 1
        output = nodes.new("ShaderNodeOutputMaterial")
        mat.node_tree.links.new(emission.outputs[0], output.inputs["Surface"])
        mat.diffuse_color = rgba(color)
    return mat


def polygon(name, points, color, z=0.0, outline=0.0):
    points = [(float(x), float(y)) for x, y in points]
    if outline:
        cx = sum(x for x, _ in points) / len(points)
        cy = sum(y for _, y in points) / len(points)
        outer = [
            (cx + (x - cx) * (1 + outline), cy + (y - cy) * (1 + outline))
            for x, y in points
        ]
        polygon(name + "_ink", outer, OUTLINE, z)
        z += 0.002
    mesh = bpy.data.meshes.new(name + "_mesh")
    mesh.from_pydata([(x, y, z) for x, y in points], [], [tuple(range(len(points)))])
    mesh.materials.append(material(name, color))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def ellipse(name, x, y, rx, ry, color, z=0.0, outline=0.0, n=20):
    points = [
        (x + rx * math.cos(i * 2 * math.pi / n),
         y + ry * math.sin(i * 2 * math.pi / n))
        for i in range(n)
    ]
    return polygon(name, points, color, z, outline)


def stroke(name, points, color, width, z=0.0):
    for index, ((x1, y1), (x2, y2)) in enumerate(zip(points, points[1:])):
        dx, dy = x2 - x1, y2 - y1
        length = math.hypot(dx, dy)
        if not length:
            continue
        nx, ny = -dy / length * width / 2, dx / length * width / 2
        polygon(f"{name}_{index}", [
            (x1 + nx, y1 + ny), (x2 + nx, y2 + ny),
            (x2 - nx, y2 - ny), (x1 - nx, y1 - ny),
        ], color, z)


def clear_objects():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def setup_camera(vertical_size):
    camera_data = bpy.data.cameras.new("Flat orthographic sprite camera")
    camera = bpy.data.objects.new("Flat orthographic sprite camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0, 0, 10)
    camera.rotation_euler = (0, 0, 0)
    camera.rotation_euler[0] = 0
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = vertical_size
    # Cameras look down their local -Z axis.
    bpy.context.scene.camera = camera


def render(path, width, height, vertical_size):
    path.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 8
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.filepath = str(path)
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "Medium High Contrast"
    setup_camera(vertical_size)
    bpy.ops.render.render(write_still=True)


def tile(biome, variant):
    p = PALETTES[biome]
    rng = random.Random(f"{biome}/{variant}")
    # The bare diamond reaches each edge: neighboring 64x32 cells tessellate.
    polygon("ground", [(-1, 0), (0, 0.5), (1, 0), (0, -0.5)], p["base"])
    if biome == "legion_hold":
        for n in range(8):
            x = rng.uniform(-0.78, 0.78)
            y = rng.uniform(-0.30, 0.30)
            if abs(x) + 2 * abs(y) > 0.84:
                continue
            rx = rng.uniform(0.07, 0.21)
            ry = rx * rng.uniform(0.22, 0.42)
            ellipse(f"sunbaked_patch_{n}", x, y, rx, ry,
                    p["light"] if n % 3 else p["shadow"], 0.01, n=9)
        stroke("dry_fissure", [(-0.45, -0.08), (-0.25, -0.03),
               (-0.11, -0.09), (0.05, -0.08)], p["crack"], 0.018, 0.02)
        for n in range(5):
            x = rng.uniform(-0.7, 0.7)
            y = rng.uniform(-0.25, 0.25)
            if abs(x) + 2 * abs(y) < 0.8:
                ellipse(f"grit_{n}", x, y, 0.025, 0.009,
                        p["speck"], 0.03, n=8)
    elif biome == "marsh_maze":
        for n in range(6):
            x = rng.uniform(-0.70, 0.70)
            y = rng.uniform(-0.22, 0.22)
            if abs(x) + 2 * abs(y) > 0.76:
                continue
            rx = rng.uniform(0.10, 0.23)
            ellipse(f"slate_{n}", x, y, rx, rx * 0.42,
                    p["light"] if n % 2 else p["shadow"], 0.01, 0.08, 9)
        stroke("water_channel", [(-0.64, 0.14), (-0.33, 0.08),
               (0.0, 0.13), (0.38, 0.05), (0.62, 0.08)],
               p["accent"], 0.045, 0.025)
        for n in range(4):
            x = rng.uniform(-0.5, 0.5)
            y = rng.uniform(-0.18, 0.18)
            if abs(x) + 2 * abs(y) < 0.75:
                ellipse(f"moss_{n}", x, y, 0.045, 0.017,
                        p["speck"], 0.03, n=9)
    else:
        # Broad mauve flagstones with dark grout and a few orange lava cracks.
        stroke("mortar_a", [(-0.77, -0.02), (-0.34, 0.06),
               (0.04, -0.05), (0.37, 0.02), (0.72, -0.03)],
               p["crack"], 0.022, 0.015)
        stroke("mortar_b", [(-0.21, 0.27), (-0.30, 0.07),
               (-0.10, -0.15), (-0.15, -0.31)], p["crack"], 0.018, 0.015)
        polygon("warm_slab", [(-0.12, 0.13), (0.22, 0.20),
                (0.38, 0.04), (0.02, -0.04)], p["light"], 0.02)
        if variant != 0:
            stroke("ember_seam", [(0.18, -0.15), (0.32, -0.11),
                   (0.42, -0.17)], p["accent"], 0.025, 0.03)
        for n in range(5):
            x = rng.uniform(-0.72, 0.72)
            y = rng.uniform(-0.22, 0.22)
            if abs(x) + 2 * abs(y) < 0.75:
                ellipse(f"wear_{n}", x, y, 0.025, 0.008,
                        p["speck"], 0.04, n=8)


def soldier(direction):
    back = direction in ("north", "north-east", "north-west")
    side = direction in ("east", "west")
    sign = -1 if "west" in direction else 1
    ellipse("shadow", 0, -0.91, 0.55, 0.13, "#533E3A", 0.01)
    polygon("cape", [(-0.40, 0.24), (0.36, 0.24), (0.57, -0.57),
            (-0.44, -0.68)], "#9D3D43", 0.02, 0.08)
    polygon("cape_fold", [(-0.20, 0.14), (0.22, 0.09),
            (0.27, -0.53), (0.05, -0.47)], "#CF5B53", 0.025)
    for x in (-0.25, 0.24):
        polygon("boot", [(x - 0.13, -0.51), (x + 0.11, -0.51),
                (x + 0.16, -0.85), (x - 0.15, -0.85)], "#4D4244", 0.04, 0.09)
    polygon("torso", [(-0.38, 0.24), (0.36, 0.24),
            (0.27, -0.54), (-0.30, -0.54)], "#626069", 0.05, 0.10)
    polygon("chestplate", [(-0.26, 0.13), (0.24, 0.13),
            (0.16, -0.34), (-0.17, -0.34)],
            "#8B8A91" if not back else "#55505A", 0.06, 0.06)
    polygon("belt", [(-0.30, -0.36), (0.26, -0.36),
            (0.24, -0.48), (-0.29, -0.48)], "#D0A958", 0.08, 0.05)
    for x in (-0.44, 0.44):
        ellipse("shoulder", x, 0.16, 0.18, 0.18,
                "#B5A371", 0.09, 0.10, 10)
    ellipse("head", 0.01 if not side else 0.08 * sign, 0.56,
            0.31, 0.34, "#D39B81" if not back else "#51464A", 0.12, 0.10)
    polygon("helmet", [(-0.36, 0.73), (-0.12, 0.95), (0.22, 0.91),
            (0.37, 0.70), (0.30, 0.48), (-0.29, 0.49)],
            "#454449", 0.14, 0.09)
    polygon("crest", [(-0.12, 0.96), (0.01, 1.11), (0.26, 0.98),
            (0.12, 0.90)], "#C94A45", 0.16, 0.07)
    if not back:
        eye_shift = 0.15 * sign if side else 0
        ellipse("eye", eye_shift, 0.58, 0.065, 0.035,
                "#F5D788", 0.20, 0.05, 8)
    # Spear echoes the narrow black shafts and warm metal in the reference.
    shaft_x = 0.62 * sign
    stroke("spear_shaft", [(shaft_x, -0.54), (shaft_x, 0.81)], OUTLINE, 0.10, 0.22)
    stroke("spear_wood", [(shaft_x, -0.54), (shaft_x, 0.81)], "#8C5A3F", 0.052, 0.23)
    polygon("spear_tip", [(shaft_x - 0.12, 0.78), (shaft_x, 1.16),
            (shaft_x + 0.12, 0.78)], "#DDC47A", 0.24, 0.12)


def basilisk(direction):
    sign = -1 if "west" in direction else 1
    back = direction in ("north", "north-east", "north-west")
    ellipse("shadow", 0, -0.80, 0.75, 0.15, "#2C4E50", 0.01)
    polygon("tail", [(-0.48 * sign, -0.39), (-0.96 * sign, -0.18),
            (-0.82 * sign, -0.55), (-0.48 * sign, -0.61)],
            "#286D6C", 0.03, 0.10)
    ellipse("body", 0.02, -0.29, 0.66, 0.46, "#4C9D82", 0.05, 0.10, 16)
    polygon("belly", [(-0.41, -0.37), (0.43, -0.36),
            (0.30, -0.65), (-0.28, -0.64)], "#B7BB78", 0.07, 0.05)
    for x in (-0.38, 0.35):
        ellipse("claw", x, -0.69, 0.18, 0.10, "#356B62", 0.09, 0.08, 9)
    for i, x in enumerate((-0.42, -0.16, 0.12, 0.39)):
        polygon(f"spine_{i}", [(x - 0.13, 0.06), (x, 0.38 + 0.05 * (i % 2)),
                (x + 0.15, 0.04)], "#A5C96E", 0.10, 0.08)
    ellipse("head", 0.37 * sign, 0.17, 0.39, 0.34,
            "#51AB8A", 0.13, 0.11, 14)
    polygon("snout", [(0.34 * sign, 0.08), (0.83 * sign, 0.06),
            (0.80 * sign, -0.19), (0.36 * sign, -0.16)],
            "#76B995", 0.16, 0.07)
    for x in (0.15, 0.50):
        polygon("horn", [((x - 0.09) * sign, 0.39), (x * sign, 0.70),
                ((x + 0.14) * sign, 0.35)], "#D6B96B", 0.18, 0.09)
    if not back:
        ellipse("eye_socket", 0.49 * sign, 0.24, 0.13, 0.13,
                "#302B39", 0.21, n=12)
        ellipse("eye", 0.52 * sign, 0.25, 0.075, 0.075,
                "#E4527F", 0.22, n=12)
        ellipse("eye_glint", 0.54 * sign, 0.28, 0.025, 0.025,
                "#F8E7C7", 0.23, n=8)
    polygon("jaw_shadow", [(0.47 * sign, -0.11), (0.82 * sign, -0.19),
            (0.65 * sign, -0.27)], "#2F6B68", 0.20)


def guardian(direction):
    back = direction in ("north", "north-east", "north-west")
    ellipse("shadow", 0, -0.88, 0.70, 0.15, "#41383A", 0.01)
    for x in (-0.30, 0.28):
        polygon("leg", [(x - 0.18, -0.41), (x + 0.18, -0.38),
                (x + 0.19, -0.84), (x - 0.20, -0.84)],
                "#65585C", 0.04, 0.10)
    for x in (-0.60, 0.60):
        polygon("arm", [(x - 0.18, 0.25), (x + 0.16, 0.20),
                (x + 0.23, -0.49), (x - 0.18, -0.55)],
                "#5F5558", 0.05, 0.10)
        stroke("arm_ember", [(x, 0.05), (x + 0.05, -0.31)],
               "#F16E34", 0.045, 0.08)
    polygon("body", [(-0.42, 0.46), (0.38, 0.44), (0.50, -0.39),
            (0.19, -0.59), (-0.34, -0.51), (-0.53, -0.21)],
            "#756569", 0.09, 0.10)
    polygon("plate", [(-0.30, 0.27), (0.22, 0.33), (0.39, -0.10),
            (0.02, -0.32), (-0.35, -0.22)],
            "#9D8882" if not back else "#605457", 0.11, 0.04)
    stroke("chest_crack", [(-0.21, 0.10), (-0.03, -0.06),
           (0.04, 0.02), (0.26, -0.22)], "#FF7A35", 0.07, 0.13)
    polygon("head", [(-0.31, 0.42), (-0.18, 0.81), (0.18, 0.88),
            (0.40, 0.63), (0.25, 0.32), (-0.21, 0.28)],
            "#62575A", 0.15, 0.10)
    for x in (-0.27, 0.26):
        polygon("horn", [(x - 0.10, 0.75), (x, 1.09),
                (x + 0.13, 0.72)], "#493F43", 0.17, 0.08)
    if not back:
        for x in (-0.13, 0.16):
            polygon("eye", [(x - 0.10, 0.58), (x + 0.11, 0.59),
                    (x + 0.05, 0.48)], "#FFAD49", 0.21, 0.08)
    stroke("brow_crack", [(-0.18, 0.70), (0.02, 0.77),
           (0.22, 0.68)], "#F67635", 0.035, 0.20)


def prop(biome):
    ellipse("ground_shadow", 0, -0.87, 0.68, 0.13, "#3C3030", 0.01)
    if biome == "legion_hold":
        polygon("monolith", [(-0.34, -0.70), (-0.48, -0.28),
                (-0.30, 0.77), (0.13, 1.02), (0.42, 0.52),
                (0.35, -0.45), (0.13, -0.75)], "#90705E", 0.04, 0.10)
        polygon("sun_facet", [(-0.29, 0.70), (0.11, 0.93),
                (0.25, 0.43), (-0.08, -0.48), (-0.36, -0.28)],
                "#BB9781", 0.06)
        stroke("stone_split", [(0.17, 0.78), (0.02, 0.36),
               (0.19, -0.10), (0.03, -0.42)], "#624C45", 0.05, 0.08)
        ellipse("base_grit", 0.32, -0.77, 0.12, 0.05, "#835140", 0.09)
    elif biome == "marsh_maze":
        polygon("pedestal", [(-0.36, -0.72), (-0.31, 0.04),
                (0.30, 0.04), (0.38, -0.72)], "#545A62", 0.04, 0.10)
        ellipse("pedestal_ring", 0, -0.64, 0.45, 0.16,
                "#747982", 0.06, 0.09, 12)
        ellipse("bowl_outer", 0, 0.12, 0.56, 0.35,
                "#4E5861", 0.09, 0.10, 14)
        ellipse("blue_glow", 0, 0.27, 0.44, 0.23,
                "#18A7D1", 0.12, 0.10, 14)
        ellipse("glow_core", -0.08, 0.31, 0.30, 0.14,
                "#4AE2ED", 0.14, n=12)
        ellipse("spark", 0.24, 0.42, 0.05, 0.025,
                "#D7FFFF", 0.16, n=8)
    else:
        for x in (-0.30, 0.30):
            polygon("brazier_leg", [(x - 0.10, -0.24), (x + 0.10, -0.24),
                    (x + 0.22, -0.80), (x - 0.15, -0.80)],
                    "#514548", 0.03, 0.10)
        polygon("fire_bowl", [(-0.61, 0.10), (0.61, 0.10),
                (0.43, -0.33), (-0.43, -0.33)],
                "#746367", 0.08, 0.08)
        polygon("coals", [(-0.45, 0.10), (0.46, 0.10),
                (0.29, -0.04), (-0.30, -0.04)],
                "#F16A32", 0.10, 0.06)
        polygon("flame_outer", [(-0.36, 0.10), (-0.24, 0.43),
                (-0.10, 0.26), (0.01, 0.86), (0.19, 0.43),
                (0.31, 0.70), (0.39, 0.12)],
                "#E94D26", 0.12, 0.09)
        polygon("flame_inner", [(-0.18, 0.10), (-0.11, 0.35),
                (0.02, 0.62), (0.11, 0.29), (0.25, 0.10)],
                "#FFC35C", 0.14)


def write_meta(path):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return
    import uuid
    meta.write_text(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n",
                    encoding="utf-8", newline="\n")


def write_folder_meta(folder):
    folder.mkdir(parents=True, exist_ok=True)
    meta = Path(str(folder) + ".meta")
    if meta.exists():
        return
    import uuid
    meta.write_text(
        f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\n"
        "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
        "  assetBundleName: \n  assetBundleVariant: \n",
        encoding="utf-8", newline="\n",
    )


def main():
    clear_objects()
    world = bpy.context.scene.world
    if world is None:
        world = bpy.data.worlds.new("Transparent world")
        bpy.context.scene.world = world
    world.color = (0, 0, 0)
    for biome in PALETTES:
        tile_dir = ART / "Tiles" / "ReferenceStyle" / biome
        for folder in (ART / "Tiles" / "ReferenceStyle", tile_dir):
            write_folder_meta(folder)
        for variant in range(3):
            clear_objects()
            tile(biome, variant)
            dest = tile_dir / f"{biome}_floor_{variant:02d}.png"
            render(dest, 64, 32, 2.16)
            write_meta(dest)
            if variant == 0:
                SOURCE_DIR.mkdir(parents=True, exist_ok=True)
                bpy.ops.wm.save_as_mainfile(
                    filepath=str(SOURCE_DIR / f"{biome}_tile.blend"))
            print("RENDERED", dest)
    actor_root = ART / "Actors" / "ReferenceStyle"
    write_folder_meta(actor_root)
    enemy_types = {
        "LegionSentinel": soldier,
        "MarshBasilisk": basilisk,
        "InfernoGuardian": guardian,
    }
    for enemy, draw in enemy_types.items():
        rotations = actor_root / enemy / "Idle" / "rotations"
        for folder in (actor_root / enemy, actor_root / enemy / "Idle", rotations):
            write_folder_meta(folder)
        for direction in DIRECTIONS:
            clear_objects()
            draw(direction)
            squeeze = 0.78 if direction in ("east", "west") else (
                0.90 if "-" in direction else 1.0)
            for obj in bpy.context.scene.objects:
                if obj.type == "MESH":
                    obj.scale.x *= squeeze
            dest = rotations / f"{direction}.png"
            render(dest, 96, 96, 2.55)
            write_meta(dest)
            if direction == "south":
                bpy.ops.wm.save_as_mainfile(
                    filepath=str(SOURCE_DIR / f"{enemy}.blend"))
            print("RENDERED", dest)
    prop_root = ART / "Props" / "ReferenceStyle"
    write_folder_meta(prop_root)
    for biome in PALETTES:
        clear_objects()
        prop(biome)
        dest = prop_root / f"{biome}_landmark.png"
        render(dest, 96, 96, 2.55)
        write_meta(dest)
        bpy.ops.wm.save_as_mainfile(
            filepath=str(SOURCE_DIR / f"{biome}_landmark.blend"))
        print("RENDERED", dest)
    print("SOURCES", SOURCE_DIR)


if __name__ == "__main__":
    main()
