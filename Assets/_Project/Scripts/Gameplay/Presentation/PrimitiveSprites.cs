using EmberDepths.Core.Grid;
using UnityEngine;

namespace EmberDepths.Gameplay.Presentation
{
    /// <summary>
    /// Procedurally generated sprites for things that must be drawn on the grid
    /// regardless of what art exists: hazard decals, telegraph warnings, selection
    /// rings, shadows.
    ///
    /// Generating these rather than shipping PNGs is deliberate. They have to line
    /// up with <see cref="IsoGrid"/> to the pixel, and a hand-drawn diamond
    /// silently stops matching the moment someone changes the tile size. These
    /// cannot drift.
    /// </summary>
    public static class PrimitiveSprites
    {
        private static Sprite _cellDiamond;
        private static Sprite _cellDiamondOutline;
        private static Sprite _shadow;
        private static Sprite _white;

        private const int W = IsoGrid.TileWidthPx;
        private const int H = IsoGrid.TileHeightPx;

        /// <summary>A filled tile-shaped diamond, white. Tint it per use.</summary>
        public static Sprite CellDiamond => _cellDiamond != null
            ? _cellDiamond
            : _cellDiamond = BuildDiamond(filled: true, "ED_CellDiamond");

        /// <summary>Hollow diamond, two pixels thick. The telegraph border.</summary>
        public static Sprite CellDiamondOutline => _cellDiamondOutline != null
            ? _cellDiamondOutline
            : _cellDiamondOutline = BuildDiamond(filled: false, "ED_CellDiamondOutline");

        /// <summary>Soft elliptical blob for under-actor shadows.</summary>
        public static Sprite Shadow => _shadow != null ? _shadow : _shadow = BuildShadow();

        /// <summary>Single white pixel, for bars and flat fills.</summary>
        public static Sprite White => _white != null ? _white : _white = BuildWhite();

        private static Sprite BuildDiamond(bool filled, string name)
        {
            var tex = NewTexture(W, H, name);
            var pixels = new Color32[W * H];

            float halfW = W * 0.5f;
            float halfH = H * 0.5f;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    // |dx|/halfW + |dy|/halfH <= 1 is the diamond the projection draws.
                    float dx = Mathf.Abs(x + 0.5f - halfW) / halfW;
                    float dy = Mathf.Abs(y + 0.5f - halfH) / halfH;
                    float d = dx + dy;

                    bool inside = d <= 1f;
                    bool onEdge = inside && d > 1f - 2f / halfW;

                    bool paint = filled ? inside : onEdge;
                    pixels[y * W + x] = paint ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            return Sprite.Create(
                tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f),
                IsoGrid.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildShadow()
        {
            const int w = 32, h = 16;
            var tex = NewTexture(w, h, "ED_Shadow");
            var pixels = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f);
                    float dy = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // Quadratic falloff reads as a soft contact shadow without
                    // needing a blur pass or a second texture.
                    float a = Mathf.Clamp01(1f - r);
                    pixels[y * w + x] = new Color32(0, 0, 0, (byte)(a * a * 140f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                IsoGrid.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildWhite()
        {
            var tex = NewTexture(1, 1, "ED_White");
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        }

        private static Texture2D NewTexture(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                // These are created per session and never serialised; hiding them
                // keeps them out of the scene's dirty state.
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }
}
