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
        [SerializeField] private Sprite[] leftHandFrames;
        [SerializeField] private Sprite[] rightHandFrames;
        [SerializeField] private Sprite[] leftFiringHandFrames;
        [SerializeField] private Sprite[] rightFiringHandFrames;
        [SerializeField] private Sprite[] leftHeldSpellFrames;
        [SerializeField] private Sprite[] rightHeldSpellFrames;
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

        public static WofHud Instance { get; private set; }
        public bool IsGameplayVisible => _gameplayVisible && !_gameplaySurfaceBlocked;
        public bool AreMobileControlsVisible => mobileRoot != null && mobileRoot.activeInHierarchy;
        public bool AreMagicHandsVisible => _magicHandsVisible &&
                                            leftHandImage != null && leftHandImage.gameObject.activeInHierarchy &&
                                            rightHandImage != null && rightHandImage.gameObject.activeInHierarchy;

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

        public void SetHeldSpellVisibility(bool leftVisible, bool rightVisible)
        {
            if (leftHeldSpellImage != null) leftHeldSpellImage.gameObject.SetActive(_magicHandsVisible && leftVisible);
            if (heldSpellImage != null) heldSpellImage.gameObject.SetActive(_magicHandsVisible && rightVisible);
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
}
