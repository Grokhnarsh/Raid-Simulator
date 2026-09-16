using System.IO;
using UnityEditor;
using UnityEngine;

namespace EmberDepths.EditorTools
{
    /// <summary>
    /// Shared plumbing for the content builders: create-or-update assets, and
    /// look them up by exact name.
    ///
    /// The exact-name lookup is the point. <see cref="AssetDatabase.FindAssets"/>
    /// matches loosely — a search for "T_basalt_cracked" also returns
    /// "T_basalt_wall" because they share a token — so taking the first hit
    /// silently wires the wrong asset into a slot and produces content that
    /// looks plausible and is wrong.
    /// </summary>
    internal static class ContentAssetUtil
    {
        public const string Root = "Assets/_Project/Content";

        /// <summary>Loads the asset at <paramref name="relativePath"/> (no extension) or creates it.</summary>
        public static T Asset<T>(string relativePath) where T : ScriptableObject
        {
            string path = $"{Root}/{relativePath}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        public static void Save(Object asset) => EditorUtility.SetDirty(asset);

        /// <summary>The single asset whose file name is exactly <paramref name="assetName"/>.</summary>
        public static T FindExact<T>(string assetName) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"{assetName} t:{typeof(T).Name}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != assetName) continue;

                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) return asset;
            }

            return null;
        }

        public static void EnsureFolder(string assetFolder)
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
    }
}
