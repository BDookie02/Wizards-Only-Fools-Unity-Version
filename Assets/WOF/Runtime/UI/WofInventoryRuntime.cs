using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofInventoryRuntime : MonoBehaviour
    {
        private static readonly Color32 EmeraldBorder = new(209, 250, 229, 166);
        private static readonly Color32 EmeraldDimBorder = new(209, 250, 229, 64);
        private static readonly Color32 EmeraldText = new(236, 253, 245, 255);
        private static readonly Color32 EmeraldMuted = new(209, 250, 229, 115);
        private static readonly Color32 YellowBorder = new(254, 249, 195, 115);
        private static readonly Color32 YellowText = new(254, 252, 232, 255);
        private static readonly Color32 YellowMuted = new(254, 249, 195, 102);
        private static readonly Color32 CardColor = new(6, 16, 12, 245);
        private static readonly Color32 DeepPanelColor = new(2, 9, 6, 179);

        [SerializeField] private WofHud hud;
        [SerializeField] private Font headingFont;
        [SerializeField] private Font bodyFont;

        private GameObject _overlay;
        private Canvas _rootCanvas;
        private RectTransform _cardScaleRoot;
        private RectTransform _cardRect;
        private GameObject _inventoryRoot;
        private GameObject _journalRoot;
        private GameObject _emptyJournalRoot;
        private GameObject _journalDetailRoot;
        private GameObject _darrelProgressRoot;
        private Button _closeButton;
        private Button _questsButton;
        private Image _questsButtonImage;
        private Text _questsButtonText;
        private Text _inventoryCountText;
        private Text _journalTitleText;
        private Text _journalStatusText;
        private Text _journalGiverText;
        private Text _journalRewardText;
        private Text _journalObjectiveText;
        private Text[] _darrelProgressTexts;
        private RectTransform _questListContent;
        private ScrollRect _questListScroll;
        private readonly List<Button> _questButtons = new();
        private readonly List<Image> _questButtonImages = new();
        private readonly List<Text> _questButtonTitleTexts = new();
        private readonly List<Text> _questButtonMetaTexts = new();
        private readonly WofInventorySlotView[] _backpackSlots = new WofInventorySlotView[WofInventoryRules.BackpackSlotCount];
        private readonly WofInventorySlotView[] _quickSlots = new WofInventorySlotView[WofInventoryRules.QuickSlotCount];
        private WofInventoryQuestEntry[] _activeQuests = Array.Empty<WofInventoryQuestEntry>();
        private WofSurvivalProfile _profile;
        private WofPlayerController _localPlayer;
        private WofInventoryControllerHoldState _controllerHoldState;
        private WofInventoryControllerRepeatState _controllerNextRepeat;
        private WofInventoryControllerRepeatState _controllerPreviousRepeat;
        private int _selectedQuestIndex;
        private bool _questJournalOpen;
        private Gamepad _controllerProbeGamepad;

        public bool IsOpen => _overlay != null && _overlay.activeSelf;
        public bool IsQuestJournalOpen => IsOpen && _questJournalOpen;
        public int SelectedQuestIndex => _selectedQuestIndex;
        public int ActiveQuestCount => _activeQuests.Length;

        public void ConfigureGeneratedView(WofHud generatedHud, Font generatedHeadingFont, Font generatedBodyFont)
        {
            hud = generatedHud;
            headingFont = generatedHeadingFont;
            bodyFont = generatedBodyFont;
        }

        private void Awake()
        {
            hud ??= FindFirstObjectByType<WofHud>();
            headingFont ??= GetComponentInChildren<Text>(true)?.font;
            bodyFont ??= headingFont;
            BuildRuntimeView();
            if (IsControllerProbeRequested())
            {
                StartCoroutine(RunControllerProbe());
            }
        }

        private void Update()
        {
            RefreshPixelScale();
            if (IsOpen)
            {
                PollOpenInput();
                return;
            }

            PollOpenShortcut();
        }

        private void OnDestroy()
        {
            RemoveControllerProbeGamepad();
            if (IsOpen)
            {
                RestoreGameplay();
            }
        }

        public void Open()
        {
            if (IsOpen || !CanOpenFromGameplay())
            {
                return;
            }

            _profile = WofSurvivalProfileStore.Load() ?? new WofSurvivalProfile();
            WofInventoryRules.NormalizeProfile(_profile);
            _questJournalOpen = false;
            _selectedQuestIndex = 0;
            _controllerNextRepeat = default;
            _controllerPreviousRepeat = default;
            WofInputRouter.SetGameplaySuppressed(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _overlay.SetActive(true);
            RefreshView();
            Debug.Log("[WOF-AUTOMATION] INVENTORY_OPEN source=gameplay");
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            _questJournalOpen = false;
            _overlay.SetActive(false);
            RestoreGameplay();
            Debug.Log("[WOF-AUTOMATION] INVENTORY_CLOSED");
        }

        private void PollOpenShortcut()
        {
            if (!CanOpenFromGameplay())
            {
                _controllerHoldState.HasStarted = false;
                _controllerHoldState.TapEligible = false;
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.iKey.wasPressedThisFrame)
            {
                Open();
                return;
            }

            var gamepad = Gamepad.current;
            var controllerActive = WofInputRouter.IsControllerGameplayActive(gamepad);
            var inventoryHeld = gamepad?.dpad.right.isPressed ?? false;
            var movementAxis = controllerActive && gamepad != null
                ? WofInputRouter.ResolveControllerStick(gamepad.leftStick.ReadUnprocessedValue())
                : Vector2.zero;
            _localPlayer = ResolveLocalPlayer(_localPlayer);
            var standingStill = _localPlayer != null && WofInventoryRules.IsStandingStillForControllerInventory(
                controllerActive,
                _localPlayer.IsMoving,
                _localPlayer.IsSprinting,
                _localPlayer.IsSliding,
                _localPlayer.IsCrouching,
                movementAxis);
            var action = WofInventoryRules.UpdateControllerInventoryHold(
                ref _controllerHoldState,
                Time.unscaledTime,
                inventoryHeld,
                standingStill,
                controllerActive);
            if (action == WofInventoryShortcutAction.OpenInventory)
            {
                Open();
            }
        }

        private void PollOpenInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var action = keyboard.jKey.wasPressedThisFrame
                    ? WofInventoryRules.GetKeyboardAction("KeyJ", _questJournalOpen)
                    : keyboard.downArrowKey.wasPressedThisFrame
                        ? WofInventoryRules.GetKeyboardAction("ArrowDown", _questJournalOpen)
                        : keyboard.upArrowKey.wasPressedThisFrame
                            ? WofInventoryRules.GetKeyboardAction("ArrowUp", _questJournalOpen)
                            : keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
                                ? WofInventoryRules.GetKeyboardAction("Enter", _questJournalOpen)
                                : keyboard.escapeKey.wasPressedThisFrame
                                    ? WofInventoryRules.GetKeyboardAction("Escape", _questJournalOpen)
                                    : keyboard.iKey.wasPressedThisFrame
                                        ? WofInventoryRules.GetKeyboardAction("KeyI", _questJournalOpen)
                                        : WofInventoryKeyboardAction.None;
                if (action != WofInventoryKeyboardAction.None)
                {
                    ApplyKeyboardAction(action);
                    return;
                }
            }

            var gamepad = Gamepad.current;
            if (gamepad == null)
            {
                _controllerNextRepeat = default;
                _controllerPreviousRepeat = default;
                return;
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                if (!_questJournalOpen)
                {
                    SetQuestJournalOpen(true);
                }
                return;
            }
            if (gamepad.buttonEast.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame)
            {
                if (_questJournalOpen)
                {
                    SetQuestJournalOpen(false);
                }
                else
                {
                    if (gamepad.dpad.right.isPressed)
                    {
                        WofInventoryRules.MarkControllerInventoryIgnoreUntilRelease(ref _controllerHoldState);
                    }
                    Close();
                }
                return;
            }

            var stickY = gamepad.leftStick.ReadUnprocessedValue().y;
            var nextHeld = gamepad.dpad.down.isPressed || stickY < -WofInventoryRules.ControllerNavigationThreshold;
            var previousHeld = gamepad.dpad.up.isPressed || stickY > WofInventoryRules.ControllerNavigationThreshold;
            var next = WofInventoryRules.ConsumeControllerRepeat(
                ref _controllerNextRepeat,
                nextHeld,
                Time.unscaledTime);
            var previous = WofInventoryRules.ConsumeControllerRepeat(
                ref _controllerPreviousRepeat,
                previousHeld,
                Time.unscaledTime);
            if (_questJournalOpen && (next || previous))
            {
                MoveQuestSelection(next ? 1 : -1);
            }
        }

        private void ApplyKeyboardAction(WofInventoryKeyboardAction action)
        {
            switch (action)
            {
                case WofInventoryKeyboardAction.ToggleQuestJournal:
                    SetQuestJournalOpen(!_questJournalOpen);
                    break;
                case WofInventoryKeyboardAction.MoveQuestNext:
                    MoveQuestSelection(1);
                    break;
                case WofInventoryKeyboardAction.MoveQuestPrevious:
                    MoveQuestSelection(-1);
                    break;
                case WofInventoryKeyboardAction.OpenQuestJournal:
                    SetQuestJournalOpen(true);
                    break;
                case WofInventoryKeyboardAction.CloseQuestJournal:
                    SetQuestJournalOpen(false);
                    break;
                case WofInventoryKeyboardAction.CloseInventory:
                    Close();
                    break;
            }
        }

        private void SetQuestJournalOpen(bool open)
        {
            if (!IsOpen || _questJournalOpen == open)
            {
                return;
            }
            _questJournalOpen = open;
            _selectedQuestIndex = WofInventoryRules.ClampQuestIndex(_selectedQuestIndex, _activeQuests.Length);
            RefreshView();
            Debug.Log($"[WOF-AUTOMATION] INVENTORY_JOURNAL open={open.ToString().ToLowerInvariant()} active={_activeQuests.Length}");
        }

        private void MoveQuestSelection(int direction)
        {
            if (!_questJournalOpen)
            {
                return;
            }
            _selectedQuestIndex = WofInventoryRules.GetNextQuestIndex(
                _selectedQuestIndex,
                direction,
                _activeQuests.Length);
            RefreshJournalSelection();
            if (_activeQuests.Length > 0)
            {
                Debug.Log($"[WOF-AUTOMATION] INVENTORY_QUEST_SELECTED index={_selectedQuestIndex} title=\"{_activeQuests[_selectedQuestIndex].Definition.Title}\"");
            }
        }

        private void RefreshView()
        {
            WofInventoryRules.NormalizeProfile(_profile);
            _activeQuests = WofInventoryRules.GetActiveQuestEntries(_profile);
            _selectedQuestIndex = WofInventoryRules.ClampQuestIndex(_selectedQuestIndex, _activeQuests.Length);
            _cardRect.sizeDelta = new Vector2(1060f, _questJournalOpen ? 670f : 570f);
            _inventoryRoot.SetActive(!_questJournalOpen);
            _journalRoot.SetActive(_questJournalOpen);
            _questsButtonImage.color = _questJournalOpen
                ? new Color32(254, 249, 195, 46)
                : new Color32(110, 231, 183, 26);
            _questsButtonText.color = _questJournalOpen ? YellowText : EmeraldText;
            _questsButtonText.text = $"QUESTS\n{_activeQuests.Length} ACTIVE";
            RefreshInventorySlots();
            if (_questJournalOpen)
            {
                RefreshQuestButtons();
                RefreshJournalSelection();
            }
            ApplyVerticalLayout();
        }

        private void RefreshInventorySlots()
        {
            var entries = WofInventoryRules.GetInventoryEntries(_profile);
            _inventoryCountText.text = $"{entries.Length}/36";
            for (var index = 0; index < _backpackSlots.Length; index++)
            {
                var entry = index < entries.Length ? entries[index] : (WofInventoryDisplayEntry?)null;
                _backpackSlots[index].Refresh(entry, index + 1);
            }
            for (var index = 0; index < _quickSlots.Length; index++)
            {
                var entryIndex = WofInventoryRules.BackpackSlotCount + index;
                var entry = entryIndex < entries.Length ? entries[entryIndex] : (WofInventoryDisplayEntry?)null;
                _quickSlots[index].Refresh(entry, index + 1);
            }
        }

        private void RefreshQuestButtons()
        {
            for (var index = 0; index < _questButtons.Count; index++)
            {
                if (_questButtons[index] != null)
                {
                    Destroy(_questButtons[index].gameObject);
                }
            }
            _questButtons.Clear();
            _questButtonImages.Clear();
            _questButtonTitleTexts.Clear();
            _questButtonMetaTexts.Clear();

            if (_activeQuests.Length == 0)
            {
                _questListContent.sizeDelta = new Vector2(0f, 1f);
                return;
            }

            const float rowHeight = 58f;
            const float rowGap = 6f;
            for (var index = 0; index < _activeQuests.Length; index++)
            {
                var capturedIndex = index;
                var button = CreateButton(
                    $"Quest{index + 1}",
                    _questListContent,
                    string.Empty,
                    new Color32(0, 0, 0, 89));
                SetTopStretch(button.GetComponent<RectTransform>(), index * (rowHeight + rowGap), rowHeight, 0f, 3f);
                button.onClick.AddListener(() => SelectQuest(capturedIndex));
                button.gameObject.AddComponent<WofInventoryQuestHover>().Configure(this, capturedIndex);
                var title = CreateText(
                    "Title",
                    button.transform,
                    headingFont,
                    _activeQuests[index].Definition.Title.ToUpperInvariant(),
                    12,
                    TextAnchor.UpperLeft,
                    EmeraldText);
                SetTopStretch(title.rectTransform, 8f, 20f, 9f, 9f);
                var meta = CreateText(
                    "Meta",
                    button.transform,
                    headingFont,
                    $"{_activeQuests[index].Assignment.displayName} / {_activeQuests[index].Definition.DisplayName}".ToUpperInvariant(),
                    9,
                    TextAnchor.LowerLeft,
                    new Color32(209, 250, 229, 115));
                SetBottomStretch(meta.rectTransform, 7f, 16f, 9f, 9f);
                _questButtons.Add(button);
                _questButtonImages.Add(button.targetGraphic as Image);
                _questButtonTitleTexts.Add(title);
                _questButtonMetaTexts.Add(meta);
            }
            _questListContent.sizeDelta = new Vector2(0f, _activeQuests.Length * rowHeight +
                                                          Mathf.Max(0, _activeQuests.Length - 1) * rowGap);
        }

        public void SelectQuest(int index)
        {
            if (!_questJournalOpen || index < 0 || index >= _activeQuests.Length)
            {
                return;
            }
            _selectedQuestIndex = index;
            RefreshJournalSelection();
        }

        private void RefreshJournalSelection()
        {
            var hasQuest = _activeQuests.Length > 0;
            _emptyJournalRoot.SetActive(!hasQuest);
            _journalDetailRoot.SetActive(hasQuest);
            for (var index = 0; index < _questButtonImages.Count; index++)
            {
                var selected = index == _selectedQuestIndex;
                _questButtonImages[index].color = selected
                    ? new Color32(254, 249, 195, 46)
                    : new Color32(0, 0, 0, 89);
                _questButtonTitleTexts[index].color = selected ? YellowText : EmeraldText;
            }
            if (!hasQuest)
            {
                return;
            }

            var entry = _activeQuests[_selectedQuestIndex];
            _journalTitleText.text = entry.Definition.Title.ToUpperInvariant();
            _journalStatusText.text = WofInventoryRules.GetQuestStatus(entry, _profile).ToUpperInvariant();
            _journalGiverText.text = $"GIVER:  {entry.Assignment.displayName}";
            _journalRewardText.text = $"REWARD:  {entry.Definition.DisplayName}";
            _journalObjectiveText.text = entry.Definition.Objective;
            var isDarrel = string.Equals(entry.Assignment.spell, WofSpellQuestRules.DarrelRewardSpell, StringComparison.Ordinal);
            _darrelProgressRoot.SetActive(isDarrel);
            if (isDarrel)
            {
                var rows = WofInventoryRules.GetDarrelProgressRows(_profile);
                for (var index = 0; index < _darrelProgressTexts.Length; index++)
                {
                    _darrelProgressTexts[index].text = $"{rows[index].Label.ToUpperInvariant()}                         {(rows[index].Done ? "DONE" : "NEEDED")}";
                    _darrelProgressTexts[index].color = rows[index].Done ? YellowText : EmeraldMuted;
                }
            }

            var contentHeight = _questListContent.rect.height;
            var viewportHeight = _questListScroll.viewport.rect.height;
            if (contentHeight > viewportHeight)
            {
                var rowCenter = _selectedQuestIndex * 64f + 29f;
                var desiredTop = Mathf.Clamp(rowCenter - viewportHeight * 0.5f, 0f, contentHeight - viewportHeight);
                _questListContent.anchoredPosition = new Vector2(0f, desiredTop);
            }
        }

        private void ApplyVerticalLayout()
        {
            var quickTop = _questJournalOpen ? 518f : 418f;
            var quickLabel = _cardRect.Find("QuickLabel") as RectTransform;
            var quickPanel = _cardRect.Find("QuickPanel") as RectTransform;
            if (quickLabel != null)
            {
                SetTopLeft(quickLabel, 16f, quickTop - 26f, 400f, 18f);
            }
            if (quickPanel != null)
            {
                SetTopLeft(quickPanel, 16f, quickTop, 1028f, 116f);
            }
        }

        private void BuildRuntimeView()
        {
            if (headingFont == null || bodyFont == null)
            {
                Debug.LogError("[WOF-AUTOMATION] INVENTORY_VIEW_FAILED reason=missing-font");
                return;
            }

            _overlay = CreatePanel("InventoryOverlay", transform, new Color(0f, 0f, 0f, 0.45f));
            SetFullStretch(_overlay.GetComponent<RectTransform>());
            _rootCanvas = GetComponentInParent<Canvas>();
            _cardScaleRoot = new GameObject("InventoryPixelScale", typeof(RectTransform)).GetComponent<RectTransform>();
            _cardScaleRoot.SetParent(_overlay.transform, false);
            SetCentered(_cardScaleRoot, 0f, 0f);
            RefreshPixelScale();
            var card = CreatePanel("InventoryCard", _cardScaleRoot, CardColor);
            _cardRect = card.GetComponent<RectTransform>();
            SetCentered(_cardRect, 1060f, 570f);
            var cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = EmeraldBorder;
            cardOutline.effectDistance = new Vector2(2f, -2f);

            var header = CreatePanel("Header", card.transform, new Color32(2, 44, 34, 51));
            SetTopStretch(header.GetComponent<RectTransform>(), 0f, 62f, 0f, 0f);
            var divider = CreatePanel("Divider", header.transform, new Color32(209, 250, 229, 64));
            SetBottomStretch(divider.GetComponent<RectTransform>(), 0f, 1f, 0f, 0f);
            var kicker = CreateText("Kicker", header.transform, headingFont, "SURVIVAL PACK", 10, TextAnchor.MiddleLeft, EmeraldMuted);
            SetTopLeft(kicker.rectTransform, 16f, 8f, 700f, 18f);
            var title = CreateText("Title", header.transform, headingFont, "INVENTORY", 22, TextAnchor.MiddleLeft, EmeraldText);
            SetTopLeft(title.rectTransform, 16f, 27f, 700f, 28f);
            _closeButton = CreateButton("Close", header.transform, "X", new Color32(110, 231, 183, 26));
            SetTopRight(_closeButton.GetComponent<RectTransform>(), 14f, 13f, 36f, 36f);
            _closeButton.onClick.AddListener(Close);

            BuildWizardPanel(card.transform);
            BuildInventoryPanel(card.transform);
            BuildJournalPanel(card.transform);
            BuildQuickRow(card.transform);
            _overlay.SetActive(false);
        }

        private void BuildWizardPanel(Transform parent)
        {
            var wizardPanel = CreatePanel("WizardPanel", parent, new Color32(0, 0, 0, 89));
            SetTopLeft(wizardPanel.GetComponent<RectTransform>(), 16f, 78f, 220f, 310f);
            AddOutline(wizardPanel, new Color32(209, 250, 229, 77));
            var wizardLabel = CreateText("WizardLabel", wizardPanel.transform, headingFont, "WIZARD", 10, TextAnchor.MiddleLeft, EmeraldMuted);
            SetTopLeft(wizardLabel.rectTransform, 12f, 9f, 196f, 18f);

            var preview = CreatePanel("WizardPreview", wizardPanel.transform, DeepPanelColor);
            SetTopLeft(preview.GetComponent<RectTransform>(), 12f, 34f, 196f, 168f);
            AddOutline(preview, new Color32(209, 250, 229, 64));
            var previewImage = preview.GetComponent<Image>();
            previewImage.preserveAspect = true;
            var renderer = preview.AddComponent<WofLaunchWizardPreviewRenderer>();
            var profile = WofSurvivalProfileStore.Load() ?? new WofSurvivalProfile();
            renderer.UseInventoryStyle();
            renderer.Render(profile.topColor, profile.skinColor, profile.hairColor, profile.hatStyle, profile.hairStyle);

            _questsButton = CreateButton("Quests", wizardPanel.transform, string.Empty, new Color32(110, 231, 183, 26));
            SetTopLeft(_questsButton.GetComponent<RectTransform>(), 12f, 214f, 196f, 82f);
            _questsButtonImage = _questsButton.targetGraphic as Image;
            AddOutline(_questsButton.gameObject, new Color32(209, 250, 229, 102));
            BuildQuestBookIcon(_questsButton.transform);
            _questsButtonText = CreateText("QuestLabel", _questsButton.transform, headingFont, "QUESTS\n0 ACTIVE", 10, TextAnchor.MiddleCenter, EmeraldText);
            SetTopLeft(_questsButtonText.rectTransform, 78f, 9f, 105f, 64f);
            _questsButton.onClick.AddListener(() => SetQuestJournalOpen(!_questJournalOpen));
        }

        private void BuildInventoryPanel(Transform parent)
        {
            _inventoryRoot = CreatePanel("BackpackRoot", parent, Color.clear);
            SetTopLeft(_inventoryRoot.GetComponent<RectTransform>(), 252f, 78f, 792f, 310f);
            _inventoryRoot.GetComponent<Image>().raycastTarget = false;
            var label = CreateText("BackpackLabel", _inventoryRoot.transform, headingFont, "BACKPACK", 10, TextAnchor.MiddleLeft, EmeraldMuted);
            SetTopLeft(label.rectTransform, 0f, 0f, 500f, 20f);
            _inventoryCountText = CreateText("Count", _inventoryRoot.transform, headingFont, "0/36", 10, TextAnchor.MiddleRight, new Color32(209, 250, 229, 89));
            SetTopRight(_inventoryCountText.rectTransform, 0f, 0f, 180f, 20f);
            var slotsPanel = CreatePanel("BackpackSlots", _inventoryRoot.transform, new Color32(0, 0, 0, 89));
            SetTopLeft(slotsPanel.GetComponent<RectTransform>(), 0f, 30f, 792f, 268f);
            AddOutline(slotsPanel, EmeraldDimBorder);
            for (var index = 0; index < _backpackSlots.Length; index++)
            {
                var column = index % 9;
                var row = index / 9;
                _backpackSlots[index] = CreateSlot(slotsPanel.transform, 12f + column * 86f, 10f + row * 86f, 80f);
            }
        }

        private void BuildJournalPanel(Transform parent)
        {
            _journalRoot = CreatePanel("JournalRoot", parent, new Color32(0, 0, 0, 89));
            SetTopLeft(_journalRoot.GetComponent<RectTransform>(), 252f, 78f, 792f, 410f);
            AddOutline(_journalRoot, new Color32(254, 249, 195, 89));
            var kicker = CreateText("Kicker", _journalRoot.transform, headingFont, "QUEST JOURNAL", 10, TextAnchor.MiddleLeft, YellowMuted);
            SetTopLeft(kicker.rectTransform, 12f, 8f, 450f, 18f);
            var title = CreateText("Title", _journalRoot.transform, headingFont, "ACTIVE QUESTS", 14, TextAnchor.MiddleLeft, YellowText);
            SetTopLeft(title.rectTransform, 12f, 25f, 450f, 24f);
            var back = CreateButton("Back", _journalRoot.transform, "BACK", new Color32(254, 249, 195, 26));
            SetTopRight(back.GetComponent<RectTransform>(), 12f, 11f, 74f, 32f);
            AddOutline(back.gameObject, YellowBorder);
            back.onClick.AddListener(() => SetQuestJournalOpen(false));

            var listViewport = CreatePanel("QuestList", _journalRoot.transform, new Color32(0, 0, 0, 51));
            SetTopLeft(listViewport.GetComponent<RectTransform>(), 12f, 58f, 286f, 304f);
            AddOutline(listViewport, EmeraldDimBorder);
            var mask = listViewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            _questListContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            _questListContent.SetParent(listViewport.transform, false);
            _questListContent.anchorMin = new Vector2(0f, 1f);
            _questListContent.anchorMax = Vector2.one;
            _questListContent.pivot = new Vector2(0.5f, 1f);
            _questListContent.offsetMin = Vector2.zero;
            _questListContent.offsetMax = Vector2.zero;
            _questListScroll = listViewport.AddComponent<ScrollRect>();
            _questListScroll.viewport = listViewport.GetComponent<RectTransform>();
            _questListScroll.content = _questListContent;
            _questListScroll.horizontal = false;
            _questListScroll.vertical = true;
            _questListScroll.movementType = ScrollRect.MovementType.Clamped;

            _emptyJournalRoot = CreatePanel("EmptyJournal", _journalRoot.transform, new Color32(0, 0, 0, 77));
            SetTopLeft(_emptyJournalRoot.GetComponent<RectTransform>(), 310f, 58f, 470f, 304f);
            AddOutline(_emptyJournalRoot, EmeraldDimBorder);
            var emptyText = CreateText("Empty", _emptyJournalRoot.transform, headingFont, "NO ACTIVE SPELL QUESTS YET.", 12, TextAnchor.MiddleCenter, EmeraldMuted);
            SetFullStretch(emptyText.rectTransform, 28f);

            _journalDetailRoot = CreatePanel("QuestDetail", _journalRoot.transform, new Color32(3, 9, 6, 191));
            SetTopLeft(_journalDetailRoot.GetComponent<RectTransform>(), 310f, 58f, 470f, 304f);
            AddOutline(_journalDetailRoot, EmeraldDimBorder);
            var selectedKicker = CreateText("SelectedKicker", _journalDetailRoot.transform, headingFont, "SELECTED QUEST", 9, TextAnchor.MiddleLeft, YellowMuted);
            SetTopLeft(selectedKicker.rectTransform, 12f, 9f, 260f, 17f);
            _journalTitleText = CreateText("SelectedTitle", _journalDetailRoot.transform, headingFont, string.Empty, 15, TextAnchor.UpperLeft, YellowText);
            SetTopLeft(_journalTitleText.rectTransform, 12f, 27f, 446f, 47f);
            _journalTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var statusPanel = CreatePanel("Status", _journalDetailRoot.transform, new Color32(254, 240, 138, 26));
            SetTopRight(statusPanel.GetComponent<RectTransform>(), 12f, 78f, 190f, 28f);
            AddOutline(statusPanel, new Color32(254, 249, 195, 89));
            _journalStatusText = CreateText("Text", statusPanel.transform, headingFont, string.Empty, 9, TextAnchor.MiddleCenter, YellowText);
            SetFullStretch(_journalStatusText.rectTransform, 3f);
            _journalGiverText = CreateText("Giver", _journalDetailRoot.transform, bodyFont, string.Empty, 22, TextAnchor.MiddleLeft, new Color32(236, 253, 245, 204));
            SetTopLeft(_journalGiverText.rectTransform, 12f, 77f, 250f, 28f);
            _journalRewardText = CreateText("Reward", _journalDetailRoot.transform, bodyFont, string.Empty, 22, TextAnchor.MiddleLeft, new Color32(236, 253, 245, 204));
            SetTopLeft(_journalRewardText.rectTransform, 12f, 106f, 446f, 28f);
            var objectivePanel = CreatePanel("Objective", _journalDetailRoot.transform, new Color32(8, 51, 68, 51));
            SetTopLeft(objectivePanel.GetComponent<RectTransform>(), 12f, 140f, 446f, 70f);
            AddOutline(objectivePanel, new Color32(207, 250, 254, 38));
            _journalObjectiveText = CreateText("Text", objectivePanel.transform, bodyFont, string.Empty, 20, TextAnchor.UpperLeft, new Color32(236, 254, 255, 230));
            SetFullStretch(_journalObjectiveText.rectTransform, 8f);
            _journalObjectiveText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _journalObjectiveText.verticalOverflow = VerticalWrapMode.Truncate;

            _darrelProgressRoot = CreatePanel("DarrelProgress", _journalDetailRoot.transform, Color.clear);
            SetTopLeft(_darrelProgressRoot.GetComponent<RectTransform>(), 12f, 216f, 446f, 82f);
            _darrelProgressRoot.GetComponent<Image>().raycastTarget = false;
            _darrelProgressTexts = new Text[4];
            for (var index = 0; index < _darrelProgressTexts.Length; index++)
            {
                var row = CreatePanel($"Progress{index + 1}", _darrelProgressRoot.transform, new Color32(0, 0, 0, 64));
                SetTopLeft(row.GetComponent<RectTransform>(), 0f, index * 20f, 446f, 18f);
                AddOutline(row, new Color32(209, 250, 229, 51));
                _darrelProgressTexts[index] = CreateText("Text", row.transform, headingFont, string.Empty, 8, TextAnchor.MiddleLeft, EmeraldMuted);
                SetFullStretch(_darrelProgressTexts[index].rectTransform, 5f);
            }
            var help = CreateText("Help", _journalRoot.transform, headingFont, "D-PAD / STICK CHOOSE / A OPEN / B BACK", 9, TextAnchor.MiddleLeft, new Color32(254, 249, 195, 102));
            SetTopLeft(help.rectTransform, 12f, 376f, 768f, 18f);
            _journalRoot.SetActive(false);
        }

        private void BuildQuickRow(Transform parent)
        {
            var label = CreateText("QuickLabel", parent, headingFont, "QUICK ROW", 10, TextAnchor.MiddleLeft, EmeraldMuted);
            SetTopLeft(label.rectTransform, 16f, 392f, 400f, 18f);
            var quickPanel = CreatePanel("QuickPanel", parent, new Color32(254, 249, 195, 20));
            SetTopLeft(quickPanel.GetComponent<RectTransform>(), 16f, 418f, 1028f, 116f);
            AddOutline(quickPanel, new Color32(254, 249, 195, 89));
            for (var index = 0; index < _quickSlots.Length; index++)
            {
                _quickSlots[index] = CreateSlot(quickPanel.transform, 16f + index * 112f, 8f, 100f);
            }
        }

        private WofInventorySlotView CreateSlot(Transform parent, float left, float top, float size)
        {
            var slot = CreatePanel("Slot", parent, DeepPanelColor);
            SetTopLeft(slot.GetComponent<RectTransform>(), left, top, size, size);
            AddOutline(slot, new Color32(209, 250, 229, 51));
            var inset = CreatePanel("Inset", slot.transform, new Color32(255, 255, 255, 10));
            SetFullStretch(inset.GetComponent<RectTransform>(), 4f);
            inset.GetComponent<Image>().raycastTarget = false;
            var mark = CreateText("Mark", slot.transform, headingFont, string.Empty, Mathf.RoundToInt(size * 0.19f), TextAnchor.MiddleCenter, EmeraldText);
            SetFullStretch(mark.rectTransform, size * 0.19f);
            var quantity = CreateText("Quantity", slot.transform, headingFont, string.Empty, Mathf.RoundToInt(size * 0.12f), TextAnchor.LowerRight, EmeraldText);
            SetFullStretch(quantity.rectTransform, 6f);
            var empty = CreateText("Empty", slot.transform, headingFont, string.Empty, Mathf.RoundToInt(size * 0.10f), TextAnchor.MiddleCenter, new Color32(209, 250, 229, 51));
            SetFullStretch(empty.rectTransform, 6f);
            return new WofInventorySlotView(slot.GetComponent<Image>(), mark, quantity, empty);
        }

        private void BuildQuestBookIcon(Transform parent)
        {
            var icon = CreatePanel("QuestBookIcon", parent, new Color32(0, 0, 0, 255));
            SetTopLeft(icon.GetComponent<RectTransform>(), 12f, 9f, 62f, 62f);
            AddOutline(icon, new Color32(209, 250, 229, 102));
            var page = CreatePanel("Page", icon.transform, Color.clear);
            SetTopLeft(page.GetComponent<RectTransform>(), 13f, 11f, 36f, 40f);
            AddOutline(page, new Color32(255, 255, 255, 255));
            for (var index = 0; index < 3; index++)
            {
                var line = CreatePanel($"Line{index + 1}", page.transform, Color.white);
                SetTopLeft(line.GetComponent<RectTransform>(), 7f, 8f + index * 9f, 20f - index * 4f, 2f);
                line.GetComponent<Image>().raycastTarget = false;
            }
            var spine = CreatePanel("Spine", page.transform, Color.white);
            SetTopLeft(spine.GetComponent<RectTransform>(), 28f, 0f, 2f, 40f);
            spine.GetComponent<Image>().raycastTarget = false;
        }

        private bool CanOpenFromGameplay()
        {
            return WofBootstrap.Instance != null && WofBootstrap.Instance.Mode != WofSessionMode.None &&
                   WofBootstrap.Instance.IsSurvivalSession && !WofInputRouter.GameplaySuppressed;
        }

        private void RestoreGameplay()
        {
            WofInputRouter.SetGameplaySuppressed(false);
            if (WofBootstrap.Instance != null && WofBootstrap.Instance.Mode != WofSessionMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void RefreshPixelScale()
        {
            if (_cardScaleRoot == null)
            {
                return;
            }
            _rootCanvas ??= GetComponentInParent<Canvas>();
            var canvasScale = _rootCanvas == null ? 1f : Mathf.Max(0.01f, _rootCanvas.scaleFactor);
            var inverse = 1f / canvasScale;
            _cardScaleRoot.localScale = new Vector3(inverse, inverse, 1f);
        }

        private static WofPlayerController ResolveLocalPlayer(WofPlayerController cached)
        {
            if (cached != null && cached.IsOwner && !cached.IsDead)
            {
                return cached;
            }
            var players = FindObjectsByType<WofPlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var index = 0; index < players.Length; index++)
            {
                if (players[index] != null && players[index].IsOwner && !players[index].IsDead)
                {
                    return players[index];
                }
            }
            return null;
        }

        private IEnumerator RunControllerProbe()
        {
            var readyDeadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < readyDeadline &&
                   (WofBootstrap.Instance == null || WofBootstrap.Instance.Mode == WofSessionMode.None ||
                    ResolveLocalPlayer(null) == null))
            {
                yield return null;
            }

            _controllerProbeGamepad = InputSystem.AddDevice<Gamepad>("WOF Inventory QA Controller");
            _controllerProbeGamepad.MakeCurrent();
            yield return null;
            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.DpadRight);
            yield return new WaitForSecondsRealtime(0.1f);
            if (!IsOpen)
            {
                FailControllerProbe("open");
                yield break;
            }

            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.A);
            if (!IsQuestJournalOpen)
            {
                FailControllerProbe("journal");
                yield break;
            }

            var navigationExpected = ActiveQuestCount > 1;
            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.DpadDown);
            if (navigationExpected && SelectedQuestIndex != 1)
            {
                FailControllerProbe("navigation");
                yield break;
            }

            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.B);
            if (!IsOpen || IsQuestJournalOpen)
            {
                FailControllerProbe("journal-back");
                yield break;
            }
            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.B);
            if (IsOpen)
            {
                FailControllerProbe("close");
                yield break;
            }

            Debug.Log($"[WOF-AUTOMATION] INVENTORY_CONTROLLER_PROBE_COMPLETE open=true journal=true navigation={(navigationExpected ? "true" : "not-required")} back=true close=true quests={ActiveQuestCount}");
            RemoveControllerProbeGamepad();
        }

        private void FailControllerProbe(string stage)
        {
            Debug.LogError($"[WOF-AUTOMATION] INVENTORY_CONTROLLER_PROBE_FAILED stage={stage}");
            RemoveControllerProbeGamepad();
        }

        private static IEnumerator TapControllerButton(Gamepad gamepad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
        }

        private void RemoveControllerProbeGamepad()
        {
            if (_controllerProbeGamepad != null && _controllerProbeGamepad.added)
            {
                InputSystem.RemoveDevice(_controllerProbeGamepad);
            }
            _controllerProbeGamepad = null;
        }

        private static bool IsControllerProbeRequested()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], "--wof-inventory-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
#endif
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private Text CreateText(string name, Transform parent, Font font, string value, int size, TextAnchor alignment, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(7, size / 2);
            text.resizeTextMaxSize = size;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Color color)
        {
            var buttonObject = CreatePanel(name, parent, color);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            if (!string.IsNullOrEmpty(label))
            {
                var text = CreateText("Label", buttonObject.transform, headingFont, label, 12, TextAnchor.MiddleCenter, EmeraldText);
                SetFullStretch(text.rectTransform, 2f);
            }
            return button;
        }

        private static void AddOutline(GameObject target, Color color)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static void SetFullStretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.localScale = Vector3.one;
        }

        private static void SetCentered(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetTopStretch(RectTransform rect, float top, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        private static void SetBottomStretch(RectTransform rect, float bottom, float height, float left, float right)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
            rect.localScale = Vector3.one;
        }

        private static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private static void SetTopRight(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private sealed class WofInventorySlotView
        {
            private readonly Image _background;
            private readonly Text _mark;
            private readonly Text _quantity;
            private readonly Text _empty;

            public WofInventorySlotView(Image background, Text mark, Text quantity, Text empty)
            {
                _background = background;
                _mark = mark;
                _quantity = quantity;
                _empty = empty;
            }

            public void Refresh(WofInventoryDisplayEntry? entry, int slotNumber)
            {
                if (!entry.HasValue)
                {
                    _background.color = DeepPanelColor;
                    _mark.text = string.Empty;
                    _quantity.text = string.Empty;
                    _empty.text = slotNumber.ToString();
                    return;
                }

                var value = entry.Value;
                _background.color = new Color32(110, 231, 183, 31);
                _mark.text = value.Definition.Name.Substring(0, Mathf.Min(2, value.Definition.Name.Length)).ToUpperInvariant();
                _quantity.text = value.Quantity.ToString();
                _empty.text = string.Empty;
            }
        }
    }

    public sealed class WofInventoryQuestHover : MonoBehaviour, IPointerEnterHandler
    {
        private WofInventoryRuntime _runtime;
        private int _questIndex;

        public void Configure(WofInventoryRuntime runtime, int questIndex)
        {
            _runtime = runtime;
            _questIndex = questIndex;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _runtime?.SelectQuest(_questIndex);
        }
    }
}
