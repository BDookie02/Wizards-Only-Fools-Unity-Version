using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WOF
{
    public sealed class WofHud : MonoBehaviour
    {
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private GameObject mobileRoot;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image armorFill;
        [SerializeField] private Text healthText;
        [SerializeField] private Text armorText;
        [SerializeField] private Image aetherFill;
        [SerializeField] private Image leftManaFill;
        [SerializeField] private Image rightManaFill;
        [SerializeField] private Text leftSpellText;
        [SerializeField] private Text rightSpellText;
        [SerializeField] private Text leftHotkeysText;
        [SerializeField] private Text rightHotkeysText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text roomText;
        [SerializeField] private Image leftHandImage;
        [SerializeField] private Image rightHandImage;
        [SerializeField] private Image leftHeldSpellImage;
        [SerializeField] private Image heldSpellImage;
        [SerializeField] private Image leftHeldSpellIcon;
        [SerializeField] private Image rightHeldSpellIcon;
        [SerializeField] private Sprite[] leftHandFrames;
        [SerializeField] private Sprite[] rightHandFrames;
        [SerializeField] private Sprite[] leftFiringHandFrames;
        [SerializeField] private Sprite[] rightFiringHandFrames;
        [SerializeField] private Sprite[] leftHeldSpellFrames;
        [SerializeField] private Sprite[] rightHeldSpellFrames;
        [SerializeField] private Sprite[] heldSpellIcons;
        [SerializeField] private WofMagicHandsLayout magicHandsLayout;

        private float _handClock;
        private float _spellClock;
        private int _handFrame;
        private int _spellFrame;
        private bool _gameplayVisible;
        private bool _forceMobileControls;
        private bool _forceLeftFiring;
        private bool _forceRightFiring;
        private bool _leftCastHeld;
        private bool _rightCastHeld;
        private bool _handIdleProbe;
        private int _lastProbedHandFrame = -1;
        private bool _magicArmed = true;
        private bool _magicHandsVisible = true;
        private bool _gameplaySurfaceBlocked;
        private float _leftFiringStartedAt = float.NegativeInfinity;
        private float _rightFiringStartedAt = float.NegativeInfinity;
        private float _leftFiringUntil;
        private float _rightFiringUntil;
        private readonly Dictionary<int, int> _baseTextSizes = new();
        private Image _flashbangOverlay;
        private WofMagicGlassOrbGraphic _leftMagicGlassOrb;
        private WofMagicGlassOrbGraphic _rightMagicGlassOrb;
        private WofPlayerController _magicGlassOrbOwner;
        private float _nextMagicGlassOrbSignalAt;
        private WofSpellId _leftPresentedSpell = WofSpellLoadout.ReactDefaultLeft;
        private WofSpellId _rightPresentedSpell = WofSpellLoadout.ReactDefaultRight;

        public static WofHud Instance { get; private set; }
        public bool IsGameplayVisible => _gameplayVisible && !_gameplaySurfaceBlocked;
        public bool AreMobileControlsVisible => mobileRoot != null && mobileRoot.activeInHierarchy;
        public Transform MobileControlsRoot => mobileRoot == null ? null : mobileRoot.transform;
        public bool AreMagicHandsVisible => _magicHandsVisible &&
                                            leftHandImage != null && leftHandImage.gameObject.activeInHierarchy &&
                                            rightHandImage != null && rightHandImage.gameObject.activeInHierarchy;
        internal bool MagicGlassOrbHasSignal =>
            (_leftMagicGlassOrb != null && _leftMagicGlassOrb.gameObject.activeInHierarchy && _leftMagicGlassOrb.HasSignal) ||
            (_rightMagicGlassOrb != null && _rightMagicGlassOrb.gameObject.activeInHierarchy && _rightMagicGlassOrb.HasSignal);
        internal bool MagicGlassOrbIsLocked =>
            (_leftMagicGlassOrb != null && _leftMagicGlassOrb.gameObject.activeInHierarchy && _leftMagicGlassOrb.IsLocked) ||
            (_rightMagicGlassOrb != null && _rightMagicGlassOrb.gameObject.activeInHierarchy && _rightMagicGlassOrb.IsLocked);

        public void SetTextScale(float scale)
        {
            if (gameplayRoot == null) return;
            scale = Mathf.Clamp(scale, 0.75f, 1.45f);
            foreach (var text in gameplayRoot.GetComponentsInChildren<Text>(true))
            {
                var key = text.GetInstanceID();
                if (!_baseTextSizes.TryGetValue(key, out var baseSize))
                {
                    baseSize = Mathf.Max(1, text.fontSize);
                    _baseTextSizes[key] = baseSize;
                }

                var scaledSize = Mathf.Max(1, Mathf.RoundToInt(baseSize * scale));
                text.fontSize = scaledSize;
                if (text.resizeTextForBestFit)
                {
                    text.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, baseSize / 2f) * scale));
                    text.resizeTextMaxSize = scaledSize;
                }
            }
        }

        private void Awake()
        {
            Instance = this;
            _forceMobileControls = System.Array.Exists(
                System.Environment.GetCommandLineArgs(),
                value => value == "--wof-mobile-ui");
            foreach (var argument in System.Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-hand-fire-probe=left", System.StringComparison.OrdinalIgnoreCase) ||
                    argument.Equals("--wof-hand-fire-probe=both", System.StringComparison.OrdinalIgnoreCase))
                {
                    _forceLeftFiring = true;
                }
                if (argument.Equals("--wof-hand-fire-probe=right", System.StringComparison.OrdinalIgnoreCase) ||
                    argument.Equals("--wof-hand-fire-probe=both", System.StringComparison.OrdinalIgnoreCase))
                {
                    _forceRightFiring = true;
                }
                if (argument.Equals("--wof-hand-idle-probe", System.StringComparison.OrdinalIgnoreCase))
                {
                    _handIdleProbe = true;
                }
            }
            EnsureHandOccludesHeldSpell(leftHeldSpellImage, leftHandImage);
            EnsureHandOccludesHeldSpell(heldSpellImage, rightHandImage);
            CreateFlashbangOverlay();
            CreateMagicGlassOrbEffects();
            SetGameplayVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            RefreshMobileControlVisibility();
            UpdateMagicGlassOrbEffects();
            if (gameplayRoot == null || !gameplayRoot.activeInHierarchy)
            {
                return;
            }

            AnimateFrameClock(ref _handClock, ref _handFrame, 4, 0.28f);
            var leftFiringForced = _forceLeftFiring || _leftCastHeld;
            var rightFiringForced = _forceRightFiring || _rightCastHeld;
            var leftFiring = ResolveFiringPoseActive(leftFiringForced, Time.unscaledTime, _leftFiringUntil);
            var rightFiring = ResolveFiringPoseActive(rightFiringForced, Time.unscaledTime, _rightFiringUntil);
            var leftFlexFrame = ResolveFiringFlexFrame(
                leftFiringForced,
                Time.unscaledTime,
                _leftFiringStartedAt,
                _leftFiringUntil,
                leftFiringHandFrames?.Length ?? 0);
            var rightFlexFrame = ResolveFiringFlexFrame(
                rightFiringForced,
                Time.unscaledTime,
                _rightFiringStartedAt,
                _rightFiringUntil,
                rightFiringHandFrames?.Length ?? 0);
            SyncFrame(
                leftHandImage,
                _magicArmed ? leftFiringHandFrames : leftHandFrames,
                ResolveEquippedHandFrame(_magicArmed, leftFiring, _handFrame, leftFlexFrame));
            SyncFrame(
                rightHandImage,
                _magicArmed ? rightFiringHandFrames : rightHandFrames,
                ResolveEquippedHandFrame(_magicArmed, rightFiring, _handFrame, rightFlexFrame));
            if (_handIdleProbe && !leftFiring && !rightFiring && _lastProbedHandFrame != _handFrame)
            {
                _lastProbedHandFrame = _handFrame;
                Debug.Log(
                    $"[WOF-AUTOMATION] HAND_IDLE_FRAME index={_handFrame} " +
                    $"left={leftHandImage?.sprite?.name ?? "none"} right={rightHandImage?.sprite?.name ?? "none"}");
            }
            // Equipped magic always uses the outward-pointing React pose. A cast
            // flexes the fingers inside that pose instead of swapping the arms
            // back to the unequipped open-palm layout.
            magicHandsLayout?.SetFiringPose(_magicArmed, _magicArmed);
            AnimateFrames(leftHeldSpellImage, leftHeldSpellFrames, ref _spellClock, ref _spellFrame, 0.1f);
            SyncFrame(heldSpellImage, rightHeldSpellFrames, _spellFrame);
            UpdateHeldSpellIconPose(leftHeldSpellIcon, _leftPresentedSpell, false, leftFiring);
            UpdateHeldSpellIconPose(rightHeldSpellIcon, _rightPresentedSpell, true, rightFiring);
        }

        public void PlayFiringPose(WofHandSide hand, float holdSeconds = 0.14f)
        {
            var now = Time.unscaledTime;
            var until = now + Mathf.Max(0.14f, holdSeconds);
            if (hand == WofHandSide.Left)
            {
                if (now >= _leftFiringUntil)
                {
                    _leftFiringStartedAt = now;
                }
                _leftFiringUntil = Mathf.Max(_leftFiringUntil, until);
            }
            else
            {
                if (now >= _rightFiringUntil)
                {
                    _rightFiringStartedAt = now;
                }
                _rightFiringUntil = Mathf.Max(_rightFiringUntil, until);
            }

            Debug.Log($"[WOF] FIRING_HAND {hand} hold={Mathf.Max(0.14f, holdSeconds):F2}");
        }

        public void SetHandCasting(WofHandSide hand, bool active)
        {
            if (hand == WofHandSide.Left)
            {
                _leftCastHeld = active;
            }
            else
            {
                _rightCastHeld = active;
            }
        }

        public void SetFlashbangOpacity(float opacity)
        {
            if (_flashbangOverlay == null) CreateFlashbangOverlay();
            if (_flashbangOverlay == null) return;
            var resolved = Mathf.Clamp01(opacity);
            _flashbangOverlay.color = new Color(1f, 1f, 1f, resolved);
            _flashbangOverlay.gameObject.SetActive(resolved > 0.0001f);
        }

        private void CreateFlashbangOverlay()
        {
            if (_flashbangOverlay != null || gameplayRoot == null) return;
            var overlayObject = new GameObject(
                "IceSpellFlashbang",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlayObject.transform.SetParent(gameplayRoot.transform, false);
            overlayObject.transform.SetAsLastSibling();
            var rect = overlayObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _flashbangOverlay = overlayObject.GetComponent<Image>();
            _flashbangOverlay.raycastTarget = false;
            _flashbangOverlay.color = new Color(1f, 1f, 1f, 0f);
            overlayObject.SetActive(false);
        }

        public void SetGameplayVisible(bool visible)
        {
            _gameplayVisible = visible;
            if (gameplayRoot != null)
            {
                gameplayRoot.SetActive(visible && !_gameplaySurfaceBlocked);
            }

            if (!visible)
            {
                WofInputRouter.ResetMobile();
                _leftFiringUntil = 0f;
                _rightFiringUntil = 0f;
                _leftFiringStartedAt = float.NegativeInfinity;
                _rightFiringStartedAt = float.NegativeInfinity;
                magicHandsLayout?.SetFiringPose(false, false);
            }

            RefreshMobileControlVisibility();
        }

        public void SetGameplaySurfaceBlocked(bool blocked)
        {
            _gameplaySurfaceBlocked = blocked;
            if (gameplayRoot != null)
            {
                gameplayRoot.SetActive(_gameplayVisible && !blocked);
            }
            if (blocked)
            {
                WofInputRouter.ResetMobile();
            }
            RefreshMobileControlVisibility();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                WofInputRouter.ResetMobile();
                mobileRoot?.SetActive(false);
                return;
            }

            RefreshMobileControlVisibility();
        }

        public void SetVitals(float health, float armor)
        {
            if (healthFill != null)
            {
                healthFill.fillAmount = Mathf.Clamp01(health / (float)WofGameConstants.MaxHealth);
            }

            if (armorFill != null)
            {
                armorFill.fillAmount = Mathf.Clamp01(armor / (float)WofGameConstants.MaxArmor);
            }

            if (healthText != null)
            {
                healthText.text = FormatVitalValue(health, WofGameConstants.MaxHealth);
            }

            if (armorText != null)
            {
                armorText.text = FormatVitalValue(armor, WofGameConstants.MaxArmor);
            }
        }

        public void SetAether(float normalizedFuel)
        {
            if (aetherFill != null)
            {
                aetherFill.fillAmount = Mathf.Clamp01(normalizedFuel);
            }
        }

        public void SetMana(float leftNormalized, float rightNormalized)
        {
            if (leftManaFill != null)
            {
                leftManaFill.fillAmount = Mathf.Clamp01(leftNormalized);
            }
            if (rightManaFill != null)
            {
                rightManaFill.fillAmount = Mathf.Clamp01(rightNormalized);
            }
        }

        public void SetEquippedSpells(string leftSpell, string rightSpell, bool armed = true)
        {
            _magicArmed = armed;
            if (leftSpellText != null)
            {
                leftSpellText.text = armed ? $"L {NormalizeSpellLabel(leftSpell)}" : "MAGIC STOWED";
            }
            if (rightSpellText != null)
            {
                rightSpellText.gameObject.SetActive(armed);
                rightSpellText.text = $"R {NormalizeSpellLabel(rightSpell)}";
            }
            magicHandsLayout?.SetFiringPose(armed, armed);
        }

        public void SetHotbarSelection(int leftSlot, int rightSlot)
        {
            if (leftHotkeysText != null)
            {
                leftHotkeysText.text = BuildHotbarLabel("L", leftSlot, "#FDE047", "#FACC15");
            }
            if (rightHotkeysText != null)
            {
                rightHotkeysText.text = BuildHotbarLabel("R", rightSlot, "#F0ABFC", "#E879F9");
            }
        }

        private static string BuildHotbarLabel(string hand, int selectedSlot, string handColor, string selectedColor)
        {
            selectedSlot = Mathf.Clamp(selectedSlot, 0, WofSpellHotbarRuntime.SlotCount - 1);
            var builder = new System.Text.StringBuilder(180);
            builder.Append("<color=").Append(handColor).Append('>').Append(hand).Append("</color>   ");
            for (var index = 0; index < WofSpellHotbarRuntime.SlotCount; index++)
            {
                if (index > 0) builder.Append(' ');
                builder.Append("<color=")
                    .Append(index == selectedSlot ? selectedColor : "#555555")
                    .Append('>')
                    .Append(index == 9 ? 0 : index + 1)
                    .Append("</color>");
            }
            return builder.ToString();
        }

        public void SetHeldSpellPresentation(
            WofSpellId leftSpell,
            WofSpellId rightSpell,
            WofPlayerController owner)
        {
            _leftPresentedSpell = leftSpell;
            _rightPresentedSpell = rightSpell;
            _magicGlassOrbOwner = owner;
            CreateMagicGlassOrbEffects();
            ApplyHeldSpellPresentation(leftHeldSpellImage, leftHeldSpellIcon, _leftMagicGlassOrb, leftSpell, false);
            ApplyHeldSpellPresentation(heldSpellImage, rightHeldSpellIcon, _rightMagicGlassOrb, rightSpell, true);
            _nextMagicGlassOrbSignalAt = 0f;
        }

        public void SetMagicHandsVisible(bool visible)
        {
            _magicHandsVisible = visible;
            if (leftHandImage != null) leftHandImage.gameObject.SetActive(visible);
            if (rightHandImage != null) rightHandImage.gameObject.SetActive(visible);
            if (!visible)
            {
                if (leftHeldSpellImage != null) leftHeldSpellImage.gameObject.SetActive(false);
                if (heldSpellImage != null) heldSpellImage.gameObject.SetActive(false);
                _leftFiringUntil = 0f;
                _rightFiringUntil = 0f;
            }
            else
            {
                ApplyHeldSpellPresentation(
                    leftHeldSpellImage,
                    leftHeldSpellIcon,
                    _leftMagicGlassOrb,
                    _leftPresentedSpell,
                    false);
                ApplyHeldSpellPresentation(
                    heldSpellImage,
                    rightHeldSpellIcon,
                    _rightMagicGlassOrb,
                    _rightPresentedSpell,
                    true);
            }
        }

        public void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        public void SetRoom(string value)
        {
            if (roomText != null)
            {
                roomText.text = value;
            }
        }

        private void RefreshMobileControlVisibility()
        {
            if (mobileRoot == null)
            {
                return;
            }

            var shouldShow = _gameplayVisible && !_gameplaySurfaceBlocked && WofInputRouter.ShouldShowTouchControls(
                WofPerformanceModeRuntime.IsTouchGameplayDevice,
                _forceMobileControls,
                Gamepad.all.Count > 0);
            if (mobileRoot.activeSelf != shouldShow)
            {
                mobileRoot.SetActive(shouldShow);
            }
        }

        private void CreateMagicGlassOrbEffects()
        {
            _leftMagicGlassOrb ??= CreateMagicGlassOrbEffect(leftHeldSpellImage, "LeftMagicGlassOrb", false);
            _rightMagicGlassOrb ??= CreateMagicGlassOrbEffect(heldSpellImage, "RightMagicGlassOrb", true);
        }

        private static WofMagicGlassOrbGraphic CreateMagicGlassOrbEffect(Image parentImage, string name, bool right)
        {
            if (parentImage == null) return null;
            var existing = parentImage.transform.Find(name)?.GetComponent<WofMagicGlassOrbGraphic>();
            if (existing != null) return existing;

            var effectObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(WofMagicGlassOrbGraphic));
            effectObject.transform.SetParent(parentImage.transform, false);
            var rect = effectObject.GetComponent<RectTransform>();
            var anchor = right
                ? new Vector2(358f / 859f, (495f - 224f) / 495f)
                : new Vector2(338f / 859f, (495f - 218f) / 495f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            var graphic = effectObject.GetComponent<WofMagicGlassOrbGraphic>();
            graphic.raycastTarget = false;
            effectObject.SetActive(false);
            return graphic;
        }

        private void ApplyHeldSpellPresentation(
            Image parentImage,
            Image spellIcon,
            WofMagicGlassOrbGraphic magicGlassOrb,
            WofSpellId spell,
            bool right)
        {
            if (parentImage == null) return;
            var spec = WofHeldSpellPresentationRules.Get(spell);
            var showFireball = _magicHandsVisible && spec.Kind == WofHeldSpellVisualKind.AnimatedFireball;
            var showMagicGlassOrb = _magicHandsVisible && spec.Kind == WofHeldSpellVisualKind.MagicGlassOrb;
            var showIcon = _magicHandsVisible && spec.Kind == WofHeldSpellVisualKind.ReactSprite;
            parentImage.gameObject.SetActive(_magicHandsVisible);
            parentImage.enabled = showFireball;
            if (spellIcon != null)
            {
                spellIcon.gameObject.SetActive(showIcon);
                if (showIcon)
                {
                    var spriteIndex = WofHeldSpellPresentationRules.GetSpriteIndex(spell);
                    spellIcon.sprite = heldSpellIcons != null && spriteIndex >= 0 && spriteIndex < heldSpellIcons.Length
                        ? heldSpellIcons[spriteIndex]
                        : null;
                    spellIcon.enabled = spellIcon.sprite != null;
                    ConfigureHeldSpellIcon(spellIcon.rectTransform, spec, right);
                }
            }
            magicGlassOrb?.gameObject.SetActive(showMagicGlassOrb);
        }

        private void ConfigureHeldSpellIcon(RectTransform rect, WofHeldSpellVisualSpec spec, bool right)
        {
            if (rect == null) return;
            var anchor = right
                ? new Vector2(358f / 859f, (495f - 224f) / 495f)
                : new Vector2(338f / 859f, (495f - 218f) / 495f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            var canvas = GetComponentInParent<Canvas>();
            var scaleFactor = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
            var size = spec.ResolveSizePixels(Screen.height) / scaleFactor;
            rect.sizeDelta = Vector2.one * size;
            UpdateHeldSpellIconPose(rect.GetComponent<Image>(), right ? _rightPresentedSpell : _leftPresentedSpell, right, false);
        }

        private static void UpdateHeldSpellIconPose(Image icon, WofSpellId spell, bool right, bool firing)
        {
            if (icon == null || !icon.gameObject.activeSelf) return;
            var spec = WofHeldSpellPresentationRules.Get(spell);
            var pulse = firing ? 1.08f + Mathf.Sin(Time.unscaledTime * 18f) * 0.025f : 1f;
            icon.rectTransform.localScale = new Vector3(right ? -pulse : pulse, pulse, 1f);
            icon.rectTransform.localEulerAngles = new Vector3(
                0f,
                0f,
                right ? -spec.RotationDegrees : spec.RotationDegrees);
        }

        internal static void EnsureHandOccludesHeldSpell(Image spell, Image hand)
        {
            if (spell == null || hand == null || spell.transform.parent != hand.transform.parent) return;
            var firstIndex = Mathf.Min(spell.transform.GetSiblingIndex(), hand.transform.GetSiblingIndex());
            spell.transform.SetSiblingIndex(firstIndex);
            hand.transform.SetSiblingIndex(firstIndex + 1);
        }

        private void UpdateMagicGlassOrbEffects()
        {
            var leftActive = _leftMagicGlassOrb != null && _leftMagicGlassOrb.gameObject.activeInHierarchy;
            var rightActive = _rightMagicGlassOrb != null && _rightMagicGlassOrb.gameObject.activeInHierarchy;
            if (!leftActive && !rightActive) return;

            var charging = (leftActive && _leftCastHeld) || (rightActive && _rightCastHeld);
            var size = ResolveMagicGlassOrbSize(charging);
            if (leftActive) _leftMagicGlassOrb.rectTransform.sizeDelta = Vector2.one * size;
            if (rightActive) _rightMagicGlassOrb.rectTransform.sizeDelta = Vector2.one * size;

            if (Time.unscaledTime < _nextMagicGlassOrbSignalAt) return;
            _nextMagicGlassOrbSignalAt = Time.unscaledTime + 0.16f;
            var hasSignal = TryResolveMagicGlassOrbSignal(_magicGlassOrbOwner, out var locked);
            if (leftActive) _leftMagicGlassOrb.SetSignal(hasSignal, locked);
            if (rightActive) _rightMagicGlassOrb.SetSignal(hasSignal, locked);
        }

        private float ResolveMagicGlassOrbSize(bool charging)
        {
            var physicalMinimum = charging ? 203f : 169f;
            var physicalMaximum = charging ? 378f : 316f;
            var viewportRatio = charging ? 0.4644f : 0.3784f;
            var physicalSize = Mathf.Clamp(Screen.height * viewportRatio, physicalMinimum, physicalMaximum);
            var canvas = GetComponent<Canvas>();
            var scaleFactor = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
            return physicalSize / scaleFactor;
        }

        private static bool TryResolveMagicGlassOrbSignal(WofPlayerController owner, out bool locked)
        {
            locked = false;
            if (owner == null || !owner.IsSpawned || owner.IsDead) return false;

            WofPlayerController nearest = null;
            var nearestDistanceSquared = float.PositiveInfinity;
            foreach (var player in FindObjectsByType<WofPlayerController>(FindObjectsSortMode.None))
            {
                if (player == null || !player.IsSpawned || player.IsDead ||
                    player.OwnerClientId == owner.OwnerClientId) continue;
                var offset = player.transform.position - owner.transform.position;
                offset.y = 0f;
                var distanceSquared = offset.sqrMagnitude;
                if (distanceSquared <= 0.01f || distanceSquared >= nearestDistanceSquared) continue;
                nearest = player;
                nearestDistanceSquared = distanceSquared;
            }

            if (nearest == null) return false;
            var direction = nearest.transform.position - owner.transform.position;
            var angle = WofSpellOutcomeRules.ResolveMagicGlassOrbRelativeAngle(owner.transform.forward, direction);
            locked = WofSpellOutcomeRules.IsMagicGlassOrbLocked(angle);
            return true;
        }

        private static void AnimateFrames(Image target, Sprite[] frames, ref float clock, ref int index, float frameSeconds)
        {
            if (target == null || frames == null || frames.Length == 0)
            {
                return;
            }

            clock += Time.unscaledDeltaTime;
            if (clock < frameSeconds)
            {
                return;
            }

            clock -= frameSeconds;
            index = (index + 1) % frames.Length;
            target.sprite = frames[index];
        }

        private static void AnimateFrameClock(ref float clock, ref int index, int frameCount, float frameSeconds)
        {
            if (frameCount <= 0)
            {
                return;
            }

            clock += Time.unscaledDeltaTime;
            if (clock < frameSeconds)
            {
                return;
            }

            clock -= frameSeconds;
            index = (index + 1) % frameCount;
        }

        private static void SyncFrame(Image target, Sprite[] frames, int index)
        {
            if (target == null || frames == null || frames.Length == 0)
            {
                return;
            }

            target.sprite = frames[index % frames.Length];
        }

        internal static bool ResolveFiringPoseActive(bool forced, float now, float firingUntil)
        {
            return forced || now < firingUntil;
        }

        internal static int ResolveEquippedHandFrame(
            bool magicArmed,
            bool firing,
            int idleFrame,
            int firingFlexFrame)
        {
            // The player's requested armed idle uses the outward-pointing sheet,
            // but that sheet still has the same four-frame 280 ms React idle loop.
            // Casting temporarily replaces the loop index with its 140 ms flex.
            return magicArmed && firing ? firingFlexFrame : idleFrame;
        }

        internal static int ResolveFiringFlexFrame(
            bool forced,
            float now,
            float firingStartedAt,
            float firingUntil,
            int frameCount)
        {
            if (frameCount <= 1)
            {
                return 0;
            }

            if (forced)
            {
                return Mathf.Min(frameCount - 1, 2);
            }

            var duration = firingUntil - firingStartedAt;
            if (duration <= 0f || now < firingStartedAt || now >= firingUntil)
            {
                return 0;
            }

            // The exact React firing sheet has four progressive finger poses.
            // Play a restrained flex and release during the 140 ms cast window.
            var phase = Mathf.Clamp01((now - firingStartedAt) / duration);
            var flex = phase < 0.25f ? 0 : phase < 0.5f ? 1 : phase < 0.75f ? 2 : 1;
            return Mathf.Min(frameCount - 1, flex);
        }

        internal static string FormatVitalValue(float value, int maximum)
        {
            var safeMaximum = Mathf.Max(0, maximum);
            var rounded = Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Max(0f, value) + 0.5f),
                0,
                safeMaximum);
            return $"{rounded}/{safeMaximum}";
        }

        internal static string NormalizeSpellLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "NO MANA" : value.Trim().ToUpperInvariant();
        }
    }

    /// <summary>
    /// Exact 24x24 code-native port of React's equipped Magic Glass Orb SVG.
    /// The center signal is cyan with no target, red off-axis, and green while
    /// the nearest living remote wizard is inside the 0.12-radian lock cone.
    /// </summary>
    public sealed class WofMagicGlassOrbGraphic : MaskableGraphic
    {
        private readonly struct PixelBlock
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Width;
            public readonly int Height;
            public readonly Color32 Color;

            public PixelBlock(int x, int y, int width, int height, Color32 color)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Color = color;
            }
        }

        private static readonly PixelBlock[] Blocks =
        {
            Block(8, 1, 8, 1, "E0F7FF", 0.74f),
            Block(5, 2, 14, 2, "7DD3FC"),
            Block(4, 4, 17, 2, "67E8F9"),
            Block(3, 6, 19, 4, "38BDF8"),
            Block(2, 10, 20, 5, "0EA5E9"),
            Block(3, 15, 18, 3, "0284C7"),
            Block(5, 18, 14, 2, "0369A1"),
            Block(8, 20, 8, 1, "075985"),
            Block(3, 10, 2, 4, "7DD3FC", 0.52f),
            Block(5, 6, 3, 3, "BAE6FD", 0.56f),
            Block(9, 4, 4, 3, "ECFEFF", 0.74f),
            Block(13, 6, 2, 2, "FFFFFF", 0.82f),
            Block(16, 8, 2, 2, "E0F2FE", 0.54f),
            Block(15, 16, 4, 2, "075985", 0.28f),
            Block(6, 17, 3, 1, "67E8F9", 0.42f)
        };

        private bool _hasSignal;
        private bool _locked;

        public bool HasSignal => _hasSignal;
        public bool IsLocked => _locked;

        public void SetSignal(bool hasSignal, bool locked)
        {
            locked &= hasSignal;
            if (_hasSignal == hasSignal && _locked == locked) return;
            _hasSignal = hasSignal;
            _locked = locked;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            for (var index = 0; index < Blocks.Length; index++) AddBlock(vertexHelper, Blocks[index]);

            var signalColor = !_hasSignal
                ? Hex("7DD3FC", 0.78f)
                : _locked ? Hex("4ADE80") : Hex("EF4444");
            AddBlock(vertexHelper, new PixelBlock(11, 11, _hasSignal ? 3 : 2, _hasSignal ? 3 : 2, signalColor));
            AddBlock(vertexHelper, Block(11, 11, 1, 1, "FFFFFF", 0.58f));
        }

        private void AddBlock(VertexHelper vertexHelper, PixelBlock block)
        {
            var bounds = GetPixelBounds(block.X, block.Y, block.Width, block.Height);
            var start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(new Vector3(bounds.xMin, bounds.yMin), block.Color, Vector2.zero);
            vertexHelper.AddVert(new Vector3(bounds.xMin, bounds.yMax), block.Color, Vector2.up);
            vertexHelper.AddVert(new Vector3(bounds.xMax, bounds.yMax), block.Color, Vector2.one);
            vertexHelper.AddVert(new Vector3(bounds.xMax, bounds.yMin), block.Color, Vector2.right);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private Rect GetPixelBounds(int x, int y, int width, int height)
        {
            var area = GetPixelAdjustedRect();
            var pixelWidth = area.width / 24f;
            var pixelHeight = area.height / 24f;
            return new Rect(
                area.xMin + x * pixelWidth,
                area.yMax - (y + height) * pixelHeight,
                width * pixelWidth,
                height * pixelHeight);
        }

        private static PixelBlock Block(int x, int y, int width, int height, string hex, float opacity = 1f)
        {
            return new PixelBlock(x, y, width, height, Hex(hex, opacity));
        }

        private static Color32 Hex(string hex, float opacity = 1f)
        {
            ColorUtility.TryParseHtmlString($"#{hex}", out var parsed);
            parsed.a = Mathf.Clamp01(opacity);
            return parsed;
        }
    }
}
