using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofInputAndSafeAreaTests
    {
        [Test]
        public void ResolveMoveUsesStrongerSourceAndClampsDiagonals()
        {
            var keyboardWins = WofInputRouter.ResolveMove(new Vector2(1f, 1f), new Vector2(0.2f, 0.1f));
            var mobileWins = WofInputRouter.ResolveMove(new Vector2(0.1f, 0f), new Vector2(-0.8f, 0.2f));

            Assert.That(keyboardWins.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(keyboardWins.x, Is.EqualTo(keyboardWins.y).Within(0.0001f));
            Assert.That(mobileWins, Is.EqualTo(new Vector2(-0.8f, 0.2f)));
        }

        [Test]
        public void ResolveMoveIncludesNativeControllerAsAnIndependentSource()
        {
            var controllerWins = WofInputRouter.ResolveMove(
                new Vector2(0.1f, 0f),
                new Vector2(0.6f, 0.4f),
                new Vector2(0.2f, 0.2f));
            var keyboardWins = WofInputRouter.ResolveMove(
                new Vector2(1f, 1f),
                new Vector2(0.9f, 0f),
                Vector2.zero);

            Assert.That(controllerWins, Is.EqualTo(new Vector2(0.6f, 0.4f)));
            Assert.That(keyboardWins.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(keyboardWins.x, Is.EqualTo(keyboardWins.y).Within(0.0001f));
        }

        [Test]
        public void ControllerStickDeadzoneMatchesReactPerAxisRemapping()
        {
            Assert.That(WofInputRouter.ApplyControllerDeadzone(0.22f), Is.Zero);
            Assert.That(WofInputRouter.ApplyControllerDeadzone(-0.22f), Is.Zero);
            Assert.That(WofInputRouter.ApplyControllerDeadzone(1f), Is.EqualTo(1f));
            Assert.That(WofInputRouter.ApplyControllerDeadzone(-1f), Is.EqualTo(-1f));
            Assert.That(
                WofInputRouter.ApplyControllerDeadzone(0.61f),
                Is.EqualTo(0.5f).Within(0.0001f));
            var stick = WofInputRouter.ResolveControllerStick(new Vector2(0.21f, -0.61f));
            Assert.That(stick.x, Is.Zero);
            Assert.That(stick.y, Is.EqualTo(-0.5f).Within(0.0001f));
        }

        [Test]
        public void ControllerSprintLatchMatchesReactPressWhileMovingRule()
        {
            Assert.That(WofInputRouter.ResolveControllerSprintLatch(false, true, true), Is.True);
            Assert.That(WofInputRouter.ResolveControllerSprintLatch(true, false, true), Is.True);
            Assert.That(WofInputRouter.ResolveControllerSprintLatch(true, false, false), Is.False);
            Assert.That(WofInputRouter.ResolveControllerSprintLatch(false, true, false), Is.False);
        }

        [Test]
        public void ResolveCastBlocksOnlyMouseClicksOverUi()
        {
            Assert.That(WofInputRouter.ResolveCast(true, false, true, false, false), Is.False);
            Assert.That(WofInputRouter.ResolveCast(false, true, true, false, false), Is.False);
            Assert.That(WofInputRouter.ResolveCast(true, false, false, false, false), Is.True);
            Assert.That(WofInputRouter.ResolveCast(false, true, false, false, false), Is.True);
            Assert.That(WofInputRouter.ResolveCast(false, false, true, true, false), Is.True);
            Assert.That(WofInputRouter.ResolveCast(false, false, true, false, true), Is.True);
        }

        [Test]
        public void ResolveCastHandMatchesReactMouseAndMobileHandMapping()
        {
            Assert.That(
                WofInputRouter.ResolveCastHand(true, false, false, false, false, out var leftMouseHand),
                Is.True);
            Assert.That(leftMouseHand, Is.EqualTo(WofHandSide.Left));

            Assert.That(
                WofInputRouter.ResolveCastHand(false, true, false, false, false, out var rightMouseHand),
                Is.True);
            Assert.That(rightMouseHand, Is.EqualTo(WofHandSide.Right));

            Assert.That(
                WofInputRouter.ResolveCastHand(false, false, false, true, false, out var mobileLeftHand),
                Is.True);
            Assert.That(mobileLeftHand, Is.EqualTo(WofHandSide.Left));

            Assert.That(
                WofInputRouter.ResolveCastHand(false, false, false, false, true, out var mobileRightHand),
                Is.True);
            Assert.That(mobileRightHand, Is.EqualTo(WofHandSide.Right));

            Assert.That(
                WofInputRouter.ResolveCastHand(true, false, true, false, false, out _),
                Is.False);
            Assert.That(
                WofInputRouter.ResolveCastHand(false, true, true, false, false, out _),
                Is.False);
        }

        [Test]
        public void ResolveCastHandMatchesReactNativeControllerTriggers()
        {
            Assert.That(
                WofInputRouter.ResolveCastHand(
                    false, false, false,
                    true, false,
                    false, false,
                    out var leftTriggerHand),
                Is.True);
            Assert.That(leftTriggerHand, Is.EqualTo(WofHandSide.Left));

            Assert.That(
                WofInputRouter.ResolveCastHand(
                    false, false, false,
                    false, true,
                    false, false,
                    out var rightTriggerHand),
                Is.True);
            Assert.That(rightTriggerHand, Is.EqualTo(WofHandSide.Right));
        }

        [Test]
        public void FiringPoseRemainsThroughReactReleaseWindowThenReturnsToIdle()
        {
            const float releasedAt = 10f;
            const float firingUntil = releasedAt + 0.14f;

            Assert.That(WofHud.ResolveFiringPoseActive(false, releasedAt, firingUntil), Is.True);
            Assert.That(WofHud.ResolveFiringPoseActive(false, firingUntil - 0.001f, firingUntil), Is.True);
            Assert.That(WofHud.ResolveFiringPoseActive(false, firingUntil, firingUntil), Is.False);
            Assert.That(WofHud.ResolveFiringPoseActive(true, firingUntil + 10f, firingUntil), Is.True);
        }

        [Test]
        public void EquippedFiringHandUsesRestrainedFlexThenReturnsToPointingFrame()
        {
            const float startedAt = 20f;
            const float firingUntil = startedAt + 0.14f;

            Assert.That(WofHud.ResolveFiringFlexFrame(false, startedAt, startedAt, firingUntil, 4), Is.EqualTo(0));
            Assert.That(WofHud.ResolveFiringFlexFrame(false, startedAt + 0.04f, startedAt, firingUntil, 4), Is.EqualTo(1));
            Assert.That(WofHud.ResolveFiringFlexFrame(false, startedAt + 0.08f, startedAt, firingUntil, 4), Is.EqualTo(2));
            Assert.That(WofHud.ResolveFiringFlexFrame(false, startedAt + 0.12f, startedAt, firingUntil, 4), Is.EqualTo(1));
            Assert.That(WofHud.ResolveFiringFlexFrame(false, firingUntil, startedAt, firingUntil, 4), Is.EqualTo(0));
            Assert.That(WofHud.ResolveFiringFlexFrame(true, firingUntil + 5f, startedAt, firingUntil, 4), Is.EqualTo(2));
        }

        [Test]
        public void EquippedOutwardPointingHandsKeepTheReactFourFrameIdleLoop()
        {
            Assert.That(WofHud.ResolveEquippedHandFrame(true, false, 0, 3), Is.EqualTo(0));
            Assert.That(WofHud.ResolveEquippedHandFrame(true, false, 1, 3), Is.EqualTo(1));
            Assert.That(WofHud.ResolveEquippedHandFrame(true, false, 2, 3), Is.EqualTo(2));
            Assert.That(WofHud.ResolveEquippedHandFrame(true, false, 3, 2), Is.EqualTo(3));
            Assert.That(WofHud.ResolveEquippedHandFrame(true, true, 3, 2), Is.EqualTo(2));
            Assert.That(WofHud.ResolveEquippedHandFrame(false, false, 2, 3), Is.EqualTo(2));
        }

        [Test]
        public void ScoreboardStatusMatchesReactCombinedEffectOrder()
        {
            Assert.That(WofPauseAndScoreboardRuntime.BuildScoreboardStatus(0f, true, true, true, true), Is.EqualTo("DOWN"));
            Assert.That(WofPauseAndScoreboardRuntime.BuildScoreboardStatus(100f, false, false, false, false), Is.EqualTo("READY"));
            Assert.That(WofPauseAndScoreboardRuntime.BuildScoreboardStatus(100f, true, true, true, true),
                Is.EqualTo("SLEEP / SLOWED / POISON / ACID"));
            Assert.That(WofPauseAndScoreboardRuntime.BuildScoreboardStatus(100f, false, true, false, true),
                Is.EqualTo("SLOWED / ACID"));
        }

        [Test]
        public void ResolveLookPreservesLegacyMouseScaleWithoutScalingTouchLook()
        {
            var look = WofInputRouter.ResolveLook(new Vector2(20f, -10f), new Vector2(0.5f, 0.25f));

            Assert.That(look, Is.EqualTo(new Vector2(2.5f, -0.75f)));
        }

        [Test]
        public void ControllerLookMatchesReactSensitivityAndVerticalMultiplier()
        {
            const float deltaSeconds = 0.5f;
            Assert.That(WofInputRouter.ControllerLookVerticalMultiplier, Is.EqualTo(0.78f));

            var routerLook = WofInputRouter.ResolveControllerLook(new Vector2(1f, -1f), deltaSeconds);
            var appliedDegrees = routerLook * WofGameConstants.MouseSensitivity;
            var expectedYaw = WofInputRouter.ControllerLookSensitivityRadiansPerSecond * Mathf.Rad2Deg * deltaSeconds;

            Assert.That(appliedDegrees.x, Is.EqualTo(expectedYaw).Within(0.0001f));
            Assert.That(
                appliedDegrees.y,
                Is.EqualTo(-expectedYaw * WofInputRouter.ControllerLookVerticalMultiplier).Within(0.0001f));
            Assert.That(WofInputRouter.ResolveControllerLook(Vector2.one, float.NaN), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ResolveJoystickValueNormalizesAndRejectsInvalidRadius()
        {
            var clamped = WofInputRouter.ResolveJoystickValue(new Vector2(80f, 0f), 40f);

            Assert.That(clamped, Is.EqualTo(Vector2.right));
            Assert.That(WofInputRouter.ResolveJoystickValue(Vector2.one, 0f), Is.EqualTo(Vector2.zero));
            Assert.That(WofInputRouter.ResolveJoystickValue(Vector2.one, float.NaN), Is.EqualTo(Vector2.zero));
        }

        [TestCase(false, false, false, false)]
        [TestCase(true, false, false, true)]
        [TestCase(false, true, false, true)]
        [TestCase(true, false, true, false)]
        [TestCase(false, true, true, false)]
        public void TouchControlVisibilityRequiresTouchAndNoConnectedController(
            bool isTouchGameplayDevice,
            bool forced,
            bool hasConnectedController,
            bool expected)
        {
            Assert.That(
                WofInputRouter.ShouldShowTouchControls(
                    isTouchGameplayDevice,
                    forced,
                    hasConnectedController),
                Is.EqualTo(expected));
        }

        [Test]
        public void SafeAreaAnchorsNormalizeLandscapeInsets()
        {
            var anchors = WofSafeAreaFitter.CalculateNormalizedAnchors(
                new Rect(100f, 40f, 2200f, 1000f),
                new Vector2(2400f, 1080f));

            Assert.That(anchors.xMin, Is.EqualTo(100f / 2400f).Within(0.0001f));
            Assert.That(anchors.xMax, Is.EqualTo(2300f / 2400f).Within(0.0001f));
            Assert.That(anchors.yMin, Is.EqualTo(40f / 1080f).Within(0.0001f));
            Assert.That(anchors.yMax, Is.EqualTo(1040f / 1080f).Within(0.0001f));
        }

        [Test]
        public void SafeAreaAnchorsClampToScreenAndFallbackForInvalidDimensions()
        {
            var clamped = WofSafeAreaFitter.CalculateNormalizedAnchors(
                new Rect(-20f, -10f, 1100f, 550f),
                new Vector2(1000f, 500f));
            var fallback = WofSafeAreaFitter.CalculateNormalizedAnchors(
                new Rect(10f, 10f, 100f, 100f),
                Vector2.zero);

            Assert.That(clamped, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(fallback, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
        }
    }
}
