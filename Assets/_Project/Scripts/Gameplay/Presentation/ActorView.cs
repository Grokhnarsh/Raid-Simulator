using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.Combat;
using UnityEngine;

namespace EmberDepths.Gameplay.Presentation
{
    /// <summary>
    /// Draws one actor. Reads simulation state and never writes to it.
    ///
    /// The separation earns its keep in two places. Movement is simulated at
    /// 20 Hz but interpolated here, so the game looks smooth without the
    /// simulation caring about frame rate. And because nothing in Gameplay reads
    /// back from this component, a headless balance run can spawn actors with no
    /// view attached at all.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorView : MonoBehaviour
    {
        private const float HurtFlashSeconds = 0.12f;

        private Actor _actor;
        private SpriteRenderer _body;
        private SpriteRenderer _shadow;
        private ActorVisualSet _visuals;

        private ActorAnimState _state = ActorAnimState.Idle;
        private float _stateElapsed;

        private Vector2 _previousCell;
        private Vector2 _currentCell;

        private float _hurtFlashRemaining;
        private Color _baseColour = Color.white;

        public void Bind(Actor actor, float scale = 1f)
        {
            _actor = actor;
            _visuals = actor.Visuals;

            transform.localScale = Vector3.one * scale;

            _shadow = CreateRenderer("Shadow", SortingLayers.Shadows, 0);
            _shadow.sprite = _visuals != null && _visuals.Shadow != null ? _visuals.Shadow : PrimitiveSprites.Shadow;
            _shadow.color = new Color(1f, 1f, 1f, 0.75f);
            if (_visuals != null)
            {
                _shadow.transform.localPosition = _visuals.ShadowOffset;
                _shadow.transform.localScale = Vector3.one * _visuals.ShadowScale;
            }

            _body = CreateRenderer("Body", SortingLayers.Entities, 0);
            _body.sprite = _visuals != null ? _visuals.GetIdle(actor.Facing) : PrimitiveSprites.CellDiamond;

            // Without art the actor still needs to be locatable, so fall back to a
            // faction-tinted diamond rather than an invisible object.
            if (_visuals == null)
            {
                _baseColour = actor.Faction == Faction.Party
                    ? new Color(0.36f, 0.78f, 0.76f)
                    : new Color(0.93f, 0.35f, 0.13f);
                _body.color = _baseColour;
            }

            CreateOverheadBar(actor);

            _currentCell = _previousCell = new Vector2(actor.Cell.X, actor.Cell.Y);
            actor.transform.position = IsoGrid.CellToWorld(actor.Cell);

            actor.Damaged += OnDamaged;
            actor.Died += OnDied;
            actor.World.Clock.Ticked += OnSimTick;
        }

        private void CreateOverheadBar(Actor actor)
        {
            var go = new GameObject("OverheadBar");
            go.transform.SetParent(transform, false);

            float offset = _visuals != null ? _visuals.OverheadOffset : 1.1f;

            // Bosses get a wider bar so their health reads at a glance from
            // across the arena; the on-screen boss frame carries the detail.
            float width = actor.Rank switch
            {
                EnemyRank.Boss => 1.8f,
                EnemyRank.Elite => 1.2f,
                _ => 0.9f
            };

            go.AddComponent<OverheadBar>().Bind(actor, width, offset);
        }

        private SpriteRenderer CreateRenderer(string label, string sortingLayer, int order)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
            return sr;
        }

        private void OnDestroy()
        {
            if (_actor == null) return;
            _actor.Damaged -= OnDamaged;
            _actor.Died -= OnDied;
            if (_actor.World != null) _actor.World.Clock.Ticked -= OnSimTick;
        }

        /// <summary>Snapshots the simulation position so LateUpdate can interpolate between ticks.</summary>
        private void OnSimTick(int tick)
        {
            _previousCell = _currentCell;
            _currentCell = _actor.Motor.FractionalCell;
        }

        private void LateUpdate()
        {
            if (_actor == null) return;

            float alpha = _actor.World.Clock.Alpha;
            Vector2 shown = Vector2.Lerp(_previousCell, _currentCell, alpha);
            _actor.transform.position = IsoGrid.CellToWorld(shown);

            UpdateState();
            UpdateSprite();
            UpdateTint();
        }

        private void UpdateState()
        {
            ActorAnimState desired;

            if (!_actor.IsAlive) desired = ActorAnimState.Die;
            else if (_actor.Abilities.IsCasting) desired = ActorAnimState.Cast;
            else if (_actor.Motor.IsMoving) desired = ActorAnimState.Walk;
            else desired = ActorAnimState.Idle;

            if (desired != _state)
            {
                _state = desired;
                _stateElapsed = 0f;
            }
            else
            {
                _stateElapsed += Time.deltaTime;
            }
        }

        private void UpdateSprite()
        {
            if (_visuals == null) return;

            Sprite sprite = _visuals.Sample(_state, _actor.Facing, _stateElapsed, out bool finished);
            if (sprite != null) _body.sprite = sprite;

            // Hold the last frame of a death animation rather than snapping back
            // to idle, then let the corpse linger for the runner to clean up.
            if (_state == ActorAnimState.Die && finished) enabled = false;
        }

        private void UpdateTint()
        {
            if (_hurtFlashRemaining <= 0f)
            {
                if (_visuals != null) _body.color = _baseColour;
                return;
            }

            _hurtFlashRemaining -= Time.deltaTime;
            float t = Mathf.Clamp01(_hurtFlashRemaining / HurtFlashSeconds);
            _body.color = Color.Lerp(_baseColour, Color.white, t);
        }

        private void OnDamaged(Actor actor, DamageResult result)
        {
            _hurtFlashRemaining = HurtFlashSeconds;
        }

        private void OnDied(Actor actor, Actor killer)
        {
            if (_shadow != null) _shadow.enabled = false;

            // Dead actors must never occlude living ones, so drop them a layer.
            if (_body != null) _body.sortingLayerName = SortingLayers.GroundDecal;
        }
    }
}
