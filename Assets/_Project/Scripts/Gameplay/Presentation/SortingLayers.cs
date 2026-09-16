namespace EmberDepths.Gameplay.Presentation
{
    /// <summary>
    /// Sorting layer names, in draw order.
    ///
    /// Isometric depth *within* the Entities layer is handled by the camera's
    /// custom transparency sort axis (0,1,0) — an actor further up the screen is
    /// further away and draws behind. These layers exist for the things that must
    /// ignore that rule: a floor decal always under everyone, a shadow always
    /// under its owner, an overhead bar always on top.
    ///
    /// <c>EmberDepths/Setup/Configure Project</c> creates these in the project's
    /// tag manager; it is safe to run repeatedly.
    /// </summary>
    public static class SortingLayers
    {
        public const string Background = "Background";
        public const string Ground = "Ground";
        public const string GroundDecal = "GroundDecal";
        public const string Shadows = "Shadows";

        /// <summary>Actors, props and walls. Y-sorted against each other.</summary>
        public const string Entities = "Entities";

        public const string Overhead = "Overhead";
        public const string Vfx = "VFX";
        public const string WorldUi = "WorldUI";

        /// <summary>Ordered lowest to highest. Used by the project setup tool.</summary>
        public static readonly string[] InDrawOrder =
        {
            Background, Ground, GroundDecal, Shadows, Entities, Overhead, Vfx, WorldUi
        };
    }
}
