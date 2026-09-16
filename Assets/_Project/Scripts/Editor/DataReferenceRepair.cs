using System.Linq;
using RaidSim.Game.Bootstrap;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RaidSim.EditorTools
{
    /// <summary>
    /// Fills in references that cannot be authored outside the editor.
    /// </summary>
    /// <remarks>
    /// <para>Most of this project's data assets are written as plain YAML by
    /// <c>Tools/Unity/generate_default_data.py</c>, which can compute any asset's GUID from its
    /// path. The input-actions asset is the exception: it is produced by the Input System's scripted
    /// importer, and the sub-object id a reference needs is assigned by the editor on import. It
    /// cannot be predicted from outside, and a guessed value would resolve to nothing.</para>
    /// <para>Rather than leave the field silently empty, this command resolves it in one click. It
    /// is deliberately explicit rather than an automatic on-load fixup: a tool that quietly rewrites
    /// the user's assets is worse than one they choose to run.</para>
    /// </remarks>
    public static class DataReferenceRepair
    {
        [MenuItem("Raid Simulator/Repair Data References", priority = 101)]
        public static void RepairDataReferences()
        {
            InputActionAsset controls = FindSingle<InputActionAsset>("input-actions");
            if (controls == null)
            {
                return;
            }

            int repaired = 0;
            foreach (BootstrapConfig config in LoadAll<BootstrapConfig>())
            {
                if (config.Controls != null)
                {
                    continue;
                }

                var serialized = new SerializedObject(config);
                SerializedProperty property = serialized.FindProperty("_controls");
                if (property == null)
                {
                    Debug.LogError($"{config.name}: no '_controls' field to repair.", config);
                    continue;
                }

                property.objectReferenceValue = controls;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                repaired++;
                Debug.Log($"{config.name}: controls set to '{controls.name}'.", config);
            }

            if (repaired > 0)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"Raid Simulator: repaired {repaired} reference(s).");
        }

        private static T FindSingle<T>(string humanName) where T : Object
        {
            T[] found = LoadAll<T>();
            switch (found.Length)
            {
                case 0:
                    Debug.LogError($"No {humanName} asset found in the project; nothing to repair.");
                    return null;
                case 1:
                    return found[0];
                default:
                    Debug.LogError(
                        $"Found {found.Length} {humanName} assets. Assign the intended one by hand " +
                        "so the choice is explicit.");
                    return null;
            }
        }

        private static T[] LoadAll<T>() where T : Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();
    }
}
