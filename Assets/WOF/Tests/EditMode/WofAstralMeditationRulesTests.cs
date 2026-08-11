using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofAstralMeditationRulesTests
    {
        [Test]
        public void ReactMeditationConstantsRemainExact()
        {
            Assert.That(WofAstralMeditationRules.ExitHoldSeconds, Is.EqualTo(5d));
            Assert.That(WofAstralMeditationRules.ReactCameraHeight, Is.EqualTo(0.58f));
            Assert.That(WofAstralMeditationRules.CameraLerpAlpha, Is.EqualTo(0.18f));
            Assert.That(WofAstralMeditationRules.AvatarFrameDelaySeconds, Is.EqualTo(0.52f));
            Assert.That(WofAstralMeditationRules.SkyBlendSeconds, Is.EqualTo(1.3f));
            Assert.That(WofAstralMeditationRules.VeilFadeStartSeconds, Is.EqualTo(0.15f));
            Assert.That(WofAstralMeditationRules.VeilFadeEndSeconds, Is.EqualTo(1.35f));
            Assert.That(WofAstralMeditationRules.BlinkFadeStartSeconds, Is.EqualTo(0.22f));
            Assert.That(WofAstralMeditationRules.BlinkFadeEndSeconds, Is.EqualTo(1.15f));
            Assert.That(WofAstralMeditationRules.VeilDistance, Is.EqualTo(1.15f));
            Assert.That(WofMovementMath.UnityMeditationCameraHeight, Is.EqualTo(1.15f).Within(0.0001f));
        }

        [Test]
        public void AstralRealmTransitionMatchesReactFadeTiming()
        {
            var entered = WofAstralMeditationRules.EvaluatePresentation(true, 0f, 0f);
            Assert.That(entered.Active, Is.True);
            Assert.That(entered.SkyStrength, Is.Zero);
            Assert.That(entered.VeilStrength, Is.Zero);
            Assert.That(entered.BlinkStrength, Is.EqualTo(1f));
            Assert.That(entered.BlinkAlpha, Is.EqualTo(0.82f).Within(0.0001f));

            var settled = WofAstralMeditationRules.EvaluatePresentation(true, 1.35f, 4f);
            Assert.That(settled.SkyStrength, Is.EqualTo(1f));
            Assert.That(settled.VeilStrength, Is.EqualTo(1f));
            Assert.That(settled.BlinkStrength, Is.Zero);
            Assert.That(settled.VeilAlpha, Is.InRange(0.245f, 0.315f));
            Assert.That(settled.VeilRotationRadians, Is.EqualTo(0.1f).Within(0.0001f));

            var inactive = WofAstralMeditationRules.EvaluatePresentation(false, 20f, 20f);
            Assert.That(inactive.Active, Is.False);
            Assert.That(inactive.SkyStrength, Is.Zero);
            Assert.That(inactive.VeilAlpha, Is.Zero);
            Assert.That(inactive.BlinkAlpha, Is.Zero);
        }

        [Test]
        public void FirstControlPressEntersAndDoesNotArmExit()
        {
            var state = default(WofAstralMeditationState);

            var transition = WofAstralMeditationRules.HandleControlPressed(ref state, 10d, true);

            Assert.That(transition, Is.EqualTo(WofAstralMeditationTransition.Entered));
            Assert.That(state.IsActive, Is.True);
            Assert.That(state.ExitArmed, Is.False);
            Assert.That(state.ExitHoldStartedAt, Is.EqualTo(WofAstralMeditationRules.NoExitHold));
        }

        [Test]
        public void GameplayBlockRejectsEntry()
        {
            var state = default(WofAstralMeditationState);

            var transition = WofAstralMeditationRules.HandleControlPressed(ref state, 10d, false);

            Assert.That(transition, Is.EqualTo(WofAstralMeditationTransition.None));
            Assert.That(state.IsActive, Is.False);
        }

        [Test]
        public void GameplayBlockDoesNotStartAnArmedExitHold()
        {
            var state = EnterAndArm();

            var transition = WofAstralMeditationRules.HandleControlPressed(ref state, 4d, false);

            Assert.That(transition, Is.EqualTo(WofAstralMeditationTransition.None));
            Assert.That(state.IsActive, Is.True);
            Assert.That(state.ExitArmed, Is.True);
            Assert.That(state.ExitHoldStartedAt, Is.EqualTo(WofAstralMeditationRules.NoExitHold));
        }

        [Test]
        public void ExitOnlyArmsAfterEveryControlKeyIsReleased()
        {
            var state = default(WofAstralMeditationState);
            WofAstralMeditationRules.HandleControlPressed(ref state, 1d, true);

            WofAstralMeditationRules.HandleControlReleased(ref state, true);
            Assert.That(state.ExitArmed, Is.False);

            WofAstralMeditationRules.HandleControlReleased(ref state, false);
            Assert.That(state.ExitArmed, Is.True);
            Assert.That(state.ExitHoldStartedAt, Is.EqualTo(WofAstralMeditationRules.NoExitHold));
        }

        [Test]
        public void ShortExitHoldCancelsWithoutLeavingMeditation()
        {
            var state = EnterAndArm();
            WofAstralMeditationRules.HandleControlPressed(ref state, 4d, true);

            var transition = WofAstralMeditationRules.UpdateExitHold(ref state, 8.999d);
            WofAstralMeditationRules.HandleControlReleased(ref state, false);

            Assert.That(transition, Is.EqualTo(WofAstralMeditationTransition.None));
            Assert.That(state.IsActive, Is.True);
            Assert.That(state.ExitArmed, Is.True);
            Assert.That(state.ExitHoldStartedAt, Is.EqualTo(WofAstralMeditationRules.NoExitHold));
        }

        [Test]
        public void UninterruptedFiveSecondExitHoldLeavesMeditation()
        {
            var state = EnterAndArm();
            WofAstralMeditationRules.HandleControlPressed(ref state, 4d, true);

            var transition = WofAstralMeditationRules.UpdateExitHold(ref state, 9d);

            Assert.That(transition, Is.EqualTo(WofAstralMeditationTransition.Exited));
            Assert.That(state.IsActive, Is.False);
            Assert.That(state.ExitArmed, Is.False);
            Assert.That(state.ExitHoldStartedAt, Is.EqualTo(WofAstralMeditationRules.NoExitHold));
        }

        private static WofAstralMeditationState EnterAndArm()
        {
            var state = default(WofAstralMeditationState);
            WofAstralMeditationRules.HandleControlPressed(ref state, 1d, true);
            WofAstralMeditationRules.HandleControlReleased(ref state, false);
            return state;
        }
    }
}
