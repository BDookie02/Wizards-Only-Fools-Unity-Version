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
        private bool _gameplaySurfaceBlocked;
        private float _leftFiringUntil;
        private float _rightFiringUntil;

        public static WofHud Instance { get; private set; }
        public bool IsGameplayVisible => _gameplayVisible && !_gameplaySurfaceBlocked;
        public bool AreMobileControlsVisible => mobileRoot != null && mobileRoot.activeInHierarchy;

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
            var leftFiring = ResolveFiringPoseActive(_forceLeftFiring, Time.unscaledTime, _leftFiringUntil);
            var rightFiring = ResolveFiringPoseActive(_forceRightFiring, Time.unscaledTime, _rightFiringUntil);
            SyncFrame(leftHandImage, leftFiring ? leftFiringHandFrames : leftHandFrames, _handFrame);
            SyncFrame(rightHandImage, rightFiring ? rightFiringHandFrames : rightHandFrames, _handFrame);
            magicHandsLayout?.SetFiringPose(leftFiring, rightFiring);
            AnimateFrames(leftHeldSpellImage, leftHeldSpellFrames, ref _spellClock, ref _spellFrame, 0.1f);
            SyncFrame(heldSpellImage, rightHeldSpellFrames, _spellFrame);
        }

        public void PlayFiringPose(WofHandSide hand, float holdSeconds = 0.14f)
        {
            var until = Time.unscaledTime + Mathf.Max(0.14f, holdSeconds);
            if (hand == WofHandSide.Left)
            {
                _leftFiringUntil = Mathf.Max(_leftFiringUntil, until);
            }
            else
            {
                _rightFiringUntil = Mathf.Max(_rightFiringUntil, until);
            }

            Debug.Log($"[WOF] FIRING_HAND {hand} hold={Mathf.Max(0.14f, holdSeconds):F2}");
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
            if (leftSpellText != null)
            {
                leftSpellText.text = armed ? $"L {NormalizeSpellLabel(leftSpell)}" : "MAGIC STOWED";
            }
            if (rightSpellText != null)
            {
                rightSpellText.gameObject.SetActive(armed);
                rightSpellText.text = $"R {NormalizeSpellLabel(rightSpell)}";
            }
        }

        public void SetHeldSpellVisibility(bool leftVisible, bool rightVisible)
        {
            if (leftHeldSpellImage != null) leftHeldSpellImage.gameObject.SetActive(leftVisible);
            if (heldSpellImage != null) heldSpellImage.gameObject.SetActive(rightVisible);
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
