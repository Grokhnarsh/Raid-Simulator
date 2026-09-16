using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.Items;
using EmberDepths.Gameplay.Run;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDepths.Gameplay.UI
{
    /// <summary>
    /// The reward half of the HUD: the ember purse, a feed of what just
    /// dropped, and a character panel showing what everyone is wearing.
    ///
    /// Split out of <see cref="DungeonHud"/> rather than added to it, because
    /// the combat HUD and the loot HUD change for completely different reasons.
    /// It is a plain class, not a component — it borrows the existing canvas
    /// instead of adding a second one.
    ///
    /// The feed matters more than it looks. Auto-equip means items go straight
    /// onto whoever benefits, and without a line saying so the player would
    /// watch their stats change for no visible reason.
    /// </summary>
    public sealed class LootHud
    {
        private const int MaxToasts = 6;
        private const float ToastFadeSeconds = 0.8f;

        private sealed class Toast
        {
            public Text Label;
            public float Remaining;
            public float Total;
        }

        private readonly DungeonRunner _runner;
        private readonly Font _font;
        private readonly Transform _canvas;

        private Text _emberLabel;
        private Image _emberIcon;
        private Text _upgradeHint;

        private readonly List<Toast> _toasts = new List<Toast>(MaxToasts);

        private GameObject _characterPanel;
        private Text _characterText;
        private bool _panelVisible;

        public Sprite EmberSprite { get; set; }

        public LootHud(DungeonRunner runner, Transform canvas, Font font)
        {
            _runner = runner;
            _font = font;
            _canvas = canvas;

            BuildPurse();
            BuildToastArea();
            BuildCharacterPanel();
        }

        // --- construction -------------------------------------------------------

        private void BuildPurse()
        {
            GameObject panel = Panel("Purse", new Vector2(1f, 1f), new Vector2(-16f, -16f),
                new Vector2(220f, 40f), new Vector2(1f, 1f));

            _emberIcon = Image(panel.transform, new Vector2(0f, 0.5f), new Vector2(10f, 0f),
                new Vector2(28f, 28f), new Vector2(0f, 0.5f), new Color(1f, 0.68f, 0.22f));

            _emberLabel = Label(panel.transform, "0", 20, TextAnchor.MiddleLeft,
                new Vector2(46f, 0f), new Vector2(160f, 28f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            _emberLabel.color = new Color(1f, 0.82f, 0.45f);

            _upgradeHint = Label(_canvas, "", 14, TextAnchor.UpperRight,
                new Vector2(-20f, -62f), new Vector2(420f, 22f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            _upgradeHint.color = new Color(1f, 1f, 1f, 0.6f);
        }

        private void BuildToastArea()
        {
            for (int i = 0; i < MaxToasts; i++)
            {
                Text label = Label(_canvas, "", 16, TextAnchor.UpperRight,
                    new Vector2(-20f, -92f - i * 22f), new Vector2(520f, 20f),
                    new Vector2(1f, 1f), new Vector2(1f, 1f));

                label.gameObject.SetActive(false);
                _toasts.Add(new Toast { Label = label });
            }
        }

        private void BuildCharacterPanel()
        {
            _characterPanel = Panel("CharacterPanel", new Vector2(1f, 0.5f), new Vector2(-16f, 40f),
                new Vector2(440f, 330f), new Vector2(1f, 0.5f));

            _characterText = Label(_characterPanel.transform, "", 14, TextAnchor.UpperLeft,
                new Vector2(14f, -12f), new Vector2(412f, 306f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _characterPanel.SetActive(false);
        }

        // --- events ---------------------------------------------------------------

        public void OnItemLooted(ItemInstance item, Actor receiver)
        {
            if (item == null) return;

            string who = receiver != null ? receiver.DisplayName : "stash";
            Push($"{item.DisplayName}  →  {who}", item.TintColour, item.Rarity.ToastSeconds());
        }

        public void OnEmbersLooted(int amount)
        {
            // Ember pickups are constant; showing every one would bury the item
            // lines that actually matter.
            if (amount >= 25) Push($"+{amount} embers", new Color(1f, 0.72f, 0.3f), 2f);
        }

        public void OnItemUpgraded(ItemInstance item)
        {
            if (item == null) return;
            Push($"Reforged: {item.DisplayName}", new Color(0.6f, 0.9f, 1f), 3f);
        }

        private void Push(string message, Color colour, float seconds)
        {
            // Newest at the top: shift everything down one slot and reuse the
            // labels, so the feed never allocates during a fight.
            for (int i = _toasts.Count - 1; i > 0; i--)
            {
                _toasts[i].Label.text = _toasts[i - 1].Label.text;
                _toasts[i].Label.color = _toasts[i - 1].Label.color;
                _toasts[i].Remaining = _toasts[i - 1].Remaining;
                _toasts[i].Total = _toasts[i - 1].Total;
                _toasts[i].Label.gameObject.SetActive(_toasts[i].Remaining > 0f);
            }

            Toast head = _toasts[0];
            head.Label.text = message;
            head.Label.color = colour;
            head.Remaining = seconds;
            head.Total = seconds;
            head.Label.gameObject.SetActive(true);
        }

        // --- per-frame -------------------------------------------------------------

        public void Update()
        {
            UpdatePurse();
            UpdateToasts();
            if (_panelVisible) UpdateCharacterPanel();
        }

        public void TogglePanel()
        {
            _panelVisible = !_panelVisible;
            _characterPanel.SetActive(_panelVisible);
            if (_panelVisible) UpdateCharacterPanel();
        }

        private void UpdatePurse()
        {
            Inventory inventory = _runner != null ? _runner.Inventory : null;

            if (_emberIcon != null && EmberSprite != null && _emberIcon.sprite != EmberSprite)
            {
                _emberIcon.sprite = EmberSprite;
                _emberIcon.color = Color.white;
            }

            if (inventory == null)
            {
                _emberLabel.text = "0";
                _upgradeHint.text = "";
                return;
            }

            _emberLabel.text = inventory.Embers.ToString();

            ItemInstance next = FindUpgradeCandidate(inventory);
            _upgradeHint.text = next != null
                ? $"[U] Reforge {next.DisplayName}  ·  {next.NextUpgradeCost} embers      [C] Gear"
                : "[C] Gear";
        }

        private ItemInstance FindUpgradeCandidate(Inventory inventory)
        {
            var sets = new List<Equipment>(_runner.Party.Count);
            for (int i = 0; i < _runner.Party.Count; i++)
                if (_runner.Party[i] != null) sets.Add(_runner.Party[i].Equipment);

            return inventory.FindBestUpgradeCandidate(sets);
        }

        private void UpdateToasts()
        {
            for (int i = 0; i < _toasts.Count; i++)
            {
                Toast toast = _toasts[i];
                if (toast.Remaining <= 0f) continue;

                toast.Remaining -= Time.deltaTime;

                if (toast.Remaining <= 0f)
                {
                    toast.Label.gameObject.SetActive(false);
                    continue;
                }

                Color c = toast.Label.color;
                c.a = Mathf.Clamp01(toast.Remaining / ToastFadeSeconds);
                toast.Label.color = c;
            }
        }

        private readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(1024);

        private void UpdateCharacterPanel()
        {
            _sb.Clear();

            IReadOnlyList<Actor> party = _runner != null ? _runner.Party : null;
            if (party == null || party.Count == 0)
            {
                _characterText.text = "No party.";
                return;
            }

            // The selected character in full, everyone else on one line. Five
            // full inventories do not fit on screen, and four of them are not
            // what the player opened the panel to read.
            Actor focus = _runner.LocalPlayer ?? party[0];
            AppendDetailed(focus);

            _sb.AppendLine();
            _sb.AppendLine("── party ──");

            for (int i = 0; i < party.Count; i++)
            {
                Actor member = party[i];
                if (member?.Equipment == null || member == focus) continue;
                AppendSummary(member);
            }

            _characterText.text = _sb.ToString();
        }

        private void AppendDetailed(Actor member)
        {
            if (member?.Equipment == null) return;

            _sb.AppendLine($"{member.DisplayName}   ilvl {member.Equipment.AverageItemLevel:0.0}");

            for (int s = 0; s < Equipment.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                ItemInstance item = member.Equipment.Get(slot);
                _sb.Append($"  {slot,-8} ");
                _sb.AppendLine(item != null ? item.DisplayName : "—");
            }

            foreach (KeyValuePair<ItemSetDefinition, int> pair in member.Equipment.ActiveSets)
            {
                int next = pair.Key.NextThreshold(pair.Value);
                _sb.Append($"  {pair.Key.DisplayName} ({pair.Value}/{pair.Key.Members.Count})");
                if (next > 0) _sb.Append($" — next at {next}");
                _sb.AppendLine();

                foreach (SetBonusTier tier in pair.Key.ActiveTiers(pair.Value))
                    _sb.AppendLine($"    + {tier.Description}");
            }
        }

        private void AppendSummary(Actor member)
        {
            _sb.Append($"  {member.DisplayName,-10} ilvl {member.Equipment.AverageItemLevel,4:0.0}");

            foreach (KeyValuePair<ItemSetDefinition, int> pair in member.Equipment.ActiveSets)
                _sb.Append($"  {pair.Key.DisplayName} {pair.Value}/{pair.Key.Members.Count}");

            _sb.AppendLine();
        }

        // --- widget helpers ----------------------------------------------------------

        private GameObject Panel(string label, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            var go = new GameObject(label);
            go.transform.SetParent(_canvas, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.05f, 0.04f, 0.05f, 0.78f);
            image.raycastTarget = false;
            return go;
        }

        private Image Image(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, Color colour)
        {
            var go = new GameObject("img");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private Text Label(Transform parent, string text, int size, TextAnchor anchor,
            Vector2 position, Vector2 dimensions, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("txt");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            var label = go.AddComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.alignment = anchor;
            label.text = text;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }
    }
}
