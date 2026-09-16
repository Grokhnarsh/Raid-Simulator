using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Gameplay.Presentation;
using UnityEngine;

namespace EmberDepths.Gameplay.Items
{
    /// <summary>
    /// A dropped item or pile of embers, lying on the floor waiting to be
    /// walked over.
    ///
    /// Presentation only — the pickup decision lives in <see cref="LootSystem"/>
    /// so that it happens on the simulation tick rather than whenever a frame
    /// lands. What this component contributes is the part that makes loot feel
    /// like loot: a rarity-coloured glow and a slow bob that catches the eye on
    /// a busy floor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootDrop : MonoBehaviour
    {
        private const float BobHeight = 0.12f;
        private const float BobSpeed = 2.2f;

        public ItemInstance Item { get; private set; }
        public int Embers { get; private set; }
        public GridCoord Cell { get; private set; }

        private SpriteRenderer _icon;
        private SpriteRenderer _glow;
        private Vector3 _restPosition;
        private float _phase;

        public bool IsEmbers => Item == null && Embers > 0;

        public void Initialise(ItemInstance item, int embers, GridCoord cell, Transform parent)
        {
            Item = item;
            Embers = embers;
            Cell = cell;

            transform.SetParent(parent, false);
            _restPosition = IsoGrid.CellToWorld(cell);
            transform.position = _restPosition;

            Color tint = item != null ? item.TintColour : new Color(1f, 0.68f, 0.22f);

            // The glow sits on the floor rather than on the item, so it reads as
            // a marker on the ground instead of a second sprite floating in air.
            _glow = CreateRenderer("Glow", SortingLayers.GroundDecal, 5);
            _glow.sprite = PrimitiveSprites.CellDiamond;
            _glow.color = new Color(tint.r, tint.g, tint.b, 0.30f);

            _icon = CreateRenderer("Icon", SortingLayers.Entities, 0);
            _icon.sprite = item?.Definition.Icon != null
                ? item.Definition.Icon
                : PrimitiveSprites.CellDiamondOutline;
            _icon.color = tint;
            _icon.transform.localScale = Vector3.one * (item != null ? 0.55f : 0.4f);

            // Offset the phase per cell so a pile of drops does not pulse in
            // lockstep like one object.
            _phase = (cell.X * 7 + cell.Y * 13) % 10 * 0.31f;
        }

        private SpriteRenderer CreateRenderer(string label, string sortingLayer, int order)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void Update()
        {
            _phase += Time.deltaTime * BobSpeed;
            float bob = Mathf.Sin(_phase) * BobHeight;

            if (_icon != null) _icon.transform.localPosition = new Vector3(0f, 0.35f + bob, 0f);

            if (_glow != null)
            {
                Color c = _glow.color;
                c.a = 0.22f + 0.12f * Mathf.Sin(_phase * 0.8f);
                _glow.color = c;
            }
        }
    }
}
