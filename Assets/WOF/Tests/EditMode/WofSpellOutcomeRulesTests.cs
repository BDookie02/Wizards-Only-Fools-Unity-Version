using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofSpellOutcomeRulesTests
    {
        [Test]
        public void DirectProjectileTuningMatchesExecutedReactComponents()
        {
            Assert.That(WofSpellRuntimeTuning.GetSpeed(WofSpellId.Fireball), Is.EqualTo(80f));
            Assert.That(WofSpellRuntimeTuning.GetSpeed(WofSpellId.IceShard), Is.EqualTo(70f));
            Assert.That(WofSpellRuntimeTuning.GetSpeed(WofSpellId.RingsOfPower), Is.EqualTo(40f));
            Assert.That(WofSpellRuntimeTuning.GetSpeed(WofSpellId.Flamethrower), Is.EqualTo(90f));
            Assert.That(WofSpellRuntimeTuning.GetSpeed(WofSpellId.Kunai), Is.EqualTo(120f));
        }

        [Test]
        public void IceSpellUsesReactLocalRemoteRangeAndFadeContract()
        {
            Assert.That(WofSpellOutcomeRules.ResolveIceSpellOpacity(true, float.PositiveInfinity), Is.EqualTo(0.4f));
            Assert.That(WofSpellOutcomeRules.ResolveIceSpellOpacity(false, 39f * 39f), Is.EqualTo(1f));
            Assert.That(WofSpellOutcomeRules.ResolveIceSpellOpacity(false, 40f * 40f), Is.Zero);
            Assert.That(WofSpellOutcomeRules.ResolveIceSpellOpacityAtTime(1f, 0.4f), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(WofSpellOutcomeRules.ResolveIceSpellOpacityAtTime(0.4f, 0.4f), Is.Zero);
        }

        [Test]
        public void GrabHoldAndThrowUseReactDistancesFollowRateAndVelocityClamp()
        {
            Assert.That(WofSpellOutcomeRules.ClampGrabDistance(2f), Is.EqualTo(4f));
            Assert.That(WofSpellOutcomeRules.ClampGrabDistance(42f), Is.EqualTo(36f));

            var hold = WofSpellOutcomeRules.ResolveGrabHoldPoint(Vector3.one, Vector3.forward, 10f);
            Assert.That(hold, Is.EqualTo(new Vector3(1f, 1f, 11f)));

            var followed = WofSpellOutcomeRules.ResolveGrabFollowPosition(Vector3.zero, Vector3.forward * 10f, 0.02f);
            var expectedAlpha = 1f - Mathf.Exp(-18f * 0.02f);
            Assert.That(followed.z, Is.EqualTo(10f * expectedAlpha).Within(0.0001f));

            var upward = WofSpellOutcomeRules.ResolveGrabThrowVelocity(Vector3.up);
            var downward = WofSpellOutcomeRules.ResolveGrabThrowVelocity(Vector3.down);
            Assert.That(upward.y, Is.EqualTo(26f));
            Assert.That(downward.y, Is.EqualTo(-18f));
        }

        [Test]
        public void TornadoPullUsesReactDistanceFalloffInwardSpinAndLift()
        {
            var velocity = WofSpellOutcomeRules.ResolveTornadoPullVelocity(
                new Vector3(8.5f, 3f, 0f),
                Vector3.zero);
            Assert.That(velocity.x, Is.EqualTo(-8f).Within(0.0001f));
            Assert.That(velocity.z, Is.EqualTo(-1.9f).Within(0.0001f));
            Assert.That(velocity.y, Is.EqualTo(1.3f).Within(0.0001f));
            Assert.That(WofSpellOutcomeRules.ResolveTornadoPullVelocity(Vector3.right * 17f, Vector3.zero), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void DiscShieldOnlyBlocksIncomingSpellsFromPlayersFront()
        {
            Assert.That(
                WofSpellOutcomeRules.DiscShieldBlocks(Vector3.zero, Vector3.forward, Vector3.forward * 8f),
                Is.True);
            Assert.That(
                WofSpellOutcomeRules.DiscShieldBlocks(Vector3.zero, Vector3.forward, Vector3.back * 8f),
                Is.False);
        }

        [Test]
        public void MeteorPatternUsesReactFiveImpactDesktopContract()
        {
            Assert.That(WofSpellRuntimeTuning.MeteorCount, Is.EqualTo(5));
            Assert.That(WofSpellRuntimeTuning.MeteorImpactRadiusMinimum, Is.EqualTo(3.2f));
            Assert.That(WofSpellRuntimeTuning.MeteorImpactRadiusRandom, Is.EqualTo(0.5f));
            Assert.That(WofSpellRuntimeTuning.GetLifetimeSeconds(WofSpellId.MeteorShower), Is.EqualTo(7.4f));
        }

        [Test]
        public void PortalUsesReactGlobalTwoEndpointBoundsLifetimeAndCooldownContract()
        {
            Assert.That(WofSpellOutcomeRules.CanAddPortalEndpoint(0), Is.True);
            Assert.That(WofSpellOutcomeRules.CanAddPortalEndpoint(1), Is.True);
            Assert.That(WofSpellOutcomeRules.CanAddPortalEndpoint(2), Is.False);
            Assert.That(WofSpellRuntimeTuning.PortalLifetimeSeconds, Is.EqualTo(12f));
            Assert.That(WofSpellRuntimeTuning.PortalTeleportCooldownSeconds, Is.EqualTo(1f));
            Assert.That(WofSpellOutcomeRules.IsInsidePortalBounds(
                new Vector3(1.6f, 2.4f, 1.6f), Vector3.zero), Is.True);
            Assert.That(WofSpellOutcomeRules.IsInsidePortalBounds(
                new Vector3(1.61f, 0f, 0f), Vector3.zero), Is.False);
            Assert.That(WofSpellOutcomeRules.IsInsidePortalBounds(
                new Vector3(0f, 2.41f, 0f), Vector3.zero), Is.False);
        }

        [Test]
        public void MagicGlassOrbUsesReactRelativeAngleAndLockThreshold()
        {
            var forward = Vector3.forward;
            Assert.That(WofSpellOutcomeRules.ResolveMagicGlassOrbRelativeAngle(forward, forward), Is.Zero);
            Assert.That(WofSpellOutcomeRules.ResolveMagicGlassOrbRelativeAngle(forward, Vector3.right),
                Is.EqualTo(Mathf.PI * 0.5f).Within(0.0001f));
            Assert.That(WofSpellOutcomeRules.IsMagicGlassOrbLocked(
                WofSpellRuntimeTuning.MagicGlassOrbLockAngleRadians), Is.True);
            Assert.That(WofSpellOutcomeRules.IsMagicGlassOrbLocked(0.121f), Is.False);
        }
    }
}
