using RaidSim.Core.Mathematics;
using UnityEngine;

namespace RaidSim.Game.Interop
{
    /// <summary>
    /// The one place where the simulation's <see cref="Vec3"/> meets Unity's <see cref="Vector3"/>.
    /// </summary>
    /// <remarks>
    /// Conversions are confined to this file so the boundary between kernel and engine stays a line
    /// you can point at. Both types use the same axis convention (X right, Y up, Z forward), so the
    /// conversion is a straight copy.
    /// </remarks>
    public static class VecConversions
    {
        public static Vector3 ToUnity(this Vec3 value) => new Vector3(value.X, value.Y, value.Z);

        public static Vec3 ToSim(this Vector3 value) => new Vec3(value.x, value.y, value.z);
    }
}
