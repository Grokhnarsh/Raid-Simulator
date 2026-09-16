using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.AI;
using EmberDepths.Gameplay.Party;
using EmberDepths.Gameplay.Run;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDepths.Gameplay.UI
{
    /// <summary>
    /// The in-run HUD: party frames, the boss frame, the action bar and the
    /// banner.
    ///
    /// Built entirely from code. That is a deliberate trade for a project at this
    /// stage — the layout is readable and diffable, and nobody has to keep a
    /// prefab in sync while the party size and ability count are still moving.
    /// When the visual design firms up, replace this class with a prefab or a UI
    /// Toolkit document; nothing outside it will need to change, because it only
    /// ever reads from <see cref="DungeonRunner"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonHud : MonoBehaviour
    {
        private sealed class PartyFrame
        {
            public Actor Actor;
            public GameObject Root;
            public Text Name;
            public Image HealthFill;
            public Image ResourceFill;
            public Image Highlight;
        }

        private sealed class AbilitySlot
        {
            public AbilityDefinition Ability;
            public Image Background;
            public Image CooldownOverlay;
            public Text Key;
            public Text Label;
        }

        public DungeonRunner Runner;
        public LocalPlayerController Player;

        [Tooltip("Shown next to the ember count. Assigned by the scene builder.")]
        public Sprite EmberIcon;

        private LootHud _loot;

        private static readonly Color PanelColour = new Color(0.05f, 0.04f, 0.05f, 0.78f);
        private static readonly Color HealthColour = new Color(0.36f, 0.78f, 0.76f);
        private static readonly Color ResourceColour = new Color(0.42f, 0.55f, 0.85f);
        private static readonly Color BossColour = new Color(0.85f, 0.18f, 0.12f);
        private static readonly Color EmberColour = new Color(1f, 0.62f, 0.24f);

        private readonly List<PartyFrame> _frames = new List<PartyFrame>(5);
        private readonly List<AbilitySlot> _slots = new List<AbilitySlot>(6);

        private Font _font;
        private Canvas _canvas;

        private GameObject _bossPanel;
        private Text _bossName;
        private Image _bossFill;
        private Text _bossPhase;

        private Image _castFill;
        private Text _castLabel;
        private GameObject _castPanel;

        private Text _banner;
        private float _bannerRemaining;

        private Actor _trackedBoss;
        private BossBrain _trackedBossBrain;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
            BuildPartyPanel();
            BuildBossPanel();
            BuildActionBar();
            BuildBanner();

            _loot = new LootHud(Runner, _canvas.transform, _font) { EmberSprite = EmberIcon };
        }

        private void OnEnable()
        {
            if (Runner == null) return;
            Runner.RunStarted += OnRunStarted;
            Runner.RunEnded += OnRunEnded;
        }

        private void OnDisable()
        {
            if (Runner == null) return;
            Runner.RunStarted -= OnRunStarted;
            Runner.RunEnded -= OnRunEnded;
        }

        // --- construction -----------------------------------------------------------

        private void BuildCanvas()
        {
            var go = new GameObject("HUD Canvas");
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Match height: the HUD is anchored top and bottom, so vertical space
            // is what must stay consistent across aspect ratios.
            scaler.matchWidthOrHeight = 1f;

            go.AddComponent<GraphicRaycaster>();
        }

        private void BuildPartyPanel()
        {
            GameObject panel = MakePanel("PartyFrames", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -16f), new Vector2(300f, 5 * 62f + 8f), Vector2.up + Vector2.zero);

            var rect = panel.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0f, 1f);

            for (int i = 0; i < 5; i++)
            {
                var frame = new PartyFrame();
                float y = -8f - i * 62f;

                frame.Root = MakePanel($"Frame{i}", new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(6f, y), new Vector2(288f, 56f), new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0.35f));
                frame.Root.transform.SetParent(panel.transform, false);

                frame.Highlight = MakeImage(frame.Root.transform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(0f, 0.5f), EmberColour);

                frame.Name = MakeLabel(frame.Root.transform, "Name", 16, TextAnchor.UpperLeft,
                    new Vector2(10f, -4f), new Vector2(200f, 20f));

                MakeImage(frame.Root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(10f, -26f), new Vector2(268f, 14f), new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0.6f));

                frame.HealthFill = MakeImage(frame.Root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(10f, -26f), new Vector2(268f, 14f), new Vector2(0f, 1f), HealthColour);
                MakeFilled(frame.HealthFill);

                MakeImage(frame.Root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(10f, -43f), new Vector2(268f, 7f), new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0.6f));

                frame.ResourceFill = MakeImage(frame.Root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(10f, -43f), new Vector2(268f, 7f), new Vector2(0f, 1f), ResourceColour);
                MakeFilled(frame.ResourceFill);

                frame.Root.SetActive(false);
                _frames.Add(frame);
            }
        }

        private void BuildBossPanel()
        {
            _bossPanel = MakePanel("BossFrame", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(720f, 62f), new Vector2(0.5f, 1f));

            _bossName = MakeLabel(_bossPanel.transform, "Boss", 20, TextAnchor.UpperCenter,
                new Vector2(0f, -4f), new Vector2(700f, 24f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            MakeImage(_bossPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(700f, 20f), new Vector2(0.5f, 1f), new Color(0f, 0f, 0f, 0.65f));

            _bossFill = MakeImage(_bossPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(700f, 20f), new Vector2(0.5f, 1f), BossColour);
            MakeFilled(_bossFill);

            _bossPhase = MakeLabel(_bossPanel.transform, "", 14, TextAnchor.UpperCenter,
                new Vector2(0f, -52f), new Vector2(700f, 18f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            _bossPhase.color = EmberColour;

            _bossPanel.SetActive(false);
        }

        private void BuildActionBar()
        {
            const int slots = 6;
            const float size = 64f;
            const float gap = 8f;
            float totalWidth = slots * size + (slots - 1) * gap;

            GameObject bar = MakePanel("ActionBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(totalWidth + 12f, size + 12f), new Vector2(0.5f, 0f));

            for (int i = 0; i < slots; i++)
            {
                float x = -totalWidth * 0.5f + size * 0.5f + i * (size + gap);
                var slot = new AbilitySlot();

                slot.Background = MakeImage(bar.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f), new Vector2(size, size), new Vector2(0.5f, 0.5f),
                    new Color(0.13f, 0.11f, 0.12f, 0.95f));

                slot.CooldownOverlay = MakeImage(slot.Background.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(size, size), new Vector2(0.5f, 0.5f), new Color(0f, 0f, 0f, 0.72f));
                slot.CooldownOverlay.type = Image.Type.Filled;
                slot.CooldownOverlay.fillMethod = Image.FillMethod.Radial360;
                slot.CooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
                slot.CooldownOverlay.fillClockwise = false;

                slot.Label = MakeLabel(slot.Background.transform, "", 12, TextAnchor.MiddleCenter,
                    Vector2.zero, new Vector2(size - 4f, size - 18f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

                slot.Key = MakeLabel(slot.Background.transform, (i + 1).ToString(), 14, TextAnchor.LowerRight,
                    new Vector2(-4f, 2f), new Vector2(20f, 18f), new Vector2(1f, 0f), new Vector2(1f, 0f));
                slot.Key.color = new Color(1f, 1f, 1f, 0.7f);

                _slots.Add(slot);
            }

            _castPanel = MakePanel("CastBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 100f), new Vector2(340f, 24f), new Vector2(0.5f, 0f));

            _castFill = MakeImage(_castPanel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(4f, 0f), new Vector2(332f, 16f), new Vector2(0f, 0.5f), EmberColour);
            MakeFilled(_castFill);

            _castLabel = MakeLabel(_castPanel.transform, "", 14, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(332f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            _castPanel.SetActive(false);
        }

        private void BuildBanner()
        {
            _banner = MakeLabel(_canvas.transform, "", 44, TextAnchor.MiddleCenter,
                new Vector2(0f, 120f), new Vector2(1200f, 80f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _banner.color = EmberColour;
            _banner.gameObject.SetActive(false);
        }

        // --- run wiring --------------------------------------------------------------

        private void OnRunStarted(EmberDepths.Gameplay.World.DungeonWorld world)
        {
            for (int i = 0; i < _frames.Count; i++)
            {
                Actor member = i < Runner.Party.Count ? Runner.Party[i] : null;
                _frames[i].Actor = member;
                _frames[i].Root.SetActive(member != null);

                if (member == null) continue;
                _frames[i].Name.text = member.DisplayName;
                _frames[i].HealthFill.color = member.ClassDefinition != null
                    ? member.ClassDefinition.AccentColor
                    : HealthColour;
            }

            if (Runner.Director != null) Runner.Director.BossEngaged += OnBossEngaged;

            if (Runner.Loot != null)
            {
                Runner.Loot.ItemLooted += _loot.OnItemLooted;
                Runner.Loot.EmbersLooted += _loot.OnEmbersLooted;
            }

            if (Runner.Inventory != null) Runner.Inventory.ItemUpgraded += _loot.OnItemUpgraded;

            ShowBanner(Runner.Dungeon != null ? Runner.Dungeon.DisplayName : "Descend", 3f);
        }

        private void OnRunEnded(RunState state)
        {
            _trackedBoss = null;
            _trackedBossBrain = null;
            _bossPanel.SetActive(false);

            ShowBanner(state switch
            {
                RunState.Cleared => "Dungeon cleared",
                RunState.Wiped => "The party has fallen",
                _ => ""
            }, 6f);
        }

        private void OnBossEngaged(Actor boss)
        {
            _trackedBoss = boss;
            _trackedBossBrain = null;

            var brains = boss.World.Brains;
            for (int i = 0; i < brains.Count; i++)
            {
                if (brains[i].Owner != boss || !(brains[i] is BossBrain bb)) continue;
                _trackedBossBrain = bb;
                bb.PhaseChanged += OnBossPhaseChanged;
                bb.Enraged += () => ShowBanner("ENRAGED", 3f);
                break;
            }

            _bossName.text = boss.DisplayName;
            _bossPanel.SetActive(true);
        }

        private void OnBossPhaseChanged(BossPhase phase, int index)
        {
            _bossPhase.text = phase != null ? phase.Name : "";
            if (phase != null && !string.IsNullOrWhiteSpace(phase.BannerText))
                ShowBanner(phase.BannerText, 3.5f);
        }

        /// <summary>
        /// Shows or hides the gear panel. Public so a button, a gamepad binding
        /// or a test can open it without going through the keyboard.
        /// </summary>
        public void ToggleGearPanel() => _loot?.TogglePanel();

        public void ShowBanner(string text, float seconds)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _banner.text = text;
            _banner.gameObject.SetActive(true);
            _bannerRemaining = seconds;
        }

        // --- per-frame refresh -----------------------------------------------------------

        private void Update()
        {
            HandleHudKeys();
            UpdatePartyFrames();
            UpdateBossFrame();
            UpdateActionBar();
            UpdateBanner();
            _loot?.Update();
        }

        /// <summary>
        /// Keys that act on the HUD rather than on the character. Kept here and
        /// not in <see cref="LocalPlayerController"/> so that the controller
        /// stays a pure source of actor intents — which is what makes it
        /// replaceable by a network message later.
        /// </summary>
        private void HandleHudKeys()
        {
            if (Runner == null) return;

            if (Input.GetKeyDown(KeyCode.C)) _loot?.TogglePanel();

            if (Input.GetKeyDown(KeyCode.U) && Runner.UpgradeBestItem() == null && Runner.Inventory != null)
                ShowBanner("Not enough embers", 1.2f);
        }

        private void UpdatePartyFrames()
        {
            Actor local = Runner != null ? Runner.LocalPlayer : null;

            for (int i = 0; i < _frames.Count; i++)
            {
                PartyFrame frame = _frames[i];
                if (frame.Actor == null) continue;

                frame.HealthFill.fillAmount = frame.Actor.HealthFraction;
                frame.ResourceFill.fillAmount = frame.Actor.Stats.MaxResource > 0f
                    ? Mathf.Clamp01(frame.Actor.Resource / frame.Actor.Stats.MaxResource)
                    : 0f;

                // Dead members grey out rather than disappear: the player needs to
                // know who is down, not just who is left.
                Color tint = frame.Actor.IsAlive ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                frame.Name.color = tint;

                frame.Highlight.enabled = frame.Actor == local;
            }
        }

        private void UpdateBossFrame()
        {
            if (_trackedBoss == null) return;

            if (!_trackedBoss.IsAlive)
            {
                _bossPanel.SetActive(false);
                _trackedBoss = null;
                return;
            }

            _bossFill.fillAmount = _trackedBoss.HealthFraction;

            if (_trackedBossBrain != null && _trackedBossBrain.CurrentPhase != null)
                _bossPhase.text = _trackedBossBrain.CurrentPhase.Name;
        }

        private void UpdateActionBar()
        {
            Actor player = Runner != null ? Runner.LocalPlayer : null;
            if (player == null) return;

            int tick = player.World.Clock.Tick;
            IReadOnlyList<AbilityDefinition> abilities = player.Abilities.Abilities;

            for (int i = 0; i < _slots.Count; i++)
            {
                AbilitySlot slot = _slots[i];
                AbilityDefinition ability = i < abilities.Count ? abilities[i] : null;
                slot.Ability = ability;

                if (ability == null)
                {
                    slot.Background.color = new Color(0.1f, 0.09f, 0.1f, 0.55f);
                    slot.Label.text = "";
                    slot.CooldownOverlay.fillAmount = 0f;
                    continue;
                }

                slot.Label.text = ability.DisplayName;

                float remaining = player.Abilities.CooldownRemaining(ability, tick);
                float total = Mathf.Max(0.01f, ability.Cooldown / Mathf.Max(0.1f, player.Stats.Haste));
                slot.CooldownOverlay.fillAmount = Mathf.Clamp01(remaining / total);

                bool affordable = ability.ResourceCost <= player.Resource;
                slot.Background.color = affordable
                    ? new Color(0.16f, 0.14f, 0.15f, 0.95f)
                    : new Color(0.22f, 0.09f, 0.09f, 0.95f);
            }

            bool casting = player.Abilities.IsCasting;
            _castPanel.SetActive(casting);
            if (!casting) return;

            _castFill.fillAmount = player.Abilities.CastProgress(tick);
            _castLabel.text = player.Abilities.CastingAbility != null
                ? player.Abilities.CastingAbility.DisplayName
                : "";
        }

        private void UpdateBanner()
        {
            if (!_banner.gameObject.activeSelf) return;

            _bannerRemaining -= Time.deltaTime;
            if (_bannerRemaining > 0f)
            {
                // Fade the last second so the banner leaves rather than blinks out.
                Color c = _banner.color;
                c.a = Mathf.Clamp01(_bannerRemaining);
                _banner.color = c;
                return;
            }

            _banner.gameObject.SetActive(false);
        }

        // --- tiny uGUI helpers ------------------------------------------------------------

        private GameObject MakePanel(string label, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, Vector2 pivot, Color? colour = null)
        {
            var go = new GameObject(label);
            go.transform.SetParent(_canvas.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = colour ?? PanelColour;
            image.raycastTarget = false;
            return go;
        }

        private Image MakeImage(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, Vector2 pivot, Color colour)
        {
            var go = new GameObject("img");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private Text MakeLabel(Transform parent, string text, int size, TextAnchor anchor,
            Vector2 position, Vector2 dimensions,
            Vector2? anchorMin = null, Vector2? anchorMax = null)
        {
            var go = new GameObject("txt");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin ?? new Vector2(0f, 1f);
            rect.anchorMax = anchorMax ?? new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
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

        /// <summary>Turns an Image into a left-to-right fill bar.</summary>
        private static void MakeFilled(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
        }
    }
}
