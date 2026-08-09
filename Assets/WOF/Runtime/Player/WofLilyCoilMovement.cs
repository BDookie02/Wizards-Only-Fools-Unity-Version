using UnityEngine;

namespace WOF
{
    internal struct WofLilyCoilMovementState
    {
        public bool Active;
        public float T;
        public float SurfaceAngle;
        public float JumpOffset;
        public float JumpVelocity;
        public float ThrusterFuel;
        public bool ThrusterLocked;
        public bool WasJumpHeld;
        public bool IsSliding;
        public float SlideSecondsRemaining;
        public float LastSlideAt;
    }

    internal readonly struct WofLilyCoilMovementFrame
    {
        public WofLilyCoilMovementFrame(
            Vector3 position,
            Quaternion bodyRotation,
            Quaternion cameraLocalRotation,
            Vector3 viewForward,
            Vector3 playerUp,
            bool isGrounded,
            bool isMoving,
            bool isSprinting,
            bool isSliding,
            float pathInput,
            float surfaceInput,
            float moveSpeed)
        {
            Position = position;
            BodyRotation = bodyRotation;
            CameraLocalRotation = cameraLocalRotation;
            ViewForward = viewForward;
            PlayerUp = playerUp;
            IsGrounded = isGrounded;
            IsMoving = isMoving;
            IsSprinting = isSprinting;
            IsSliding = isSliding;
            PathInput = pathInput;
            SurfaceInput = surfaceInput;
            MoveSpeed = moveSpeed;
        }

        public Vector3 Position { get; }
        public Quaternion BodyRotation { get; }
        public Quaternion CameraLocalRotation { get; }
        public Vector3 ViewForward { get; }
        public Vector3 PlayerUp { get; }
        public bool IsGrounded { get; }
        public bool IsMoving { get; }
        public bool IsSprinting { get; }
        public bool IsSliding { get; }
        public float PathInput { get; }
        public float SurfaceInput { get; }
        public float MoveSpeed { get; }
    }

    internal static class WofLilyCoilMovement
    {
        internal static void Reset(ref WofLilyCoilMovementState state)
        {
            state = new WofLilyCoilMovementState
            {
                ThrusterFuel = 1f,
                LastSlideAt = float.NegativeInfinity
            };
        }

        internal static void Enter(ref WofLilyCoilMovementState state, Vector3 position)
        {
            var nearest = WofLilyCoilLayout.GetNearestState(position);
            state.Active = true;
            state.T = nearest.T;
            state.SurfaceAngle = nearest.SurfaceAngle;
            state.JumpOffset = 0f;
            state.JumpVelocity = 0f;
            state.ThrusterFuel = 1f;
            state.ThrusterLocked = false;
            state.WasJumpHeld = false;
            state.IsSliding = false;
            state.SlideSecondsRemaining = 0f;
            state.LastSlideAt = float.NegativeInfinity;
        }

        internal static WofLilyCoilMovementFrame Simulate(
            ref WofLilyCoilMovementState state,
            Vector2 move,
            float viewYaw,
            float viewPitch,
            bool sprintRequested,
            bool slideHeld,
            bool jumpHeld,
            float now,
            float deltaTime)
        {
            if (!state.Active)
            {
                throw new System.InvalidOperationException("Lily Coil movement must be entered before simulation.");
            }

            move = IsFinite(move) ? Vector2.ClampMagnitude(move, 1f) : Vector2.zero;
            viewYaw = IsFinite(viewYaw) ? viewYaw : 0f;
            viewPitch = IsFinite(viewPitch) ? Mathf.Clamp(viewPitch, -82f, 82f) : 0f;
            now = IsFinite(now) ? now : 0f;
            deltaTime = IsFinite(deltaTime) && deltaTime > 0f ? deltaTime : 0f;

            var currentFrame = WofLilyCoilLayout.GetFrame(state.T);
            var currentRadial = WofLilyCoilLayout.GetRadial(currentFrame, state.SurfaceAngle);
            var currentPlayerUp = -currentRadial;
            var currentAround = WofLilyCoilLayout.GetAroundSurface(currentFrame, state.SurfaceAngle);
            var baseRotation = Quaternion.LookRotation(currentFrame.Tangent, currentPlayerUp);
            var cameraLocalRotation = Quaternion.Euler(viewPitch, viewYaw, 0f);
            var cameraRotation = baseRotation * cameraLocalRotation;
            var cameraForward = cameraRotation * Vector3.forward;
            var cameraRight = Vector3.Cross(cameraForward, currentPlayerUp);
            if (cameraRight.sqrMagnitude < 0.0001f) cameraRight = currentAround;
            cameraRight.Normalize();

            var surfaceForward = Vector3.ProjectOnPlane(cameraForward, currentPlayerUp);
            if (surfaceForward.sqrMagnitude < 0.0001f) surfaceForward = currentFrame.Tangent;
            surfaceForward.Normalize();
            var surfaceRight = Vector3.ProjectOnPlane(cameraRight, currentPlayerUp);
            if (surfaceRight.sqrMagnitude < 0.0001f) surfaceRight = currentAround;
            surfaceRight.Normalize();

            var moveDirection = surfaceForward * move.y + surfaceRight * move.x;
            if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();
            var pathInput = Mathf.Clamp(Vector3.Dot(moveDirection, currentFrame.Tangent), -1f, 1f);
            var surfaceInput = Mathf.Clamp(Vector3.Dot(moveDirection, currentAround), -1f, 1f);
            var hasPlanarInput = move.sqrMagnitude > 0f;

            var groundedBeforeMove = state.JumpOffset <= 0.025f && state.JumpVelocity <= 0f;
            var tubeSlideHeld = slideHeld && hasPlanarInput;
            if (groundedBeforeMove && tubeSlideHeld && !state.IsSliding &&
                now - state.LastSlideAt >= WofMovementMath.SlideRestartCooldownSeconds)
            {
                state.IsSliding = true;
                state.SlideSecondsRemaining = WofMovementMath.SlideDurationSeconds;
                state.LastSlideAt = now;
            }
            if (state.IsSliding)
            {
                state.SlideSecondsRemaining -= deltaTime;
                if (state.SlideSecondsRemaining <= 0f || !tubeSlideHeld)
                {
                    state.IsSliding = false;
                    state.SlideSecondsRemaining = 0f;
                }
            }

            var sprinting = hasPlanarInput && sprintRequested && !state.IsSliding;
            var currentSpeed = sprinting
                ? WofGameConstants.WalkSpeed * WofMovementMath.SprintMultiplier
                : WofGameConstants.WalkSpeed;
            var tubeMoveSpeed = (state.IsSliding ? WofMovementMath.SlideSpeed : currentSpeed) *
                                WofLilyCoilLayout.TubeMovementMultiplier;
            state.T = Mathf.Clamp01(state.T + pathInput * tubeMoveSpeed * deltaTime /
                WofLilyCoilLayout.TubePathLength);
            state.SurfaceAngle = WofLilyCoilLayout.NormalizeAngle(
                state.SurfaceAngle + surfaceInput *
                (tubeMoveSpeed / Mathf.Max(8f, WofLilyCoilLayout.TubePlayerRadius)) * deltaTime);

            var jumpRequested = jumpHeld && !state.WasJumpHeld;
            if (!jumpHeld) state.ThrusterLocked = false;
            if (jumpRequested && groundedBeforeMove)
            {
                state.JumpVelocity = WofLilyCoilLayout.TubeJumpForce;
                state.ThrusterLocked = false;
            }
            else if (jumpHeld && !groundedBeforeMove && state.ThrusterFuel > 0f && !state.ThrusterLocked)
            {
                state.JumpVelocity += WofLilyCoilLayout.TubeThrusterImpulsePerSecond * deltaTime;
                state.ThrusterFuel = Mathf.Max(
                    0f,
                    state.ThrusterFuel - deltaTime * WofLilyCoilLayout.TubeThrusterFuelDrainPerSecond);
                if (state.ThrusterFuel <= 0f) state.ThrusterLocked = true;
            }

            if (state.JumpOffset > 0f || state.JumpVelocity > 0f)
            {
                state.JumpVelocity -= WofLilyCoilLayout.TubeJumpGravity * deltaTime;
                state.JumpOffset = Mathf.Clamp(
                    state.JumpOffset + state.JumpVelocity * deltaTime,
                    0f,
                    WofLilyCoilLayout.TubeMaxJumpOffset);
                if (state.JumpOffset <= 0f)
                {
                    state.JumpOffset = 0f;
                    state.JumpVelocity = 0f;
                }
            }
            if (groundedBeforeMove && state.ThrusterFuel < 1f)
            {
                state.ThrusterFuel = Mathf.Min(
                    1f,
                    state.ThrusterFuel + deltaTime * WofLilyCoilLayout.TubeThrusterFuelRechargePerSecond);
            }
            state.WasJumpHeld = jumpHeld;

            var frame = WofLilyCoilLayout.GetFrame(state.T);
            var radial = WofLilyCoilLayout.GetRadial(frame, state.SurfaceAngle);
            var playerUp = -radial;
            var bodyRotation = Quaternion.LookRotation(frame.Tangent, playerUp);
            cameraLocalRotation = Quaternion.Euler(viewPitch, viewYaw, 0f);
            cameraRotation = bodyRotation * cameraLocalRotation;
            var viewForward = cameraRotation * Vector3.forward;
            var position = frame.Center + radial * WofLilyCoilLayout.TubePlayerRadius +
                           playerUp * state.JumpOffset;
            var airborne = state.JumpOffset > 0.025f;
            var moving = hasPlanarInput || Mathf.Abs(surfaceInput) > 0.05f || Mathf.Abs(move.y) > 0.05f;

            return new WofLilyCoilMovementFrame(
                position,
                bodyRotation,
                cameraLocalRotation,
                viewForward,
                playerUp,
                !airborne,
                moving,
                sprinting,
                state.IsSliding,
                pathInput,
                surfaceInput,
                tubeMoveSpeed);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }
    }
}
