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
    public sealed class WofQuestDialogRuntime : MonoBehaviour
    {
        private const float InteractionCooldownSeconds = 0.7f;
        private const float ControllerNavigationThreshold = 0.6f;
        private const float ControllerRepeatDelaySeconds = 0.26f;
        private const float ControllerRepeatSeconds = 0.16f;
        private const float NotificationLifetimeSeconds = 12f;
        private const int NotificationLimit = 5;

        [SerializeField] private GameObject dialogRoot;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text lineText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private Text[] choiceNumberTexts;
        [SerializeField] private Text[] choiceLabelTexts;
        [SerializeField] private WofHud hud;
        [SerializeField] private Font dialogFont;

        private WofVillagerManager[] _villagerManagers = Array.Empty<WofVillagerManager>();
        private Camera _camera;
        private WofQuestDialogSession _session;
        private WofSurvivalProfile _profile;
        private int _selectedChoiceIndex;
        private int _heldNavigationDirection;
        private bool _controllerInteractHeld;
        private float _nextNavigationRepeatAt;
        private float _lastInteractionAt = float.NegativeInfinity;
        private RectTransform _dialogBorderRect;
        private RectTransform _dialogCardRect;
        private RectTransform _headerRect;
        private RectTransform _headerDividerRect;
        private RectTransform _questKickerRect;
        private RectTransform _closeBorderRect;
        private RectTransform _lineBorderRect;
        private RectTransform _linePanelRect;
        private RectTransform[] _choiceBorderRects;
        private RectTransform[] _choiceNumberBorderRects;
        private RectTransform _controllerHelpRect;
        private GameObject _notificationRoot;
        private Text[] _notificationTexts;
        private readonly List<QuestNotification> _notifications = new(NotificationLimit);
        private int _lastLayoutScreenWidth;
        private int _lastLayoutScreenHeight;
        private Gamepad _controllerProbeGamepad;
        private string _lastGenericQuestInteractionNpcId;
        private string _lastGenericQuestInteractionTownId;
        private string _lastGenericQuestAssignmentSpell;
        private int _lastGenericQuestMessageCount;

        public bool IsOpen => _session != null;
        public WofQuestDialogSession Session => _session;
        public int SelectedChoiceIndex => _selectedChoiceIndex;

        public void ConfigureGeneratedView(WofHud generatedHud, Font generatedDialogFont)
        {
            hud = generatedHud;
            dialogFont = generatedDialogFont;
        }

        private void Awake()
        {
            if (hud == null)
            {
                hud = FindFirstObjectByType<WofHud>();
            }
            if (dialogRoot == null)
            {
                BuildRuntimeView();
            }
            closeButton?.onClick.AddListener(Close);
            if (choiceButtons != null)
            {
                for (var index = 0; index < choiceButtons.Length; index++)
                {
                    var capturedIndex = index;
                    choiceButtons[index]?.onClick.AddListener(() => Choose(capturedIndex));
                }
            }
            dialogRoot?.SetActive(false);
            if (IsDarrelDragonControllerProbeRequested())
            {
                StartCoroutine(RunDarrelDragonControllerProbe());
            }
            else if (IsDarrelControllerProbeRequested())
            {
                StartCoroutine(RunDarrelControllerProbe());
            }
            else if (IsGenericQuestControllerProbeRequested())
            {
                StartCoroutine(RunGenericQuestControllerProbe());
            }
        }

        private void BuildRuntimeView()
        {
            if (dialogFont == null)
            {
                dialogFont = GetComponentInChildren<Text>(true)?.font;
            }
            if (dialogFont == null)
            {
                Debug.LogError("[WOF-AUTOMATION] QUEST_DIALOG_VIEW_FAILED reason=missing-font");
                return;
            }

            var overlay = CreatePanel(
                "QuestDialogOverlay",
                transform,
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0f, 0f, 0.35f));

            var border = CreatePanel(
                "DialogBorder",
                overlay.transform,
                new Vector2(0.302f, 0.205f),
                new Vector2(0.698f, 0.795f),
                new Color32(255, 251, 220, 166));
            _dialogBorderRect = border.GetComponent<RectTransform>();
            var card = CreatePanel(
                "DialogCard",
                border.transform,
                new Vector2(0.003f, 0.004f),
                new Vector2(0.997f, 0.996f),
                new Color32(7, 6, 17, 245));
            _dialogCardRect = card.GetComponent<RectTransform>();

            var header = CreatePanel(
                "Header",
                card.transform,
                new Vector2(0f, 0.79f),
                Vector2.one,
                new Color32(7, 6, 17, 255));
            _headerRect = header.GetComponent<RectTransform>();
            var headerDivider = CreatePanel(
                "HeaderDivider",
                header.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.012f),
                new Color32(255, 251, 220, 64));
            _headerDividerRect = headerDivider.GetComponent<RectTransform>();
            headerDivider.GetComponent<Image>().raycastTarget = false;
            var questText = CreateText(
                "QuestKicker",
                header.transform,
                dialogFont,
                "QUEST",
                18,
                TextAnchor.LowerLeft,
                new Color32(255, 251, 220, 153));
            SetRect(questText.rectTransform, new Vector2(0.025f, 0.53f), new Vector2(0.72f, 0.84f));
            _questKickerRect = questText.rectTransform;
            DisableTextOutline(questText);
            speakerText = CreateText(
                "Speaker",
                header.transform,
                dialogFont,
                "Darrel",
                34,
                TextAnchor.UpperLeft,
                new Color32(255, 253, 230, 255));
            SetRect(speakerText.rectTransform, new Vector2(0.025f, 0.12f), new Vector2(0.72f, 0.56f));
            DisableTextOutline(speakerText);

            var closeBorder = CreatePanel(
                "CloseBorder",
                header.transform,
                new Vector2(0.82f, 0.26f),
                new Vector2(0.968f, 0.74f),
                new Color32(255, 251, 220, 128));
            _closeBorderRect = closeBorder.GetComponent<RectTransform>();
            closeButton = CreateButton(
                "CloseButton",
                closeBorder.transform,
                dialogFont,
                "CLOSE",
                new Color32(250, 240, 190, 24));
            SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(0.012f, 0.03f), new Vector2(0.988f, 0.97f));
            var closeLabel = closeButton.transform.Find("Label")?.GetComponent<Text>();
            if (closeLabel != null)
            {
                closeLabel.fontSize = 19;
                closeLabel.resizeTextMaxSize = 19;
                DisableTextOutline(closeLabel);
            }

            var lineBorder = CreatePanel(
                "LineBorder",
                card.transform,
                new Vector2(0.025f, 0.43f),
                new Vector2(0.975f, 0.755f),
                new Color32(165, 243, 252, 51));
            _lineBorderRect = lineBorder.GetComponent<RectTransform>();
            var linePanel = CreatePanel(
                "LinePanel",
                lineBorder.transform,
                new Vector2(0.002f, 0.006f),
                new Vector2(0.998f, 0.994f),
                new Color32(8, 51, 68, 51));
            _linePanelRect = linePanel.GetComponent<RectTransform>();
            lineText = CreateText(
                "Line",
                linePanel.transform,
                dialogFont,
                WofQuestDialogRules.OpeningLine,
                24,
                TextAnchor.UpperLeft,
                new Color32(236, 254, 255, 255));
            SetRect(lineText.rectTransform, new Vector2(0.025f, 0.08f), new Vector2(0.975f, 0.92f));
            lineText.horizontalOverflow = HorizontalWrapMode.Wrap;
            lineText.verticalOverflow = VerticalWrapMode.Truncate;
            lineText.resizeTextMinSize = 15;
            DisableTextOutline(lineText);

            choiceButtons = new Button[2];
            choiceNumberTexts = new Text[2];
            choiceLabelTexts = new Text[2];
            _choiceBorderRects = new RectTransform[2];
            _choiceNumberBorderRects = new RectTransform[2];
            for (var index = 0; index < choiceButtons.Length; index++)
            {
                var top = 0.385f - index * 0.14f;
                var bottom = top - 0.115f;
                var choiceBorder = CreatePanel(
                    $"Choice{index + 1}Border",
                    card.transform,
                    new Vector2(0.025f, bottom),
                    new Vector2(0.975f, top),
                    new Color32(255, 251, 220, 102));
                _choiceBorderRects[index] = choiceBorder.GetComponent<RectTransform>();
                var button = CreateButton(
                    $"Choice{index + 1}",
                    choiceBorder.transform,
                    dialogFont,
                    string.Empty,
                    index == 0 ? new Color32(250, 240, 190, 46) : new Color32(0, 0, 0, 89));
                SetRect(button.GetComponent<RectTransform>(), new Vector2(0.002f, 0.02f), new Vector2(0.998f, 0.98f));
                var hiddenLabel = button.transform.Find("Label")?.GetComponent<Text>();
                if (hiddenLabel != null)
                {
                    hiddenLabel.enabled = false;
                }

                var numberBorder = CreatePanel(
                    "NumberBorder",
                    button.transform,
                    new Vector2(0.018f, 0.18f),
                    new Vector2(0.075f, 0.82f),
                    new Color32(255, 251, 220, 128));
                _choiceNumberBorderRects[index] = numberBorder.GetComponent<RectTransform>();
                var number = CreateText(
                    "Number",
                    numberBorder.transform,
                    dialogFont,
                    (index + 1).ToString(),
                    20,
                    TextAnchor.MiddleCenter,
                    new Color32(255, 251, 220, 255));
                SetRect(number.rectTransform, Vector2.zero, Vector2.one);
                DisableTextOutline(number);
                var label = CreateText(
                    "ChoiceLabel",
                    button.transform,
                    dialogFont,
                    index == 0 ? "None of your business." : "What kind of wizard has only 2 spells?",
                    22,
                    TextAnchor.MiddleLeft,
                    new Color32(255, 253, 230, 255));
                SetRect(label.rectTransform, new Vector2(0.095f, 0.08f), new Vector2(0.975f, 0.92f));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.resizeTextMinSize = 15;
                DisableTextOutline(label);
                choiceButtons[index] = button;
                choiceNumberTexts[index] = number;
                choiceLabelTexts[index] = label;
                button.gameObject.AddComponent<WofQuestChoiceHover>().Configure(this, index);
            }

            var helpText = CreateText(
                "ControllerHelp",
                card.transform,
                dialogFont,
                "D-PAD / STICK CHOOSE  -  A SELECT  -  B CLOSE",
                16,
                TextAnchor.MiddleLeft,
                new Color32(255, 251, 220, 115));
            SetRect(helpText.rectTransform, new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.105f));
            _controllerHelpRect = helpText.rectTransform;
            DisableTextOutline(helpText);

            dialogRoot = overlay;
            dialogRoot.SetActive(false);
            BuildNotificationFeed();
        }

        private void BuildNotificationFeed()
        {
            _notificationRoot = new GameObject("LobbyNotificationFeed", typeof(RectTransform));
            _notificationRoot.transform.SetParent(transform, false);
            var rootRect = _notificationRoot.GetComponent<RectTransform>();
            SetTopLeft(rootRect, 18f, 18f, 495f, 420f);
            _notificationTexts = new Text[NotificationLimit];
            for (var index = 0; index < _notificationTexts.Length; index++)
            {
                var label = CreateText(
                    $"SystemMessage{index + 1}",
                    _notificationRoot.transform,
                    dialogFont,
                    string.Empty,
                    15,
                    TextAnchor.UpperLeft,
                    new Color32(236, 254, 255, 255));
                label.resizeTextForBestFit = false;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.lineSpacing = 1f;
                var outline = label.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectDistance = new Vector2(3f, -3f);
                    outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                }
                label.gameObject.SetActive(false);
                _notificationTexts[index] = label;
            }
            _notificationRoot.SetActive(false);
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            SetRect(panel.GetComponent<RectTransform>(), anchorMin, anchorMax);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string value,
            int size,
            TextAnchor anchor,
            Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, size / 2);
            text.resizeTextMaxSize = size;
            textObject.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label, Color color)
        {
            var buttonObject = CreatePanel(name, parent, Vector2.zero, Vector2.one, color);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            var labelText = CreateText("Label", buttonObject.transform, font, label, 22, TextAnchor.MiddleCenter, Color.white);
            SetRect(labelText.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }

        private static void DisableTextOutline(Text text)
        {
            var outline = text == null ? null : text.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetFullInset(
            RectTransform rect,
            float left,
            float right,
            float bottom,
            float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
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

        private static void SetTopLeft(
            RectTransform rect,
            float left,
            float top,
            float width,
            float height)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private static void SetTopRight(
            RectTransform rect,
            float right,
            float top,
            float width,
            float height)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private static void SetMiddleLeft(RectTransform rect, float left, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            RemoveControllerProbeGamepad();
            if (_session != null)
            {
                RestoreGameplay();
            }
        }

        private IEnumerator RunDarrelControllerProbe()
        {
            var readyDeadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < readyDeadline &&
                   (WofBootstrap.Instance == null || WofBootstrap.Instance.Mode == WofSessionMode.None ||
                    !HasLivingLocalPlayer()))
            {
                yield return null;
            }

            _controllerProbeGamepad = InputSystem.AddDevice<Gamepad>("WOF Darrel QA Controller");
            _controllerProbeGamepad.MakeCurrent();
            var openDeadline = Time.realtimeSinceStartup + 15f;
            while (!IsOpen && Time.realtimeSinceStartup < openDeadline)
            {
                yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.X);
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (!IsOpen)
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_CONTROLLER_PROBE_FAILED stage=open");
                RemoveControllerProbeGamepad();
                yield break;
            }

            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.DpadDown);
            if (SelectedChoiceIndex != 1)
            {
                Debug.LogError($"[WOF-AUTOMATION] DARREL_CONTROLLER_PROBE_FAILED stage=navigate selected={SelectedChoiceIndex}");
                RemoveControllerProbeGamepad();
                yield break;
            }

            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.A);
            if (!IsOpen || Session == null || !string.Equals(Session.Line, WofQuestDialogRules.JobOfferLine, StringComparison.Ordinal))
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_CONTROLLER_PROBE_FAILED stage=select");
                RemoveControllerProbeGamepad();
                yield break;
            }

            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.B);
            if (IsOpen)
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_CONTROLLER_PROBE_FAILED stage=close");
                RemoveControllerProbeGamepad();
                yield break;
            }

            Debug.Log("[WOF-AUTOMATION] DARREL_CONTROLLER_PROBE_COMPLETE open=true navigation=true select=true close=true");
            RemoveControllerProbeGamepad();
        }

        private IEnumerator RunGenericQuestControllerProbe()
        {
            var readyDeadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < readyDeadline &&
                   (WofBootstrap.Instance == null || WofBootstrap.Instance.Mode == WofSessionMode.None ||
                    !HasLivingLocalPlayer()))
            {
                yield return null;
            }

            var desertProbe = IsDesertVillagerControllerProbeRequested();
            var swampProbe = IsSwampVillagerControllerProbeRequested();
            var mountainProbe = IsMountainVillagerControllerProbeRequested();
            if (desertProbe || swampProbe || mountainProbe)
            {
                var localPlayer = ResolveLivingLocalPlayer();
                if (localPlayer == null || !localPlayer.PrepareForAutomationVillagerInteractionProbe(
                        mountainProbe
                            ? WofMountainVillageLayout.FirstVillagerControllerProbeSpawn
                            : swampProbe
                                ? WofSwampVillageLayout.FirstVillagerControllerProbeSpawn
                                : WofDesertVillageLayout.FirstVillagerControllerProbeSpawn,
                        0f,
                        26f))
                {
                    Debug.LogError(mountainProbe
                        ? "[WOF-AUTOMATION] MOUNTAIN_VILLAGER_CONTROLLER_PROBE_FAILED stage=world-position"
                        : swampProbe
                            ? "[WOF-AUTOMATION] SWAMP_VILLAGER_CONTROLLER_PROBE_FAILED stage=world-position"
                            : "[WOF-AUTOMATION] DESERT_VILLAGER_CONTROLLER_PROBE_FAILED stage=world-position");
                    yield break;
                }
                WofVillagerBillboard targetedVillager = null;
                var expectedVillagerId = mountainProbe
                    ? "3:0-mountain-hut-0"
                    : swampProbe
                        ? "0:-3-swamp-hut-0"
                        : "4:-4-desert-building-0";
                var targetDeadline = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < targetDeadline)
                {
                    yield return null;
                    _camera = Camera.main;
                    _villagerManagers = Array.Empty<WofVillagerManager>();
                    if (_camera != null && TryResolveTargetedVillager(out _, out targetedVillager) &&
                        string.Equals(targetedVillager.VillagerId, expectedVillagerId, StringComparison.Ordinal))
                    {
                        break;
                    }
                }
                if (_camera == null || targetedVillager == null ||
                    !string.Equals(targetedVillager.VillagerId, expectedVillagerId, StringComparison.Ordinal))
                {
                    var targetId = targetedVillager == null ? "none" : targetedVillager.VillagerId;
                    var cameraPosition = _camera == null ? Vector3.negativeInfinity : _camera.transform.position;
                    var cameraForward = _camera == null ? Vector3.zero : _camera.transform.forward;
                    Debug.LogError(mountainProbe
                        ? $"[WOF-AUTOMATION] MOUNTAIN_VILLAGER_CONTROLLER_PROBE_FAILED stage=targeting target={targetId} camera={cameraPosition} forward={cameraForward} managers={_villagerManagers.Length}"
                        : swampProbe
                            ? $"[WOF-AUTOMATION] SWAMP_VILLAGER_CONTROLLER_PROBE_FAILED stage=targeting target={targetId} camera={cameraPosition} forward={cameraForward} managers={_villagerManagers.Length}"
                            : $"[WOF-AUTOMATION] DESERT_VILLAGER_CONTROLLER_PROBE_FAILED stage=targeting target={targetId} camera={cameraPosition} forward={cameraForward} managers={_villagerManagers.Length}");
                    yield break;
                }
                Debug.Log(mountainProbe
                    ? $"[WOF-AUTOMATION] MOUNTAIN_VILLAGER_CONTROLLER_TARGET_READY npc={targetedVillager.VillagerId} camera={_camera.transform.position} forward={_camera.transform.forward} target={targetedVillager.InteractionCenter} managers={_villagerManagers.Length}"
                    : swampProbe
                        ? $"[WOF-AUTOMATION] SWAMP_VILLAGER_CONTROLLER_TARGET_READY npc={targetedVillager.VillagerId} camera={_camera.transform.position} forward={_camera.transform.forward} target={targetedVillager.InteractionCenter} managers={_villagerManagers.Length}"
                        : $"[WOF-AUTOMATION] DESERT_VILLAGER_CONTROLLER_TARGET_READY npc={targetedVillager.VillagerId} camera={_camera.transform.position} forward={_camera.transform.forward} target={targetedVillager.InteractionCenter} managers={_villagerManagers.Length}");
            }

            _controllerProbeGamepad = InputSystem.AddDevice<Gamepad>("WOF Generic Quest QA Controller");
            _controllerProbeGamepad.MakeCurrent();
            var interactDeadline = Time.realtimeSinceStartup + 15f;
            while (string.IsNullOrWhiteSpace(_lastGenericQuestInteractionNpcId) &&
                   Time.realtimeSinceStartup < interactDeadline)
            {
                yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.X);
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (string.IsNullOrWhiteSpace(_lastGenericQuestInteractionNpcId))
            {
                Debug.LogError(desertProbe
                    ? "[WOF-AUTOMATION] DESERT_VILLAGER_CONTROLLER_PROBE_FAILED stage=interact"
                    : mountainProbe
                        ? "[WOF-AUTOMATION] MOUNTAIN_VILLAGER_CONTROLLER_PROBE_FAILED stage=interact"
                    : swampProbe
                        ? "[WOF-AUTOMATION] SWAMP_VILLAGER_CONTROLLER_PROBE_FAILED stage=interact"
                        : "[WOF-AUTOMATION] GENERIC_QUEST_CONTROLLER_PROBE_FAILED stage=interact");
                RemoveControllerProbeGamepad();
                yield break;
            }

            Debug.Log($"[WOF-AUTOMATION] GENERIC_QUEST_CONTROLLER_PROBE_COMPLETE interact=true npc={_lastGenericQuestInteractionNpcId} assignment={_lastGenericQuestAssignmentSpell} messages={_lastGenericQuestMessageCount}");
            if (desertProbe)
            {
                var exactNpc = string.Equals(
                    _lastGenericQuestInteractionNpcId,
                    "4:-4-desert-building-0",
                    StringComparison.Ordinal);
                var exactTown = string.Equals(
                    _lastGenericQuestInteractionTownId,
                    "survival-desert-villagers-4:-4",
                    StringComparison.Ordinal);
                if (!exactNpc || !exactTown)
                {
                    Debug.LogError($"[WOF-AUTOMATION] DESERT_VILLAGER_CONTROLLER_PROBE_FAILED stage=identity npc={_lastGenericQuestInteractionNpcId} town={_lastGenericQuestInteractionTownId}");
                    RemoveControllerProbeGamepad();
                    yield break;
                }
                Debug.Log($"[WOF-AUTOMATION] DESERT_VILLAGER_CONTROLLER_PROBE_COMPLETE interactX=true npc={_lastGenericQuestInteractionNpcId} town={_lastGenericQuestInteractionTownId} assignment={_lastGenericQuestAssignmentSpell} messages={_lastGenericQuestMessageCount}");
            }
            else if (swampProbe)
            {
                var exactNpc = string.Equals(_lastGenericQuestInteractionNpcId, "0:-3-swamp-hut-0", StringComparison.Ordinal);
                var exactTown = string.Equals(_lastGenericQuestInteractionTownId, "survival-swamp-villagers-0:-3", StringComparison.Ordinal);
                if (!exactNpc || !exactTown)
                {
                    Debug.LogError($"[WOF-AUTOMATION] SWAMP_VILLAGER_CONTROLLER_PROBE_FAILED stage=identity npc={_lastGenericQuestInteractionNpcId} town={_lastGenericQuestInteractionTownId}");
                    RemoveControllerProbeGamepad();
                    yield break;
                }
                Debug.Log($"[WOF-AUTOMATION] SWAMP_VILLAGER_CONTROLLER_PROBE_COMPLETE interactX=true npc={_lastGenericQuestInteractionNpcId} town={_lastGenericQuestInteractionTownId} assignment={_lastGenericQuestAssignmentSpell} messages={_lastGenericQuestMessageCount}");
            }
            else if (mountainProbe)
            {
                var exactNpc = string.Equals(_lastGenericQuestInteractionNpcId, "3:0-mountain-hut-0", StringComparison.Ordinal);
                var exactTown = string.Equals(_lastGenericQuestInteractionTownId, "survival-mountain-villagers-3:0", StringComparison.Ordinal);
                if (!exactNpc || !exactTown)
                {
                    Debug.LogError($"[WOF-AUTOMATION] MOUNTAIN_VILLAGER_CONTROLLER_PROBE_FAILED stage=identity npc={_lastGenericQuestInteractionNpcId} town={_lastGenericQuestInteractionTownId}");
                    RemoveControllerProbeGamepad();
                    yield break;
                }
                Debug.Log($"[WOF-AUTOMATION] MOUNTAIN_VILLAGER_CONTROLLER_PROBE_COMPLETE interactX=true npc={_lastGenericQuestInteractionNpcId} town={_lastGenericQuestInteractionTownId} assignment={_lastGenericQuestAssignmentSpell} messages={_lastGenericQuestMessageCount}");
            }
            RemoveControllerProbeGamepad();
        }

        private IEnumerator RunDarrelDragonControllerProbe()
        {
            var readyDeadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < readyDeadline &&
                   (WofBootstrap.Instance == null || WofBootstrap.Instance.Mode == WofSessionMode.None ||
                    !HasLivingLocalPlayer()))
            {
                yield return null;
            }

            _controllerProbeGamepad = InputSystem.AddDevice<Gamepad>("WOF Darrel Dragon QA Controller");
            _controllerProbeGamepad.MakeCurrent();
            yield return null;
            var localPlayer = ResolveLivingLocalPlayer();
            var interactionPosition = WofDarrelGroveLayout.WorldOrigin + new Vector3(10f, 33.35f, -15f);
            if (localPlayer == null || !localPlayer.PrepareForAutomationCombatProbe(
                    interactionPosition,
                    WofDarrelGroveLayout.UnitySpawnYawDegrees))
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_FAILED stage=world-position");
                RemoveControllerProbeGamepad();
                yield break;
            }
            yield return null;
            yield return null;
            if (WofDarrelGroveRuntime.Instance == null || !WofDarrelGroveRuntime.Instance.IsPromptVisible)
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_FAILED stage=world-prompt");
                RemoveControllerProbeGamepad();
                yield break;
            }

            yield return TapControllerTrigger(_controllerProbeGamepad, true);
            if (!IsOpen)
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_FAILED stage=left-cast-world-interact");
                RemoveControllerProbeGamepad();
                yield break;
            }
            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.B);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return TapControllerTrigger(_controllerProbeGamepad, false);
            if (!IsOpen)
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_FAILED stage=right-cast-world-interact");
                RemoveControllerProbeGamepad();
                yield break;
            }
            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.B);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.X);
            if (!IsOpen)
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_FAILED stage=x-world-interact");
                RemoveControllerProbeGamepad();
                yield break;
            }
            if (Session == null || Session.Choices.Length != 2)
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_FAILED stage=choices");
                RemoveControllerProbeGamepad();
                yield break;
            }

            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.A);
            var deathDeadline = Time.realtimeSinceStartup + 2f;
            while (localPlayer != null && !localPlayer.IsDead && Time.realtimeSinceStartup < deathDeadline)
            {
                yield return null;
            }
            _profile = WofSurvivalProfileStore.Load();
            if (localPlayer == null || !localPlayer.IsDead || _profile == null ||
                !string.Equals(
                    WofSpellQuestRules.GetFlag(_profile, WofDarrelProgressionRules.DragonFoughtFlag),
                    "true",
                    StringComparison.Ordinal))
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_FAILED stage=fight");
                RemoveControllerProbeGamepad();
                yield break;
            }
            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.B);
            var respawnDeadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < respawnDeadline && !HasLivingLocalPlayer())
            {
                yield return null;
            }
            if (!HasLivingLocalPlayer() || !OpenSpiritDragonDialog())
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_FAILED stage=reopen");
                RemoveControllerProbeGamepad();
                yield break;
            }

            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.DpadDown);
            if (SelectedChoiceIndex != 1)
            {
                Debug.LogError($"[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_FAILED stage=navigate selected={SelectedChoiceIndex}");
                RemoveControllerProbeGamepad();
                yield break;
            }
            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.A);
            _profile = WofSurvivalProfileStore.Load();
            var peaceCompleted = _profile != null &&
                                 WofInventoryRules.GetQuantity(_profile, "healing-crystals") == 2 &&
                                 Array.IndexOf(_profile.questUnlockedSpells, WofSpellQuestRules.DarrelRewardSpell) >= 0;
            if (!peaceCompleted || Session == null ||
                !string.Equals(Session.Line, WofDarrelProgressionRules.DragonPeaceLine, StringComparison.Ordinal))
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_FAILED stage=peace");
                RemoveControllerProbeGamepad();
                yield break;
            }
            yield return TapControllerButton(_controllerProbeGamepad, GamepadButton.B);
            if (IsOpen)
            {
                Debug.LogError("[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_FAILED stage=close");
                RemoveControllerProbeGamepad();
                yield break;
            }

            Debug.Log("[WOF-AUTOMATION] DARREL_DRAGON_CONTROLLER_PROBE_COMPLETE worldPrompt=true interactX=true leftCast=true rightCast=true open=true fight=true death=true respawn=true navigation=true peace=true reward=true close=true crystals=2");
            RemoveControllerProbeGamepad();
        }

        private static IEnumerator TapControllerButton(Gamepad gamepad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
        }

        private static IEnumerator TapControllerTrigger(Gamepad gamepad, bool left)
        {
            InputSystem.QueueStateEvent(gamepad, left
                ? new GamepadState { leftTrigger = 1f }
                : new GamepadState { rightTrigger = 1f });
            yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
        }

        private void RemoveControllerProbeGamepad()
        {
            if (_controllerProbeGamepad == null || !_controllerProbeGamepad.added)
            {
                _controllerProbeGamepad = null;
                return;
            }

            InputSystem.RemoveDevice(_controllerProbeGamepad);
            _controllerProbeGamepad = null;
        }

        private static bool IsDarrelControllerProbeRequested()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], "--wof-darrel-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
#endif
        }

        private static bool IsDarrelDragonControllerProbeRequested()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], "--wof-darrel-dragon-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
#endif
        }

        private static bool IsGenericQuestControllerProbeRequested()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], "--wof-generic-quest-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (string.Equals(args[index], "--wof-desert-villager-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (string.Equals(args[index], "--wof-swamp-villager-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (string.Equals(args[index], "--wof-mountain-villager-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
#endif
        }

        private static bool IsDesertVillagerControllerProbeRequested()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], "--wof-desert-villager-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
#endif
        }

        private static bool IsSwampVillagerControllerProbeRequested()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], "--wof-swamp-villager-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
#endif
        }

        private static bool IsMountainVillagerControllerProbeRequested()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], "--wof-mountain-villager-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
#endif
        }

        private void Update()
        {
            UpdateNotifications();
            if (_session == null)
            {
                PollInteractionInput();
                return;
            }

            if (_lastLayoutScreenWidth != Screen.width || _lastLayoutScreenHeight != Screen.height)
            {
                ApplyDialogLayout();
            }

            PollDialogInput();
        }

        public void SetSelectedChoice(int index)
        {
            if (_session == null || _session.Choices.Length == 0)
            {
                return;
            }

            _selectedChoiceIndex = ((index % _session.Choices.Length) + _session.Choices.Length) %
                                   _session.Choices.Length;
            RefreshChoiceSelection();
        }

        public bool OpenSpiritDragonDialog()
        {
            if (_session != null)
            {
                return false;
            }
            _profile = WofSurvivalProfileStore.Load();
            if (_profile == null)
            {
                return false;
            }
            var result = WofDarrelProgressionRules.OpenDragonDialog(_profile);
            if (result.ProfileChanged)
            {
                WofSurvivalProfileStore.Save(_profile);
            }
            if (result.Session == null)
            {
                return false;
            }
            OpenSession(result.Session);
            Debug.Log($"[WOF-AUTOMATION] DARREL_DRAGON_DIALOG_OPEN choices={result.Session.Choices.Length}");
            return true;
        }

        public void Choose(int index)
        {
            if (_session == null || index < 0 || index >= _session.Choices.Length)
            {
                return;
            }

            var choice = _session.Choices[index];
            if (string.Equals(_session.NpcId, WofDarrelProgressionRules.DragonNpcId, StringComparison.Ordinal))
            {
                ChooseDragon(choice);
                return;
            }
            if (!WofQuestDialogRules.TryChoose(_session, choice.Id, out var transition))
            {
                return;
            }

            Debug.Log($"[WOF-AUTOMATION] QUEST_DIALOG_CHOICE npc={_session.NpcId} id={choice.Id}");
            if (transition.AcceptedQuest)
            {
                PersistDarrelAcceptance();
            }
            if (transition.Closed)
            {
                Close();
                return;
            }

            OpenSession(transition.Session);
        }

        private void ChooseDragon(WofQuestDialogChoice choice)
        {
            _profile ??= WofSurvivalProfileStore.Load();
            if (_profile == null)
            {
                return;
            }
            var result = WofDarrelProgressionRules.ChooseDragon(
                _profile,
                _session,
                choice.Id,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Debug.Log($"[WOF-AUTOMATION] QUEST_DIALOG_CHOICE npc={_session.NpcId} id={choice.Id}");
            if (result.ProfileChanged)
            {
                WofSurvivalProfileStore.Save(_profile);
            }
            ShowSystemMessages(result.Messages);
            if (result.ShouldKillPlayer)
            {
                ResolveLivingLocalPlayer()?.RequestQuestFatalDamage();
            }
            if (result.Closed)
            {
                Close();
                return;
            }
            if (result.Session != null)
            {
                OpenSession(result.Session);
            }
        }

        public void Close()
        {
            if (_session == null)
            {
                return;
            }

            Debug.Log($"[WOF-AUTOMATION] QUEST_DIALOG_CLOSED npc={_session.NpcId}");
            _session = null;
            dialogRoot?.SetActive(false);
            RestoreGameplay();
        }

        private void PollInteractionInput()
        {
            var bootstrap = WofBootstrap.Instance;
            if (bootstrap == null || bootstrap.Mode == WofSessionMode.None || !bootstrap.IsSurvivalSession ||
                WofInputRouter.GameplaySuppressed)
            {
                return;
            }

            var keyboardPressed = Keyboard.current?.fKey.wasPressedThisFrame ?? false;
            var controllerHeld = Gamepad.current?.buttonWest.isPressed ?? false;
            var controllerPressed = controllerHeld && !_controllerInteractHeld;
            _controllerInteractHeld = controllerHeld;
            if (!keyboardPressed && !controllerPressed)
            {
                return;
            }

            var localPlayer = ResolveLivingLocalPlayer();
            if (WofDarrelGroveRuntime.TryInteractWithDragon(localPlayer))
            {
                return;
            }
            TryInteractWithTargetedVillager();
        }

        private void TryInteractWithTargetedVillager()
        {
            if (Time.unscaledTime - _lastInteractionAt < InteractionCooldownSeconds || !HasLivingLocalPlayer())
            {
                return;
            }
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }
            if (_camera == null || !TryResolveTargetedVillager(out var villagerManager, out var villager))
            {
                return;
            }

            _lastInteractionAt = Time.unscaledTime;
            villager.TriggerInteraction(Time.unscaledTime);
            _profile = WofSurvivalProfileStore.Load();
            if (!villager.IsDarrel)
            {
                var displayName = villagerManager.GetReactDisplayName(villager);
                var townId = villagerManager.GetReactTownId(villager);
                var result = WofSpellQuestRules.InteractWithGenericVillager(
                    _profile,
                    villager.VillagerId,
                    townId,
                    displayName,
                    UnityEngine.Random.value,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                if (result.ProfileChanged)
                {
                    WofSurvivalProfileStore.Save(_profile);
                }
                ShowSystemMessages(result.Messages);
                _lastGenericQuestInteractionNpcId = villager.VillagerId;
                _lastGenericQuestInteractionTownId = townId;
                _lastGenericQuestAssignmentSpell = result.Assignment?.spell ?? "none";
                _lastGenericQuestMessageCount = result.Messages.Length;
                Debug.Log($"[WOF-AUTOMATION] GENERIC_QUEST_INTERACTION npc={villager.VillagerId} name=\"{displayName}\" assignment={result.Assignment?.spell ?? "none"} messages={result.Messages.Length} changed={result.ProfileChanged}");
                return;
            }
            var progress = WofQuestDialogRules.ResolveProgress(_profile?.darrelHealingCrystalsQuestStatus);
            OpenSession(WofQuestDialogRules.CreateInitial(progress));
        }

        private bool TryResolveTargetedVillager(
            out WofVillagerManager targetedManager,
            out WofVillagerBillboard targetedVillager)
        {
            targetedManager = null;
            targetedVillager = null;
            if (_camera == null)
            {
                return false;
            }
            if (_villagerManagers == null || _villagerManagers.Length == 0)
            {
                _villagerManagers = FindObjectsByType<WofVillagerManager>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            }

            var bestScore = float.PositiveInfinity;
            for (var index = 0; index < _villagerManagers.Length; index++)
            {
                var manager = _villagerManagers[index];
                if (manager == null || !manager.TryGetTargetedVillager(_camera, out var candidate) ||
                    !WofQuestTargetMath.TryScoreTarget(
                        _camera.transform.position,
                        _camera.transform.forward,
                        candidate.InteractionCenter,
                        out var score) ||
                    score >= bestScore)
                {
                    continue;
                }
                bestScore = score;
                targetedManager = manager;
                targetedVillager = candidate;
            }
            return targetedVillager != null;
        }

        public void ShowSystemMessages(string[] messages)
        {
            if (messages == null || messages.Length == 0 || _notificationRoot == null)
            {
                return;
            }
            var expiresAt = Time.unscaledTime + NotificationLifetimeSeconds;
            for (var index = 0; index < messages.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(messages[index]))
                {
                    continue;
                }
                _notifications.Add(new QuestNotification(messages[index], expiresAt));
            }
            while (_notifications.Count > NotificationLimit)
            {
                _notifications.RemoveAt(0);
            }
            RefreshNotificationFeed();
        }

        private void UpdateNotifications()
        {
            var changed = false;
            while (_notifications.Count > 0 && Time.unscaledTime > _notifications[0].ExpiresAt)
            {
                _notifications.RemoveAt(0);
                changed = true;
            }
            if (changed)
            {
                RefreshNotificationFeed();
            }
        }

        private void RefreshNotificationFeed()
        {
            if (_notificationRoot == null || _notificationTexts == null)
            {
                return;
            }
            var top = 0f;
            for (var index = 0; index < _notificationTexts.Length; index++)
            {
                var label = _notificationTexts[index];
                var active = index < _notifications.Count;
                label.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }
                label.text = _notifications[index].Text;
                SetTopLeft(label.rectTransform, 0f, top, 495f, 24f);
                var height = Mathf.Max(24f, label.preferredHeight);
                SetTopLeft(label.rectTransform, 0f, top, 495f, height);
                top += height + 6f;
            }
            _notificationRoot.SetActive(_notifications.Count > 0);
        }

        private void OpenSession(WofQuestDialogSession session)
        {
            if (session == null)
            {
                return;
            }

            _session = session;
            _selectedChoiceIndex = 0;
            _heldNavigationDirection = 0;
            _nextNavigationRepeatAt = 0f;
            WofInputRouter.SetGameplaySuppressed(true);
            hud?.SetGameplaySurfaceBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            dialogRoot?.SetActive(true);
            RefreshDialog();
            Debug.Log($"[WOF-AUTOMATION] QUEST_DIALOG_OPEN npc={session.NpcId} choices={session.Choices.Length}");
        }

        private void PollDialogInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    Close();
                    return;
                }
                if (WasNumberPressed(keyboard, 0))
                {
                    Choose(0);
                    return;
                }
                if (WasNumberPressed(keyboard, 1))
                {
                    Choose(1);
                    return;
                }
            }

            var gamepad = Gamepad.current;
            if (gamepad == null)
            {
                _heldNavigationDirection = 0;
                return;
            }
            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                Choose(_selectedChoiceIndex);
                return;
            }
            if (gamepad.buttonEast.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame)
            {
                Close();
                return;
            }

            var stickY = gamepad.leftStick.ReadUnprocessedValue().y;
            var direction = gamepad.dpad.down.isPressed || stickY < -ControllerNavigationThreshold
                ? 1
                : gamepad.dpad.up.isPressed || stickY > ControllerNavigationThreshold
                    ? -1
                    : 0;
            if (direction == 0)
            {
                _heldNavigationDirection = 0;
                return;
            }

            var now = Time.unscaledTime;
            if (direction != _heldNavigationDirection)
            {
                _heldNavigationDirection = direction;
                _nextNavigationRepeatAt = now + ControllerRepeatDelaySeconds;
                SetSelectedChoice(_selectedChoiceIndex + direction);
            }
            else if (now >= _nextNavigationRepeatAt)
            {
                _nextNavigationRepeatAt = now + ControllerRepeatSeconds;
                SetSelectedChoice(_selectedChoiceIndex + direction);
            }
        }

        private void RefreshDialog()
        {
            if (_session == null)
            {
                return;
            }
            if (speakerText != null)
            {
                speakerText.text = _session.DisplayName;
            }
            if (lineText != null)
            {
                lineText.text = _session.Line;
            }
            for (var index = 0; index < choiceButtons.Length; index++)
            {
                var active = index < _session.Choices.Length;
                if (_choiceBorderRects != null && index < _choiceBorderRects.Length &&
                    _choiceBorderRects[index] != null)
                {
                    _choiceBorderRects[index].gameObject.SetActive(active);
                }
                choiceButtons[index].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }
                choiceNumberTexts[index].text = (index + 1).ToString();
                choiceLabelTexts[index].text = _session.Choices[index].Label;
            }
            ApplyDialogLayout();
            RefreshChoiceSelection();
        }

        private void ApplyDialogLayout()
        {
            if (_session == null || _dialogBorderRect == null || _dialogCardRect == null)
            {
                return;
            }

            var canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
            var canvasScale = canvas == null ? 1f : Mathf.Max(0.0001f, canvas.scaleFactor);
            var cardPixelWidth = Mathf.Max(320f, Mathf.Min(760f, Screen.width - 24f));
            _dialogBorderRect.anchorMin = new Vector2(0.5f, 0.5f);
            _dialogBorderRect.anchorMax = new Vector2(0.5f, 0.5f);
            _dialogBorderRect.pivot = new Vector2(0.5f, 0.5f);
            _dialogBorderRect.localScale = Vector3.one / canvasScale;
            _dialogBorderRect.sizeDelta = new Vector2(cardPixelWidth, 297f);
            _dialogBorderRect.anchoredPosition = Vector2.zero;
            SetFullInset(_dialogCardRect, 2f, 2f, 2f, 2f);

            SetTopStretch(_headerRect, 0f, 68f, 0f, 0f);
            SetBottomStretch(_headerDividerRect, 0f, 1f, 0f, 0f);
            SetTopLeft(_questKickerRect, 16f, 11f, 300f, 15f);
            SetTopLeft(speakerText.rectTransform, 16f, 27f, 420f, 29f);
            SetTopRight(_closeBorderRect, 16f, 13f, 75f, 44f);
            SetFullInset(closeButton.GetComponent<RectTransform>(), 1f, 1f, 1f, 1f);

            const float contentTop = 84f;
            SetTopStretch(_lineBorderRect, contentTop, 50f, 16f, 16f);
            SetFullInset(_linePanelRect, 1f, 1f, 1f, 1f);
            SetFullInset(lineText.rectTransform, 12f, 12f, 12f, 12f);
            Canvas.ForceUpdateCanvases();
            var lineHeight = Mathf.Max(50f, Mathf.Ceil(lineText.preferredHeight) + 26f);
            SetTopStretch(_lineBorderRect, contentTop, lineHeight, 16f, 16f);

            var nextTop = contentTop + lineHeight + 12f;
            var activeChoiceCount = Mathf.Min(_session.Choices.Length, choiceButtons.Length);
            for (var index = 0; index < choiceButtons.Length; index++)
            {
                if (index >= activeChoiceCount)
                {
                    continue;
                }

                SetTopStretch(_choiceBorderRects[index], nextTop, 50f, 16f, 16f);
                SetFullInset(choiceButtons[index].GetComponent<RectTransform>(), 1f, 1f, 1f, 1f);
                SetMiddleLeft(_choiceNumberBorderRects[index], 12f, 28f, 28f);
                SetFullInset(choiceNumberTexts[index].rectTransform, 0f, 0f, 0f, 0f);
                SetFullInset(choiceLabelTexts[index].rectTransform, 52f, 12f, 6f, 6f);
                nextTop += 58f;
            }

            SetTopStretch(_controllerHelpRect, nextTop, 15f, 16f, 16f);
            var cardPixelHeight = nextTop + 31f + 4f;
            _dialogBorderRect.sizeDelta = new Vector2(cardPixelWidth, cardPixelHeight);
            _lastLayoutScreenWidth = Screen.width;
            _lastLayoutScreenHeight = Screen.height;
        }

        private void RefreshChoiceSelection()
        {
            if (choiceButtons == null)
            {
                return;
            }
            for (var index = 0; index < choiceButtons.Length; index++)
            {
                var image = choiceButtons[index]?.targetGraphic as Image;
                if (image == null)
                {
                    continue;
                }
                image.color = index == _selectedChoiceIndex
                    ? new Color32(250, 240, 190, 46)
                    : new Color32(0, 0, 0, 89);
            }
        }

        private void RestoreGameplay()
        {
            WofInputRouter.SetGameplaySuppressed(false);
            hud?.SetGameplaySurfaceBlocked(false);
            if (WofBootstrap.Instance != null && WofBootstrap.Instance.Mode != WofSessionMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void PersistDarrelAcceptance()
        {
            _profile ??= WofSurvivalProfileStore.Load();
            if (_profile != null)
            {
                _profile.darrelHealingCrystalsQuestStatus = WofQuestDialogRules.QuestStatusAccepted;
                if (_profile.darrelHealingCrystalsAssignedAt <= 0)
                {
                    _profile.darrelHealingCrystalsAssignedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
                WofSpellQuestRules.AcceptDarrelQuest(_profile, _profile.darrelHealingCrystalsAssignedAt);
                WofSurvivalProfileStore.Save(_profile);
            }
            Debug.Log("[WOF-AUTOMATION] DARREL_QUEST_ACCEPTED spell=healingcrystals");
        }

        private static bool WasNumberPressed(Keyboard keyboard, int index)
        {
            return index switch
            {
                0 => keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame,
                1 => keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame,
                _ => false
            };
        }

        private static bool HasLivingLocalPlayer()
        {
            return ResolveLivingLocalPlayer() != null;
        }

        private static WofPlayerController ResolveLivingLocalPlayer()
        {
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

        private readonly struct QuestNotification
        {
            public QuestNotification(string text, float expiresAt)
            {
                Text = text;
                ExpiresAt = expiresAt;
            }

            public string Text { get; }
            public float ExpiresAt { get; }
        }
    }

    public sealed class WofQuestChoiceHover : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private WofQuestDialogRuntime runtime;
        [SerializeField] private int choiceIndex;

        public void Configure(WofQuestDialogRuntime owner, int index)
        {
            runtime = owner;
            choiceIndex = index;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            runtime?.SetSelectedChoice(choiceIndex);
        }
    }
}
