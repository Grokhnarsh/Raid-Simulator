// Minimal Unity math surface used for headless type-checking. See README.md in this folder.
#pragma warning disable CA1050, IDE0060

using System;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero => default;
        public static Vector2 one => new Vector2(1f, 1f);

        public float magnitude => (float)Math.Sqrt((x * x) + (y * y));

        public float sqrMagnitude => (x * x) + (y * y);

        public Vector2 normalized => this;

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);

        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);

        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);

        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0f);
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => default;
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);

        public float magnitude => (float)Math.Sqrt(sqrMagnitude);

        public float sqrMagnitude => (x * x) + (y * y) + (z * z);

        public Vector3 normalized => this;

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);

        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);

        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);

        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);

        public static Vector3 operator *(float d, Vector3 a) => a * d;

        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);

        public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal) => vector;

        public static float Dot(Vector3 a, Vector3 b) => (a.x * b.x) + (a.y * b.y) + (a.z * b.z);

        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;

        public static Vector3 SmoothDamp(
            Vector3 current,
            Vector3 target,
            ref Vector3 currentVelocity,
            float smoothTime,
            float maxSpeed,
            float deltaTime) => target;

        public static Vector3 SmoothDamp(
            Vector3 current,
            Vector3 target,
            ref Vector3 currentVelocity,
            float smoothTime) => target;
    }

    public struct Quaternion
    {
        public static Quaternion identity => default;

        public static Quaternion Euler(float x, float y, float z) => default;

        public static Quaternion Euler(Vector3 euler) => default;

        public static Quaternion LookRotation(Vector3 forward) => default;

        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards) => default;

        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta) => default;

        public static Vector3 operator *(Quaternion rotation, Vector3 point) => point;

        public static Quaternion operator *(Quaternion lhs, Quaternion rhs) => lhs;
    }

    public struct Bounds
    {
        public Bounds(Vector3 center, Vector3 size)
        {
            this.center = center;
            this.size = size;
        }

        public Vector3 center { get; set; }

        public Vector3 size { get; set; }

        public Vector3 extents => size * 0.5f;

        public Vector3 min => center - extents;

        public Vector3 max => center + extents;
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            a = 1f;
        }

        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static Color white => new Color(1f, 1f, 1f);
        public static Color black => new Color(0f, 0f, 0f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);
    }

    public static class Mathf
    {
        public const float Epsilon = 1.401298E-45f;
        public const float Infinity = float.PositiveInfinity;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;
        public const float PI = 3.14159274f;

        public static float Max(float a, float b) => a > b ? a : b;

        public static int Max(int a, int b) => a > b ? a : b;

        public static float Min(float a, float b) => a < b ? a : b;

        public static int Min(int a, int b) => a < b ? a : b;

        public static float Abs(float value) => Math.Abs(value);

        public static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);

        public static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 1e-6f;

        public static float Tan(float f) => (float)Math.Tan(f);

        public static int RoundToInt(float f) => (int)Math.Round(f, MidpointRounding.AwayFromZero);

        public static int FloorToInt(float f) => (int)Math.Floor(f);

        public static int CeilToInt(float f) => (int)Math.Ceiling(f);

        public static float Sqrt(float f) => (float)Math.Sqrt(f);

        public static float Lerp(float a, float b, float t) => a + ((b - a) * Clamp01(t));

        public static float SmoothDamp(
            float current,
            float target,
            ref float currentVelocity,
            float smoothTime,
            float maxSpeed,
            float deltaTime) => target;

        public static float SmoothDamp(
            float current,
            float target,
            ref float currentVelocity,
            float smoothTime) => target;
    }

    public struct LayerMask
    {
        public int value { get; set; }

        public static implicit operator int(LayerMask mask) => mask.value;

        public static implicit operator LayerMask(int intVal) => new LayerMask { value = intVal };
    }
}
