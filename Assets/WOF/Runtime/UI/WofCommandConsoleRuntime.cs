using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofCommandConsoleRuntime : MonoBehaviour
    {
        private static readonly Color32 Cyan50 = new(236, 254, 255, 255);
        private static readonly Color32 Cyan100 = new(207, 250, 254, 255);
        private static readonly Color32 Cyan100Muted = new(207, 250, 254, 140);
        private static readonly Color32 Cyan200Border = new(165, 243, 252, 179);
        private static readonly Color32 Cyan300Border = new(103, 232, 249, 140);
        private static readonly Color32 Cyan300DimBorder = new(103, 232, 249, 64);
        private static readonly Color32 Cyan300DimFill = new(103, 232, 249, 26);
        private static readonly Color32 Yellow200Border = new(254, 240, 138, 179);
        private static readonly Color32 Yellow200Fill = new(254, 240, 138, 26);
        private static readonly Color32 Slate400 = new(148, 163, 184, 255);

        [SerializeField] private WofHud hud;
        [SerializeField] private Font commandFont;

        private GameObject _root;
        private RectTransform _rootRect;
        private Text _vclipText;
        private InputField _input;
        private RectTransform _suggestionRoot;
        private readonly List<WofCommandSuggestionButton> _suggestionButtons = new();
        private WofInventoryRuntime _inventory;
        private WofQuestDialogRuntime _questDialog;
        private Canvas _canvas;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private bool _submitting;

        public bool IsOpen => _root != null && _root.activeSelf;
        public string CurrentValue => _input == null ? string.Empty : _input.text;
        public int VisibleSuggestionCount => _suggestionButtons.Count;

        public void ConfigureGeneratedView(WofHud generatedHud, Font generatedFont)
        {
            hud = generatedHud;
            commandFont = generatedFont;
        }

        private void Awake()
        {
            hud ??= FindFirstObjectByType<WofHud>();
            commandFont ??= GetComponentInChildren<Text>(true)?.font;
            _inventory = FindFirstObjectByType<WofInventoryRuntime>();
            _questDialog = FindFirstObjectByType<WofQuestDialogRuntime>();
            _canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
            BuildRuntimeView();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                PollOpenShortcut();
                return;
            }
            if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height)
            {
                ApplyPixelLayout();
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
            else if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && !_submitting)
            {
                Submit();
            }
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                RestoreGameplay(true);
            }
        }

        public void Open()
        {
            if (IsOpen || !CanOpenFromGameplay())
            {
                return;
            }
            WofInputRouter.SetGameplaySuppressed(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _root.SetActive(true);
            _input.SetTextWithoutNotify("/");
            SetCaretToEnd();
            RefreshSuggestions();
            ApplyPixelLayout();
            StartCoroutine(FocusInputAfterOpening());
            Debug.Log("[WOF-AUTOMATION] COMMAND_CONSOLE_OPEN value=/");
        }

        public void Close()
        {
            Close(true);
        }

        public void Submit()
        {
            if (!IsOpen || _submitting)
            {
                return;
            }
            _submitting = true;
            var rawValue = _input.text;
            var submission = WofCommandConsoleRules.Evaluate(rawValue);
            Debug.Log($"[WOF-AUTOMATION] COMMAND_CONSOLE_SUBMIT value=\"{rawValue.Replace("\"", "'")}\" action={submission.Action}");
            switch (submission.Action)
            {
                case WofCommandConsoleAction.OpenInventory:
                    Close(false);
                    StartCoroutine(OpenInventoryAfterSubmit());
                    break;
                case WofCommandConsoleAction.ForageLeaves:
                    ApplyCollectIngredient("leaves");
                    Close();
                    break;
                case WofCommandConsoleAction.ForageBerries:
                    ApplyCollectIngredient("berries");
                    Close();
                    break;
                case WofCommandConsoleAction.ForageRoots:
                    ApplyCollectIngredient("roots");
                    Close();
                    break;
                case WofCommandConsoleAction.BrewGardenDraught:
                    ApplyBrewGardenDraught();
                    Close();
                    break;
                case WofCommandConsoleAction.DrinkGardenDraught:
                    ApplyDrinkGardenDraught();
                    Close();
                    break;
                default:
                    ShowMessages(new[] { submission.Message });
                    Close();
                    break;
            }
            _submitting = false;
        }

        private void PollOpenShortcut()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.slashKey.wasPressedThisFrame && CanOpenFromGameplay())
            {
                Open();
            }
        }

        private void Close(bool relockGameplay)
        {
            if (!IsOpen)
            {
                return;
            }
            _input.DeactivateInputField();
            EventSystem.current?.SetSelectedGameObject(null);
            _input.SetTextWithoutNotify("/");
            _root.SetActive(false);
            RestoreGameplay(relockGameplay);
            Debug.Log($"[WOF-AUTOMATION] COMMAND_CONSOLE_CLOSED relock={relockGameplay.ToString().ToLowerInvariant()}");
        }

        private void RestoreGameplay(bool relock)
        {
            WofInputRouter.SetGameplaySuppressed(false);
            if (relock && WofBootstrap.Instance != null && WofBootstrap.Instance.Mode != WofSessionMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private WofSurvivalProfile LoadProfile()
        {
            return WofSurvivalProfileStore.Load();
        }

        private void ApplyCollectIngredient(string ingredient)
        {
            var profile = LoadProfile();
            var result = WofDarrelProgressionRules.CollectIngredient(
                profile,
                ingredient,
                IsSurvivalMode(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            ApplyProgression(profile, result);
        }

        private void ApplyBrewGardenDraught()
        {
            var profile = LoadProfile();
            var result = WofDarrelProgressionRules.BrewGardenDraught(
                profile,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            ApplyProgression(profile, result);
        }

        private void ApplyDrinkGardenDraught()
        {
            var localPlayer = ResolveLivingLocalPlayer();
            if (localPlayer == null)
            {
                ShowMessages(new[] { "The garden draught can only be used by a living local wizard." });
                Debug.Log("[WOF-AUTOMATION] DARREL_DRINK_BLOCKED reason=local-player-unavailable");
                return;
            }

            var profile = LoadProfile();
            var result = WofDarrelProgressionRules.DrinkGardenDraught(profile);
            if (result.ShouldTeleport && !localPlayer.RequestDarrelGroveTeleport())
            {
                ShowMessages(new[] { "The sacred garden could not be reached; the garden draught was not consumed." });
                Debug.Log("[WOF-AUTOMATION] DARREL_DRINK_BLOCKED reason=teleport-request-rejected");
                return;
            }
            ApplyProgression(profile, result);
            Debug.Log($"[WOF-AUTOMATION] DARREL_DRINK changed={result.ProfileChanged.ToString().ToLowerInvariant()} teleport={result.ShouldTeleport.ToString().ToLowerInvariant()}");
        }

        private void ApplyProgression(WofSurvivalProfile profile, WofDarrelProgressionResult result)
        {
            if (result.ProfileChanged && profile != null)
            {
                WofSurvivalProfileStore.Save(profile);
            }
            ShowMessages(result.Messages);
        }

        private void ShowMessages(string[] messages)
        {
            _questDialog ??= FindFirstObjectByType<WofQuestDialogRuntime>();
            _questDialog?.ShowSystemMessages(messages);
        }

        private bool CanOpenFromGameplay()
        {
            return WofBootstrap.Instance != null &&
                   WofBootstrap.Instance.Mode != WofSessionMode.None &&
                   !WofInputRouter.GameplaySuppressed;
        }

        private static bool IsSurvivalMode()
        {
            return WofBootstrap.Instance != null && WofBootstrap.Instance.IsSurvivalSession;
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

        private IEnumerator FocusInputAfterOpening()
        {
            yield return null;
            if (!IsOpen)
            {
                yield break;
            }
            _input.SetTextWithoutNotify("/");
            _input.ActivateInputField();
            _input.Select();
            yield return null;
            if (!IsOpen)
            {
                yield break;
            }
            SetCaretToEnd();
        }

        private IEnumerator OpenInventoryAfterSubmit()
        {
            var keyboard = Keyboard.current;
            while (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed))
            {
                yield return null;
                keyboard = Keyboard.current;
            }
            yield return null;
            _inventory ??= FindFirstObjectByType<WofInventoryRuntime>();
            _inventory?.Open();
        }

        private void OnInputValueChanged(string value)
        {
            if (IsOpen)
            {
                RefreshSuggestions();
            }
        }

        private void SetSuggestion(WofCommandSuggestion suggestion)
        {
            _input.text = suggestion.Sample;
            _input.ActivateInputField();
            _input.Select();
            SetCaretToEnd();
            Debug.Log($"[WOF-AUTOMATION] COMMAND_CONSOLE_SUGGESTION command={suggestion.Command}");
        }

        private void RefreshSuggestions()
        {
            for (var index = 0; index < _suggestionButtons.Count; index++)
            {
                if (_suggestionButtons[index] != null)
                {
                    Destroy(_suggestionButtons[index].gameObject);
                }
            }
            _suggestionButtons.Clear();
            var suggestions = WofCommandConsoleRules.GetSuggestions(CurrentValue);
            for (var index = 0; index < suggestions.Length; index++)
            {
                var captured = suggestions[index];
                _suggestionButtons.Add(CreateSuggestionButton(captured));
            }
            ApplyPixelLayout();
        }

        private void BuildRuntimeView()
        {
            if (commandFont == null)
            {
                Debug.LogError("[WOF-AUTOMATION] COMMAND_CONSOLE_VIEW_FAILED reason=missing-font");
                return;
            }

            var border = CreatePanel("CommandConsoleBorder", transform, Cyan200Border);
            _root = border;
            _rootRect = border.GetComponent<RectTransform>();
            var glow = border.AddComponent<Outline>();
            glow.effectColor = new Color32(34, 211, 238, 82);
            glow.effectDistance = new Vector2(5f, -5f);

            var card = CreatePanel("Card", border.transform, new Color32(5, 7, 17, 242));
            SetFullInset(card.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);

            var headerLeft = CreateText("Title", card.transform, "CHAT CONSOLE", 9, TextAnchor.MiddleLeft, new Color32(207, 250, 254, 179));
            SetTopStretch(headerLeft.rectTransform, 8f, 13f, 8f, 220f);
            _vclipText = CreateText("Vclip", card.transform, "VCLIP OFF", 9, TextAnchor.MiddleRight, Slate400);
            SetTopStretch(_vclipText.rectTransform, 8f, 13f, 220f, 8f);

            var inputBorder = CreatePanel("InputBorder", card.transform, Cyan300Border);
            SetTopStretch(inputBorder.GetComponent<RectTransform>(), 27f, 42f, 8f, 8f);
            var inputBackground = CreatePanel("InputBackground", inputBorder.transform, new Color32(0, 0, 0, 217));
            SetFullInset(inputBackground.GetComponent<RectTransform>(), 1f, 1f, 1f, 1f);
            var placeholder = CreateText("Placeholder", inputBackground.transform, "/navrecord start", 14, TextAnchor.MiddleLeft, new Color32(207, 250, 254, 89));
            SetFullInset(placeholder.rectTransform, 12f, 12f, 8f, 8f);
            var inputText = CreateText("Text", inputBackground.transform, "/", 14, TextAnchor.MiddleLeft, Cyan50);
            SetFullInset(inputText.rectTransform, 12f, 12f, 8f, 8f);
            inputText.supportRichText = false;
            inputText.resizeTextForBestFit = false;
            inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
            inputText.verticalOverflow = VerticalWrapMode.Truncate;
            _input = inputBorder.AddComponent<InputField>();
            _input.targetGraphic = inputBackground.GetComponent<Image>();
            _input.textComponent = inputText;
            _input.placeholder = placeholder;
            _input.characterLimit = WofCommandConsoleRules.MaximumInputLength;
            _input.lineType = InputField.LineType.SingleLine;
            _input.contentType = InputField.ContentType.Standard;
            _input.caretColor = Cyan50;
            _input.selectionColor = new Color32(254, 240, 138, 80);
            _input.navigation = new Navigation { mode = Navigation.Mode.None };
            _input.onValueChanged.AddListener(OnInputValueChanged);

            _suggestionRoot = new GameObject("Suggestions", typeof(RectTransform)).GetComponent<RectTransform>();
            _suggestionRoot.SetParent(card.transform, false);
            SetTopStretch(_suggestionRoot, 77f, 78f, 8f, 8f);

            _root.SetActive(false);
        }

        private WofCommandSuggestionButton CreateSuggestionButton(WofCommandSuggestion suggestion)
        {
            var border = CreatePanel($"Suggestion-{suggestion.Command}", _suggestionRoot, Cyan300DimBorder);
            var button = border.AddComponent<Button>();
            button.targetGraphic = border.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var fill = CreatePanel("Fill", border.transform, Cyan300DimFill);
            SetFullInset(fill.GetComponent<RectTransform>(), 1f, 1f, 1f, 1f);
            fill.GetComponent<Image>().raycastTarget = false;
            var label = CreateText("Label", fill.transform, suggestion.Label.ToUpperInvariant(), 10, TextAnchor.UpperLeft, Cyan100);
            SetTopStretch(label.rectTransform, 4f, 13f, 7f, 7f);
            var sample = CreateText("Sample", fill.transform, suggestion.Sample, 10, TextAnchor.LowerLeft, Cyan100Muted);
            SetBottomStretch(sample.rectTransform, 4f, 13f, 7f, 7f);
            sample.horizontalOverflow = HorizontalWrapMode.Overflow;
            button.onClick.AddListener(() => SetSuggestion(suggestion));
            var view = border.AddComponent<WofCommandSuggestionButton>();
            view.Configure(border.GetComponent<Image>(), fill.GetComponent<Image>(), Cyan300DimBorder, Cyan300DimFill, Yellow200Border, Yellow200Fill);
            return view;
        }

        private void ApplyPixelLayout()
        {
            if (_rootRect == null || _suggestionRoot == null)
            {
                return;
            }
            _canvas ??= GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
            var scale = _canvas == null ? 1f : Mathf.Max(0.01f, _canvas.scaleFactor);
            var width = Mathf.Min(720f, Mathf.Max(320f, Screen.width - 20f));
            var columns = Screen.width >= 640 ? 3 : 2;
            var rows = Mathf.CeilToInt(_suggestionButtons.Count / (float)columns);
            var suggestionsHeight = rows <= 0 ? 0f : rows * 36f + (rows - 1) * 6f;
            var height = 77f + suggestionsHeight + 10f;
            var safeTop = Mathf.Max(12f, Screen.height - Screen.safeArea.yMax);

            _rootRect.anchorMin = new Vector2(0.5f, 1f);
            _rootRect.anchorMax = new Vector2(0.5f, 1f);
            _rootRect.pivot = new Vector2(0.5f, 1f);
            _rootRect.localScale = Vector3.one / scale;
            _rootRect.sizeDelta = new Vector2(width, height);
            _rootRect.anchoredPosition = new Vector2(0f, -safeTop / scale);
            SetTopStretch(_suggestionRoot, 77f, suggestionsHeight, 8f, 8f);

            var contentWidth = width - 20f;
            var cellWidth = (contentWidth - (columns - 1) * 6f) / columns;
            for (var index = 0; index < _suggestionButtons.Count; index++)
            {
                var row = index / columns;
                var column = index % columns;
                SetTopLeft(
                    _suggestionButtons[index].GetComponent<RectTransform>(),
                    column * (cellWidth + 6f),
                    row * 42f,
                    cellWidth,
                    36f);
            }
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }

        private void SetCaretToEnd()
        {
            if (_input == null)
            {
                return;
            }
            var end = _input.text.Length;
            _input.caretPosition = end;
            _input.selectionAnchorPosition = end;
            _input.selectionFocusPosition = end;
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = commandFont;
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            text.lineSpacing = 1f;
            return text;
        }

        private static void SetFullInset(RectTransform rect, float left, float right, float bottom, float top)
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

        private static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }
    }

    public sealed class WofCommandSuggestionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image _border;
        private Image _fill;
        private Color _normalBorder;
        private Color _normalFill;
        private Color _hoverBorder;
        private Color _hoverFill;

        public void Configure(Image border, Image fill, Color normalBorder, Color normalFill, Color hoverBorder, Color hoverFill)
        {
            _border = border;
            _fill = fill;
            _normalBorder = normalBorder;
            _normalFill = normalFill;
            _hoverBorder = hoverBorder;
            _hoverFill = hoverFill;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_border != null) _border.color = _hoverBorder;
            if (_fill != null) _fill.color = _hoverFill;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_border != null) _border.color = _normalBorder;
            if (_fill != null) _fill.color = _normalFill;
        }
    }
}
