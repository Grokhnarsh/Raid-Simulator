using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Gameplay.Actors;
using UnityEngine;

namespace EmberDepths.Gameplay.AI
{
    /// <summary>
    /// Plays a party slot that no human is driving.
    ///
    /// Behaviour is chosen by <see cref="ActorRole"/>, so the companions read as
    /// a group that understands its jobs: the tank grabs loose enemies, the
    /// healer watches health bars and hangs back, damage dealers focus what the
    /// tank is holding rather than scattering across the room.
    ///
    /// The same component sits on the human's slot with <c>Enabled = false</c>.
    /// Flipping that one flag is the entire difference between an AI companion
    /// and a player character, which is what keeps a future multiplayer mode from
    /// needing a parallel implementation.
    /// </summary>
    public sealed class CompanionBrain : BrainBase
    {
        /// <summary>Health fraction below which the healer prioritises healing over damage.</summary>
        private const float HealThreshold = 0.85f;

        /// <summary>Health fraction that counts as an emergency and overrides positioning.</summary>
        private const float EmergencyThreshold = 0.4f;

        private readonly ClassDefinition _class;

        public CompanionBrain(Actor owner) : base(owner)
        {
            _class = owner.ClassDefinition;
            DecisionInterval = 4;
        }

        private int PreferredRange => _class != null ? _class.PreferredRange : 1;
        private int FollowRadius => _class != null ? _class.FollowRadius : 6;
        private float HazardAvoidance => _class != null ? _class.HazardAvoidance : 0.8f;

        protected override void Decide(int tick)
        {
            // Companions dodge reliably. They are the player's team, and a
            // companion that stands in fire reads as broken rather than as
            // characterful.
            if (TryStepOutOfDanger(Mathf.Max(0.85f, HazardAvoidance))) return;

            if (Owner.Abilities.IsCasting) return;

            Actor enemy = SelectEnemy();

            if (enemy == null)
            {
                FollowLeader();
                return;
            }

            switch (Owner.Role)
            {
                case ActorRole.Healer: ActAsHealer(enemy, tick); break;
                case ActorRole.Tank: ActAsTank(enemy, tick); break;
                default: ActAsDamage(enemy, tick); break;
            }
        }

        // --- target selection -------------------------------------------------------

        /// <summary>
        /// Damage dealers focus whatever the tank is already holding. Spreading
        /// damage across a pack is how a party loses control of it.
        /// </summary>
        private Actor SelectEnemy()
        {
            Actor tankTarget = FindTankTarget();
            if (tankTarget != null && tankTarget.IsAlive) return tankTarget;

            return World.Actors.NearestEnemyOf(Owner, 12);
        }

        private Actor FindTankTarget()
        {
            var party = World.Actors.Party;
            for (int i = 0; i < party.Count; i++)
            {
                Actor member = party[i];
                if (member == null || !member.IsAlive || member.Role != ActorRole.Tank) continue;

                Actor enemy = World.Actors.NearestEnemyOf(member, 3);
                if (enemy != null) return enemy;
            }
            return null;
        }

        // --- role behaviours ---------------------------------------------------------

        private void ActAsHealer(Actor enemy, int tick)
        {
            Actor patient = World.Actors.LowestHealthAlly(Owner, 14, HealThreshold);

            if (patient != null)
            {
                // Out of range to heal is the healer's only real positioning
                // problem; solve it before anything else.
                if (GridCoord.Chebyshev(Owner.Cell, patient.Cell) > PreferredRange)
                {
                    MoveTowards(patient.Cell, stopAdjacent: false);
                    if (patient.HealthFraction < EmergencyThreshold) return;
                }

                if (TryUseBestAbility(HealingAbilities(), patient, tick)) return;
            }

            // Nothing to heal: contribute damage, but keep the distance.
            int distance = GridCoord.Chebyshev(Owner.Cell, enemy.Cell);
            if (distance < PreferredRange - 1)
            {
                MoveAwayFrom(enemy.Cell, PreferredRange);
                return;
            }

            if (distance > PreferredRange)
            {
                MoveTowards(enemy.Cell, stopAdjacent: true);
                return;
            }

            if (!TryUseBestAbility(DamageAbilities(), enemy, tick)) TryBasicAttack(enemy, tick);
        }

        private void ActAsTank(Actor enemy, int tick)
        {
            // First job: anything chewing on a squishy party member gets pulled off.
            Actor loose = FindEnemyAttackingNonTank();
            if (loose != null) enemy = loose;

            int distance = GridCoord.Chebyshev(Owner.Cell, enemy.Cell);

            if (distance > 1)
            {
                if (World.TryFindMeleeSlot(Owner, enemy, out GridCoord slot))
                    MoveTowards(slot, stopAdjacent: false);
                else
                    MoveTowards(enemy.Cell, stopAdjacent: true);

                // Still cast on the way in if something has the range for it.
                TryUseBestAbility(_class != null ? _class.Abilities : null, enemy, tick);
                return;
            }

            if (TryUseBestAbility(_class != null ? _class.Abilities : null, enemy, tick)) return;
            if (TryBasicAttack(enemy, tick)) return;
            Owner.Motor.Stop();
        }

        /// <summary>An enemy whose current threat target is not a tank. The taunt trigger.</summary>
        private Actor FindEnemyAttackingNonTank()
        {
            var hostiles = World.Actors.Hostiles;
            for (int i = 0; i < hostiles.Count; i++)
            {
                Actor h = hostiles[i];
                if (h == null || !h.IsAlive || h.Threat == null) continue;

                Actor victim = h.Threat.CurrentTarget;
                if (victim == null || victim.Role == ActorRole.Tank) continue;
                if (GridCoord.Chebyshev(Owner.Cell, h.Cell) > 10) continue;

                return h;
            }
            return null;
        }

        private void ActAsDamage(Actor enemy, int tick)
        {
            int distance = GridCoord.Chebyshev(Owner.Cell, enemy.Cell);

            if (distance > PreferredRange)
            {
                if (PreferredRange <= 1 && World.TryFindMeleeSlot(Owner, enemy, out GridCoord slot))
                    MoveTowards(slot, stopAdjacent: false);
                else
                    MoveTowards(enemy.Cell, stopAdjacent: true);
                return;
            }

            // Ranged classes back off if something closed the gap on them.
            if (PreferredRange > 2 && distance < PreferredRange - 2)
            {
                MoveAwayFrom(enemy.Cell, PreferredRange);
                return;
            }

            Owner.Motor.Stop();
            if (TryUseBestAbility(_class != null ? _class.Abilities : null, enemy, tick)) return;
            TryBasicAttack(enemy, tick);
        }

        // --- out of combat -------------------------------------------------------------

        private void FollowLeader()
        {
            Actor leader = World.PartyLeader;
            if (leader == null || leader == Owner || !leader.IsAlive)
            {
                Owner.Motor.Stop();
                return;
            }

            if (GridCoord.Chebyshev(Owner.Cell, leader.Cell) <= FollowRadius)
            {
                Owner.Motor.Stop();
                return;
            }

            // Aim beside the leader, not at them, or the whole party queues up
            // trying to occupy one tile.
            if (World.TryFindMeleeSlot(Owner, leader, out GridCoord slot))
                MoveTowards(slot, stopAdjacent: false);
            else
                MoveTowards(leader.Cell, stopAdjacent: true);
        }

        // --- ability partitioning ---------------------------------------------------------

        private System.Collections.Generic.List<AbilityDefinition> _healing;
        private System.Collections.Generic.List<AbilityDefinition> _damage;

        /// <summary>
        /// Splits the class kit into "things that help allies" and "things that
        /// hurt enemies" by looking at what the abilities target. Cached, because
        /// the kit never changes at runtime.
        /// </summary>
        private void PartitionAbilities()
        {
            _healing = new System.Collections.Generic.List<AbilityDefinition>(4);
            _damage = new System.Collections.Generic.List<AbilityDefinition>(6);

            if (_class == null) return;

            for (int i = 0; i < _class.Abilities.Count; i++)
            {
                AbilityDefinition a = _class.Abilities[i];
                if (a == null) continue;

                bool friendly = a.AffectsFaction == AffectsFaction.Allies
                                || a.TargetKind == TargetKind.Ally
                                || a.TargetKind == TargetKind.Self;

                if (friendly) _healing.Add(a);
                else _damage.Add(a);
            }
        }

        private System.Collections.Generic.List<AbilityDefinition> HealingAbilities()
        {
            if (_healing == null) PartitionAbilities();
            return _healing;
        }

        private System.Collections.Generic.List<AbilityDefinition> DamageAbilities()
        {
            if (_damage == null) PartitionAbilities();
            return _damage;
        }
    }
}
