// Minimal UnityEditor surface used for headless type-checking. See README.md.
#pragma warning disable CA1050, IDE0060

using System;
using UnityEngine;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }

        public MenuItem(string itemName, bool isValidateFunction) { }

        public MenuItem(string itemName, bool isValidateFunction, int priority) { }

        public int priority { get; set; }
    }

    public static class AssetDatabase
    {
        public static string[] FindAssets(string filter) => Array.Empty<string>();

        public static string[] FindAssets(string filter, string[] searchInFolders) => Array.Empty<string>();

        public static string GUIDToAssetPath(string guid) => string.Empty;

        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object => default;

        public static void CreateAsset(UnityEngine.Object asset, string path) { }

        public static void SaveAssets() { }

        public static void Refresh() { }
    }
}

namespace UnityEditor
{
    public class SerializedProperty
    {
        public UnityEngine.Object objectReferenceValue { get; set; }

        public int intValue { get; set; }

        public string stringValue { get; set; }
    }

    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object obj) { }

        public SerializedProperty FindProperty(string propertyPath) => default;

        public bool ApplyModifiedProperties() => false;

        public void Update() { }
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object target) { }
    }
}
