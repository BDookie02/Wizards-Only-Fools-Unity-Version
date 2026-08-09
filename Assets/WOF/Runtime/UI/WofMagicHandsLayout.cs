using UnityEngine;

namespace WOF
{
    /// <summary>
    /// Reproduces the canonical React MagicHands frame, bottom overscan,
    /// horizontal hand offsets, touch Fill safe frame, and held-spell transform.
    /// Values are calculated in physical pixels and converted through the
    /// Canvas scale so CSS clamp() behavior remains stable at every viewport.
    /// </summary>
    public sealed class WofMagicHandsLayout : MonoBehaviour
    {
        private const float FrameAspect = 16f / 9f;
        private const float IdleHandTranslate = 0.085f;
        private const float FiringHandTranslate = 0.08f;
        private const float FiringPoseScale = 0.76f;
        private const float HeldSpellFrameTranslate = 0.08f;
        private const float TouchFillNudge = 0.02f;
        private const float HeldSpellScale = 0.76f * 1.09f;

        [SerializeField] private RectTransform leftHandFrame;
        [SerializeField] private RectTransform rightHandFrame;
        [SerializeField] private RectTransform leftSpellFrame;
        [SerializeField] private RectTransform rightSpellFrame;

        private Canvas _canvas;
        private bool _forceMobileLayout;
        private Vector2Int _lastScreenSize;
        private float _lastScaleFactor;
        private bool _leftFiring;
        private bool _rightFiring;

        public void SetFiringPose(bool leftFiring, bool rightFiring)
        {
            if (_leftFiring == leftFiring && _rightFiring == rightFiring)
            {
                return;
            }

            _leftFiring = leftFiring;
            _rightFiring = rightFiring;
            ApplyLayout();
        }

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            _forceMobileLayout = System.Array.Exists(
                System.Environment.GetCommandLineArgs(),
                value => value == "--wof-mobile-ui");
            ApplyLayout();
        }

        private void LateUpdate()
        {
            var scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (screenSize != _lastScreenSize || !Mathf.Approximately(scaleFactor, _lastScaleFactor))
            {
                ApplyLayout();
            }
        }

        private void ApplyLayout()
        {
            var isFirstLayout = _lastScreenSize == Vector2Int.zero;
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            var scaleFactor = Mathf.Max(0.0001f, _canvas != null ? _canvas.scaleFactor : 1f);
            // React automatically selects Fill on a mobile-like device. Its
            // magic-hands safe frame is a centered 21:9 strip, while desktop
            // keeps the full configured game frame.
            var safeFrameHeightPixels = _forceMobileLayout
                ? Mathf.Min(Screen.height, Screen.width * 9f / 21f)
                : Screen.height;
            var safeFrameBottomPixels = _forceMobileLayout
                ? (Screen.height - safeFrameHeightPixels) * 0.5f
                : 0f;
            var layerHeightPixels = _forceMobileLayout
                ? Mathf.Min(safeFrameHeightPixels, Screen.width / FrameAspect)
                : safeFrameHeightPixels;
            // The hand frame is nested inside React's half-screen mask. Both
            // the mask and the frame add their own HUD clearance; the spell
            // layer is a sibling and only receives the frame clearance.
            const float maskHudAnchorPixels = 28f;
            const float frameHudAnchorPixels = 76f;
            var handFrameHeightPixels = layerHeightPixels + maskHudAnchorPixels + frameHudAnchorPixels;
            var spellFrameHeightPixels = layerHeightPixels + frameHudAnchorPixels;
            var handFrameWidthPixels = handFrameHeightPixels * FrameAspect;
            var spellFrameWidthPixels = spellFrameHeightPixels * FrameAspect;
            var bottomOverscanPixels = Mathf.Clamp(Screen.height * 0.02f, 4f, 14f);
            var maskBottomOverscanPixels = Mathf.Clamp(Screen.height * 0.004f, 1f, 3f);
            var fillNudge = _forceMobileLayout ? TouchFillNudge : 0f;
            var leftHandOffsetPixels = handFrameWidthPixels * ((_leftFiring ? FiringHandTranslate : IdleHandTranslate) - fillNudge);
            var rightHandOffsetPixels = handFrameWidthPixels * ((_rightFiring ? FiringHandTranslate : IdleHandTranslate) - fillNudge);
            var spellFrameOffsetPixels = spellFrameWidthPixels * (HeldSpellFrameTranslate - fillNudge);
            var spellTranslateXPixels = Mathf.Clamp(spellFrameWidthPixels * 0.059f, 88f, 126f);
            var spellTranslateYPixels = Mathf.Clamp(spellFrameHeightPixels * 0.187f, 158f, 216f);
            var frameScale = ResolveFrameScale(_forceMobileLayout, Screen.width, Screen.height);

            var handFrameSize = new Vector2(handFrameWidthPixels / scaleFactor, handFrameHeightPixels / scaleFactor);
            var spellFrameSize = new Vector2(spellFrameWidthPixels / scaleFactor, spellFrameHeightPixels / scaleFactor);
            var handBottom = (safeFrameBottomPixels - maskBottomOverscanPixels - bottomOverscanPixels) / scaleFactor;
            var spellBottom = (safeFrameBottomPixels - bottomOverscanPixels) / scaleFactor;
            ConfigureFrame(
                leftHandFrame,
                false,
                handFrameSize,
                new Vector2(-leftHandOffsetPixels / scaleFactor, handBottom),
                frameScale * (_leftFiring ? FiringPoseScale : 1f));
            ConfigureFrame(
                rightHandFrame,
                true,
                handFrameSize,
                new Vector2(rightHandOffsetPixels / scaleFactor, handBottom),
                frameScale * (_rightFiring ? FiringPoseScale : 1f));

            // The held effect is nested inside the scaled outer frame in React,
            // so its internal translation is scaled while the frame translation
            // remains in safe-frame pixels.
            var spellFrameOffset = spellFrameOffsetPixels / scaleFactor;
            var spellX = spellTranslateXPixels * frameScale / scaleFactor;
            var spellY = spellTranslateYPixels * frameScale / scaleFactor;
            ConfigureFrame(
                leftSpellFrame,
                false,
                spellFrameSize,
                _leftFiring ? new Vector2(-spellFrameOffset, spellBottom) : new Vector2(-spellFrameOffset + spellX, spellBottom - spellY),
                frameScale * HeldSpellScale);
            ConfigureFrame(
                rightSpellFrame,
                true,
                spellFrameSize,
                _rightFiring ? new Vector2(spellFrameOffset, spellBottom) : new Vector2(spellFrameOffset - spellX, spellBottom - spellY),
                frameScale * HeldSpellScale);

            if (isFirstLayout)
            {
                var parentRect = transform is RectTransform rectTransform ? rectTransform.rect.size : Vector2.zero;
                Debug.Log(
                    $"[WOF] MAGIC_HANDS_LAYOUT screen={Screen.width}x{Screen.height} canvasScale={scaleFactor:F4} " +
                    $"parent={parentRect.x:F1}x{parentRect.y:F1} mobileFill={_forceMobileLayout} " +
                    $"safeHeight={safeFrameHeightPixels:F1} safeBottom={safeFrameBottomPixels:F1} " +
                    $"handFrame={handFrameSize.x:F1}x{handFrameSize.y:F1} " +
                    $"spellFrame={spellFrameSize.x:F1}x{spellFrameSize.y:F1} frameScale={frameScale:F2} " +
                    $"left={leftHandFrame?.anchoredPosition} right={rightHandFrame?.anchoredPosition} " +
                    $"spellLeft={leftSpellFrame?.anchoredPosition} spellRight={rightSpellFrame?.anchoredPosition}");
            }

            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastScaleFactor = scaleFactor;
        }

        private static float ResolveFrameScale(bool mobileLayout, int width, int height)
        {
            if (!mobileLayout)
            {
                return 1f;
            }

            if (height > width)
            {
                return 0.86f;
            }

            if (height <= 340)
            {
                return 0.66f;
            }

            if (height <= 430)
            {
                return 0.72f;
            }

            return width <= 760 || height <= 460 ? 0.82f : 1f;
        }

        private static void ConfigureFrame(
            RectTransform frame,
            bool anchorRight,
            Vector2 size,
            Vector2 position,
            float scale)
        {
            if (frame == null)
            {
                return;
            }

            var anchor = anchorRight ? new Vector2(1f, 0f) : Vector2.zero;
            frame.anchorMin = anchor;
            frame.anchorMax = anchor;
            frame.pivot = anchorRight ? new Vector2(1f, 0f) : Vector2.zero;
            frame.sizeDelta = size;
            frame.anchoredPosition = position;
            frame.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
