using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WOF
{
    /// <summary>React-style local pause overlay and hold-to-view multiplayer roster.</summary>
    public sealed class WofPauseAndScoreboardRuntime : MonoBehaviour
    {
        private const int MaximumScoreRows = 16;

        [SerializeField] private WofHud hud;
        [SerializeField] private Transform uiParent;
        [SerializeField] private Font font;

        private GameObject _pauseRoot;
        private GameObject _pauseHomeRoot;
        private GameObject _settingsRoot;
        private GameObject _scoreboardRoot;
        private Button[] _pauseButtons;
        private WofSettingsPanelRuntime _settingsPanel;
        private Text[] _scoreRows;
        private GameObject[] _scoreRowPanels;
        private int _pauseSelection;
        private float _nextRosterRefreshAt;
        private bool _settingsOpen;
        private bool _pauseProbe;
        private bool _scoreboardProbe;
        private bool _probeApplied;

        public static WofPauseAndScoreboardRuntime Instance { get; private set; }
        public static bool IsPauseOpen => Instance != null && Instance._pauseRoot != null && Instance._pauseRoot.activeSelf;
        public static bool IsScoreboardOpen => Instance != null && Instance._scoreboardRoot != null && Instance._scoreboardRoot.activeSelf;
        public static bool IsAnyMenuOpen => IsPauseOpen || IsScoreboardOpen;

        public void ConfigureGeneratedView(WofHud generatedHud, Transform generatedUiParent, Font generatedFont)
        {
            hud = generatedHud;
            uiParent = generatedUiParent;
            font = generatedFont;
        }

        private void Awake()
        {
            Instance = this;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-pause-probe", StringComparison.OrdinalIgnoreCase)) _pauseProbe = true;
                if (argument.Equals("--wof-scoreboard-probe", StringComparison.OrdinalIgnoreCase)) _scoreboardProbe = true;
            }
        }

        private void Start()
        {
            BuildView();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_pauseRoot == null || _scoreboardRoot == null || hud == null) return;

            if (!_probeApplied && hud.IsGameplayVisible && (_pauseProbe || _scoreboardProbe))
            {
                _probeApplied = true;
                if (_pauseProbe) SetPauseOpen(true);
                else SetScoreboardOpen(true);
            }

            if (IsPauseOpen)
            {
                UpdatePauseInput();
                return;
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if (IsScoreboardOpen)
            {
                var stillHeld = (keyboard?.tabKey.isPressed ?? false) ||
                                WofControllerBindings.IsPressed(gamepad, WofControllerActions.Scoreboard);
                if (!_scoreboardProbe && !stillHeld)
                {
                    SetScoreboardOpen(false);
                    return;
                }
                if (Time.unscaledTime >= _nextRosterRefreshAt) RefreshScoreboard();
                return;
            }
            var canOpenOverlay = hud.IsGameplayVisible && !WofInputRouter.GameplaySuppressed &&
                                 !WofSpellMenuRuntime.IsOpen && !WofNavigationMapRuntime.IsExpanded;
            if (canOpenOverlay &&
                ((keyboard?.escapeKey.wasPressedThisFrame ?? false) ||
                 WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.Pause)))
            {
                SetPauseOpen(true);
                return;
            }

            var scoreboardHeld = canOpenOverlay &&
                                 ((keyboard?.tabKey.isPressed ?? false) ||
                                  WofControllerBindings.IsPressed(gamepad, WofControllerActions.Scoreboard));
            if (scoreboardHeld)
            {
                SetScoreboardOpen(true);
            }
        }

        public void TogglePause()
        {
            SetPauseOpen(!IsPauseOpen);
        }

        private void UpdatePauseInput()
        {
            if (_settingsOpen)
            {
                UpdateSettingsInput();
                return;
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if ((keyboard?.escapeKey.wasPressedThisFrame ?? false) ||
                WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.Pause) ||
                WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.MenuBack))
            {
                SetPauseOpen(false);
                return;
            }

            if ((keyboard?.upArrowKey.wasPressedThisFrame ?? false) ||
                (gamepad?.dpad.up.wasPressedThisFrame ?? false))
            {
                SetPauseSelection(_pauseSelection - 1);
            }
            else if ((keyboard?.downArrowKey.wasPressedThisFrame ?? false) ||
                     (gamepad?.dpad.down.wasPressedThisFrame ?? false))
            {
                SetPauseSelection(_pauseSelection + 1);
            }

            if ((keyboard?.enterKey.wasPressedThisFrame ?? false) ||
                WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.MenuSelect))
            {
                if (_pauseSelection == 0) SetPauseOpen(false);
                else SetSettingsOpen(true);
            }
        }

        private void UpdateSettingsInput()
        {
            _settingsPanel?.HandleInput();
        }

        private void SetPauseOpen(bool open)
        {
            if (_pauseRoot == null || _pauseRoot.activeSelf == open) return;
            if (open && IsScoreboardOpen) SetScoreboardOpen(false);
            _pauseRoot.SetActive(open);
            hud.SetGameplaySurfaceBlocked(open);
            WofInputRouter.SetGameplaySuppressed(open);
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            if (open)
            {
                SetSettingsOpen(false);
                SetPauseSelection(0);
                EventSystem.current?.SetSelectedGameObject(_pauseButtons?[0]?.gameObject);
            }
            else
            {
                EventSystem.current?.SetSelectedGameObject(null);
            }
            Debug.Log($"[WOF-AUTOMATION] PAUSE_MENU open={open}");
        }

        private void SetScoreboardOpen(bool open)
        {
            if (_scoreboardRoot == null || _scoreboardRoot.activeSelf == open) return;
            _scoreboardRoot.SetActive(open);
            WofInputRouter.SetGameplaySuppressed(open);
            if (open) RefreshScoreboard();
            Debug.Log($"[WOF-AUTOMATION] SCOREBOARD_MENU open={open}");
        }

        private void SetPauseSelection(int selection)
        {
            if (_pauseButtons == null || _pauseButtons.Length == 0) return;
            _pauseSelection = ((selection % _pauseButtons.Length) + _pauseButtons.Length) % _pauseButtons.Length;
            for (var index = 0; index < _pauseButtons.Length; index++)
            {
                _pauseButtons[index].GetComponent<Image>().color = index == _pauseSelection
                    ? new Color32(88, 28, 135, 255)
                    : new Color32(55, 55, 62, 255);
            }
            EventSystem.current?.SetSelectedGameObject(_pauseButtons[_pauseSelection].gameObject);
        }

        private void SetSettingsOpen(bool open)
        {
            _settingsOpen = open;
            if (_pauseHomeRoot != null) _pauseHomeRoot.SetActive(!open);
            if (_settingsRoot != null) _settingsRoot.SetActive(open);
            if (open)
            {
                _settingsPanel?.Open();
            }
            else if (IsPauseOpen)
            {
                SetPauseSelection(1);
            }
            Debug.Log($"[WOF-AUTOMATION] SETTINGS_MENU open={open}");
        }

        private void RefreshScoreboard()
        {
            _nextRosterRefreshAt = Time.unscaledTime + 0.2f;
            var players = FindObjectsByType<WofPlayerController>(FindObjectsSortMode.None)
                .OrderBy(player => player.OwnerClientId)
                .ToArray();
            var profile = WofSurvivalProfileStore.Load();
            var localLevel = profile?.survivalLevel ?? 1;
            var localName = string.IsNullOrWhiteSpace(profile?.playerName) ? "WIZARD" : profile.playerName.ToUpperInvariant();
            for (var index = 0; index < _scoreRows.Length; index++)
            {
                var row = _scoreRows[index];
                if (index >= players.Length)
                {
                    _scoreRowPanels[index].SetActive(false);
                    continue;
                }

                var player = players[index];
                var label = player.IsOwner
                    ? $"YOU - {localName} LVL {localLevel}"
                    : $"WIZARD {player.OwnerClientId + 1} ({player.OwnerClientId:X4})";
                var status = BuildScoreboardStatus(
                    player.Health,
                    player.IsSleepEffectActive,
                    player.IsSlowEffectActive,
                    player.IsPoisonEffectActive,
                    player.IsAcidEffectActive);
                var score = Mathf.Max(0, Mathf.RoundToInt(player.Health + player.Armor));
                row.text = $"{Trim(label, 26),-26} {status,-10} {Mathf.RoundToInt(player.Health),4} {Mathf.RoundToInt(player.Armor),5} {score,5}";
                row.color = player.IsOwner ? new Color32(255, 248, 198, 255) : new Color32(207, 250, 254, 225);
                var panel = _scoreRowPanels[index];
                panel.GetComponent<Image>().color = player.IsOwner
                    ? new Color32(250, 204, 21, 26)
                    : new Color32(34, 211, 238, 13);
                panel.GetComponent<Outline>().effectColor = player.IsOwner
                    ? new Color32(254, 240, 138, 140)
                    : new Color32(165, 243, 252, 52);
                panel.SetActive(true);
            }
        }

        internal static string BuildScoreboardStatus(
            float health,
            bool sleeping,
            bool slowed,
            bool poisoned,
            bool acid)
        {
            if (health <= 0f) return "DOWN";
            var status = sleeping ? "SLEEP" : string.Empty;
            if (slowed) status = AppendStatus(status, "SLOWED");
            if (poisoned) status = AppendStatus(status, "POISON");
            if (acid) status = AppendStatus(status, "ACID");
            return string.IsNullOrEmpty(status) ? "READY" : status;
        }

        private static string AppendStatus(string current, string next)
        {
            return string.IsNullOrEmpty(current) ? next : current + " / " + next;
        }

        private static string Trim(string value, int length)
        {
            return value.Length <= length ? value : value.Substring(0, length);
        }

        private void BuildView()
        {
            if (uiParent == null || font == null || _pauseRoot != null) return;

            _pauseRoot = CreatePanel("PauseMenuOverlay", uiParent, Vector2.zero, Vector2.one, new Color32(5, 2, 7, 247));
            _pauseHomeRoot = CreatePanel("PauseHome", _pauseRoot.transform, Vector2.zero, Vector2.one, Color.clear);
            _pauseHomeRoot.GetComponent<Image>().raycastTarget = false;
            var title = CreateText("Title", _pauseHomeRoot.transform, "WIZARDS\nONLY\nFOOLS!", 62, TextAnchor.MiddleCenter, new Color32(255, 179, 71, 255));
            SetRect(title.rectTransform, new Vector2(0.16f, 0.48f), new Vector2(0.84f, 0.94f));
            var resume = CreateButton("Resume", _pauseHomeRoot.transform, "CLICK TO PLAY / START TO RESUME", new Color32(31, 15, 43, 255));
            SetRect(resume.GetComponent<RectTransform>(), new Vector2(0.29f, 0.34f), new Vector2(0.71f, 0.46f));
            var resumeOutline = resume.gameObject.AddComponent<Outline>();
            resumeOutline.effectColor = new Color32(103, 232, 249, 160);
            resumeOutline.effectDistance = new Vector2(2f, -2f);
            resume.onClick.AddListener(() => SetPauseOpen(false));
            var settings = CreateButton("Settings", _pauseHomeRoot.transform, "SETTINGS", new Color32(55, 55, 62, 255));
            SetRect(settings.GetComponent<RectTransform>(), new Vector2(0.41f, 0.23f), new Vector2(0.59f, 0.31f));
            settings.onClick.AddListener(() => SetSettingsOpen(true));
            _pauseButtons = new[] { resume, settings };

            _settingsRoot = CreatePanel("SettingsPanel", _pauseRoot.transform, new Vector2(0.22f, 0.12f), new Vector2(0.78f, 0.88f), new Color32(9, 5, 16, 245));
            var settingsOutline = _settingsRoot.AddComponent<Outline>();
            settingsOutline.effectColor = new Color32(103, 232, 249, 155);
            settingsOutline.effectDistance = new Vector2(2f, -2f);
            _settingsPanel = _settingsRoot.AddComponent<WofSettingsPanelRuntime>();
            _settingsPanel.Configure(hud, font, () => SetSettingsOpen(false));

            _scoreboardRoot = CreatePanel("PlayerScoreOverlay", uiParent, Vector2.zero, Vector2.one, Color.clear);
            _scoreboardRoot.GetComponent<Image>().raycastTarget = false;
            var scoreCard = CreatePanel("PlayerScoreCard", _scoreboardRoot.transform, new Vector2(0.2f, 0.32f), new Vector2(0.8f, 0.92f), new Color32(9, 5, 16, 199));
            var scoreOutline = scoreCard.AddComponent<Outline>();
            scoreOutline.effectColor = new Color32(103, 232, 249, 150);
            scoreOutline.effectDistance = new Vector2(2f, -2f);
            var kicker = CreateText("Kicker", scoreCard.transform, "ARENA ROSTER", 13, TextAnchor.MiddleLeft, new Color32(165, 243, 252, 170));
            SetRect(kicker.rectTransform, new Vector2(0.035f, 0.89f), new Vector2(0.45f, 0.98f));
            var scoreTitle = CreateText("ScoreTitle", scoreCard.transform, "PLAYER LIST / SCORE", 28, TextAnchor.MiddleLeft, Color.white);
            SetRect(scoreTitle.rectTransform, new Vector2(0.035f, 0.77f), new Vector2(0.72f, 0.91f));
            var hint = CreateText("Hint", scoreCard.transform, "HOLD TAB / SELECT", 12, TextAnchor.MiddleRight, new Color32(207, 250, 254, 135));
            SetRect(hint.rectTransform, new Vector2(0.64f, 0.80f), new Vector2(0.965f, 0.94f));
            var headings = CreateText("Headings", scoreCard.transform, "PLAYER                     STATE        HP ARMOR SCORE", 15, TextAnchor.MiddleLeft, new Color32(207, 250, 254, 145));
            SetRect(headings.rectTransform, new Vector2(0.035f, 0.68f), new Vector2(0.965f, 0.77f));
            _scoreRows = new Text[MaximumScoreRows];
            _scoreRowPanels = new GameObject[MaximumScoreRows];
            for (var index = 0; index < _scoreRows.Length; index++)
            {
                var top = 0.675f - index * 0.0375f;
                var rowPanel = CreatePanel($"PlayerPanel{index}", scoreCard.transform, new Vector2(0.03f, top - 0.033f), new Vector2(0.97f, top), new Color32(34, 211, 238, 13));
                var rowOutline = rowPanel.AddComponent<Outline>();
                rowOutline.effectColor = new Color32(165, 243, 252, 52);
                rowOutline.effectDistance = new Vector2(1f, -1f);
                var row = CreateText($"Player{index}", rowPanel.transform, string.Empty, 14, TextAnchor.MiddleLeft, new Color32(207, 250, 254, 225));
                SetRect(row.rectTransform, new Vector2(0.012f, 0f), new Vector2(0.988f, 1f));
                rowPanel.SetActive(false);
                _scoreRows[index] = row;
                _scoreRowPanels[index] = rowPanel;
            }
            var scoreFooter = CreateText(
                "Footer",
                scoreCard.transform,
                "SCORE IS CURRENT BATTLE POWER UNTIL KILL TRACKING IS ADDED",
                11,
                TextAnchor.MiddleLeft,
                new Color32(207, 250, 254, 115));
            SetRect(scoreFooter.rectTransform, new Vector2(0.035f, 0.015f), new Vector2(0.965f, 0.075f));

            _settingsRoot.SetActive(false);
            _pauseRoot.SetActive(false);
            _scoreboardRoot.SetActive(false);
        }

        private GameObject CreatePanel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            item.GetComponent<Image>().color = color;
            SetRect(item.GetComponent<RectTransform>(), min, max);
            return item;
        }

        private Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(8, size / 2);
            text.resizeTextMaxSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Color color)
        {
            var item = CreatePanel(name, parent, Vector2.zero, Vector2.one, color);
            var button = item.AddComponent<Button>();
            button.targetGraphic = item.GetComponent<Image>();
            var text = CreateText("Label", item.transform, label, 22, TextAnchor.MiddleCenter, Color.white);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
