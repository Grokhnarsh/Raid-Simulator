using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Gameplay.Party;
using EmberDepths.Gameplay.Presentation;
using EmberDepths.Gameplay.Run;
using EmberDepths.Gameplay.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace EmberDepths.EditorTools
{
    /// <summary>
    /// Builds the playable scene from scratch.
    ///
    /// The scene holds no authored content — it is four components wired
    /// together, and every one of their settings is either a default or comes
    /// from a ScriptableObject. Regenerating it is therefore always safe, and a
    /// merge conflict in the .unity file is never worth resolving by hand: just
    /// run this again.
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";
        private const string ScenePath = ScenesFolder + "/EmberDepths.unity";

        [MenuItem("EmberDepths/Setup/Build Playable Scene", priority = 1)]
        public static void BuildScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[EmberDepths] Exit play mode before rebuilding the scene.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ArtTools.EnsureFolder(ScenesFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera();
            DungeonRunner runner = CreateRunner();
            CreateController(runner, camera);
            CreateHud(runner);
            CreateEventSystem();

            camera.GetComponent<IsoCameraRig>().Runner = runner;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();

            Debug.Log($"[EmberDepths] Scene built at {ScenePath}. Press Play.");
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Near-black with a warm bias, so the volcano's glow has something to
            // sit against instead of a flat grey void.
            camera.backgroundColor = new Color(0.043f, 0.031f, 0.035f);

            IsoCameraRig.Configure(camera, 360);
            go.AddComponent<AudioListener>();

            var rig = go.AddComponent<IsoCameraRig>();
            rig.VerticalPixels = 360;

            return camera;
        }

        private static DungeonRunner CreateRunner()
        {
            var go = new GameObject("Game");
            var runner = go.AddComponent<DungeonRunner>();

            runner.Dungeon = FindAsset<DungeonDefinition>("DG_EmberDepths");
            runner.Roster = FindAsset<PartyRoster>("PR_Default");
            runner.AutoStart = true;

            if (runner.Dungeon == null || runner.Roster == null)
            {
                Debug.LogWarning("[EmberDepths] Dungeon or roster asset not found. " +
                                 "Run EmberDepths/Content/Build Volcano Dungeon, then rebuild the scene.");
            }

            return runner;
        }

        private static void CreateController(DungeonRunner runner, Camera camera)
        {
            var go = new GameObject("Player Input");
            var controller = go.AddComponent<LocalPlayerController>();
            controller.Runner = runner;
            controller.WorldCamera = camera;
        }

        private static void CreateHud(DungeonRunner runner)
        {
            var go = new GameObject("HUD");
            var hud = go.AddComponent<DungeonHud>();
            hud.Runner = runner;

            // Serialised rather than looked up at runtime: the HUD has to work
            // in a build, where AssetDatabase does not exist.
            hud.EmberIcon = ContentAssetUtil.FindExact<Sprite>("icon_embers");
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            for (int i = 0; i < scenes.Count; i++)
                if (scenes[i].path == ScenePath) return;

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T FindAsset<T>(string nameFilter) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"{nameFilter} t:{typeof(T).Name}");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>
        /// The one-button path from a fresh clone to something playable. Deliberately
        /// ordered: settings, then art, then content that references the art, then
        /// the scene that references the content.
        /// </summary>
        [MenuItem("EmberDepths/Setup/First-Time Setup (everything)", priority = -1)]
        public static void FullSetup()
        {
            ProjectSetup.ConfigureProject();
            ArtTools.ImportEverything();
            VolcanoContentBuilder.Build();
            BuildScene();
        }
    }
}
