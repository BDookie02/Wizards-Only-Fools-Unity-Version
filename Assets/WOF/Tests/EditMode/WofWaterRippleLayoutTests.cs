using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofWaterRippleLayoutTests
    {
        [Test]
        public void WaterBandsUseStrictReactBoundaries()
        {
            Assert.That(WofWaterRippleLayout.IsBaseVillageWaterRippleSpot(new Vector3(42f, 0f, 0f)), Is.False);
            Assert.That(WofWaterRippleLayout.IsBaseVillageWaterRippleSpot(new Vector3(42.01f, 0f, 0f)), Is.True);
            Assert.That(WofWaterRippleLayout.IsBaseVillageWaterRippleSpot(new Vector3(58f, 0f, 0f)), Is.False);
            Assert.That(WofWaterRippleLayout.IsBaseVillageWaterRippleSpot(new Vector3(125.01f, 0f, 0f)), Is.True);
            Assert.That(WofWaterRippleLayout.IsBaseVillageWaterRippleSpot(new Vector3(145f, 0f, 0f)), Is.False);
        }

        [Test]
        public void WaterSpotRequiresReactHeightGate()
        {
            Assert.That(WofWaterRippleLayout.IsBaseVillageWaterRippleSpot(new Vector3(50f, 1.149f, 0f)), Is.True);
            Assert.That(WofWaterRippleLayout.IsBaseVillageWaterRippleSpot(new Vector3(50f, 1.15f, 0f)), Is.False);
        }

        [Test]
        public void RippleAnimationMatchesReactScaleAndOpacity()
        {
            Assert.That(WofWaterRippleLayout.ResolveScale(0f), Is.EqualTo(0.5f));
            Assert.That(WofWaterRippleLayout.ResolveOpacity(0f), Is.EqualTo(1f));
            Assert.That(WofWaterRippleLayout.ResolveScale(0.25f), Is.EqualTo(1.5f));
            Assert.That(WofWaterRippleLayout.ResolveOpacity(0.25f), Is.EqualTo(0.5f));
            Assert.That(WofWaterRippleLayout.ResolveScale(0.5f), Is.EqualTo(2.5f));
            Assert.That(WofWaterRippleLayout.ResolveOpacity(0.5f), Is.EqualTo(0f));
        }
    }
}
