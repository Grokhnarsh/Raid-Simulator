using System;
using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using UnityEngine;

namespace EmberDepths.Gameplay.Combat
{
    /// <summary>Why a cast request was refused. Surfaced to the UI so the player learns the rules.</summary>
    public enum CastRefusal
    {
        None = 0,
        NoAbility,
        OnCooldown,
        GlobalCooldown,
        AlreadyCasting,
        Silenced,
        OutOfRange,
        NoLineOfSight,
        InvalidTarget,
        NotEnoughResource
    }

    /// <summary>
    /// An actor's abilities, their cooldowns, and the cast currently in progress.
    ///
    /// Players and AI both go through <see cref="TryCast"/>, so an ability that a
    /// player cannot use out of range is one the AI cannot cheat with either.
    /// </summary>
    public sealed class AbilityBook
    {
        /// <summary>Baseline shared cooldown, before haste.</summary>
        public const float BaseGlobalCooldown = 1.0f;

        private readonly Actor _owner;
        private readonly Dictionary<AbilityDefinition, int> _readyAtTick = new Dictionary<AbilityDefinition, int>(8);

        /// <summary>The class kit. Fixed for the life of the actor.</summary>
        private readonly List<AbilityDefinition> _classAbilities = new List<AbilityDefinition>(8);

        /// <summary>Granted by equipment and set bonuses. Comes and goes with gear.</summary>
        private readonly List<AbilityDefinition> _granted = new List<AbilityDefinition>(4);

        /// <summary>Class kit first, then grants. The order is the action bar's order.</summary>
        private readonly List<AbilityDefinition> _combined = new List<AbilityDefinition>(12);

        private int _globalReadyAtTick;

        public AbilityDefinition BasicAttack { get; private set; }
        public IReadOnlyList<AbilityDefinition> Abilities => _combined;

        /// <summary>Raised when a grant appears or disappears, so the action bar can relabel.</summary>
        public event Action AbilitiesChanged;

        public bool IsCasting { get; private set; }
        public AbilityDefinition CastingAbility { get; private set; }
        public int CastStartedTick { get; private set; }
        public int CastCompletesTick { get; private set; }
        public Actor CastTarget { get; private set; }
        public GridCoord CastCell { get; private set; }

        public event Action<AbilityDefinition> CastStarted;
        public event Action<AbilityDefinition> CastCompleted;
        public event Action<AbilityDefinition> CastInterrupted;

        public AbilityBook(Actor owner)
        {
            _owner = owner;
        }

        public void Load(AbilityDefinition basicAttack, IReadOnlyList<AbilityDefinition> abilities)
        {
            BasicAttack = basicAttack;
            _classAbilities.Clear();

            if (abilities != null)
                for (int i = 0; i < abilities.Count; i++)
                    if (abilities[i] != null) _classAbilities.Add(abilities[i]);

            RebuildCombined();
        }

        /// <summary>
        /// Replaces the set of abilities granted by equipment.
        ///
        /// Grants are kept apart from the class kit so that losing a legendary
        /// cannot shuffle the player's muscle memory: the class abilities keep
        /// their action-bar positions and only the tail changes.
        /// </summary>
        public void SetGrantedAbilities(IReadOnlyList<AbilityDefinition> granted)
        {
            bool changed = granted == null ? _granted.Count > 0 : !SameContents(_granted, granted);
            if (!changed) return;

            _granted.Clear();
            if (granted != null)
                for (int i = 0; i < granted.Count; i++)
                    if (granted[i] != null) _granted.Add(granted[i]);

            RebuildCombined();
            AbilitiesChanged?.Invoke();
        }

        private static bool SameContents(List<AbilityDefinition> a, IReadOnlyList<AbilityDefinition> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private void RebuildCombined()
        {
            _combined.Clear();
            _combined.AddRange(_classAbilities);

            for (int i = 0; i < _granted.Count; i++)
                if (!_combined.Contains(_granted[i])) _combined.Add(_granted[i]);
        }

        // --- readiness -----------------------------------------------------------

        public bool IsOnGlobalCooldown(int tick) => tick < _globalReadyAtTick;

        public float GlobalCooldownRemaining(int tick) =>
            Mathf.Max(0f, SimClock.TicksToSeconds(_globalReadyAtTick - tick));

        public float CooldownRemaining(AbilityDefinition ability, int tick)
        {
            if (ability == null) return 0f;
            return _readyAtTick.TryGetValue(ability, out int ready)
                ? Mathf.Max(0f, SimClock.TicksToSeconds(ready - tick))
                : 0f;
        }

        public bool IsReady(AbilityDefinition ability, int tick)
        {
            if (ability == null) return false;
            if (IsCasting) return false;
            if (_readyAtTick.TryGetValue(ability, out int ready) && tick < ready) return false;
            if (ability.TriggersGlobalCooldown && IsOnGlobalCooldown(tick)) return false;
            return true;
        }

        /// <summary>
        /// Full validation without side effects. The action bar calls this every
        /// frame to grey out buttons, and <see cref="TryCast"/> calls it again
        /// before committing.
        /// </summary>
        public CastRefusal CanCast(AbilityDefinition ability, Actor target, GridCoord cell, int tick)
        {
            if (ability == null) return CastRefusal.NoAbility;
            if (IsCasting) return CastRefusal.AlreadyCasting;
            if (!_owner.Stats.CanCast) return CastRefusal.Silenced;

            if (_readyAtTick.TryGetValue(ability, out int ready) && tick < ready)
                return CastRefusal.OnCooldown;

            if (ability.TriggersGlobalCooldown && IsOnGlobalCooldown(tick))
                return CastRefusal.GlobalCooldown;

            if (ability.ResourceCost > 0f && _owner.Resource < ability.ResourceCost)
                return CastRefusal.NotEnoughResource;

            switch (ability.TargetKind)
            {
                case TargetKind.Self:
                    return CastRefusal.None;

                case TargetKind.Enemy:
                    if (target == null || !target.IsAlive || !_owner.IsHostileTo(target))
                        return CastRefusal.InvalidTarget;
                    if (target.Stats.Untargetable) return CastRefusal.InvalidTarget;
                    break;

                case TargetKind.Ally:
                    if (target == null || !target.IsAlive || target.Faction != _owner.Faction)
                        return CastRefusal.InvalidTarget;
                    break;
            }

            GridCoord aim = ability.TargetKind == TargetKind.Ground || target == null ? cell : target.Cell;

            if (GridCoord.Chebyshev(_owner.Cell, aim) > ability.Range)
                return CastRefusal.OutOfRange;

            if (ability.RequiresLineOfSight && !_owner.World.Map.HasLineOfSight(_owner.Cell, aim))
                return CastRefusal.NoLineOfSight;

            return CastRefusal.None;
        }

        /// <summary>
        /// Starts a cast. Instant abilities resolve immediately; timed ones enter
        /// the casting state and resolve on a later tick.
        /// </summary>
        public CastRefusal TryCast(AbilityDefinition ability, Actor target, GridCoord cell, int tick)
        {
            CastRefusal refusal = CanCast(ability, target, cell, tick);
            if (refusal != CastRefusal.None) return refusal;

            GridCoord aim = ability.TargetKind == TargetKind.Ground || target == null ? cell : target.Cell;

            if (ability.ResourceCost > 0f && !_owner.SpendResource(ability.ResourceCost))
                return CastRefusal.NotEnoughResource;

            _owner.FaceTowards(aim);

            // Haste shortens both the cast bar and the cooldown, which is what
            // makes it feel like a throughput stat rather than a cosmetic one.
            float haste = Mathf.Max(0.1f, _owner.Stats.Haste);
            float castSeconds = ability.CastTime / haste;

            PutOnCooldown(ability, tick, haste);

            if (castSeconds <= 0f && ability.TelegraphLeadTime <= 0f)
            {
                Resolve(ability, target, aim);
                return CastRefusal.None;
            }

            IsCasting = true;
            CastingAbility = ability;
            CastTarget = target;
            CastCell = aim;
            CastStartedTick = tick;
            CastCompletesTick = tick + SimClock.SecondsToTicks(castSeconds + ability.TelegraphLeadTime);

            if (ability.TelegraphLeadTime > 0f)
            {
                _owner.World.Telegraphs.Show(
                    _owner, ability.Shape, _owner.Cell, aim,
                    castSeconds + ability.TelegraphLeadTime);
            }

            if (!string.IsNullOrEmpty(ability.CastVfxKey))
                _owner.World.PlayVfx(ability.CastVfxKey, _owner.Cell);

            CastStarted?.Invoke(ability);
            return CastRefusal.None;
        }

        private void PutOnCooldown(AbilityDefinition ability, int tick, float haste)
        {
            if (ability.Cooldown > 0f)
                _readyAtTick[ability] = tick + SimClock.SecondsToTicks(ability.Cooldown / haste);

            if (ability.TriggersGlobalCooldown)
                _globalReadyAtTick = tick + SimClock.SecondsToTicks(BaseGlobalCooldown / haste);
        }

        public void Tick(int tick)
        {
            if (!IsCasting) return;

            // Losing the target mid-cast only aborts abilities that needed one.
            if (CastingAbility.TargetKind != TargetKind.Ground &&
                CastingAbility.TargetKind != TargetKind.Self &&
                (CastTarget == null || !CastTarget.IsAlive))
            {
                CancelCast();
                return;
            }

            if (!_owner.Stats.CanCast)
            {
                CancelCast();
                return;
            }

            if (!CastingAbility.CastableWhileMoving && _owner.Motor.IsMoving)
            {
                CancelCast();
                return;
            }

            if (tick < CastCompletesTick) return;

            AbilityDefinition ability = CastingAbility;
            Actor target = CastTarget;
            GridCoord cell = CastCell;

            IsCasting = false;
            CastingAbility = null;
            CastTarget = null;

            Resolve(ability, target, cell);
            CastCompleted?.Invoke(ability);
        }

        private void Resolve(AbilityDefinition ability, Actor target, GridCoord cell)
        {
            _owner.World.AbilityRunner.Execute(ability, _owner, target, cell);
        }

        public void CancelCast()
        {
            if (!IsCasting) return;

            AbilityDefinition ability = CastingAbility;
            IsCasting = false;
            CastingAbility = null;
            CastTarget = null;

            _owner.World.Telegraphs.CancelFor(_owner);
            CastInterrupted?.Invoke(ability);
        }

        /// <summary>Progress of the current cast, 0..1. Drives the cast bar.</summary>
        public float CastProgress(int tick)
        {
            if (!IsCasting) return 0f;
            int span = CastCompletesTick - CastStartedTick;
            return span <= 0 ? 1f : Mathf.Clamp01((tick - CastStartedTick) / (float)span);
        }

        public void ResetCooldowns()
        {
            _readyAtTick.Clear();
            _globalReadyAtTick = 0;
        }
    }
}
