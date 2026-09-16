using RaidSim.Combat.Targeting;
using RaidSim.Core.Locomotion;
using RaidSim.Core.Simulation;
using RaidSim.Game.Player;

namespace RaidSim.Game.Bootstrap
{
    /// <summary>
    /// Ticks the player's driver: read input, resolve targeting, move the character.
    /// </summary>
    /// <remarks>
    /// <para>Registering the player as an ordinary simulation system rather than letting a
    /// MonoBehaviour's <c>Update</c> do the work has two consequences the project depends on. The
    /// player moves at the point in the tick order where movement belongs, alongside every AI-driven
    /// character; and the player freezes on pause for exactly the same reason everything else does —
    /// the clock stopped — with no pause check written anywhere.</para>
    /// <para>Phase 5's AI driver will be the same shape, at the same order, reading an AI profile
    /// instead of a keyboard.</para>
    /// </remarks>
    public sealed class PlayerDriverSystem : ISimulationSystem
    {
        private readonly PlayerController _controller;
        private readonly PlayerTargetingController _targeting;

        public PlayerDriverSystem(PlayerController controller, PlayerTargetingController targeting)
        {
            _controller = controller;
            _targeting = targeting;
        }

        /// <inheritdoc />
        public int Order => SystemOrder.Locomotion;

        /// <inheritdoc />
        public void Tick(float deltaSeconds)
        {
            if (_targeting != null)
            {
                _targeting.ProcessInput();
            }

            if (_controller == null || _controller.ControlledActor == null)
            {
                return;
            }

            MovementIntent intent = _controller.ReadIntent();
            _controller.ControlledActor.ApplyMovement(intent, deltaSeconds);
        }
    }
}
