namespace WOF
{
    internal enum WofAstralMeditationTransition
    {
        None,
        Entered,
        Exited
    }

    internal struct WofAstralMeditationState
    {
        public bool IsActive;
        public bool ExitArmed;
        public double ExitHoldStartedAt;
    }

    internal readonly struct WofAstralPresentationFrame
    {
        public WofAstralPresentationFrame(
            bool active,
            float skyStrength,
            float veilStrength,
            float blinkStrength,
            float veilAlpha,
            float blinkAlpha,
            float veilRotationRadians)
        {
            Active = active;
            SkyStrength = skyStrength;
            VeilStrength = veilStrength;
            BlinkStrength = blinkStrength;
            VeilAlpha = veilAlpha;
            BlinkAlpha = blinkAlpha;
            VeilRotationRadians = veilRotationRadians;
        }

        public bool Active { get; }
        public float SkyStrength { get; }
        public float VeilStrength { get; }
        public float BlinkStrength { get; }
        public float VeilAlpha { get; }
        public float BlinkAlpha { get; }
        public float VeilRotationRadians { get; }
    }

    /// <summary>
    /// Exact state machine used by the React player meditation runtime: Ctrl enters,
    /// releasing every Ctrl key arms exit, and the next uninterrupted Ctrl hold exits.
    /// </summary>
    internal static class WofAstralMeditationRules
    {
        internal const double ExitHoldSeconds = 5d;
        internal const double NoExitHold = -1d;
        internal const float ReactCameraHeight = 0.58f;
        internal const float CameraLerpAlpha = 0.18f;
        internal const float AvatarFrameDelaySeconds = 0.52f;
        internal const float SkyBlendSeconds = 1.3f;
        internal const float VeilFadeStartSeconds = 0.15f;
        internal const float VeilFadeEndSeconds = 1.35f;
        internal const float BlinkFadeStartSeconds = 0.22f;
        internal const float BlinkFadeEndSeconds = 1.15f;
        internal const float VeilDistance = 1.15f;

        internal static WofAstralMeditationTransition HandleControlPressed(
            ref WofAstralMeditationState state,
            double now,
            bool gameplayAllowed)
        {
            if (!gameplayAllowed)
            {
                return WofAstralMeditationTransition.None;
            }

            if (!state.IsActive)
            {
                state.IsActive = true;
                state.ExitArmed = false;
                state.ExitHoldStartedAt = NoExitHold;
                return WofAstralMeditationTransition.Entered;
            }

            if (state.ExitArmed && state.ExitHoldStartedAt < 0d)
            {
                state.ExitHoldStartedAt = now;
            }
            return WofAstralMeditationTransition.None;
        }

        internal static void HandleControlReleased(
            ref WofAstralMeditationState state,
            bool anyControlHeld)
        {
            if (anyControlHeld)
            {
                return;
            }

            state.ExitHoldStartedAt = NoExitHold;
            if (state.IsActive)
            {
                state.ExitArmed = true;
            }
        }

        internal static WofAstralMeditationTransition UpdateExitHold(
            ref WofAstralMeditationState state,
            double now)
        {
            if (!state.IsActive || state.ExitHoldStartedAt < 0d ||
                now - state.ExitHoldStartedAt < ExitHoldSeconds)
            {
                return WofAstralMeditationTransition.None;
            }

            SetAuthoritativeActive(ref state, false);
            return WofAstralMeditationTransition.Exited;
        }

        internal static void SetAuthoritativeActive(
            ref WofAstralMeditationState state,
            bool active)
        {
            state.IsActive = active;
            state.ExitArmed = false;
            state.ExitHoldStartedAt = NoExitHold;
        }

        internal static WofAstralPresentationFrame EvaluatePresentation(
            bool active,
            float secondsSinceStart,
            float animationElapsedSeconds)
        {
            if (!active)
            {
                return default;
            }

            var elapsed = UnityEngine.Mathf.Max(0f, secondsSinceStart);
            var skyStrength = SmoothstepRange(0f, SkyBlendSeconds, elapsed);
            var veilStrength = SmoothstepRange(VeilFadeStartSeconds, VeilFadeEndSeconds, elapsed);
            var blinkStrength = 1f - SmoothstepRange(BlinkFadeStartSeconds, BlinkFadeEndSeconds, elapsed);
            return new WofAstralPresentationFrame(
                true,
                skyStrength,
                veilStrength,
                blinkStrength,
                veilStrength * (0.28f + UnityEngine.Mathf.Sin(animationElapsedSeconds * 0.7f) * 0.035f),
                blinkStrength * 0.82f,
                animationElapsedSeconds * 0.025f);
        }

        private static float SmoothstepRange(float edge0, float edge1, float value)
        {
            var t = UnityEngine.Mathf.Clamp01(
                (value - edge0) / UnityEngine.Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
