using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofMovementMathTests
    {
        [Test]
        public void ReactMovementConstantsRemainExact()
        {
            Assert.That(WofGameConstants.WalkSpeed, Is.EqualTo(8f));
            Assert.That(WofMovementMath.SprintMultiplier, Is.EqualTo(1.6f));
            Assert.That(WofMovementMath.SlideSpeed, Is.EqualTo(18f));
            Assert.That(WofMovementMath.SlideDurationSeconds, Is.EqualTo(1f));
            Assert.That(WofMovementMath.SlideRestartCooldownSeconds, Is.EqualTo(0.25f));
            Assert.That(WofMovementMath.SlideStartMinSpeedSquared, Is.EqualTo(0.55f));
            Assert.That(WofMovementMath.CrouchHoldSeconds, Is.EqualTo(3f));
            Assert.That(WofMovementMath.CrouchSpeedMultiplier, Is.EqualTo(0.44f));
            Assert.That(WofMovementMath.VClipVerticalSpeed, Is.EqualTo(10f));
            Assert.That(WofMovementMath.VClipSprintMultiplier, Is.EqualTo(3.2f));
            Assert.That(
                WofMovementMath.UnityStandingCameraHeight - WofMovementMath.UnityLowCameraHeight,
                Is.EqualTo(0.56f).Within(0.0001f));
        }

        [Test]
        public void ResolveVClipVelocity_PreservesReactVerticalAndSprintTuning()
        {
            var normal = WofMovementMath.ResolveVClipVelocity(
                Vector2.up,
                0f,
                true,
                false,
                false,
                false,
                false);
            var sprint = WofMovementMath.ResolveVClipVelocity(
                Vector2.up,
                0f,
                true,
                false,
                true,
                false,
                false);

            Assert.That(normal.z, Is.EqualTo(8f).Within(0.001f));
            Assert.That(normal.y, Is.EqualTo(10f).Within(0.001f));
            Assert.That(sprint.z, Is.EqualTo(25.6f).Within(0.001f));
            Assert.That(sprint.y, Is.EqualTo(32f).Within(0.001f));
        }

        [Test]
        public void ResolveVClipVelocity_UsesSlideAsDescendAndCancelsOpposingVerticalInput()
        {
            var descend = WofMovementMath.ResolveVClipVelocity(
                Vector2.zero,
                0f,
                false,
                true,
                false,
                false,
                false);
            var cancelled = WofMovementMath.ResolveVClipVelocity(
                Vector2.zero,
                0f,
                true,
                true,
                false,
                false,
                false);

            Assert.That(descend, Is.EqualTo(Vector3.down * 10f));
            Assert.That(cancelled, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void SprintRequiresMovementAndUsesReactSpeed()
        {
            var state = default(WofMovementRuntimeState);
            var idle = Resolve(ref state, Vector2.zero, sprint: true, slide: false, now: 1f);
            var moving = Resolve(ref state, Vector2.up, sprint: true, slide: false, now: 1.02f);

            Assert.That(idle.IsSprinting, Is.False);
            Assert.That(idle.Speed, Is.EqualTo(8f));
            Assert.That(moving.IsSprinting, Is.True);
            Assert.That(moving.Speed, Is.EqualTo(12.8f).Within(0.0001f));
        }

        [Test]
        public void SlideUsesReactDurationSpeedReleaseAndRestartGate()
        {
            var state = default(WofMovementRuntimeState);
            var started = Resolve(ref state, Vector2.up, sprint: false, slide: true, now: 1f);
            var active = Resolve(ref state, Vector2.up, sprint: false, slide: true, now: 1.1f, delta: 0.1f);
            var released = Resolve(ref state, Vector2.up, sprint: false, slide: false, now: 1.2f, delta: 0.1f);
            var gated = Resolve(ref state, Vector2.up, sprint: false, slide: true, now: 1.22f);
            var restarted = Resolve(ref state, Vector2.up, sprint: false, slide: true, now: 1.26f);

            Assert.That(started.IsSliding, Is.True);
            Assert.That(started.Speed, Is.EqualTo(8f), "React applies slide speed on the frame after state starts.");
            Assert.That(active.Speed, Is.EqualTo(18f));
            Assert.That(released.IsSliding, Is.False);
            Assert.That(gated.IsSliding, Is.False);
            Assert.That(restarted.IsSliding, Is.True);
        }

        [Test]
        public void StationarySlideHoldBecomesCrouchAfterThreeSeconds()
        {
            var state = default(WofMovementRuntimeState);
            var started = Resolve(ref state, Vector2.zero, sprint: false, slide: true, now: 10f);
            var waiting = Resolve(ref state, Vector2.zero, sprint: false, slide: true, now: 12.999f);
            var activated = Resolve(ref state, Vector2.zero, sprint: false, slide: true, now: 13f);
            var crouchSpeed = Resolve(ref state, Vector2.up, sprint: false, slide: true, now: 13.02f);
            var released = Resolve(ref state, Vector2.zero, sprint: false, slide: false, now: 13.04f);

            Assert.That(started.IsCrouching, Is.False);
            Assert.That(waiting.IsCrouching, Is.False);
            Assert.That(activated.IsCrouching, Is.True);
            Assert.That(crouchSpeed.Speed, Is.EqualTo(3.52f).Within(0.0001f));
            Assert.That(released.IsCrouching, Is.False);
        }

        [Test]
        public void JumpThrusterMatchesReactFuelImpulseAndReleaseLockRules()
        {
            var state = default(WofMovementRuntimeState);
            WofMovementMath.Reset(ref state);
            var velocity = 0f;

            var jumped = WofMovementMath.ApplyJumpThruster(
                ref state,
                jumpHeld: true,
                grounded: true,
                effectiveGrounded: true,
                jumpBoostActive: false,
                ref velocity,
                deltaTime: 0.02f);
            Assert.That(jumped, Is.True);
            Assert.That(velocity, Is.EqualTo(8f));
            Assert.That(state.ThrusterFuel, Is.EqualTo(1f));

            WofMovementMath.ApplyJumpThruster(
                ref state,
                jumpHeld: true,
                grounded: false,
                effectiveGrounded: false,
                jumpBoostActive: false,
                ref velocity,
                deltaTime: 0.1f);
            Assert.That(velocity, Is.EqualTo(11.5f).Within(0.0001f));
            Assert.That(state.ThrusterFuel, Is.EqualTo(0.92f).Within(0.0001f));

            WofMovementMath.ApplyJumpThruster(
                ref state,
                jumpHeld: false,
                grounded: false,
                effectiveGrounded: false,
                jumpBoostActive: false,
                ref velocity,
                deltaTime: 0.1f);
            Assert.That(state.ThrusterLocked, Is.False);
        }

        [Test]
        public void JumpBoostDoublesReactJumpAndThrusterImpulse()
        {
            var state = default(WofMovementRuntimeState);
            WofMovementMath.Reset(ref state);
            var velocity = 0f;

            WofMovementMath.ApplyJumpThruster(
                ref state,
                jumpHeld: true,
                grounded: true,
                effectiveGrounded: true,
                jumpBoostActive: true,
                ref velocity,
                deltaTime: 0.02f);
            Assert.That(velocity, Is.EqualTo(16f));

            WofMovementMath.ApplyJumpThruster(
                ref state,
                jumpHeld: true,
                grounded: false,
                effectiveGrounded: false,
                jumpBoostActive: true,
                ref velocity,
                deltaTime: 0.1f);
            Assert.That(velocity, Is.EqualTo(23f).Within(0.0001f));
        }

        private static WofMovementFrame Resolve(
            ref WofMovementRuntimeState state,
            Vector2 move,
            bool sprint,
            bool slide,
            float now,
            float delta = 0.02f)
        {
            return WofMovementMath.ResolveFrame(
                ref state,
                move,
                sprint,
                slide,
                false,
                true,
                0f,
                0f,
                now,
                delta);
        }
    }
}
