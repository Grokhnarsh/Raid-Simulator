using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>
    /// A runnable dungeon: which biome dresses it, how big the layout is, and how
    /// hard it gets on the way in.
    ///
    /// One asset is one dungeon. "Add a second dungeon" means duplicating this,
    /// pointing it at another biome, and adding it to the run selection — the
    /// generator does not care which one it is handed.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Dungeon", fileName = "DG_NewDungeon", order = 0)]
    public sealed class DungeonDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "New Dungeon";

        [TextArea(2, 5)]
        public string Description;

        public BiomeDefinition Biome;

        [Header("Party")]
        [Tooltip("Slots in the party. Five is the tuned value; the systems handle 1-8.")]
        [Range(1, 8)] public int PartySize = 5;

        [Header("Layout")]
        [Tooltip("Bounds of the grid the dungeon is carved into, in tiles.")]
        public Vector2Int MapSize = new Vector2Int(72, 72);

        [Range(4, 24)] public int MinRooms = 7;
        [Range(4, 24)] public int MaxRooms = 10;

        public Vector2Int RoomSizeMin = new Vector2Int(8, 8);
        public Vector2Int RoomSizeMax = new Vector2Int(14, 14);

        [Tooltip("Tiles of empty space kept between rooms so corridors have somewhere to run.")]
        [Range(2, 8)] public int RoomSpacing = 3;

        [Range(1, 3)] public int CorridorWidth = 2;

        [Tooltip("Extra connections beyond the spanning tree, as a fraction of the room count. " +
                 "0 gives a pure tree (every room a dead end); 0.3 gives loops that make " +
                 "kiting and retreating viable.")]
        [Range(0f, 1f)] public float ExtraConnectionRatio = 0.25f;

        [Header("Population")]
        [Tooltip("Rooms containing an elite pack, in addition to the boss room.")]
        [Range(0, 6)] public int EliteRooms = 2;

        public bool HasTreasureRoom = true;

        [Tooltip("Threat budget spent per trash room at depth 0. Scaled by the curve below.")]
        [Min(1)] public int BaseRoomBudget = 20;

        [Tooltip("Multiplies the room budget by how deep the room sits in the dungeon graph " +
                 "(0 = entrance, 1 = boss door). Keep the left end near 1.")]
        public AnimationCurve DifficultyByDepth = AnimationCurve.Linear(0f, 1f, 1f, 2f);

        [Header("Seed")]
        [Tooltip("When true a fresh seed is drawn per run. Turn off to iterate on one layout.")]
        public bool UseRandomSeed = true;

        public int FixedSeed = 1337;

        /// <summary>Clamps authored values into a layout the generator can actually satisfy.</summary>
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = name;

            if (MaxRooms < MinRooms) MaxRooms = MinRooms;

            RoomSizeMin = new Vector2Int(Mathf.Max(4, RoomSizeMin.x), Mathf.Max(4, RoomSizeMin.y));
            RoomSizeMax = new Vector2Int(
                Mathf.Max(RoomSizeMin.x, RoomSizeMax.x),
                Mathf.Max(RoomSizeMin.y, RoomSizeMax.y));

            // A map that cannot hold the requested rooms produces a generator that
            // silently gives up half way. Catch it here instead.
            int worstCase = Mathf.CeilToInt(Mathf.Sqrt(MaxRooms)) * (RoomSizeMax.x + RoomSpacing + 2);
            MapSize = new Vector2Int(
                Mathf.Max(MapSize.x, worstCase),
                Mathf.Max(MapSize.y, worstCase));

            if (EliteRooms > MaxRooms - 2) EliteRooms = Mathf.Max(0, MaxRooms - 2);
        }
    }
}
