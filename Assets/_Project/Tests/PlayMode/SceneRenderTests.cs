using System.Collections;
using System.IO;
using EmberDepths.Gameplay.Run;
using EmberDepths.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EmberDepths.PlayTests
{
    /// <summary>
    /// Presses Play, in effect.
    ///
    /// The edit-mode suite proves the simulation is correct; it says nothing
    /// about whether anything appears on screen. Sorting layers, sprite pivots,
    /// the camera's transparency sort axis and the tilemap's cell size are all
    /// things that can be wrong in ways no headless test notices and every
    /// player notices immediately.
    ///
    /// So this loads the real scene, lets it run, renders a frame to a texture
    /// and inspects the pixels. It also drops that frame on disk, which makes it
    /// the fastest way to see what a change actually did.
    /// </summary>
    public sealed class SceneRenderTests
    {
        private const string SceneName = "EmberDepths";
        private const int Width = 960;
        private const int Height = 540;

        /// <summary>
        /// Written outside the project folder on purpose — a PNG inside Assets
        /// would trigger an import and dirty the very project under test.
        /// </summary>
        private static string ShotPath =>
            Path.Combine(Path.GetTempPath(), "emberdepths_frame.png");

        [UnityTest]
        public IEnumerator SceneStartsARunAndDrawsSomething()
        {
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var runner = Object.FindFirstObjectByType<DungeonRunner>();
            Assert.IsNotNull(runner, $"no DungeonRunner in '{SceneName}' — rebuild the scene");

            // Let the run start, the dungeon build and a few seconds of
            // simulation happen, so the frame shows a live game rather than an
            // empty first frame.
            float elapsed = 0f;
            while (elapsed < 3.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Open the gear panel so the captured frame exercises the equipment
            // UI too — it is the part most likely to break silently, since
            // nothing else reads from it.
            var hud = Object.FindFirstObjectByType<DungeonHud>();
            if (hud != null)
            {
                hud.ToggleGearPanel();
                yield return null;
                yield return null;
            }

            Assert.AreEqual(RunState.Running, runner.State, "the run did not start");
            Assert.IsNotNull(runner.World, "no world was built");
            Assert.AreEqual(5, runner.Party.Count, "the party did not fully spawn");
            Assert.IsNotNull(runner.LocalPlayer, "no locally controlled character");
            Assert.IsNotNull(runner.Layout.BossRoom, "the layout has no boss room");
            Assert.Greater(runner.World.Clock.Tick, 0, "the simulation clock never advanced");

            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "no main camera");
            Assert.AreEqual(TransparencySortMode.CustomAxis, camera.transparencySortMode,
                "the camera is not sorting isometrically; sprites will draw in the wrong order");

            Texture2D frame = Capture(camera);

            try
            {
                File.WriteAllBytes(ShotPath, frame.EncodeToPNG());
                Debug.Log($"[EmberDepths] frame written to {ShotPath}");

                AssertFrameIsNotBlank(frame, camera.backgroundColor);
                AssertHudIsDrawn(frame, camera.backgroundColor);
            }
            finally
            {
                Object.DestroyImmediate(frame);
            }
        }

        /// <summary>
        /// The HUD lives on a screen-space-overlay canvas, which
        /// <see cref="Camera.Render"/> does not draw. Borrowing the canvas onto
        /// the camera for the duration of the capture is what puts it in the
        /// frame — and turns "did the world render" into "did the game render".
        /// </summary>
        private static Canvas BorrowCanvas(Camera camera, out RenderMode previousMode)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            previousMode = RenderMode.ScreenSpaceOverlay;
            if (canvas == null) return null;

            previousMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            return canvas;
        }

        /// <summary>
        /// Checks the top-right corner, where the ember purse and the gear panel
        /// sit. A blank corner means the reward HUD silently failed to build.
        /// </summary>
        private static void AssertHudIsDrawn(Texture2D frame, Color background)
        {
            var background32 = (Color32)background;
            Color32[] pixels = frame.GetPixels32();

            int different = 0;
            int total = 0;

            // Texture rows run bottom-up, so the top of the screen is high y.
            for (int y = frame.height * 3 / 4; y < frame.height; y++)
            {
                for (int x = frame.width * 2 / 3; x < frame.width; x++)
                {
                    Color32 p = pixels[y * frame.width + x];
                    total++;

                    if (Mathf.Abs(p.r - background32.r) > 6 ||
                        Mathf.Abs(p.g - background32.g) > 6 ||
                        Mathf.Abs(p.b - background32.b) > 6)
                    {
                        different++;
                    }
                }
            }

            float coverage = total == 0 ? 0f : different / (float)total;
            Debug.Log($"[EmberDepths] hud corner coverage: {coverage:P1}");

            Assert.Greater(coverage, 0.02f, "nothing was drawn where the ember purse and gear panel live");
        }

        private static Texture2D Capture(Camera camera)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Canvas canvas = BorrowCanvas(camera, out RenderMode previousCanvasMode);

            try
            {
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                if (canvas != null) canvas.renderMode = previousCanvasMode;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// A frame of nothing but the clear colour means the dungeon rendered
        /// off-camera, behind the camera, or not at all — the exact failure a
        /// headless test cannot see.
        /// </summary>
        private static void AssertFrameIsNotBlank(Texture2D frame, Color background)
        {
            Color32[] pixels = frame.GetPixels32();
            var background32 = (Color32)background;

            int different = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (Mathf.Abs(pixels[i].r - background32.r) > 6 ||
                    Mathf.Abs(pixels[i].g - background32.g) > 6 ||
                    Mathf.Abs(pixels[i].b - background32.b) > 6)
                {
                    different++;
                }
            }

            float coverage = different / (float)pixels.Length;
            Debug.Log($"[EmberDepths] frame coverage: {coverage:P1} of pixels differ from the clear colour");

            Assert.Greater(coverage, 0.05f,
                $"only {coverage:P1} of the frame is dungeon — nothing is being drawn");
        }
    }
}
