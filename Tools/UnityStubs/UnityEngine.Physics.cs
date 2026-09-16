// Minimal Unity physics/rendering surface used for headless type-checking. See README.md.
#pragma warning disable CA1050, IDE0060

namespace UnityEngine
{
    public struct Ray
    {
        public Ray(Vector3 origin, Vector3 direction)
        {
            this.origin = origin;
            this.direction = direction;
        }

        public Vector3 origin { get; set; }

        public Vector3 direction { get; set; }
    }

    public struct RaycastHit
    {
        public Collider collider { get; }

        public Vector3 point { get; }

        public Vector3 normal { get; }

        public float distance { get; }

        public Transform transform { get; }
    }

    public enum QueryTriggerInteraction
    {
        UseGlobal = 0,
        Ignore = 1,
        Collide = 2,
    }

    public static class Physics
    {
        public const int DefaultRaycastLayers = -5;
        public const int AllLayers = -1;

        public static bool Raycast(Ray ray) => false;

        public static bool Raycast(Ray ray, out RaycastHit hitInfo) { hitInfo = default; return false; }

        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance) { hitInfo = default; return false; }

        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance, int layerMask)
        {
            hitInfo = default;
            return false;
        }

        public static bool Raycast(
            Ray ray,
            out RaycastHit hitInfo,
            float maxDistance,
            int layerMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            hitInfo = default;
            return false;
        }
    }

    public class Collider : Component
    {
        public bool enabled { get; set; }

        public bool isTrigger { get; set; }

        public Bounds bounds { get; }
    }

    public class CharacterController : Collider
    {
        public float radius { get; set; }

        public float height { get; set; }

        public Vector3 center { get; set; }

        public float slopeLimit { get; set; }

        public float stepOffset { get; set; }

        public float skinWidth { get; set; }

        public bool isGrounded { get; }

        public Vector3 velocity { get; }

        public CollisionFlags Move(Vector3 motion) => CollisionFlags.None;

        public void SimpleMove(Vector3 speed) { }
    }

    public enum CollisionFlags
    {
        None = 0,
        Sides = 1,
        Above = 2,
        Below = 4,
    }

    public class Material : Object
    {
        public Color color { get; set; }
    }

    public class Renderer : Component
    {
        public Material material { get; set; }

        public Material sharedMaterial { get; set; }

        public bool enabled { get; set; }
    }

    public class Camera : Behaviour
    {
        public static Camera main { get; }

        public bool orthographic { get; set; }

        public float orthographicSize { get; set; }

        public float fieldOfView { get; set; }

        public float nearClipPlane { get; set; }

        public float farClipPlane { get; set; }

        public Ray ScreenPointToRay(Vector3 position) => default;

        public Vector3 WorldToScreenPoint(Vector3 position) => default;
    }

    public class AudioListener : Behaviour
    {
    }
}

namespace UnityEngine
{
    public enum LightType
    {
        Spot = 0,
        Directional = 1,
        Point = 2,
        Area = 3,
    }

    public enum LightShadows
    {
        None = 0,
        Hard = 1,
        Soft = 2,
    }

    public class Light : Behaviour
    {
        public LightType type { get; set; }

        public LightShadows shadows { get; set; }

        public float intensity { get; set; }

        public Color color { get; set; }

        public float range { get; set; }
    }
}
