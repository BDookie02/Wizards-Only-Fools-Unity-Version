using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WOF
{
    public sealed class WofSpellMenuRuntime : MonoBehaviour
    {
        [SerializeField] private WofHud hud;
        [SerializeField] private Transform uiParent;
        [SerializeField] private Transform mobileControlsRoot;
        [SerializeField] private Font headingFont;
        [SerializeField] private Sprite spellbookIcon;
        [SerializeField] private Sprite fireballThumbnail;
        [SerializeField] private Sprite speedBoostThumbnail;
        [SerializeField] private Sprite jumpBoostThumbnail;

        private GameObject _overlay;
        private Button _leftHandButton;
        private Button _rightHandButton;
        private Button[] _spellButtons;
        private Text _bindingStatus;
        private WofHandSide _bindingHand = WofHandSide.Left;
        private int _selectedIndex;
        private WofPlayerController _localPlayer;
        private bool _controllerToggleHeld;
        private bool _controllerBackHeld;

        public static WofSpellMenuRuntime Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._overlay != null && Instance._overlay.activeSelf;

        public void ConfigureGeneratedView(
            WofHud generatedHud,
            Transform generatedUiParent,
            Transform generatedMobileControlsRoot,
            Font generatedHeadingFont,
            Sprite generatedSpellbookIcon,
            Sprite generatedFireballThumbnail,
            Sprite generatedSpeedBoostThumbnail,
            Sprite generatedJumpBoostThumbnail)
        {
            hud = generatedHud;
            uiParent = generatedUiParent;
            mobileControlsRoot = generatedMobileControlsRoot;
            headingFont = generatedHeadingFont;
            spellbookIcon = generatedSpellbookIcon;
            fireballThumbnail = generatedFireballThumbnail;
            speedBoostThumbnail = generatedSpeedBoostThumbnail;
            jumpBoostThumbnail = generatedJumpBoostThumbnail;
        }

        private void Awake()
        {
            Instance = this;
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
            if (_overlay == null || hud == null || (!hud.IsGameplayVisible && !_overlay.activeSelf))
            {
                return;
            }

            var keyboardToggle = Keyboard.current?.eKey.wasPressedThisFrame ?? false;
            var gamepad = Gamepad.current;
            var controllerToggleHeld = gamepad?.dpad.up.isPressed ?? false;
            var controllerToggle = controllerToggleHeld && !_controllerToggleHeld;
            _controllerToggleHeld = controllerToggleHeld;
            if (keyboardToggle && !WofNavigationMapRuntime.IsExpanded)
            {
                SetOpen(!_overlay.activeSelf);
                return;
            }
            if (controllerToggle && !_overlay.activeSelf && !WofNavigationMapRuntime.IsExpanded)
            {
                SetOpen(true);
                return;
            }

            if (!_overlay.activeSelf || gamepad == null)
            {
                return;
            }

            var controllerBackHeld = gamepad.buttonEast.isPressed;
            var controllerBack = controllerBackHeld && !_controllerBackHeld;
            _controllerBackHeld = controllerBackHeld;
            if (controllerBack)
            {
                SetOpen(false);
                return;
            }
            if (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftShoulder.wasPressedThisFrame)
            {
                SetBindingHand(WofHandSide.Left);
            }
            else if (gamepad.dpad.right.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame)
            {
                SetBindingHand(WofHandSide.Right);
            }
            if (gamepad.dpad.down.wasPressedThisFrame)
            {
                SetSelectedIndex((_selectedIndex + 1) % WofSpellLoadout.PlayableSpells.Length);
            }
            else if (gamepad.dpad.up.wasPressedThisFrame)
            {
                SetSelectedIndex((_selectedIndex - 1 + WofSpellLoadout.PlayableSpells.Length) %
                                 WofSpellLoadout.PlayableSpells.Length);
            }
            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                EquipSelectedSpell();
            }
        }

        public void Toggle()
        {
            if (_overlay != null && hud != null && hud.IsGameplayVisible && !WofNavigationMapRuntime.IsExpanded)
            {
                SetOpen(!_overlay.activeSelf);
            }
        }

        private void BuildView()
        {
            if (_overlay != null || uiParent == null || headingFont == null)
            {
                return;
            }

            _overlay = CreatePanel(
                "SpellMenuOverlay",
                uiParent,
                Vector2.zero,
                Vector2.one,
                new Color32(2, 4, 12, 148));
            var hologram = CreatePanel(
                "SpellMenuHologram",
                _overlay.transform,
                new Vector2(0.18f, 0.15f),
                new Vector2(0.82f, 0.85f),
                new Color32(18, 7, 31, 244));
            var outline = hologram.AddComponent<Outline>();
            outline.effectColor = new Color32(103, 232, 249, 180);
            outline.effectDistance = new Vector2(2f, -2f);

            var title = CreateText(
                "Title",
                hologram.transform,
                "SPELL BOOK",
                27,
                TextAnchor.MiddleLeft,
                new Color32(207, 250, 254, 255));
            SetRect(title.rectTransform, new Vector2(0.045f, 0.86f), new Vector2(0.62f, 0.97f));
            var close = CreateButton(
                "Close",
                hologram.transform,
                "CLOSE",
                new Color32(68, 68, 78, 255));
            SetRect(close.GetComponent<RectTransform>(), new Vector2(0.78f, 0.865f), new Vector2(0.955f, 0.965f));
            close.onClick.AddListener(() => SetOpen(false));

            var handLabel = CreateText(
                "HandLabel",
                hologram.transform,
                "BINDING HAND",
                13,
                TextAnchor.MiddleLeft,
                new Color32(165, 243, 252, 200));
            SetRect(handLabel.rectTransform, new Vector2(0.045f, 0.75f), new Vector2(0.29f, 0.83f));
            _leftHandButton = CreateButton(
                "LeftHand",
                hologram.transform,
                "LEFT",
                new Color32(14, 116, 144, 255));
            SetRect(_leftHandButton.GetComponent<RectTransform>(), new Vector2(0.30f, 0.75f), new Vector2(0.48f, 0.83f));
            _leftHandButton.onClick.AddListener(() => SetBindingHand(WofHandSide.Left));
            _rightHandButton = CreateButton(
                "RightHand",
                hologram.transform,
                "RIGHT",
                new Color32(39, 39, 53, 255));
            SetRect(_rightHandButton.GetComponent<RectTransform>(), new Vector2(0.50f, 0.75f), new Vector2(0.68f, 0.83f));
            _rightHandButton.onClick.AddListener(() => SetBindingHand(WofHandSide.Right));

            _spellButtons = new Button[WofSpellLoadout.PlayableSpells.Length];
            for (var index = 0; index < _spellButtons.Length; index++)
            {
                var capturedIndex = index;
                var spell = WofSpellLoadout.PlayableSpells[index];
                var top = 0.69f - index * 0.17f;
                var button = CreateButton(
                    $"Spell_{spell}",
                    hologram.transform,
                    string.Empty,
                    index == 0 ? new Color32(51, 28, 68, 255) : new Color32(24, 16, 36, 255));
                SetRect(button.GetComponent<RectTransform>(), new Vector2(0.045f, top - 0.135f), new Vector2(0.955f, top));
                button.onClick.AddListener(() =>
                {
                    SetSelectedIndex(capturedIndex);
                    EquipSelectedSpell();
                });

                var thumbnail = new GameObject("Thumbnail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                thumbnail.transform.SetParent(button.transform, false);
                var thumbnailImage = thumbnail.GetComponent<Image>();
                thumbnailImage.sprite = spell switch
                {
                    WofSpellId.SpeedBoost => speedBoostThumbnail,
                    WofSpellId.JumpBoost => jumpBoostThumbnail,
                    _ => fireballThumbnail
                };
                thumbnailImage.preserveAspect = true;
                thumbnailImage.raycastTarget = false;
                SetRect(thumbnail.GetComponent<RectTransform>(), new Vector2(0.02f, 0.10f), new Vector2(0.16f, 0.90f));

                var name = CreateText(
                    "SpellName",
                    button.transform,
                    WofSpellLoadout.GetDisplayName(spell).ToUpperInvariant(),
                    18,
                    TextAnchor.MiddleLeft,
                    spell == WofSpellId.Fireball
                        ? new Color32(249, 115, 22, 255)
                        : spell == WofSpellId.SpeedBoost
                            ? new Color32(253, 224, 71, 255)
                            : new Color32(190, 242, 100, 255));
                SetRect(name.rectTransform, new Vector2(0.19f, 0.35f), new Vector2(0.72f, 0.90f));
                var family = CreateText(
                    "Family",
                    button.transform,
                    WofSpellLoadout.GetFamilyName(spell),
                    10,
                    TextAnchor.MiddleLeft,
                    new Color32(165, 243, 252, 175));
                SetRect(family.rectTransform, new Vector2(0.19f, 0.08f), new Vector2(0.72f, 0.38f));
                var action = CreateText(
                    "Action",
                    button.transform,
                    "EQUIP",
                    12,
                    TextAnchor.MiddleCenter,
                    new Color32(207, 250, 254, 255));
                SetRect(action.rectTransform, new Vector2(0.76f, 0.22f), new Vector2(0.96f, 0.78f));
                _spellButtons[index] = button;
            }

            _bindingStatus = CreateText(
                "BindingStatus",
                hologram.transform,
                string.Empty,
                12,
                TextAnchor.MiddleCenter,
                new Color32(207, 250, 254, 210));
            SetRect(_bindingStatus.rectTransform, new Vector2(0.045f, 0.035f), new Vector2(0.955f, 0.115f));

            if (mobileControlsRoot != null)
            {
                var mobileButton = CreateButton(
                    "MobileSpellBook",
                    mobileControlsRoot,
                    string.Empty,
                    Color.clear);
                var rect = mobileButton.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(14f, -14f);
                rect.sizeDelta = new Vector2(54f, 54f);
                var image = mobileButton.GetComponent<Image>();
                image.sprite = spellbookIcon;
                image.color = Color.white;
                image.preserveAspect = true;
                mobileButton.onClick.AddListener(Toggle);
            }

            _overlay.SetActive(false);
            RefreshSelectionVisuals();
        }

        private void SetOpen(bool open)
        {
            if (_overlay == null || _overlay.activeSelf == open)
            {
                return;
            }
            _overlay.SetActive(open);
            hud.SetGameplaySurfaceBlocked(open);
            WofInputRouter.SetGameplaySuppressed(open);
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            if (open)
            {
                _localPlayer = ResolveLocalPlayer();
                _bindingHand = WofHandSide.Left;
                _selectedIndex = GetEquippedIndex(_localPlayer, _bindingHand);
                RefreshSelectionVisuals();
                EventSystem.current?.SetSelectedGameObject(_spellButtons?[_selectedIndex]?.gameObject);
            }
            else
            {
                EventSystem.current?.SetSelectedGameObject(null);
            }
            Debug.Log($"[WOF-AUTOMATION] SPELL_MENU open={open}");
        }

        private void SetBindingHand(WofHandSide hand)
        {
            _bindingHand = hand;
            _localPlayer = ResolveLocalPlayer();
            SetSelectedIndex(GetEquippedIndex(_localPlayer, hand));
        }

        private void SetSelectedIndex(int index)
        {
            _selectedIndex = Mathf.Clamp(index, 0, WofSpellLoadout.PlayableSpells.Length - 1);
            RefreshSelectionVisuals();
            EventSystem.current?.SetSelectedGameObject(_spellButtons?[_selectedIndex]?.gameObject);
        }

        private void EquipSelectedSpell()
        {
            _localPlayer = ResolveLocalPlayer();
            if (_localPlayer == null)
            {
                if (_bindingStatus != null) _bindingStatus.text = "WAITING FOR PLAYER";
                return;
            }
            var spell = WofSpellLoadout.PlayableSpells[_selectedIndex];
            _localPlayer.EquipSpell(_bindingHand, spell);
            if (_bindingStatus != null)
            {
                _bindingStatus.text = $"{WofSpellLoadout.GetDisplayName(spell).ToUpperInvariant()} EQUIPPED TO {_bindingHand.ToString().ToUpperInvariant()} HAND";
            }
            RefreshSelectionVisuals();
        }

        private void RefreshSelectionVisuals()
        {
            if (_leftHandButton != null)
            {
                _leftHandButton.GetComponent<Image>().color = _bindingHand == WofHandSide.Left
                    ? new Color32(14, 116, 144, 255)
                    : new Color32(39, 39, 53, 255);
            }
            if (_rightHandButton != null)
            {
                _rightHandButton.GetComponent<Image>().color = _bindingHand == WofHandSide.Right
                    ? new Color32(14, 116, 144, 255)
                    : new Color32(39, 39, 53, 255);
            }
            if (_spellButtons == null) return;
            for (var index = 0; index < _spellButtons.Length; index++)
            {
                _spellButtons[index].GetComponent<Image>().color = index == _selectedIndex
                    ? new Color32(56, 38, 75, 255)
                    : new Color32(24, 16, 36, 255);
            }
        }

        private static int GetEquippedIndex(WofPlayerController player, WofHandSide hand)
        {
            var equipped = player == null
                ? hand == WofHandSide.Left ? WofSpellLoadout.ReactDefaultLeft : WofSpellLoadout.ReactDefaultRight
                : hand == WofHandSide.Left ? player.LeftEquippedSpell : player.RightEquippedSpell;
            for (var index = 0; index < WofSpellLoadout.PlayableSpells.Length; index++)
            {
                if (WofSpellLoadout.PlayableSpells[index] == equipped) return index;
            }
            return 0;
        }

        private static WofPlayerController ResolveLocalPlayer()
        {
            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            return playerObject == null ? null : playerObject.GetComponent<WofPlayerController>();
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
            text.font = headingFont;
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
            if (!string.IsNullOrEmpty(label))
            {
                var text = CreateText("Label", item.transform, label, 14, TextAnchor.MiddleCenter, Color.white);
                SetRect(text.rectTransform, Vector2.zero, Vector2.one);
            }
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
