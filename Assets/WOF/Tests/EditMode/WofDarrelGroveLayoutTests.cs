using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofDarrelGroveLayoutTests
    {
        [Test]
        public void ReactChunkSpawnAndDragonCoordinatesAreExact()
        {
            Assert.That(WofDarrelGroveLayout.WorldOrigin, Is.EqualTo(new Vector3(6144f, 0f, -6144f)));
            Assert.That(WofDarrelGroveLayout.SpawnPosition, Is.EqualTo(new Vector3(6144f, 33.35f, -6196f)));
            Assert.That(WofDarrelGroveLayout.DragonWorldPosition, Is.EqualTo(new Vector3(6154f, 39.35f, -6138f)));
            Assert.That(WofDarrelGroveLayout.DragonQuestMarkerWorldPosition, Is.EqualTo(new Vector3(6154f, 43.25f, -6138f)));
            Assert.That(WofDarrelGroveLayout.ReactSpawnYawRadians, Is.EqualTo(Mathf.PI));
            Assert.That(WofDarrelGroveLayout.UnitySpawnYawDegrees, Is.Zero);
        }

        [Test]
        public void DragonInteractionUsesExactHouseAndTalkBounds()
        {
            Assert.That(WofDarrelGroveLayout.CanInteractWithDragon(WofDarrelGroveLayout.DragonWorldPosition), Is.True);
            Assert.That(WofDarrelGroveLayout.CanInteractWithDragon(
                WofDarrelGroveLayout.WorldOrigin + new Vector3(39f, 43.25f, 0f)), Is.True);
            Assert.That(WofDarrelGroveLayout.CanInteractWithDragon(
                WofDarrelGroveLayout.WorldOrigin + new Vector3(39.01f, 43.25f, 0f)), Is.False);
            Assert.That(WofDarrelGroveLayout.IsNearDragon(
                WofDarrelGroveLayout.DragonWorldPosition + Vector3.right * 34f), Is.True);
            Assert.That(WofDarrelGroveLayout.IsNearDragon(
                WofDarrelGroveLayout.DragonWorldPosition + Vector3.right * 34.01f), Is.False);
        }

        [Test]
        public void WakeAnimationPlaysOnceBeforeIdle()
        {
            var beforeEnd = WofDarrelGroveLayout.ResolveDragonFrame(
                WofDarrelDragonMode.Wake,
                10f,
                10f + WofDarrelGroveLayout.WakeFrameSeconds * 8.5f);
            Assert.That(beforeEnd.Mode, Is.EqualTo(WofDarrelDragonMode.Wake));
            Assert.That(beforeEnd.FrameIndex, Is.EqualTo(8));

            var afterEnd = WofDarrelGroveLayout.ResolveDragonFrame(
                WofDarrelDragonMode.Wake,
                10f,
                10f + WofDarrelGroveLayout.WakeFrameSeconds * 9f);
            Assert.That(afterEnd.Mode, Is.EqualTo(WofDarrelDragonMode.Idle));
            Assert.That(afterEnd.FrameIndex, Is.Zero);
        }

        [Test]
        public void DragonModesAndFrameCountsMatchReactManifest()
        {
            Assert.That(WofDarrelGroveLayout.GetFrameCount(WofDarrelDragonMode.Sleep), Is.EqualTo(8));
            Assert.That(WofDarrelGroveLayout.GetFrameCount(WofDarrelDragonMode.Wake), Is.EqualTo(9));
            Assert.That(WofDarrelGroveLayout.GetFrameCount(WofDarrelDragonMode.Idle), Is.EqualTo(11));
            Assert.That(WofDarrelGroveLayout.GetFrameCount(WofDarrelDragonMode.Attack), Is.EqualTo(16));
            Assert.That(WofDarrelGroveLayout.GetFrameSeconds(WofDarrelDragonMode.Sleep), Is.EqualTo(0.240f));
            Assert.That(WofDarrelGroveLayout.GetFrameSeconds(WofDarrelDragonMode.Wake), Is.EqualTo(0.115f));
            Assert.That(WofDarrelGroveLayout.GetFrameSeconds(WofDarrelDragonMode.Idle), Is.EqualTo(0.155f));
            Assert.That(WofDarrelGroveLayout.GetFrameSeconds(WofDarrelDragonMode.Attack), Is.EqualTo(0.095f));
        }

        [Test]
        public void ReturnGateUsesReactSensorVolume()
        {
            Assert.That(WofDarrelGroveLayout.IsInsideReturnGate(
                WofDarrelGroveLayout.ReturnGateWorldPosition + new Vector3(8f, 8f, 5f)), Is.True);
            Assert.That(WofDarrelGroveLayout.IsInsideReturnGate(
                WofDarrelGroveLayout.ReturnGateWorldPosition + new Vector3(8.01f, 8f, 0f)), Is.False);
        }

        [Test]
        public void WaterfallTextureAndOpacityMotionMatchesReactFrameMath()
        {
            const float elapsed = 2.75f;
            var visuals = WofDarrelGroveLayout.ResolveWaterfallVisuals(elapsed);
            Assert.That(visuals.FallTextureOffset.x, Is.EqualTo(Mathf.Sin(elapsed * 1.35f) * 0.035f).Within(0.000001f));
            Assert.That(visuals.FallTextureOffset.y, Is.EqualTo(Mathf.Repeat(elapsed * 0.82f, 1f)).Within(0.000001f));
            Assert.That(visuals.PoolTextureOffset.x, Is.EqualTo(Mathf.Repeat(elapsed * 0.08f, 1f)).Within(0.000001f));
            Assert.That(visuals.PoolTextureOffset.y, Is.EqualTo(Mathf.Sin(elapsed * 0.62f) * 0.035f).Within(0.000001f));
            Assert.That(visuals.RunnelTextureOffset.x, Is.EqualTo(Mathf.Sin(elapsed * 0.9f) * 0.04f).Within(0.000001f));
            Assert.That(visuals.RunnelTextureOffset.y, Is.EqualTo(Mathf.Repeat(elapsed * 0.36f, 1f)).Within(0.000001f));
            Assert.That(visuals.FallOpacity, Is.EqualTo(0.55f + Mathf.Sin(elapsed * 4.2f) * 0.08f).Within(0.000001f));
            Assert.That(visuals.FoamOpacity, Is.EqualTo(0.28f + Mathf.Sin(elapsed * 5.1f + 0.8f) * 0.08f).Within(0.000001f));
            Assert.That(visuals.PoolOpacity, Is.EqualTo(0.82f + Mathf.Sin(elapsed * 1.7f) * 0.06f).Within(0.000001f));
        }

        [Test]
        public void WaterfallRunnelAndSprayMotionPreservesReactRuntimeIndexQuirk()
        {
            const float elapsed = 1.25f;
            const int runnelIndex = 4;
            Assert.That(
                WofDarrelGroveLayout.ResolveWaterfallRunnelLocalX(runnelIndex, elapsed),
                Is.EqualTo(Mathf.Sin(runnelIndex) * 7f + Mathf.Sin(elapsed * 1.7f + runnelIndex * 1.8f) * 0.42f)
                    .Within(0.000001f));

            const int sprayIndex = 2;
            const float baseY = 8.2f;
            const float baseScale = 3.2f;
            var pulse = 0.86f + Mathf.Sin(elapsed * 3.8f + sprayIndex * 1.4f) * 0.18f;
            var spray = WofDarrelGroveLayout.ResolveWaterfallSprayVisuals(sprayIndex, baseY, baseScale, elapsed);
            Assert.That(spray.LocalY, Is.EqualTo(baseY + Mathf.Sin(elapsed * 4.7f + sprayIndex) * 0.42f).Within(0.000001f));
            Assert.That(spray.LocalScale.x, Is.EqualTo(baseScale * pulse).Within(0.000001f));
            Assert.That(spray.LocalScale.y, Is.EqualTo(baseScale * 0.45f * pulse).Within(0.000001f));
            Assert.That(spray.LocalScale.z, Is.EqualTo(baseScale * pulse).Within(0.000001f));
        }
    }
}
