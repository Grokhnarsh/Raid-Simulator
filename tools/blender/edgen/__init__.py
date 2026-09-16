"""EmberDepths generator — Blender-side asset pipeline.

Turns procedurally built 3D props into isometric pixel-art sprites that line up
exactly with the game's grid.

Why 3D at all, when the characters are generated as 2D pixel art? Because props
need something 2D generation is bad at: eight rotations of the *same object*
with consistent lighting and a footprint that matches the tile grid to the
pixel. A pillar drawn eight times drifts. A pillar modelled once and rendered
eight times cannot.

Pipeline:

    props.build(name)      -> a mesh in the scene
    isocam.setup(...)      -> a 2:1 isometric orthographic camera
    render.render_prop()   -> N rotations to PNG
    pixelize.quantise()    -> snap to the game palette, kill anti-aliasing

Run it with Blender's bundled Python, never a system one:

    blender --background --factory-startup --python tools/blender/build_props.py

See docs/art-pipeline.md for the whole story.
"""

from __future__ import annotations

__all__ = ["palette", "isocam", "mats", "props", "pixelize", "render"]

#: Bumped when output would change for the same inputs, so a stale render is obvious.
GENERATOR_VERSION = "1.0.0"
