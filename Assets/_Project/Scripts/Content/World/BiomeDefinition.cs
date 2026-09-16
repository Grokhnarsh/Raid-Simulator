using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EmberDepths.Content
{
    [Serializable]
    public struct WeightedTile
    {
        public TileBase Tile;

        [Tooltip("Relative frequency. Make the plain variant heavy and the decorated ones light, " +
                 "or the floor turns into visual noise.")]
        [Min(0f)] public float Weight;
    }

    [Serializable]
    public struct PropSpawnRule
    {
        public GameObject Prefab;

        [Range(0f, 1f)]
        [Tooltip("Chance per eligible cell.")]
        public float Density;

        [Tooltip("Only spawn against a wall. Braziers and banners want this.")]
        public bool RequiresWallAdjacency;

        [Tooltip("Blocks movement once placed.")]
        public bool Blocking;
    }

    /// <summary>
    /// Everything that makes a floor look and feel like one place: its tiles, its
    /// ambient hazard, its enemy pool, its boss.
    ///
    /// This asset is the extension point the whole project is shaped around.
    /// Shipping a second biome — a frozen crypt, a fungal deep — is a new
    /// BiomeDefinition plus its content assets, with no changes to the generator,
    /// the combat system or the AI. The volcano is simply the first one.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Biome", fileName = "BI_NewBiome", order = 1)]
    public sealed class BiomeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "New Biome";

        [TextArea(2, 4)]
        public string Flavour;

        [Header("Tiles")]
        [Tooltip("Weighted pool used for ordinary room and corridor floor.")]
        public List<WeightedTile> FloorTiles = new List<WeightedTile>();

        public List<WeightedTile> WallTiles = new List<WeightedTile>();

        [Tooltip("Used for cells marked as the biome's liquid terrain.")]
        public TileBase LiquidTile;

        [Tooltip("Floor used inside boss arenas, if it should differ.")]
        public TileBase BossFloorTile;

        [Header("Atmosphere")]
        public Color AmbientTint = new Color(1f, 0.86f, 0.78f);

        [Tooltip("Colour of the glow cast up from the liquid. Drives the pulsing floor light.")]
        public Color LiquidGlow = new Color(1f, 0.55f, 0.15f);

        public AudioClip Ambience;

        [Header("Terrain hazard")]
        [Tooltip("Applied to anyone standing on a liquid cell. For the volcano this is standing lava.")]
        public HazardDefinition LiquidHazard;

        [Range(0f, 0.4f)]
        [Tooltip("Fraction of room floor replaced with liquid. Above about 0.25 rooms stop being " +
                 "navigable for melee.")]
        public float LiquidCoverage = 0.08f;

        [Tooltip("The debuff this biome is built around. The volcano's answer to it is fire resistance.")]
        public StatusEffectDefinition SignatureDebuff;

        [Header("Population")]
        public List<EncounterDefinition> TrashEncounters = new List<EncounterDefinition>();
        public List<EncounterDefinition> EliteEncounters = new List<EncounterDefinition>();

        [Tooltip("Placed in the final room. Its EnemyDefinition must have Rank = Boss.")]
        public EnemyDefinition Boss;

        [Header("Decoration")]
        public List<PropSpawnRule> Props = new List<PropSpawnRule>();

        /// <summary>Weights array for the floor pool, cached shape for the generator.</summary>
        public void CollectFloorWeights(List<float> outWeights)
        {
            outWeights.Clear();
            for (int i = 0; i < FloorTiles.Count; i++)
                outWeights.Add(Mathf.Max(0f, FloorTiles[i].Weight));
        }

        public void CollectWallWeights(List<float> outWeights)
        {
            outWeights.Clear();
            for (int i = 0; i < WallTiles.Count; i++)
                outWeights.Add(Mathf.Max(0f, WallTiles[i].Weight));
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = name;
            NormaliseWeights(FloorTiles);
            NormaliseWeights(WallTiles);
        }

        private static void NormaliseWeights(List<WeightedTile> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Weight > 0f) continue;
                WeightedTile w = list[i];
                w.Weight = 1f;
                list[i] = w;
            }
        }
    }
}
