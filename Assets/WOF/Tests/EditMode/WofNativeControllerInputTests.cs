using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WOF.Tests
{
    public sealed class WofNativeControllerInputTests : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            WofInputRouter.BeginControllerGameplay();
        }

        public override void TearDown()
        {
            WofInputRouter.SetGameplaySuppressed(false);
            WofInputRouter.EndControllerGameplay();
            base.TearDown();
        }

        [Test]
        public void NativeGamepadFeedsMovementSprintSlideJumpAndDualHandCastEdges()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();

            Set(gamepad.leftStick, Vector2.zero);
            Assert.That(WofInputRouter.ReadMove(), Is.EqualTo(Vector2.zero));

            Set(gamepad.leftStick, new Vector2(0.61f, -0.61f));
            var movement = WofInputRouter.ReadMove();
            Assert.That(movement.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(movement.y, Is.EqualTo(-0.5f).Within(0.0001f));

            Press(gamepad.leftStickButton);
            Assert.That(WofInputRouter.ReadSprint(movement), Is.True);
            Release(gamepad.leftStickButton);
            Assert.That(WofInputRouter.ReadSprint(movement), Is.True, "L3 must latch sprint while movement continues.");
            Assert.That(WofInputRouter.ReadSprint(Vector2.zero), Is.False, "Stopping movement must clear the React sprint latch.");

            Press(gamepad.buttonEast);
            Assert.That(WofInputRouter.ReadSlide(), Is.True);
            Release(gamepad.buttonEast);
            Assert.That(WofInputRouter.ReadSlide(), Is.False);

            Press(gamepad.buttonSouth);
            Assert.That(WofInputRouter.ReadJump(), Is.True);
            Release(gamepad.buttonSouth);

            Set(gamepad.leftTrigger, 1f);
            Assert.That(WofInputRouter.ConsumeCast(out var leftHand), Is.True);
            Assert.That(leftHand, Is.EqualTo(WofHandSide.Left));
            Assert.That(WofInputRouter.ConsumeCast(out _), Is.False, "Held LT must not repeat the cast edge.");

            Set(gamepad.leftTrigger, 0f);
            Assert.That(WofInputRouter.ConsumeCast(out _), Is.False);
            Set(gamepad.rightTrigger, 1f);
            Assert.That(WofInputRouter.ConsumeCast(out var rightHand), Is.True);
            Assert.That(rightHand, Is.EqualTo(WofHandSide.Right));
        }

        [Test]
        public void GameplayModalSuppressionBlocksEveryNativeControllerGameplayAction()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            Set(gamepad.leftStick, Vector2.zero);
            Assert.That(WofInputRouter.ReadMove(), Is.EqualTo(Vector2.zero), "A released frame arms native controller gameplay.");

            WofInputRouter.SetGameplaySuppressed(true);
            Set(gamepad.leftStick, Vector2.one);
            Press(gamepad.buttonSouth);
            Press(gamepad.buttonEast);
            Set(gamepad.leftTrigger, 1f);
            Set(gamepad.rightTrigger, 1f);

            Assert.That(WofInputRouter.ReadMove(), Is.EqualTo(Vector2.zero));
            Assert.That(WofInputRouter.ReadLook(), Is.EqualTo(Vector2.zero));
            Assert.That(WofInputRouter.ReadJump(), Is.False);
            Assert.That(WofInputRouter.ReadSprint(Vector2.one), Is.False);
            Assert.That(WofInputRouter.ReadSlide(), Is.False);
            Assert.That(WofInputRouter.ConsumeCast(out _), Is.False);
        }
    }
}
