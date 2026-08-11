using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofQuestDevRuntime : MonoBehaviour
    {
        private static readonly Color32 Cyan50 = new(236, 254, 255, 255);
        private static readonly Color32 CyanMuted = new(207, 250, 254, 150);
        private static readonly Color32 CyanBorder = new(165, 243, 252, 179);
        private static readonly Color32 CyanSoftBorder = new(103, 232, 249, 85);
        private static readonly Color32 Yellow = new(254, 249, 195, 255);
        private static readonly Color32 YellowBorder = new(254, 240, 138, 179);
        private static readonly Color32 Panel = new(5, 7, 17, 246);
        private static readonly Color32 Field = new(0, 0, 0, 180);
        private static readonly Color32 Transparent = new(0, 0, 0, 0);

        [SerializeField] private WofHud hud;
        [SerializeField] private Font font;

        private Canvas _canvas;
        private GameObject _overlay;
        private RectTransform _cardScale;
        private GameObject _indicator;
        private Text _headerName;
        private Text _metaText;
        private Text _previewName;
        private Text _previewDialog;
        private Text _eventSummary;
        private InputField _nameInput;
        private Dropdown _roleDropdown;
        private InputField _townInput;
        private InputField _greetingInput;
        private InputField _titleInput;
        private InputField _dialogInput;
        private InputField _eventsInput;
        private InputField _eventMessageInput;
        private InputField _eventQuestInput;
        private InputField _eventFlagInput;
        private RectTransform _pointContent;
        private readonly List<GameObject> _pointButtons = new();
        private WofQuestNpcEditorTarget _target;
        private WofQuestNpcProgram _draft;
        private string _selectedPointId;
        private bool _modeEnabled;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private bool _viewBuilt;

        public bool IsModeEnabled => _modeEnabled;
        public bool IsOpen => _overlay != null && _overlay.activeSelf;
        public WofQuestNpcProgram Draft => WofQuestDevRules.CloneProgram(_draft);

        public void ConfigureGeneratedView(WofHud generatedHud, Font generatedFont)
        {
            hud = generatedHud;
            font = generatedFont;
            EnsureView();
        }

        private void Awake()
        {
            hud ??= FindFirstObjectByType<WofHud>();
            font ??= GetComponentInChildren<Text>(true)?.font;
            _canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
            EnsureView();
        }

        private void Start()
        {
            if (HasArgument("--wof-quest-dev-probe"))
            {
                StartCoroutine(RunAutomationProbe());
            }
        }

        private void Update()
        {
            if (!_viewBuilt) EnsureView();
            if (!_viewBuilt) return;
            if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height) ApplyLayout();
            _indicator.SetActive(_modeEnabled && !IsOpen);
            if (IsOpen)
            {
                if (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) Close();
                return;
            }
            if (!_modeEnabled || WofInputRouter.GameplaySuppressed ||
                !(Mouse.current?.rightButton.wasPressedThisFrame ?? false)) return;
            if (TryResolveTargetedVillager(out var manager, out var villager))
            {
                OpenEditor(CreateTarget(manager, villager));
            }
        }

        private void OnDestroy()
        {
            if (IsOpen) RestoreGameplay();
        }

        public void SetModeEnabled(bool enabled)
        {
            _modeEnabled = enabled;
            if (!enabled && IsOpen) Close();
            if (_indicator != null) _indicator.SetActive(enabled && !IsOpen);
            Debug.Log($"[WOF-AUTOMATION] QUEST_DEV_MODE enabled={enabled.ToString().ToLowerInvariant()}");
        }

        public void OpenManualEditor(string npcId)
        {
            var cleanId = string.IsNullOrWhiteSpace(npcId) ? "manual-dev-npc" : npcId;
            SetModeEnabled(true);
            OpenEditor(new WofQuestNpcEditorTarget(
                cleanId,
                "manual-dev-town",
                cleanId,
                "Manual Quest NPC",
                "village",
                Vector3.zero));
        }

        public void OpenEditor(WofQuestNpcEditorTarget target)
        {
            EnsureView();
            if (!_viewBuilt) return;
            _target = target;
            var saved = WofQuestDevStore.FindProgram(target.NpcId);
            _draft = saved ?? WofQuestDevRules.CreateDefaultProgram(target);
            _draft = WofQuestDevRules.CloneProgram(_draft);
            _selectedPointId = _draft.scriptPoints.Length > 0 ? _draft.scriptPoints[0].id : null;
            WofInputRouter.SetGameplaySuppressed(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _overlay.SetActive(true);
            RefreshAll();
            ApplyLayout();
            Debug.Log($"[WOF-AUTOMATION] QUEST_DEV_EDITOR_OPEN npc={target.NpcId} points={_draft.scriptPoints.Length}");
        }

        public void Close()
        {
            if (!IsOpen) return;
            EventSystem.current?.SetSelectedGameObject(null);
            _overlay.SetActive(false);
            RestoreGameplay();
            _indicator.SetActive(_modeEnabled);
            Debug.Log("[WOF-AUTOMATION] QUEST_DEV_EDITOR_CLOSED");
        }

        public string ClaimDarrelHere()
        {
            if (!TryResolveTargetedVillager(out var manager, out var villager))
            {
                var managers = FindObjectsByType<WofVillagerManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (var index = 0; index < managers.Length; index++)
                {
                    if (managers[index] == null || managers[index].InsideVillager == null) continue;
                    manager = managers[index];
                    villager = managers[index].InsideVillager;
                    break;
                }
            }
            if (villager == null || manager == null) return "No villager or occupied hut found for Darrel.";
            var target = CreateTarget(manager, villager, "Darrel");
            WofQuestDevStore.ClaimDarrel(target);
            return $"Darrel assigned to hut {target.HutId}.";
        }

        public string SetDarrelSpawnHere()
        {
            var player = ResolveLivingLocalPlayer();
            if (player == null) return "Could not read current player position for Darrel spawn.";
            var spawn = WofDarrelQuestSpawnStore.SaveOverride(player.transform.position, player.transform.eulerAngles.y);
            return $"Darrel realm spawn set: X {spawn.Position.x:F1} Y {spawn.Position.y:F1} Z {spawn.Position.z:F1}";
        }

        public string ResetDarrelSpawn()
        {
            WofDarrelQuestSpawnStore.Clear();
            return "Darrel realm spawn reset to authored default.";
        }

        private void EnsureView()
        {
            if (_viewBuilt || font == null) return;
            _canvas ??= GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
            BuildView();
            _viewBuilt = true;
            ApplyLayout();
        }

        private void BuildView()
        {
            _indicator = CreatePanel("QuestDevIndicator", transform, new Color32(0, 0, 0, 166));
            var indicatorRect = _indicator.GetComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(0f, 1f);
            indicatorRect.anchorMax = new Vector2(0f, 1f);
            indicatorRect.pivot = new Vector2(0f, 1f);
            indicatorRect.anchoredPosition = new Vector2(12f, -12f);
            indicatorRect.sizeDelta = new Vector2(104f, 28f);
            _indicator.GetComponent<Image>().color = new Color32(0, 0, 0, 166);
            var indicatorOutline = _indicator.AddComponent<Outline>();
            indicatorOutline.effectColor = new Color32(254, 240, 138, 140);
            indicatorOutline.effectDistance = new Vector2(1f, -1f);
            SetFull(CreateText("Label", _indicator.transform, "QUEST DEV", 9, TextAnchor.MiddleCenter, Yellow).rectTransform);
            _indicator.SetActive(false);

            _overlay = CreatePanel("QuestNpcEditorOverlay", transform, new Color32(0, 0, 0, 184));
            SetFull(_overlay.GetComponent<RectTransform>());
            var overlayCanvas = _overlay.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 5000;
            _overlay.AddComponent<GraphicRaycaster>();
            _cardScale = new GameObject("QuestNpcEditorPixelScale", typeof(RectTransform)).GetComponent<RectTransform>();
            _cardScale.SetParent(_overlay.transform, false);
            _cardScale.anchorMin = _cardScale.anchorMax = _cardScale.pivot = new Vector2(0.5f, 0.5f);
            var card = CreatePanel("Card", _cardScale, Panel);
            SetFull(card.GetComponent<RectTransform>());
            var cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = CyanBorder;
            cardOutline.effectDistance = new Vector2(2f, -2f);

            var header = CreatePanel("Header", card.transform, Transparent);
            SetTopLeft(header.GetComponent<RectTransform>(), 0f, 0f, 1180f, 60f);
            var kicker = CreateText("Kicker", header.transform, "QUEST NPC DEV", 9, TextAnchor.MiddleLeft, CyanMuted);
            SetTopLeft(kicker.rectTransform, 14f, 7f, 250f, 18f);
            _headerName = CreateText("NpcName", header.transform, "QUEST NPC", 18, TextAnchor.MiddleLeft, Yellow);
            SetTopLeft(_headerName.rectTransform, 14f, 24f, 620f, 30f);
            var saveButton = CreateButton("Save", header.transform, "SAVE", new Color32(16, 80, 62, 140), new Color32(167, 243, 208, 255));
            SetTopRight(saveButton.GetComponent<RectTransform>(), 94f, 13f, 76f, 34f);
            saveButton.onClick.AddListener(SaveDraft);
            var closeButton = CreateButton("Close", header.transform, "CLOSE", new Color32(8, 64, 82, 140), Cyan50);
            SetTopRight(closeButton.GetComponent<RectTransform>(), 12f, 13f, 76f, 34f);
            closeButton.onClick.AddListener(Close);

            var divider = CreatePanel("HeaderDivider", card.transform, CyanSoftBorder);
            SetTopLeft(divider.GetComponent<RectTransform>(), 0f, 59f, 1180f, 1f);
            var sidebar = CreatePanel("Sidebar", card.transform, new Color32(8, 51, 68, 55));
            SetTopLeft(sidebar.GetComponent<RectTransform>(), 0f, 60f, 280f, 636f);
            BuildSidebar(sidebar.transform);
            var sidebarDivider = CreatePanel("SidebarDivider", card.transform, CyanSoftBorder);
            SetTopLeft(sidebarDivider.GetComponent<RectTransform>(), 279f, 60f, 1f, 636f);

            var pointList = CreatePanel("ScriptPointList", card.transform, Transparent);
            SetTopLeft(pointList.GetComponent<RectTransform>(), 292f, 60f, 190f, 636f);
            BuildPointList(pointList.transform);
            var detail = CreatePanel("ScriptPointDetail", card.transform, Transparent);
            SetTopLeft(detail.GetComponent<RectTransform>(), 494f, 60f, 674f, 636f);
            BuildDetail(detail.transform);
            _overlay.SetActive(false);
        }

        private void BuildSidebar(Transform parent)
        {
            CreateFieldLabel(parent, "NAME", 12f, 12f, 250f);
            _nameInput = CreateInput("Name", parent, 12f, 28f, 256f, 36f, 42, false, string.Empty);
            _nameInput.onValueChanged.AddListener(value => { if (_draft != null) { _draft.displayName = value; RefreshPreview(); } });
            CreateFieldLabel(parent, "ROLE", 12f, 72f, 250f);
            _roleDropdown = CreateRoleDropdown(parent, 12f, 88f, 256f, 36f);
            _roleDropdown.onValueChanged.AddListener(value => { if (_draft != null) _draft.role = (WofQuestNpcRole)Mathf.Clamp(value, 0, 2); });
            CreateFieldLabel(parent, "TOWN", 12f, 132f, 250f);
            _townInput = CreateInput("Town", parent, 12f, 148f, 256f, 36f, 64, false, string.Empty);
            _townInput.onValueChanged.AddListener(value => { if (_draft != null) _draft.townId = value; });
            CreateFieldLabel(parent, "OPENING LINE", 12f, 192f, 250f);
            _greetingInput = CreateInput("Greeting", parent, 12f, 208f, 256f, 98f, 900, true, string.Empty);
            _greetingInput.onValueChanged.AddListener(value => { if (_draft != null) { _draft.greeting = value; RefreshPreview(); } });

            var addButton = CreateButton("AddPoint", parent, "ADD POINT", new Color32(8, 64, 82, 110), Cyan50);
            SetTopLeft(addButton.GetComponent<RectTransform>(), 12f, 318f, 124f, 36f);
            addButton.onClick.AddListener(AddPoint);
            var deleteButton = CreateButton("DeletePoint", parent, "DELETE", new Color32(100, 20, 30, 100), new Color32(254, 226, 226, 255));
            SetTopLeft(deleteButton.GetComponent<RectTransform>(), 144f, 318f, 124f, 36f);
            deleteButton.onClick.AddListener(DeletePoint);
            var resetButton = CreateButton("ResetNpc", parent, "RESET NPC", new Color32(63, 63, 70, 90), new Color32(244, 244, 245, 255));
            SetTopLeft(resetButton.GetComponent<RectTransform>(), 12f, 362f, 256f, 36f);
            resetButton.onClick.AddListener(ResetProgram);
            _metaText = CreateText("Meta", parent, string.Empty, 10, TextAnchor.UpperLeft, new Color32(207, 250, 254, 105));
            SetTopLeft(_metaText.rectTransform, 12f, 414f, 256f, 80f);
        }

        private void BuildPointList(Transform parent)
        {
            var label = CreateText("Label", parent, "SCRIPTPOINTS", 9, TextAnchor.MiddleLeft, CyanMuted);
            SetTopLeft(label.rectTransform, 0f, 12f, 190f, 18f);
            var viewport = CreatePanel("Viewport", parent, new Color32(0, 0, 0, 40));
            SetTopLeft(viewport.GetComponent<RectTransform>(), 0f, 34f, 190f, 522f);
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            _pointContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            _pointContent.SetParent(viewport.transform, false);
            _pointContent.anchorMin = new Vector2(0f, 1f);
            _pointContent.anchorMax = new Vector2(1f, 1f);
            _pointContent.pivot = new Vector2(0.5f, 1f);
            _pointContent.anchoredPosition = Vector2.zero;
            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = _pointContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 20f;

            var up = CreateButton("MoveUp", parent, "UP", new Color32(8, 64, 82, 110), Cyan50);
            SetTopLeft(up.GetComponent<RectTransform>(), 0f, 568f, 60f, 36f);
            up.onClick.AddListener(() => MovePoint(-1));
            var down = CreateButton("MoveDown", parent, "DOWN", new Color32(8, 64, 82, 110), Cyan50);
            SetTopLeft(down.GetComponent<RectTransform>(), 65f, 568f, 60f, 36f);
            down.onClick.AddListener(() => MovePoint(1));
            var copy = CreateButton("Copy", parent, "COPY", new Color32(8, 64, 82, 110), Cyan50);
            SetTopLeft(copy.GetComponent<RectTransform>(), 130f, 568f, 60f, 36f);
            copy.onClick.AddListener(CopyPoint);
        }

        private void BuildDetail(Transform parent)
        {
            CreateFieldLabel(parent, "POINT TITLE", 0f, 12f, 674f);
            _titleInput = CreateInput("PointTitle", parent, 0f, 28f, 674f, 36f, 48, false, string.Empty);
            _titleInput.onValueChanged.AddListener(value => { var point = SelectedPoint; if (point != null) { point.title = value; RefreshPointList(); RefreshPreview(); } });
            CreateFieldLabel(parent, "DIALOG", 0f, 72f, 674f);
            _dialogInput = CreateInput("Dialog", parent, 0f, 88f, 674f, 112f, 900, true, string.Empty);
            _dialogInput.onValueChanged.AddListener(value => { var point = SelectedPoint; if (point != null) { point.dialog = value; RefreshPreview(); } });
            CreateFieldLabel(parent, "EVENTS", 0f, 208f, 382f);
            _eventsInput = CreateInput("Events", parent, 0f, 224f, 382f, 232f, 900, true,
                "unlockSpell blink\nunlockRandomLockedSpell\nstartQuest town_01_fetch\ncompleteQuest town_01_fetch\nsetFlag town_01_quests=1\nmessage Good work, wizard.");
            _eventsInput.onValueChanged.AddListener(value => { var point = SelectedPoint; if (point != null) { point.eventScript = value; RefreshPreview(); } });
            BuildEventBuilder(parent, 394f, 208f, 280f, 248f);
            BuildPreview(parent, 0f, 468f, 674f, 140f);
        }

        private void BuildEventBuilder(Transform parent, float left, float top, float width, float height)
        {
            var panel = CreatePanel("EventBuilder", parent, new Color32(8, 51, 68, 35));
            SetTopLeft(panel.GetComponent<RectTransform>(), left, top, width, height);
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = CyanSoftBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            var label = CreateText("Label", panel.transform, "EVENT BUILDER", 9, TextAnchor.MiddleLeft, CyanMuted);
            SetTopLeft(label.rectTransform, 8f, 4f, width - 16f, 18f);
            for (var index = 0; index < WofQuestDevRules.EventPresets.Count; index++)
            {
                var preset = WofQuestDevRules.EventPresets[index];
                var button = CreateButton($"Preset{index}", panel.transform, preset.Label, new Color32(0, 0, 0, 70), Cyan50);
                var row = index / 2;
                var column = index % 2;
                SetTopLeft(button.GetComponent<RectTransform>(), 8f + column * 132f, 24f + row * 25f, 126f, 22f);
                var line = preset.Line;
                button.onClick.AddListener(() => AppendEvent(line));
            }
            _eventMessageInput = CreateInput("EventMessage", panel.transform, 8f, 126f, 194f, 26f, 120, false, "Good work, wizard.");
            var addMessage = CreateButton("AddMessage", panel.transform, "ADD", new Color32(8, 64, 82, 110), Cyan50);
            SetTopLeft(addMessage.GetComponent<RectTransform>(), 208f, 126f, 64f, 26f);
            addMessage.onClick.AddListener(() => AppendBuiltEvent(WofQuestEventBuilderKind.Message));
            _eventQuestInput = CreateInput("EventQuest", panel.transform, 8f, 158f, 194f, 26f, 80, false, "town_01_quest");
            var start = CreateButton("StartQuest", panel.transform, "START", new Color32(8, 64, 82, 110), Cyan50);
            SetTopLeft(start.GetComponent<RectTransform>(), 208f, 158f, 64f, 26f);
            start.onClick.AddListener(() => AppendBuiltEvent(WofQuestEventBuilderKind.StartQuest));
            var complete = CreateButton("CompleteQuest", panel.transform, "COMPLETE", new Color32(8, 64, 82, 110), Cyan50);
            SetTopLeft(complete.GetComponent<RectTransform>(), 208f, 190f, 64f, 26f);
            complete.onClick.AddListener(() => AppendBuiltEvent(WofQuestEventBuilderKind.CompleteQuest));
            _eventFlagInput = CreateInput("EventFlag", panel.transform, 8f, 190f, 194f, 26f, 120, false, "town_01_quests=1");
            var setFlag = CreateButton("SetFlag", panel.transform, "SET FLAG", new Color32(8, 64, 82, 110), Cyan50);
            SetTopLeft(setFlag.GetComponent<RectTransform>(), 208f, 222f, 64f, 22f);
            setFlag.onClick.AddListener(() => AppendBuiltEvent(WofQuestEventBuilderKind.SetFlag));
        }

        private void BuildPreview(Transform parent, float left, float top, float width, float height)
        {
            var preview = CreatePanel("Preview", parent, new Color32(0, 0, 0, 70));
            SetTopLeft(preview.GetComponent<RectTransform>(), left, top, 382f, height);
            var label = CreateText("Label", preview.transform, "PREVIEW", 9, TextAnchor.MiddleLeft, CyanMuted);
            SetTopLeft(label.rectTransform, 8f, 5f, 360f, 18f);
            _previewName = CreateText("Name", preview.transform, string.Empty, 14, TextAnchor.MiddleLeft, Yellow);
            SetTopLeft(_previewName.rectTransform, 8f, 24f, 360f, 24f);
            _previewDialog = CreateText("Dialog", preview.transform, string.Empty, 11, TextAnchor.UpperLeft, Cyan50);
            SetTopLeft(_previewDialog.rectTransform, 8f, 50f, 366f, 80f);

            var testPanel = CreatePanel("TestPoint", parent, new Color32(113, 63, 18, 32));
            SetTopLeft(testPanel.GetComponent<RectTransform>(), left + 394f, top, 280f, height);
            var testOutline = testPanel.AddComponent<Outline>();
            testOutline.effectColor = new Color32(254, 240, 138, 90);
            testOutline.effectDistance = new Vector2(1f, -1f);
            var scriptLabel = CreateText("Label", testPanel.transform, "SCRIPTPOINT", 9, TextAnchor.MiddleLeft, new Color32(254, 249, 195, 170));
            SetTopLeft(scriptLabel.rectTransform, 8f, 5f, 260f, 18f);
            _eventSummary = CreateText("Summary", testPanel.transform, string.Empty, 11, TextAnchor.UpperLeft, Yellow);
            SetTopLeft(_eventSummary.rectTransform, 8f, 26f, 264f, 60f);
            var test = CreateButton("Test", testPanel.transform, "TEST POINT", new Color32(113, 63, 18, 70), Yellow);
            SetTopLeft(test.GetComponent<RectTransform>(), 8f, 96f, 264f, 34f);
            test.onClick.AddListener(TestPoint);
        }

        private void RefreshAll()
        {
            if (_draft == null) return;
            _headerName.text = string.IsNullOrEmpty(_draft.displayName) ? _target.DefaultName : _draft.displayName;
            _nameInput.SetTextWithoutNotify(_draft.displayName);
            _roleDropdown.SetValueWithoutNotify((int)_draft.role);
            _roleDropdown.RefreshShownValue();
            _townInput.SetTextWithoutNotify(_draft.townId);
            _greetingInput.SetTextWithoutNotify(_draft.greeting);
            var profile = WofSurvivalProfileStore.Load() ?? new WofSurvivalProfile { playerName = "WIZARD" };
            _metaText.text = $"{_target.NpcId} - {_target.Theme} - unlocked spells tracked: {profile.questUnlockedSpells.Length}";
            RefreshPointList();
            RefreshSelectedFields();
        }

        private void RefreshPointList()
        {
            if (_pointContent == null || _draft?.scriptPoints == null) return;
            for (var index = 0; index < _pointButtons.Count; index++) Destroy(_pointButtons[index]);
            _pointButtons.Clear();
            for (var index = 0; index < _draft.scriptPoints.Length; index++)
            {
                var point = _draft.scriptPoints[index];
                var selected = point != null && string.Equals(point.id, _selectedPointId, StringComparison.Ordinal);
                var button = CreateButton(
                    $"Point{index}",
                    _pointContent,
                    $"{index + 1}. {(string.IsNullOrEmpty(point?.title) ? $"Point {index + 1}" : point.title)}",
                    selected ? new Color32(113, 63, 18, 70) : new Color32(0, 0, 0, 65),
                    selected ? Yellow : CyanMuted,
                    TextAnchor.MiddleLeft);
                SetTopLeft(button.GetComponent<RectTransform>(), 0f, index * 38f, 190f, 34f);
                var pointId = point?.id;
                button.onClick.AddListener(() => SelectPoint(pointId));
                _pointButtons.Add(button.gameObject);
            }
            _pointContent.sizeDelta = new Vector2(0f, Mathf.Max(522f, _draft.scriptPoints.Length * 38f));
        }

        private void RefreshSelectedFields()
        {
            var point = SelectedPoint;
            if (point == null) return;
            _selectedPointId = point.id;
            _titleInput.SetTextWithoutNotify(point.title);
            _dialogInput.SetTextWithoutNotify(point.dialog);
            _eventsInput.SetTextWithoutNotify(point.eventScript);
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (_draft == null) return;
            var point = SelectedPoint;
            if (point == null) return;
            _headerName.text = string.IsNullOrEmpty(_draft.displayName) ? _target.DefaultName : _draft.displayName;
            _previewName.text = _headerName.text;
            _previewDialog.text = WofQuestDevRules.GetDialogPreview(point, _draft.greeting);
            _eventSummary.text = WofQuestDevRules.FormatEventSummary(point, WofQuestDevRules.GetPointIndex(_draft, point));
        }

        private void SelectPoint(string pointId)
        {
            _selectedPointId = pointId;
            RefreshPointList();
            RefreshSelectedFields();
        }

        private void AddPoint()
        {
            if (_draft == null || _draft.scriptPoints.Length >= WofQuestDevRules.MaximumScriptPointCount) return;
            var point = WofQuestDevRules.CreatePoint(_draft.scriptPoints.Length);
            var next = new WofQuestScriptPoint[_draft.scriptPoints.Length + 1];
            Array.Copy(_draft.scriptPoints, next, _draft.scriptPoints.Length);
            next[^1] = point;
            _draft.scriptPoints = next;
            _selectedPointId = point.id;
            RefreshPointList();
            RefreshSelectedFields();
        }

        private void DeletePoint()
        {
            var point = SelectedPoint;
            var index = WofQuestDevRules.GetPointIndex(_draft, point);
            var removal = WofQuestDevRules.RemovePoint(_draft?.scriptPoints, point?.id, index);
            if (!removal.HasValue)
            {
                ShowMessages(new[] { "A dialog needs at least one scriptpoint" });
                return;
            }
            _draft.scriptPoints = removal.Value.Points;
            _selectedPointId = removal.Value.NextSelectedPointId;
            RefreshPointList();
            RefreshSelectedFields();
        }

        private void MovePoint(int direction)
        {
            if (_draft == null || !WofQuestDevRules.MovePoint(_draft.scriptPoints, _selectedPointId, direction)) return;
            RefreshPointList();
            RefreshPreview();
        }

        private void CopyPoint()
        {
            var point = SelectedPoint;
            if (point == null || _draft.scriptPoints.Length >= WofQuestDevRules.MaximumScriptPointCount) return;
            var copy = WofQuestDevRules.DuplicatePoint(point);
            _draft.scriptPoints = WofQuestDevRules.InsertPointAfter(_draft.scriptPoints, point.id, copy);
            _selectedPointId = copy.id;
            RefreshPointList();
            RefreshSelectedFields();
        }

        private void AppendEvent(string line)
        {
            var point = SelectedPoint;
            if (point == null) return;
            point.eventScript = WofQuestDevRules.AppendEventLine(point.eventScript, line);
            _eventsInput.SetTextWithoutNotify(point.eventScript);
            RefreshPreview();
        }

        private void AppendBuiltEvent(WofQuestEventBuilderKind kind)
        {
            AppendEvent(WofQuestDevRules.BuildEventLine(
                kind,
                _eventMessageInput.text,
                _eventQuestInput.text,
                _eventFlagInput.text));
        }

        private void SaveDraft()
        {
            if (_draft == null) return;
            var clean = WofQuestDevRules.SanitizeProgram(
                _draft,
                _target,
                now: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (!WofQuestDevStore.SaveProgram(clean, _target)) return;
            _draft = WofQuestDevRules.CloneProgram(clean);
            ShowMessages(new[] { $"Saved {_draft.displayName}" });
            RefreshAll();
            Debug.Log($"[WOF-AUTOMATION] QUEST_DEV_PROGRAM_SAVED npc={_draft.npcId} points={_draft.scriptPoints.Length}");
        }

        private void ResetProgram()
        {
            WofQuestDevStore.RemoveProgram(_target.NpcId);
            _draft = WofQuestDevRules.CreateDefaultProgram(_target);
            _selectedPointId = _draft.scriptPoints[0]?.id;
            ShowMessages(new[] { "NPC program reset" });
            RefreshAll();
        }

        private void TestPoint()
        {
            SaveDraft();
            var point = SelectedPoint;
            if (point == null) return;
            var profile = WofSurvivalProfileStore.Load() ?? new WofSurvivalProfile { playerName = "WIZARD" };
            var result = WofQuestScriptRuntime.Execute(
                profile,
                _draft,
                point,
                UnityEngine.Random.value,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (result.ProfileChanged) WofSurvivalProfileStore.Save(profile);
            ShowMessages(result.Messages);
            var player = ResolveLivingLocalPlayer();
            if (player != null)
            {
                if (result.Teleport == WofQuestScriptTeleport.DarrelGrove) player.RequestDarrelGroveTeleport();
                else if (result.Teleport == WofQuestScriptTeleport.LilyCoil) player.RequestMapFastTravel(WofMapDestination.LilyCoil);
            }
            Debug.Log($"[WOF-AUTOMATION] QUEST_DEV_POINT_TEST npc={_draft.npcId} point={point.id} events={WofQuestDevRules.CountEvents(point)} changed={result.ProfileChanged.ToString().ToLowerInvariant()} teleport={result.Teleport}");
        }

        private WofQuestScriptPoint SelectedPoint => WofQuestDevRules.GetSelectedPoint(_draft, _selectedPointId);

        private bool TryResolveTargetedVillager(out WofVillagerManager selectedManager, out WofVillagerBillboard selectedVillager)
        {
            selectedManager = null;
            selectedVillager = null;
            var camera = Camera.main;
            if (camera == null) return false;
            var managers = FindObjectsByType<WofVillagerManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var bestScore = float.PositiveInfinity;
            for (var index = 0; index < managers.Length; index++)
            {
                var manager = managers[index];
                if (manager == null || !manager.TryGetTargetedVillager(camera, out var candidate) || candidate == null ||
                    !WofQuestTargetMath.TryScoreTarget(camera.transform.position, camera.transform.forward, candidate.InteractionCenter, out var score) ||
                    score >= bestScore) continue;
                bestScore = score;
                selectedManager = manager;
                selectedVillager = candidate;
            }
            return selectedVillager != null;
        }

        private static WofQuestNpcEditorTarget CreateTarget(
            WofVillagerManager manager,
            WofVillagerBillboard villager,
            string forcedName = null)
        {
            var townId = manager.GetReactTownId(villager);
            var theme = townId.Contains("desert", StringComparison.OrdinalIgnoreCase)
                ? "egyptian"
                : townId.Contains("swamp", StringComparison.OrdinalIgnoreCase)
                    ? "swamp"
                    : "village";
            return new WofQuestNpcEditorTarget(
                villager.VillagerId,
                townId,
                villager.VillagerId,
                forcedName ?? manager.GetReactDisplayName(villager),
                theme,
                villager.transform.position);
        }

        private static WofPlayerController ResolveLivingLocalPlayer()
        {
            var players = FindObjectsByType<WofPlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var index = 0; index < players.Length; index++)
            {
                if (players[index] != null && players[index].IsOwner && !players[index].IsDead) return players[index];
            }
            return null;
        }

        private void ShowMessages(string[] messages)
        {
            FindFirstObjectByType<WofQuestDialogRuntime>()?.ShowSystemMessages(messages);
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

        private IEnumerator RunAutomationProbe()
        {
            var deadline = Time.realtimeSinceStartup + 30f;
            while ((WofBootstrap.Instance == null || WofBootstrap.Instance.Mode == WofSessionMode.None || ResolveLivingLocalPlayer() == null) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (WofBootstrap.Instance == null || WofBootstrap.Instance.Mode == WofSessionMode.None)
            {
                Debug.LogError("[WOF-AUTOMATION] QUEST_DEV_PROBE_FAILED stage=session");
                yield break;
            }
            SetModeEnabled(true);
            OpenManualEditor("manual-dev-npc");
            yield return null;
            _draft.displayName = "Manual Quest NPC QA";
            _nameInput.SetTextWithoutNotify(_draft.displayName);
            AddPoint();
            var point = SelectedPoint;
            point.title = "QA Point";
            point.dialog = "Quest editor physical verification.";
            point.eventScript = "setFlag qa:questdev=true\nmessage Quest editor test passed.";
            RefreshAll();
            SaveDraft();
            TestPoint();
            yield return new WaitForSecondsRealtime(1f);
            var capturePath = GetArgumentValue("--wof-quest-dev-capture");
            if (!string.IsNullOrWhiteSpace(capturePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(capturePath)) ?? ".");
                ScreenCapture.CaptureScreenshot(capturePath);
            }
            var profile = WofSurvivalProfileStore.Load() ?? new WofSurvivalProfile { playerName = "WIZARD" };
            var passed = IsOpen && WofQuestDevStore.FindProgram("manual-dev-npc")?.scriptPoints.Length == 2 &&
                         string.Equals(WofSpellQuestRules.GetFlag(profile, "qa:questdev"), "true", StringComparison.Ordinal);
            if (!passed)
            {
                Debug.LogError("[WOF-AUTOMATION] QUEST_DEV_PROBE_FAILED stage=verification");
                yield break;
            }
            Debug.Log($"[WOF-AUTOMATION] QUEST_DEV_PROBE_COMPLETE open=true saved=true points=2 flag=true eventCount={WofQuestDevRules.CountEvents(point)} capture={capturePath}");
        }

        private void ApplyLayout()
        {
            if (_cardScale == null) return;
            _canvas ??= GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
            var canvasScale = _canvas == null ? 1f : Mathf.Max(0.01f, _canvas.scaleFactor);
            var width = Mathf.Min(1180f, Mathf.Max(760f, Screen.width - 24f));
            var height = Mathf.Min(696f, Mathf.Max(560f, Screen.height - 24f));
            var fit = Mathf.Min(1f, (Screen.width - 24f) / 1180f, (Screen.height - 24f) / 696f);
            _cardScale.localScale = Vector3.one * (fit / canvasScale);
            _cardScale.sizeDelta = new Vector2(1180f, 696f);
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }

        private InputField CreateInput(
            string name,
            Transform parent,
            float left,
            float top,
            float width,
            float height,
            int limit,
            bool multiline,
            string placeholderValue)
        {
            var background = CreatePanel(name, parent, Field);
            SetTopLeft(background.GetComponent<RectTransform>(), left, top, width, height);
            var outline = background.AddComponent<Outline>();
            outline.effectColor = CyanSoftBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            var placeholder = CreateText("Placeholder", background.transform, placeholderValue, multiline ? 10 : 11, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, new Color32(207, 250, 254, 80));
            SetInset(placeholder.rectTransform, 7f, 7f, 5f, 5f);
            var text = CreateText("Text", background.transform, string.Empty, multiline ? 10 : 11, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, Cyan50);
            SetInset(text.rectTransform, 7f, 7f, 5f, 5f);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = multiline ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
            var input = background.AddComponent<InputField>();
            input.targetGraphic = background.GetComponent<Image>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = limit;
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.contentType = InputField.ContentType.Standard;
            input.caretColor = Cyan50;
            input.selectionColor = new Color32(254, 240, 138, 80);
            return input;
        }

        private Dropdown CreateRoleDropdown(Transform parent, float left, float top, float width, float height)
        {
            var root = CreatePanel("Role", parent, Field);
            SetTopLeft(root.GetComponent<RectTransform>(), left, top, width, height);
            var caption = CreateText("Label", root.transform, "VILLAGER", 11, TextAnchor.MiddleLeft, Cyan50);
            SetInset(caption.rectTransform, 8f, 28f, 4f, 4f);
            var arrow = CreateText("Arrow", root.transform, "v", 12, TextAnchor.MiddleCenter, CyanMuted);
            SetTopRight(arrow.rectTransform, 4f, 4f, 24f, height - 8f);
            var template = CreatePanel("Template", root.transform, Panel);
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -2f);
            templateRect.sizeDelta = new Vector2(0f, 96f);
            var viewport = CreatePanel("Viewport", template.transform, Transparent);
            SetFull(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport.transform, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 96f);
            var item = CreatePanel("Item", content, new Color32(0, 0, 0, 210));
            SetTopLeft(item.GetComponent<RectTransform>(), 0f, 0f, width, 32f);
            var toggle = item.AddComponent<Toggle>();
            toggle.targetGraphic = item.GetComponent<Image>();
            var checkmark = CreatePanel("Item Checkmark", item.transform, new Color32(254, 240, 138, 70));
            SetFull(checkmark.GetComponent<RectTransform>());
            toggle.graphic = checkmark.GetComponent<Image>();
            var itemLabel = CreateText("Item Label", item.transform, "VILLAGER", 11, TextAnchor.MiddleLeft, Cyan50);
            SetInset(itemLabel.rectTransform, 8f, 8f, 2f, 2f);
            var scroll = template.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            var dropdown = root.AddComponent<Dropdown>();
            dropdown.targetGraphic = root.GetComponent<Image>();
            dropdown.captionText = caption;
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
            dropdown.options = new List<Dropdown.OptionData>
            {
                new("VILLAGER"), new("QUEST-GIVER"), new("TOWN-LEADER")
            };
            template.SetActive(false);
            return dropdown;
        }

        private void CreateFieldLabel(Transform parent, string value, float left, float top, float width)
        {
            var label = CreateText(value.Replace(" ", string.Empty), parent, value, 9, TextAnchor.MiddleLeft, CyanMuted);
            SetTopLeft(label.rectTransform, left, top, width, 14f);
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private Button CreateButton(
            string name,
            Transform parent,
            string value,
            Color fill,
            Color textColor,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var panel = CreatePanel(name, parent, fill);
            var button = panel.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = CyanSoftBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            var label = CreateText("Label", panel.transform, value, 9, anchor, textColor);
            SetInset(label.rectTransform, anchor == TextAnchor.MiddleLeft ? 7f : 3f, 3f, 2f, 2f);
            return button;
        }

        private Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor, Color color)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void SetFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void SetInset(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetTopRight(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static bool HasArgument(string expected)
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string GetArgumentValue(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase)) return arguments[index + 1];
            }
            return string.Empty;
        }
    }
}
