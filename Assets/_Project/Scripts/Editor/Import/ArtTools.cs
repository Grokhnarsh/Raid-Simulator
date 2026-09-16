using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EmberDepths.EditorTools
{
    /// <summary>
    /// Turns raw generated PNGs into the assets the game actually references:
    /// correct pivots, <see cref="Tile"/> assets for the tilemap, and
    /// <see cref="ActorVisualSet"/> assets for actors.
    ///
    /// The whole art pipeline is one direction:
    ///
    ///     PixelLab / Blender  ->  tools/*/fetch  ->  Art/*.png  ->  these tools  ->  assets
    ///
    /// Nothing here edits the PNGs. Re-running any of these is safe and produces
    /// the same result, which is what lets the raw art be regenerated freely.
    /// </summary>
    public static class ArtTools
    {
        private const string ArtRoot = "Assets/_Project/Art";
        private const string TilesFolder = ArtRoot + "/Tiles";
        private const string ActorsFolder = ArtRoot + "/Actors";
        private const string TileAssetFolder = "Assets/_Project/Content/Tiles";
        private const string VisualSetFolder = "Assets/_Project/Content/Visuals";

        // --- pivots -----------------------------------------------------------------

        [MenuItem("EmberDepths/Art/Fix Pivots", priority = 40)]
        public static void FixPivots()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
            int changed = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("EmberDepths", $"Pivoting {Path.GetFileName(path)}", i / (float)guids.Length);
                    if (ApplyPivot(path)) changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[EmberDepths] Pivots updated on {changed} of {guids.Length} sprites.");
        }

        private static bool ApplyPivot(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer) return false;
            if (!TryReadPng(assetPath, out Texture2D texture)) return false;

            try
            {
                if (!TryGetOpaqueBounds(texture, out RectInt bounds)) return false;

                Vector2 pivot = assetPath.StartsWith(TilesFolder)
                    ? TilePivot(texture, bounds)
                    : ActorPivot(texture, bounds);

                if ((importer.spritePivot - pivot).sqrMagnitude < 1e-6f &&
                    importer.spriteImportMode == SpriteImportMode.Single) return false;

                // spritePivot only takes effect with a Custom alignment.
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// For a tile the pivot is the centre of the top face, so that a tile
        /// dropped on cell (x,y) covers exactly that cell and its block body
        /// hangs down over the cell in front.
        /// </summary>
        private static Vector2 TilePivot(Texture2D texture, RectInt bounds)
        {
            // Measured from the CANVAS, not from the opaque content. The generator
            // emits tiles whose top diamond apex is at the top of the canvas, but
            // a tile whose topmost row happens to be fully transparent would
            // otherwise get a pivot one pixel lower than its neighbours — and
            // tiles that disagree by a pixel do not tessellate.
            float centreFromBottom = texture.height - IsoGrid.TileHeightPx * 0.5f;
            return new Vector2(0.5f, Mathf.Clamp01(centreFromBottom / texture.height));
        }

        /// <summary>
        /// For an actor the pivot is the bottom centre of the artwork — the feet.
        /// Placing that on the cell centre is what makes a character look like it
        /// is standing on the tile rather than hovering over it.
        /// </summary>
        private static Vector2 ActorPivot(Texture2D texture, RectInt bounds)
        {
            // Rounded to whole pixels. A pivot on a half-pixel offsets the whole
            // sprite by half a texel, which at 32 pixels per unit is a visible
            // shimmer as the actor walks.
            float x = Mathf.Round((bounds.xMin + bounds.xMax) * 0.5f) / texture.width;
            float y = bounds.yMin / (float)texture.height;
            return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
        }

        /// <summary>
        /// Loads a PNG straight off disk instead of using the imported asset, so
        /// the texture does not have to be marked readable just to be measured.
        /// </summary>
        private static bool TryReadPng(string assetPath, out Texture2D texture)
        {
            texture = null;
            string full = Path.GetFullPath(assetPath);
            if (!File.Exists(full)) return false;

            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(File.ReadAllBytes(full))) return true;

            UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
            return false;
        }

        private static bool TryGetOpaqueBounds(Texture2D texture, out RectInt bounds)
        {
            Color32[] pixels = texture.GetPixels32();
            int w = texture.width, h = texture.height;
            int minX = w, minY = h, maxX = -1, maxY = -1;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a <= 8) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0)
            {
                bounds = default;
                return false;
            }

            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        // --- tiles ---------------------------------------------------------------------

        [MenuItem("EmberDepths/Art/Build Tile Assets", priority = 41)]
        public static void BuildTileAssets()
        {
            EnsureFolder(TileAssetFolder);

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { TilesFolder });
            int built = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                string tilePath = $"{TileAssetFolder}/T_{Path.GetFileNameWithoutExtension(path)}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);

                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }

                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                built++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[EmberDepths] Built {built} tile assets in {TileAssetFolder}.");
        }

        // --- visual sets ------------------------------------------------------------------

        /// <summary>
        /// Builds one <see cref="ActorVisualSet"/> per actor folder.
        ///
        /// Expects the layout PixelLab's character download produces:
        /// <c>&lt;Actor&gt;/&lt;State&gt;/rotations/&lt;direction&gt;.png</c> and
        /// <c>&lt;Actor&gt;/&lt;State&gt;/animations/&lt;name&gt;/&lt;direction&gt;/frame_NNN.png</c>.
        /// Existing assets are updated in place so references from enemy and class
        /// definitions survive a re-import.
        /// </summary>
        [MenuItem("EmberDepths/Art/Rebuild Visual Sets", priority = 42)]
        public static void RebuildVisualSets()
        {
            if (!Directory.Exists(Path.GetFullPath(ActorsFolder)))
            {
                Debug.LogWarning($"[EmberDepths] {ActorsFolder} does not exist. " +
                                 "Run tools/pixellab/fetch_assets.ps1 first.");
                return;
            }

            EnsureFolder(VisualSetFolder);

            int built = 0;
            foreach (string rotationsDir in Directory.GetDirectories(
                         Path.GetFullPath(ActorsFolder), "rotations", SearchOption.AllDirectories))
            {
                // .../Actors/<Group>/<Actor>/<State>/rotations
                string stateDir = Path.GetDirectoryName(rotationsDir);
                string actorDir = Path.GetDirectoryName(stateDir);
                if (actorDir == null) continue;

                if (BuildVisualSet(actorDir, stateDir, rotationsDir)) built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[EmberDepths] Rebuilt {built} visual sets in {VisualSetFolder}.");
        }

        private static bool BuildVisualSet(string actorDir, string stateDir, string rotationsDir)
        {
            string actorName = new DirectoryInfo(actorDir).Name;
            string assetPath = $"{VisualSetFolder}/VS_{actorName}.asset";

            var set = AssetDatabase.LoadAssetAtPath<ActorVisualSet>(assetPath);
            bool created = set == null;
            if (created)
            {
                set = ScriptableObject.CreateInstance<ActorVisualSet>();
                AssetDatabase.CreateAsset(set, assetPath);
            }

            set.IdleByDirection = new Sprite[8];
            bool any = false;

            for (int d = 0; d < 8; d++)
            {
                var direction = (IsoDirection)d;
                string file = Path.Combine(rotationsDir, direction.ToAssetKey() + ".png");
                Sprite sprite = LoadSprite(file);
                set.IdleByDirection[d] = sprite;
                if (sprite != null) any = true;
            }

            if (!any)
            {
                Debug.LogWarning($"[EmberDepths] {actorName}: no rotation sprites found in {rotationsDir}.");
                if (created) AssetDatabase.DeleteAsset(assetPath);
                return false;
            }

            set.Animations.Clear();

            string animationsRoot = Path.Combine(stateDir, "animations");
            if (Directory.Exists(animationsRoot))
                foreach (string animDir in Directory.GetDirectories(animationsRoot))
                    AppendAnimation(set, animDir);

            // Feet-height offset for the overhead bar, derived from the south
            // sprite so a 128px boss does not wear its health bar at its knees.
            Sprite south = set.IdleByDirection[0];
            if (south != null) set.OverheadOffset = south.rect.height / IsoGrid.PixelsPerUnit * 0.85f;

            EditorUtility.SetDirty(set);
            return true;
        }

        private static void AppendAnimation(ActorVisualSet set, string animDir)
        {
            string animName = new DirectoryInfo(animDir).Name;
            if (!TryMapAnimationName(animName, out ActorAnimState state)) return;

            var animation = new ActorAnimation
            {
                State = state,
                Fps = state == ActorAnimState.Walk ? 12 : 10,
                Loop = state == ActorAnimState.Idle || state == ActorAnimState.Walk,
                ByDirection = ActorAnimation.NewDirectionArray()
            };

            bool any = false;

            for (int d = 0; d < 8; d++)
            {
                var direction = (IsoDirection)d;
                string dirPath = Path.Combine(animDir, direction.ToAssetKey());
                if (!Directory.Exists(dirPath)) continue;

                // Sorted by name so frame_000 .. frame_015 stay in order; a plain
                // directory listing is not guaranteed to be sorted.
                List<Sprite> frames = Directory.GetFiles(dirPath, "*.png")
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .Select(LoadSprite)
                    .Where(s => s != null)
                    .ToList();

                if (frames.Count == 0) continue;

                animation.ByDirection[d].Frames = frames.ToArray();
                any = true;
            }

            if (any) set.Animations.Add(animation);
        }

        /// <summary>
        /// Maps a generator's animation folder name onto the engine's state enum.
        /// Unrecognised names are skipped rather than guessed at, so a new
        /// template shows up as a warning instead of silently becoming an idle.
        /// </summary>
        private static bool TryMapAnimationName(string folderName, out ActorAnimState state)
        {
            string key = folderName.ToLowerInvariant();

            if (key.Contains("walk") || key.Contains("run")) { state = ActorAnimState.Walk; return true; }
            if (key.Contains("idle") || key.Contains("breath")) { state = ActorAnimState.Idle; return true; }
            if (key.Contains("death") || key.Contains("die")) { state = ActorAnimState.Die; return true; }
            if (key.Contains("hurt") || key.Contains("taking")) { state = ActorAnimState.Hurt; return true; }
            if (key.Contains("cast") || key.Contains("fireball") || key.Contains("spell"))
            {
                state = ActorAnimState.Cast;
                return true;
            }
            if (key.Contains("punch") || key.Contains("kick") || key.Contains("attack") ||
                key.Contains("jab") || key.Contains("slash") || key.Contains("throw"))
            {
                state = ActorAnimState.Attack;
                return true;
            }

            Debug.LogWarning($"[EmberDepths] Unmapped animation folder '{folderName}'. " +
                             "Add it to ArtTools.TryMapAnimationName if it should be used.");
            state = ActorAnimState.Idle;
            return false;
        }

        private static Sprite LoadSprite(string absolutePath)
        {
            if (!File.Exists(absolutePath)) return null;
            string assetPath = ToAssetPath(absolutePath);
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(absolutePath)
                .Substring(projectRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
        }

        internal static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            string[] parts = assetFolder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // --- convenience --------------------------------------------------------------------

        [MenuItem("EmberDepths/Art/Import Everything", priority = 39)]
        public static void ImportEverything()
        {
            AssetDatabase.Refresh();
            FixPivots();
            BuildTileAssets();
            RebuildVisualSets();
        }
    }
}
