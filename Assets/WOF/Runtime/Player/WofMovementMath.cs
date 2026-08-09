using UnityEngine;

namespace WOF
{
    internal struct WofMovementRuntimeState
    {
        public bool Initialized;
        public bool IsSliding;
        public bool IsCrouching;
        public float SlideSecondsRemaining;
        public float LastSlideAt;
        public float CrouchHoldStartedAt;
    }

    internal readonly struct WofMovementFrame
    {
        public WofMovementFrame(float speed, bool isSprinting, bool isSliding, bool isCrouching)
        {
            Speed = speed;
            IsSprinting = isSprinting;
            IsSliding = isSliding;
            IsCrouching = isCrouching;
        }

        public float Speed { get; }
        public bool IsSprinting { get; }
        public bool IsSliding { get; }
        public bool IsCrouching { get; }
    }

    internal static class WofMovementMath
    {
        internal const float SprintMultiplier = 1.6f;
        internal const float SlideSpeed = 18f;
        internal const float SlideDurationSeconds = 1f;
        internal const float SlideRestartCooldownSeconds = 0.25f;
        internal const float SlideStartMinSpeedSquared = 0.55f;
        internal const float CrouchHoldSeconds = 3f;
        internal const float CrouchSpeedMultiplier = 0.44f;
        internal const float ReactStandingCameraHeight = 1.08f;
        internal const float ReactLowCameraHeight = 0.52f;
        internal const float UnityStandingCameraHeight = 1.65f;
        internal const float UnityLowCameraHeight =
            UnityStandingCameraHeight - (ReactStandingCameraHeight - ReactLowCameraHeight);

        internal static WofMovementFrame ResolveFrame(
            ref WofMovementRuntimeState state,
            Vector2 move,
            bool sprintRequested,
            bool slideInputHeld,
            bool jumpHeld,
            bool effectiveGrounded,
            float verticalVelocity,
            float planarVelocitySquared,
            float now,
            float deltaTime)
        {
            EnsureInitialized(ref state);

            if (!IsFinite(move) || !IsFinite(verticalVelocity) || !IsFinite(planarVelocitySquared) ||
                !IsFinite(now) || !IsFinite(deltaTime) || deltaTime < 0f)
            {
                return new WofMovementFrame(
                    WofGameConstants.WalkSpeed,
                    false,
                    state.IsSliding,
                    state.IsCrouching);
            }

            var hadSlidingState = state.IsSliding;
            var hadCrouchingState = state.IsCrouching;
            var hasPlanarMovementInput = move.sqrMagnitude > 0f;
            var isSprinting = hasPlanarMovementInput && sprintRequested && !hadSlidingState && !hadCrouchingState;
            var resolvedSlideInputHeld = slideInputHeld && !hadCrouchingState;
            var slideHeld = resolvedSlideInputHeld &&
                            (hadSlidingState || isSprinting || hasPlanarMovementInput);
            var speed = hadSlidingState
                ? SlideSpeed
                : hadCrouchingState
                    ? WofGameConstants.WalkSpeed * CrouchSpeedMultiplier
                    : isSprinting
                        ? WofGameConstants.WalkSpeed * SprintMultiplier
                        : WofGameConstants.WalkSpeed;

            var crouchAllowed = slideInputHeld &&
                                effectiveGrounded &&
                                !jumpHeld &&
                                !hadSlidingState &&
                                !isSprinting &&
                                Mathf.Abs(verticalVelocity) < 0.35f;
            UpdateCrouch(ref state, crouchAllowed, now);

            if (effectiveGrounded && slideHeld && !hadSlidingState &&
                (hasPlanarMovementInput || planarVelocitySquared > SlideStartMinSpeedSquared) &&
                now - state.LastSlideAt >= SlideRestartCooldownSeconds)
            {
                state.IsSliding = true;
                state.SlideSecondsRemaining = SlideDurationSeconds;
                state.LastSlideAt = now;
            }

            if (hadSlidingState)
            {
                state.SlideSecondsRemaining -= deltaTime;
                if (state.SlideSecondsRemaining <= 0f || !slideHeld)
                {
                    state.IsSliding = false;
                    state.SlideSecondsRemaining = 0f;
                }
            }

            return new WofMovementFrame(speed, isSprinting, state.IsSliding, state.IsCrouching);
        }

        internal static float ResolveCameraHeight(bool isSliding, bool isCrouching)
        {
            return isSliding || isCrouching ? UnityLowCameraHeight : UnityStandingCameraHeight;
        }

        internal static void Reset(ref WofMovementRuntimeState state)
        {
            state = new WofMovementRuntimeState
            {
                Initialized = true,
                CrouchHoldStartedAt = float.NegativeInfinity
            };
        }

        private static void EnsureInitialized(ref WofMovementRuntimeState state)
        {
            if (!state.Initialized)
            {
                Reset(ref state);
            }
        }

        private static void UpdateCrouch(ref WofMovementRuntimeState state, bool crouchAllowed, float now)
        {
            if (!crouchAllowed)
            {
                state.CrouchHoldStartedAt = float.NegativeInfinity;
                state.IsCrouching = false;
                return;
            }

            if (float.IsNegativeInfinity(state.CrouchHoldStartedAt))
            {
                state.CrouchHoldStartedAt = now;
                return;
            }

            if (!state.IsCrouching && now - state.CrouchHoldStartedAt >= CrouchHoldSeconds)
            {
                state.IsCrouching = true;
            }
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
