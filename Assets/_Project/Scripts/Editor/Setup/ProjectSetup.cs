using EmberDepths.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

namespace EmberDepths.EditorTools
{
    /// <summary>
    /// Applies the project settings the isometric renderer depends on.
    ///
    /// These live in code rather than in a committed ProjectSettings asset for
    /// one reason: they must stay in agreement with <c>IsoGrid</c> and
    /// <c>SortingLayers</c>, and a YAML file cannot be kept in agreement with
    /// anything. Running this is idempotent, so it is safe to invoke from the
    /// menu at any time — and it runs once automatically on a fresh checkout,
    /// where the sorting layers would otherwise be missing and every sprite
    /// would land on Default.
    /// </summary>
    public static class ProjectSetup
    {
        private const string SetupVersionKey = "EmberDepths.SetupVersion";
        private const int CurrentSetupVersion = 1;

        [MenuItem("EmberDepths/Setup/Configure Project", priority = 0)]
        public static void ConfigureProject()
        {
            int layers = EnsureSortingLayers();
            EnsurePhysics2DSettings();
            ApplyEditorPreferences();

            AssetDatabase.SaveAssets();
            EditorPrefs.SetInt(SetupVersionKey, CurrentSetupVersion);

            Debug.Log($"[EmberDepths] Project configured. Sorting layers added: {layers}. " +
                      "Next: EmberDepths/Content/Build Volcano Dungeon, then EmberDepths/Setup/Build Playable Scene.");
        }

        /// <summary>Runs once per machine on a fresh checkout so the project opens usable.</summary>
        [InitializeOnLoadMethod]
        private static void AutoConfigureOnce()
        {
            if (EditorPrefs.GetInt(SetupVersionKey, 0) >= CurrentSetupVersion) return;

            // Deferred: the asset database is not ready during InitializeOnLoad.
            EditorApplication.delayCall += ConfigureProject;
        }

        /// <summary>
        /// Adds any missing sorting layer, preserving the ones already present and
        /// their order. Returns how many were created.
        /// </summary>
        private static int EnsureSortingLayers()
        {
            Object tagManagerAsset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAsset == null)
            {
                Debug.LogWarning("[EmberDepths] Could not open TagManager.asset; sorting layers not configured.");
                return 0;
            }

            var tagManager = new SerializedObject(tagManagerAsset);
            SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");
            if (layers == null) return 0;

            int added = 0;

            foreach (string wanted in SortingLayers.InDrawOrder)
            {
                if (HasSortingLayer(layers, wanted)) continue;

                layers.InsertArrayElementAtIndex(layers.arraySize);
                SerializedProperty entry = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                entry.FindPropertyRelative("name").stringValue = wanted;

                // Unity keys sorting layers by a persistent id, not by name. Any
                // stable non-zero value works as long as it is unique.
                entry.FindPropertyRelative("uniqueID").intValue = Animator.StringToHash(wanted);
                entry.FindPropertyRelative("locked").boolValue = false;
                added++;
            }

            if (added > 0) tagManager.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }

        private static bool HasSortingLayer(SerializedProperty layers, string name)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty nameProp = layers.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                if (nameProp != null && nameProp.stringValue == name) return true;
            }
            return false;
        }

        /// <summary>
        /// The simulation resolves collision on the grid, not with colliders, so
        /// 2D physics only needs to stay out of the way.
        /// </summary>
        private static void EnsurePhysics2DSettings()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.queriesStartInColliders = false;
        }

        private static void ApplyEditorPreferences()
        {
            // Force-text serialisation keeps scenes and assets reviewable in a diff.
            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        [MenuItem("EmberDepths/Setup/Print Grid Constants", priority = 20)]
        public static void PrintGridConstants()
        {
            Debug.Log(
                $"[EmberDepths] Isometric grid\n" +
                $"  tile         : {EmberDepths.Core.Grid.IsoGrid.TileWidthPx} x {EmberDepths.Core.Grid.IsoGrid.TileHeightPx} px\n" +
                $"  pixels/unit  : {EmberDepths.Core.Grid.IsoGrid.PixelsPerUnit}\n" +
                $"  unity cell   : {EmberDepths.Core.Grid.IsoGrid.UnityCellSize}\n" +
                $"  tile pivot   : (0.5, 0.75)  <- top-face centre for a 64x64 block sprite\n" +
                $"  sim rate     : {EmberDepths.Core.Sim.SimClock.TicksPerSecond} Hz");
        }
    }
}
