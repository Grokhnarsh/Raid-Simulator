using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EmberDepths.Gameplay.World
{
    /// <summary>
    /// Turns a generated <see cref="GridMap"/> into visible tilemaps.
    ///
    /// The Unity <c>Grid</c> is configured from <see cref="IsoGrid"/> rather than
    /// from inspector values on purpose: tiles placed by the tilemap and actors
    /// positioned by script must agree to the pixel, and the only way to
    /// guarantee that is for both to read the same constants.
    /// </summary>
    public sealed class DungeonBuilder
    {
        private Tilemap _floor;
        private Tilemap _walls;
        private Tilemap _liquid;

        private readonly List<float> _weights = new List<float>(8);

        public Grid GridComponent { get; private set; }

        public void Build(DungeonWorld world, BiomeDefinition biome, Transform root)
        {
            CreateTilemaps(root);
            Paint(world, biome);
        }

        private void CreateTilemaps(Transform root)
        {
            var gridGo = new GameObject("DungeonGrid");
            gridGo.transform.SetParent(root, false);

            GridComponent = gridGo.AddComponent<Grid>();
            GridComponent.cellLayout = GridLayout.CellLayout.Isometric;
            GridComponent.cellSize = IsoGrid.UnityCellSize;
            GridComponent.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            _floor = CreateLayer(gridGo.transform, "Floor", SortingLayers.Ground, 0);
            _liquid = CreateLayer(gridGo.transform, "Liquid", SortingLayers.Ground, 1);
            _walls = CreateLayer(gridGo.transform, "Walls", SortingLayers.Entities, 0);
        }

        private static Tilemap CreateLayer(Transform parent, string label, string sortingLayer, int order)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);

            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();

            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;

            // Individual mode makes the renderer sort each tile against the scene
            // instead of drawing the whole chunk at one depth. Without it, walls
            // draw either entirely in front of or entirely behind every actor.
            renderer.mode = TilemapRenderer.Mode.Individual;

            return tilemap;
        }

        private void Paint(DungeonWorld world, BiomeDefinition biome)
        {
            GridMap map = world.Map;
            DeterministicRandom rng = world.Rng.Fork("tiles");

            TileBase fallbackFloor = RuntimeTile(PrimitiveSprites.CellDiamond, new Color(0.21f, 0.17f, 0.18f));
            TileBase fallbackWall = RuntimeTile(PrimitiveSprites.CellDiamond, new Color(0.09f, 0.07f, 0.08f));
            TileBase fallbackLiquid = RuntimeTile(PrimitiveSprites.CellDiamond, new Color(0.93f, 0.41f, 0.11f));

            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var cell = new GridCoord(x, y);
                    var pos = new Vector3Int(x, y, 0);

                    switch (map.TerrainAt(cell))
                    {
                        case CellTerrain.Floor:
                        case CellTerrain.Door:
                            _floor.SetTile(pos, PickWeighted(biome?.FloorTiles, rng) ?? fallbackFloor);
                            break;

                        case CellTerrain.Lava:
                            // Liquid still gets a floor underneath so the pool does
                            // not read as a hole when its sprite has soft edges.
                            _floor.SetTile(pos, PickWeighted(biome?.FloorTiles, rng) ?? fallbackFloor);
                            _liquid.SetTile(pos, biome?.LiquidTile ?? fallbackLiquid);
                            break;

                        case CellTerrain.Wall:
                            _walls.SetTile(pos, PickWeighted(biome?.WallTiles, rng) ?? fallbackWall);
                            break;
                    }
                }
            }

            PaintBossFloor(world, biome);
        }

        /// <summary>Swaps the boss arena's floor so the final room reads as somewhere else.</summary>
        private void PaintBossFloor(DungeonWorld world, BiomeDefinition biome)
        {
            if (biome == null || biome.BossFloorTile == null) return;

            Room boss = world.Layout?.BossRoom;
            if (boss == null) return;

            RectInt interior = boss.Interior;
            for (int x = interior.xMin; x < interior.xMax; x++)
            {
                for (int y = interior.yMin; y < interior.yMax; y++)
                {
                    if (world.Map.TerrainAt(new GridCoord(x, y)) != CellTerrain.Floor) continue;
                    _floor.SetTile(new Vector3Int(x, y, 0), biome.BossFloorTile);
                }
            }
        }

        private TileBase PickWeighted(List<WeightedTile> pool, DeterministicRandom rng)
        {
            if (pool == null || pool.Count == 0) return null;

            _weights.Clear();
            for (int i = 0; i < pool.Count; i++)
                _weights.Add(pool[i].Tile != null ? Mathf.Max(0f, pool[i].Weight) : 0f);

            int index = rng.PickWeighted(_weights);
            return index >= 0 ? pool[index].Tile : null;
        }

        /// <summary>
        /// A tile built at runtime from a generated sprite. Lets the dungeon render
        /// before any art exists, which keeps the systems testable on their own.
        /// </summary>
        private static TileBase RuntimeTile(Sprite sprite, Color tint)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = tint;
            tile.hideFlags = HideFlags.HideAndDontSave;
            return tile;
        }

        /// <summary>Repaints a single cell. Used when terrain changes mid-run.</summary>
        public void RefreshCell(DungeonWorld world, BiomeDefinition biome, GridCoord cell)
        {
            var pos = new Vector3Int(cell.X, cell.Y, 0);
            CellTerrain terrain = world.Map.TerrainAt(cell);

            _liquid.SetTile(pos, terrain == CellTerrain.Lava ? biome?.LiquidTile : null);
            _walls.SetTile(pos, terrain == CellTerrain.Wall ? PickWeighted(biome?.WallTiles, world.Rng) : null);
        }
    }
}
