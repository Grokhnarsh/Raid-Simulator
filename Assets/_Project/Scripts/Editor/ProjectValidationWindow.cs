using RaidSim.Game.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace RaidSim.EditorTools
{
    /// <summary>
    /// Menu commands that check authored data is complete before play mode is entered.
    /// </summary>
    /// <remarks>
    /// <para>A data-driven project fails in a particular way: the code is fine and an asset has an
    /// empty field. Those failures are cheap to catch and expensive to debug at runtime, so every
    /// data asset in the project exposes a <c>Validate()</c> returning a human-readable problem, and
    /// this menu runs all of them at once.</para>
    /// <para>As content grows — abilities, bosses, loot tables, raid layouts — each new asset type
    /// adds its validation here rather than inventing a new checking mechanism.</para>
    /// </remarks>
    public static class ProjectValidationWindow
    {
        [MenuItem("Raid Simulator/Validate Project Data", priority = 100)]
        public static void ValidateProjectData()
        {
            int checkedAssets = 0;
            int problems = 0;

            problems += ValidateAll<BootstrapConfig>(config => config.Validate(), ref checkedAssets);
            problems += ValidateAll<Characters.Data.CharacterDefinition>(
                character => character.Validate(), ref checkedAssets);

            if (problems == 0)
            {
                Debug.Log($"Raid Simulator: {checkedAssets} data asset(s) validated, no problems found.");
                return;
            }

            Debug.LogError($"Raid Simulator: {problems} problem(s) found in {checkedAssets} data asset(s). See the errors above.");
        }

        private static int ValidateAll<T>(System.Func<T, string> validate, ref int checkedAssets)
            where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            int problems = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                {
                    continue;
                }

                checkedAssets++;
                string problem = validate(asset);
                if (problem == null)
                {
                    continue;
                }

                problems++;
                Debug.LogError($"{path}: {problem}", asset);
            }

            return problems;
        }
    }
}
