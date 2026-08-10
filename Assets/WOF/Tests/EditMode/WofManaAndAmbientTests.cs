using NUnit.Framework;

namespace WOF.Tests.EditMode
{
    public sealed class WofManaAndAmbientTests
    {
        [Test]
        public void ReactManaRechargeTargetsTheMostEmptyHandAndUsesExactTiming()
        {
            var left = WofManaRules.RechargeMostEmpty(12f, 38f);
            Assert.That(left.Changed, Is.True);
            Assert.That(left.RechargedHand, Is.EqualTo(WofHandSide.Left));
            Assert.That(left.Left, Is.EqualTo(60f));
            Assert.That(left.Right, Is.EqualTo(38f));
            var tied = WofManaRules.RechargeMostEmpty(10f, 10f);
            Assert.That(tied.RechargedHand, Is.EqualTo(WofHandSide.Left));
            Assert.That(WofManaRules.FlowerRespawnSeconds, Is.EqualTo(142d));
            Assert.That(WofManaRules.Decay(60f, 7), Is.EqualTo(53f));
        }

        [Test]
        public void ReactAmbientInsectCountsRemainBiomeAndMobileSpecific()
        {
            Assert.That(WofSurvivalAmbientMath.GetAmbientInsectTargetCount(
                WofSurvivalBiome.Plains, false, WofAmbientInsectKind.Butterfly), Is.EqualTo(8));
            Assert.That(WofSurvivalAmbientMath.GetAmbientInsectTargetCount(
                WofSurvivalBiome.Tallgrass, false, WofAmbientInsectKind.Bee), Is.EqualTo(14));
            Assert.That(WofSurvivalAmbientMath.GetAmbientInsectTargetCount(
                WofSurvivalBiome.Desert, true, WofAmbientInsectKind.Butterfly), Is.EqualTo(2));
        }

        [Test]
        public void ManaFlowersAreDeterministicAndRespectReactRanges()
        {
            var flowers = WofSurvivalAmbientMath.GetNearbyManaFlowers(0, -1);
            Assert.That(flowers.Length, Is.GreaterThan(0));
            foreach (var flower in flowers)
            {
                Assert.That(WofSurvivalAmbientMath.TryGetManaFlower(
                    flower.ChunkX, flower.ChunkZ, flower.Index, out var replay), Is.True);
                Assert.That(replay.Id, Is.EqualTo(flower.Id));
                Assert.That(replay.Position, Is.EqualTo(flower.Position));
                Assert.That(flower.Radius, Is.EqualTo(2.15f));
                Assert.That(flower.StemHeight, Is.InRange(1.35f, 2.05f));
                Assert.That(flower.HeadScale, Is.InRange(0.72f, 1f));
            }
        }

        [Test]
        public void AuthoredVillageCentersDoNotReceiveAmbientInsectBatches()
        {
            Assert.That(WofSurvivalAmbientMath.MakeAmbientInsects(
                0, 0, false, WofAmbientInsectKind.Butterfly), Is.Empty);
            Assert.That(WofSurvivalAmbientMath.MakeAmbientInsects(
                WofLilyCoilLayout.ChunkX,
                WofLilyCoilLayout.ChunkZ,
                false,
                WofAmbientInsectKind.Bee), Is.Empty);
        }
    }
}
