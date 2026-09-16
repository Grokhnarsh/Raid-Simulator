using RaidSim.Core.GameFlow;

namespace RaidSim.Core.Events
{
    /// <summary>Raised after the game state changed. Menus, HUD visibility and input maps listen.</summary>
    public readonly struct GameStateChangedEvent
    {
        public readonly GameState Previous;
        public readonly GameState Current;

        public GameStateChangedEvent(GameState previous, GameState current)
        {
            Previous = previous;
            Current = current;
        }
    }
}
