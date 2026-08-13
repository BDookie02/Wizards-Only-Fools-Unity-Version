using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

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

        [Test]
        public void InfiniteManaSourcesMatchReactLocationsRadiiAndTiming()
        {
            var baseSource = WofManaSourceRules.BaseSource;
            Assert.That(baseSource.Id, Is.EqualTo("bonfire-mana-spawner"));
            Assert.That(baseSource.Kind, Is.EqualTo(WofManaSourceKind.BaseInfinite));
            Assert.That(baseSource.Position.x, Is.EqualTo(11.5f));
            Assert.That(baseSource.Position.z, Is.EqualTo(31.5f));
            Assert.That(baseSource.Radius, Is.EqualTo(2.6f));

            var well = WofManaSourceRules.DesertWell;
            Assert.That(well.Id, Is.EqualTo("desert-well-mana-4:-4"));
            Assert.That(well.Kind, Is.EqualTo(WofManaSourceKind.DesertWell));
            Assert.That(well.Position.x, Is.EqualTo(WofDesertVillageLayout.WorldOrigin.x));
            Assert.That(well.Position.y,
                Is.EqualTo(WofDesertVillageLayout.ReactBaseHeight + 7.35f).Within(0.0001f));
            Assert.That(well.Position.z, Is.EqualTo(WofDesertVillageLayout.WorldOrigin.z));
            Assert.That(well.Radius, Is.EqualTo(34f));
            Assert.That(WofManaSourceRules.InfiniteSourceDebounceSeconds, Is.EqualTo(0.55d));
            Assert.That(WofManaSourceRules.PickupPulseSeconds, Is.EqualTo(0.95f));
        }

        [Test]
        public void HutRuneCyclesActivateExactlyTwoThirdsOfCanonicalHuts()
        {
            var huts = WofManaSourceRules.HutPlacements;
            var active = WofManaSourceRules.BuildActiveRuneIndices(1234L);
            Assert.That(active.Length, Is.EqualTo(huts.Count * 2 / 3));
            var unique = new HashSet<int>(active);
            Assert.That(unique.Count, Is.EqualTo(active.Length));
            foreach (var index in active)
            {
                Assert.That(index, Is.InRange(0, huts.Count - 1));
                Assert.That(WofManaSourceRules.IsRuneActive(index, 1234L), Is.True);
                Assert.That(WofManaSourceRules.TryGetHutRune(index, out var source), Is.True);
                Assert.That(source.Kind, Is.EqualTo(WofManaSourceKind.HutRune));
                Assert.That(source.SourceIndex, Is.EqualTo(index));
                Assert.That(source.Radius, Is.EqualTo(WofManaSourceRules.HutRuneCollectionRadius));
            }
        }

        [Test]
        public void HutRuneSelectionIsStableInsideCycleAndChangesAcrossCycles()
        {
            Assert.That(WofManaSourceRules.GetRuneCycle(0d), Is.EqualTo(0L));
            Assert.That(WofManaSourceRules.GetRuneCycle(14.999d), Is.EqualTo(0L));
            Assert.That(WofManaSourceRules.GetRuneCycle(15d), Is.EqualTo(1L));
            Assert.That(WofManaSourceRules.GetRuneCycle(double.NaN), Is.EqualTo(0L));

            var cycleSeven = WofManaSourceRules.BuildActiveRuneIndices(7L);
            var replay = WofManaSourceRules.BuildActiveRuneIndices(7L);
            var cycleEight = WofManaSourceRules.BuildActiveRuneIndices(8L);
            Assert.That(replay, Is.EqualTo(cycleSeven));
            Assert.That(cycleEight, Is.Not.EqualTo(cycleSeven));
        }

        [Test]
        public void ManaSourceVisibilityMatchesReactChunkGates()
        {
            Assert.That(WofManaSourceRules.ShouldShowBaseSources(true, Vector3.zero), Is.True);
            Assert.That(WofManaSourceRules.ShouldShowBaseSources(true,
                new Vector3(WofSurvivalTerrainMath.BlockSize, 0f, 0f)), Is.False);
            Assert.That(WofManaSourceRules.ShouldShowBaseSources(false,
                new Vector3(9999f, 0f, 9999f)), Is.True);

            Assert.That(WofManaSourceRules.ShouldShowDesertWell(true,
                WofDesertVillageLayout.WorldOrigin), Is.True);
            Assert.That(WofManaSourceRules.ShouldShowDesertWell(true,
                WofDesertVillageLayout.WorldOrigin + Vector3.right * WofSurvivalTerrainMath.BlockSize), Is.True);
            Assert.That(WofManaSourceRules.ShouldShowDesertWell(true,
                WofDesertVillageLayout.WorldOrigin + Vector3.right * WofSurvivalTerrainMath.BlockSize * 2f), Is.False);
            Assert.That(WofManaSourceRules.ShouldShowDesertWell(false,
                WofDesertVillageLayout.WorldOrigin), Is.False);
        }

        [Test]
        public void ManaSourceCollectionRadiusIsStrictAndHorizontalLikeReact()
        {
            var source = WofManaSourceRules.BaseSource;
            Assert.That(WofManaSourceRules.IsWithinHorizontalRadius(
                source.Position + Vector3.up * 500f, source), Is.True);
            Assert.That(WofManaSourceRules.IsWithinHorizontalRadius(
                source.Position + Vector3.right * (source.Radius - 0.001f), source), Is.True);
            Assert.That(WofManaSourceRules.IsWithinHorizontalRadius(
                source.Position + Vector3.right * source.Radius, source), Is.False);
        }
    }
}
