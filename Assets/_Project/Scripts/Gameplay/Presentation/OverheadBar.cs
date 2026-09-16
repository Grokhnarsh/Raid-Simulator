using EmberDepths.Content;
using EmberDepths.Gameplay.Actors;
using UnityEngine;

namespace EmberDepths.Gameplay.Presentation
{
    /// <summary>
    /// The health bar that floats over an actor.
    ///
    /// Built from generated sprites rather than world-space Canvases: a canvas
    /// per actor is expensive, and these need to sort with the rest of the scene
    /// anyway. Hidden while an enemy is at full health so an untouched room does
    /// not look like a spreadsheet.
    /// </summary>
    public sealed class OverheadBar : MonoBehaviour
    {
        private const float BarHeight = 0.09f;
        private const float BorderPadding = 0.03f;

        private Actor _actor;
        private SpriteRenderer _background;
        private SpriteRenderer _fill;
        private SpriteRenderer _shield;
        private SpriteRenderer _cast;

        private float _width;

        public void Bind(Actor actor, float width, float heightOffset)
        {
            _actor = actor;
            _width = width;

            transform.localPosition = new Vector3(0f, heightOffset, 0f);

            _background = CreateBar(new Color(0.04f, 0.03f, 0.03f, 0.85f), 0,
                width + BorderPadding, BarHeight + BorderPadding);

            _fill = CreateBar(HealthColour(actor), 1, width, BarHeight);
            _shield = CreateBar(new Color(0.85f, 0.92f, 1f, 0.85f), 2, width, BarHeight * 0.4f);
            _shield.transform.localPosition = new Vector3(0f, BarHeight * 0.5f, 0f);

            _cast = CreateBar(new Color(1f, 0.82f, 0.32f, 0.95f), 3, width, BarHeight * 0.5f);
            _cast.transform.localPosition = new Vector3(0f, -BarHeight, 0f);
            _cast.enabled = false;
        }

        private static Color HealthColour(Actor actor)
        {
            if (actor.Faction == Faction.Party) return new Color(0.36f, 0.78f, 0.76f);
            return actor.Rank switch
            {
                EnemyRank.Boss => new Color(0.85f, 0.16f, 0.12f),
                EnemyRank.Elite => new Color(0.93f, 0.45f, 0.14f),
                _ => new Color(0.72f, 0.24f, 0.18f)
            };
        }

        private SpriteRenderer CreateBar(Color colour, int order, float width, float height)
        {
            var go = new GameObject("bar");
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(width, height, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprites.White;
            sr.color = colour;
            sr.sortingLayerName = SortingLayers.Overhead;
            sr.sortingOrder = order;
            return sr;
        }

        private void LateUpdate()
        {
            if (_actor == null) return;

            bool visible = _actor.IsAlive && (_actor.Faction == Faction.Party
                                              || _actor.HealthFraction < 0.999f
                                              || _actor.Abilities.IsCasting);

            _background.enabled = visible;
            _fill.enabled = visible;

            if (!visible)
            {
                _shield.enabled = false;
                _cast.enabled = false;
                return;
            }

            SetFill(_fill, _actor.HealthFraction, _width);

            float shieldFraction = _actor.MaxHealth > 0f
                ? Mathf.Clamp01(_actor.Shield / _actor.MaxHealth)
                : 0f;

            _shield.enabled = shieldFraction > 0.001f;
            if (_shield.enabled) SetFill(_shield, shieldFraction, _width);

            bool casting = _actor.Abilities.IsCasting;
            _cast.enabled = casting;
            if (casting) SetFill(_cast, _actor.Abilities.CastProgress(_actor.World.Clock.Tick), _width);
        }

        /// <summary>
        /// Scales the bar and shifts it left by the same amount, so it drains
        /// from the right instead of shrinking towards its centre.
        /// </summary>
        private static void SetFill(SpriteRenderer bar, float fraction, float fullWidth)
        {
            fraction = Mathf.Clamp01(fraction);

            Vector3 scale = bar.transform.localScale;
            scale.x = fullWidth * fraction;
            bar.transform.localScale = scale;

            Vector3 pos = bar.transform.localPosition;
            pos.x = -fullWidth * 0.5f * (1f - fraction);
            bar.transform.localPosition = pos;
        }
    }
}
