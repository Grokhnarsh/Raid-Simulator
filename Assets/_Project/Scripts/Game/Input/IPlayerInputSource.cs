using UnityEngine;

namespace RaidSim.Game.Input
{
    /// <summary>
    /// Everything the player controller needs to know about input this frame.
    /// </summary>
    /// <remarks>
    /// <para>The controller talks to this interface, never to a device. That has three payoffs the
    /// project needs: a play-mode test can drive a character with a scripted input source, the
    /// headless encounter simulator can run with no input device present at all, and a group member
    /// can be handed from the player to the AI by swapping the source rather than by branching on
    /// "is this the player".</para>
    /// <para>Buttons are reported as "was pressed this frame" edges rather than held states, because
    /// every Phase 1 action — select, cycle, clear, pause — is an edge-triggered command.</para>
    /// </remarks>
    public interface IPlayerInputSource
    {
        /// <summary>
        /// Movement on the horizontal plane, in camera space. Magnitude is 0..1; a diagonal reading
        /// is not faster than a cardinal one.
        /// </summary>
        Vector2 MoveAxis { get; }

        /// <summary>Zoom delta this frame. Positive zooms in.</summary>
        float ZoomAxis { get; }

        /// <summary>Pointer position in screen pixels, for click-to-target.</summary>
        Vector2 PointerPosition { get; }

        /// <summary>True on the frame the select button went down.</summary>
        bool SelectTargetPressed { get; }

        /// <summary>True on the frame the cycle-target button went down.</summary>
        bool CycleTargetPressed { get; }

        /// <summary>True on the frame the clear-target button went down.</summary>
        bool ClearTargetPressed { get; }

        /// <summary>True on the frame the pause button went down.</summary>
        bool TogglePausePressed { get; }
    }
}
