using System;
using RaidSim.Characters.Data;
using RaidSim.Core.Common;
using UnityEngine;

namespace RaidSim.Game.Bootstrap
{
    /// <summary>
    /// One authored "put this character here" instruction.
    /// </summary>
    /// <remarks>
    /// Deliberately the same structure for the player, for group members and for enemies. Phase 7's
    /// raid zones will author trash packs and the boss arena as lists of these, so the spawning code
    /// written now does not need to change when the content arrives.
    /// </remarks>
    [Serializable]
    public struct SpawnEntry
    {
        [Tooltip("The character to spawn. An entry with no character is skipped.")]
        public CharacterDefinition Character;

        [Tooltip("Which side the spawned actor fights for.")]
        public Faction Faction;

        [Tooltip("Where to place it, in world space.")]
        public Vector3 Position;

        [Tooltip("Which way it faces on spawn, in degrees around the vertical axis.")]
        public float FacingYaw;

        /// <summary>Facing as a direction vector.</summary>
        public Vector3 FacingDirection => Quaternion.Euler(0f, FacingYaw, 0f) * Vector3.forward;

        public bool IsValid => Character != null;
    }
}
