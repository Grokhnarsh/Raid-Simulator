// Minimal Unity API surface used for headless type-checking. See README.md in this folder.
// Signatures mirror the Unity scripting reference; behaviour is intentionally absent.
#pragma warning disable CA1050, IDE0060, CS0067

using System;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }

        public static T Instantiate<T>(T original) where T : Object => original;

        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;

        public static void Destroy(Object obj) { }

        public static void DestroyImmediate(Object obj) { }

        public static implicit operator bool(Object exists) => !ReferenceEquals(exists, null);

        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);

        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);

        public override bool Equals(object other) => ReferenceEquals(this, other);

        public override int GetHashCode() => base.GetHashCode();
    }

    public class Component : Object
    {
        public Transform transform { get; }

        public GameObject gameObject { get; }

        public T GetComponent<T>() => default;

        public T GetComponentInParent<T>() => default;

        public T GetComponentInChildren<T>() => default;
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }

        public bool isActiveAndEnabled { get; }
    }

    public class MonoBehaviour : Behaviour
    {
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => default;
    }

    public sealed class GameObject : Object
    {
        public GameObject() { }

        public GameObject(string name) { }

        public GameObject(string name, params Type[] components) { }

        public Transform transform { get; }

        public string tag { get; set; }

        public int layer { get; set; }

        public bool activeSelf { get; }

        public void SetActive(bool value) { }

        public T AddComponent<T>() where T : Component => default;

        public T GetComponent<T>() => default;

        public T GetComponentInParent<T>() => default;

        public static GameObject CreatePrimitive(PrimitiveType type) => default;
    }

    public sealed class Transform : Component
    {
        public Vector3 position { get; set; }

        public Vector3 localPosition { get; set; }

        public Quaternion rotation { get; set; }

        public Quaternion localRotation { get; set; }

        public Vector3 localScale { get; set; }

        public Vector3 forward { get; set; }

        public Vector3 right { get; set; }

        public Vector3 up { get; set; }

        public Transform parent { get; set; }

        public void SetParent(Transform parent) { }

        public void SetParent(Transform parent, bool worldPositionStays) { }

        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) { }
    }

    public enum PrimitiveType
    {
        Sphere = 0,
        Capsule = 1,
        Cylinder = 2,
        Cube = 3,
        Plane = 4,
        Quad = 5,
    }

    public static class Time
    {
        public static float deltaTime { get; }

        public static float unscaledDeltaTime { get; }

        public static float time { get; }

        public static float timeScale { get; set; }
    }

    public static class Application
    {
        public static bool isPlaying { get; }

        public static bool isEditor { get; }
    }

    public static class Debug
    {
        public static void Log(object message) { }

        public static void Log(object message, Object context) { }

        public static void LogWarning(object message) { }

        public static void LogWarning(object message, Object context) { }

        public static void LogError(object message) { }

        public static void LogError(object message, Object context) { }
    }

    public static class Gizmos
    {
        public static Color color { get; set; }

        public static void DrawWireCube(Vector3 center, Vector3 size) { }

        public static void DrawWireSphere(Vector3 center, float radius) { }

        public static void DrawLine(Vector3 from, Vector3 to) { }
    }
}
