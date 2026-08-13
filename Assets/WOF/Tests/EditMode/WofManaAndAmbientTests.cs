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
        public void AmbientBirdFlocksMatchExactReactBiomeCountsAndFirstBirdFixtures()
        {
            var jungle = WofSurvivalAmbientBirdRules.MakeFlock(-2, -2);
            Assert.That(jungle.Biome, Is.EqualTo(WofSurvivalBiome.Jungle));
            Assert.That(jungle.Seed, Is.EqualTo(0.19214980959804961d).Within(1e-12d));
            Assert.That(jungle.BaseY, Is.EqualTo(192f));
            Assert.That(jungle.Birds.Length, Is.EqualTo(10));
            AssertBird(jungle.Birds[0], "macaw", 250.7270428166733d, 50.94203872376238d,
                172.88083192929486d, 0.7746743002564356d, 0.49407289426380885d,
                3.3843006841763312d);

            var desert = WofSurvivalAmbientBirdRules.MakeFlock(1, 0);
            Assert.That(desert.Biome, Is.EqualTo(WofSurvivalBiome.Desert));
            Assert.That(desert.Seed, Is.EqualTo(0.40742369050713023d).Within(1e-12d));
            Assert.That(desert.BaseY, Is.EqualTo(176f));
            Assert.That(desert.Birds.Length, Is.EqualTo(9));
            AssertBird(desert.Birds[0], "hawk", 86.80101576366806d, 50.20312161404581d,
                289.9895745352664d, 0.9954229762325266d, -0.021595425161649473d,
                5.054436652511752d);

            Assert.That(WofSurvivalAmbientBirdRules.MakeFlock(-2, 0).Birds.Length, Is.EqualTo(8));
            Assert.That(WofSurvivalAmbientBirdRules.MakeFlock(1, -2).Birds.Length, Is.EqualTo(9));
            Assert.That(WofSurvivalAmbientBirdRules.MakeFlock(0, -1).Birds.Length, Is.EqualTo(9));
            Assert.That(WofSurvivalAmbientBirdRules.MakeFlock(-1, -2).Birds.Length, Is.EqualTo(9));
        }

        [Test]
        public void AmbientBirdVisibilityAndStageOneDelayMatchReactGates()
        {
            Assert.That(WofSurvivalAmbientBirdRules.ShouldShowBirds(
                true, true, false, -2, -2, 0), Is.True);
            Assert.That(WofSurvivalAmbientBirdRules.ShouldShowBirds(
                false, true, false, -2, -2, 0), Is.False);
            Assert.That(WofSurvivalAmbientBirdRules.ShouldShowBirds(
                true, false, false, -2, -2, 0), Is.False);
            Assert.That(WofSurvivalAmbientBirdRules.ShouldShowBirds(
                true, true, true, -2, -2, 0), Is.False);
            Assert.That(WofSurvivalAmbientBirdRules.ShouldShowBirds(
                true, true, false, -2, -2, 1), Is.False);
            Assert.That(WofSurvivalAmbientBirdRules.ShouldShowBirds(
                true, true, false, 0, 0, 0), Is.False);
            Assert.That(WofSurvivalAmbientBirdRules.ShouldShowBirds(
                true, true, false, WofLilyCoilLayout.ChunkX, WofLilyCoilLayout.ChunkZ, 0), Is.False);

            Assert.That(WofSurvivalAmbientBirdRules.GetAmbientReadyDelaySeconds(-2, -2, false),
                Is.EqualTo(0.3623308903823272f).Within(0.000001f));
            Assert.That(WofSurvivalAmbientBirdRules.GetAmbientReadyDelaySeconds(-2, -2, true),
                Is.EqualTo(0.5464836090203243f).Within(0.000001f));
        }

        [Test]
        public void AmbientBirdOrbitAndVerticalDriftMatchReactAnimation()
        {
            var flock = WofSurvivalAmbientBirdRules.MakeFlock(-2, -2);
            var bird = flock.Birds[0];
            var initial = WofSurvivalAmbientBirdRules.GetBirdWorldPosition(flock, bird, 0d);
            var later = WofSurvivalAmbientBirdRules.GetBirdWorldPosition(flock, bird, 5d);
            Assert.That(WofSurvivalAmbientBirdRules.GetFlockRotationRadians(flock, 5d),
                Is.EqualTo(flock.Seed * System.Math.PI * 2d + 0.6d).Within(0.000001d));
            Assert.That(WofSurvivalAmbientBirdRules.GetFlockWorldY(flock, 5d),
                Is.EqualTo(flock.BaseY +
                    System.Math.Sin(5d * 0.45d + flock.Seed * 3d) * 8d).Within(0.00001d));
            Assert.That(Vector3.Distance(initial, later), Is.GreaterThan(1f));

            var desert = WofSurvivalAmbientBirdRules.MakeFlock(1, 0);
            Assert.That(WofSurvivalAmbientBirdRules.GetFlockRotationRadians(desert, 5d),
                Is.EqualTo(desert.Seed * System.Math.PI * 2d + 0.4d).Within(0.000001d));
        }

        [Test]
        public void WorldWillowRecordsMatchExactReactOracle()
        {
            var willows = WofSurvivalWorldWillowRules.MakeWillows();
            Assert.That(willows, Has.Length.EqualTo(6));
            AssertWillow(willows[0], 2133.3244640358885d, 3.1386506266226393d, -377.9160702629584d,
                4, -1, 1.8453690208391544d, 1.718482250538218d, WofSurvivalBiome.Plains,
                0.7885268008540152d);
            AssertWillow(willows[1], 1143.962640943835d, 49.84277119429964d, 2045.5625833451156d,
                2, 4, 3.0344038667668074d, 1.2913091083783366d, WofSurvivalBiome.Swamp,
                0.20540473375149304d);
            AssertWillow(willows[2], -2001.4214840786742d, 12.391319742292685d, 1994.743619514932d,
                -4, 4, 4.4029090882971875d, 1.212982402195339d, WofSurvivalBiome.Desert,
                0.41880593575478997d);
            AssertWillow(willows[3], -2007.6895932942919d, 30.483858035578738d, 220.41293651849176d,
                -4, 0, 5.195444134409524d, 1.6245045663557538d, WofSurvivalBiome.Jungle,
                0.2694741344166687d);
            AssertWillow(willows[4], -694.1874635912268d, 58.55910411358743d, -1031.8880366642466d,
                -1, -2, 7.070273907668422d, 1.722854681937715d, WofSurvivalBiome.Tallgrass,
                0.18704176704704878d);
            AssertWillow(willows[5], 594.7691439895705d, 26.76804391925288d, -1416.7618687335016d,
                1, -3, 6.692001048512163d, 1.661983687061438d, WofSurvivalBiome.Swamp,
                0.8794090552983107d);
        }

        [Test]
        public void WorldWillowStructureAndParticlesMatchReactOracle()
        {
            var willow = WofSurvivalWorldWillowRules.MakeWillows()[4];
            var branches = WofSurvivalWorldWillowRules.MakeBranches(willow);
            var lobes = WofSurvivalWorldWillowRules.MakeLobes(willow);
            var vines = WofSurvivalWorldWillowRules.MakeVines(willow);
            var desktopParticles = WofSurvivalWorldWillowRules.MakeParticles(willow, false);
            var mobileParticles = WofSurvivalWorldWillowRules.MakeParticles(willow, true);
            Assert.That(branches, Has.Length.EqualTo(8));
            Assert.That(lobes, Has.Length.EqualTo(11));
            Assert.That(vines, Has.Length.EqualTo(14));
            Assert.That(desktopParticles, Has.Length.EqualTo(72));
            Assert.That(mobileParticles, Has.Length.EqualTo(36));
            AssertVector(branches[0].Start, 0d, 56.302891005724526d, 0d);
            AssertVector(branches[0].End, 16.449904650109445d, 83.01895344527847d, 37.694032801205395d);
            Assert.That(branches[0].Radius, Is.EqualTo(2.6945447225505865d).Within(0.00001d));
            AssertVector(lobes[1].Position, 24.417130819693565d, 160.26104496306363d, 25.33906529630326d);
            Assert.That(lobes[1].Radius, Is.EqualTo(26.63784203240624d).Within(0.00001d));
            AssertVector(lobes[1].Scale, 1.0081246270934208d, 0.8571537889855971d, 1.0188767975267545d);
            Assert.That(vines[0].Length, Is.EqualTo(75.26526857376953d).Within(0.00001d));
            Assert.That(vines[0].Sway, Is.EqualTo(2.4698991007404403d).Within(0.00001d));
            AssertVector(vines[0].End, 28.19518994888654d, 104.31600678945028d, 17.705524348393883d);
            var particle = desktopParticles[0];
            Assert.That(particle.Angle, Is.EqualTo(5.398197128143956d).Within(0.00001d));
            Assert.That(particle.Radius, Is.EqualTo(21.740102858932683d).Within(0.00001d));
            Assert.That(particle.Height, Is.EqualTo(45.157954508993484d).Within(0.00001d));
            Assert.That(particle.Speed, Is.EqualTo(0.48542671012204663d).Within(0.000001d));
            Assert.That(particle.Size, Is.EqualTo(1.7433720085461935d).Within(0.000001d));
            Assert.That(particle.Phase, Is.EqualTo(0.13289725346404382d).Within(0.000001d));
            AssertVector(WofSurvivalWorldWillowRules.GetParticleLocalPosition(willow, particle, 0d),
                -17.179984237105973d, 222.9774074536067d, 14.544934043745675d);
            AssertVector(WofSurvivalWorldWillowRules.GetParticleLocalPosition(willow, particle, 5d),
                -13.619770567252264d, 193.8518048462839d, 15.865679148206903d);
            Assert.That(WofSurvivalWorldWillowRules.GetParticleScale(particle, 5d),
                Is.EqualTo(1.0650370780126628d).Within(0.00001d));
        }

        [Test]
        public void WorldWillowVisibilityMatchesReactRenderRadiusContract()
        {
            var willow = WofSurvivalWorldWillowRules.MakeWillows()[4];
            Assert.That(WofSurvivalWorldWillowRules.ShouldShowWillows(true, false), Is.True);
            Assert.That(WofSurvivalWorldWillowRules.ShouldShowWillows(false, false), Is.False);
            Assert.That(WofSurvivalWorldWillowRules.ShouldShowWillows(true, true), Is.False);
            Assert.That(WofSurvivalWorldWillowRules.IsVisible(willow, -1, -2, 3), Is.True);
            Assert.That(WofSurvivalWorldWillowRules.ShouldShowParticles(willow, -1, -2, 3), Is.True);
            Assert.That(WofSurvivalWorldWillowRules.IsVisible(willow, 3, -2, 3), Is.True);
            Assert.That(WofSurvivalWorldWillowRules.ShouldShowParticles(willow, 3, -2, 3), Is.False);
            Assert.That(WofSurvivalWorldWillowRules.IsVisible(willow, 4, -2, 3), Is.False);
            Assert.That(WofSurvivalWorldWillowRules.MobileParticleUpdateInterval,
                Is.EqualTo(1f / 24f).Within(0.000001f));
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

        private static void AssertBird(
            WofAmbientBirdRecord bird,
            string species,
            double x,
            double y,
            double z,
            double scale,
            double tilt,
            double wingPhase)
        {
            Assert.That(bird.Species.Name, Is.EqualTo(species));
            Assert.That(bird.LocalPosition.x, Is.EqualTo(x).Within(0.0001d));
            Assert.That(bird.LocalPosition.y, Is.EqualTo(y).Within(0.0001d));
            Assert.That(bird.LocalPosition.z, Is.EqualTo(z).Within(0.0001d));
            Assert.That(bird.Scale, Is.EqualTo(scale).Within(0.000001d));
            Assert.That(bird.Tilt, Is.EqualTo(tilt).Within(0.000001d));
            Assert.That(bird.WingPhase, Is.EqualTo(wingPhase).Within(0.000001d));
        }

        private static void AssertWillow(
            WofWorldWillowRecord willow,
            double x,
            double y,
            double z,
            int chunkX,
            int chunkZ,
            double yaw,
            double scale,
            WofSurvivalBiome biome,
            double variant)
        {
            AssertVector(willow.Position, x, y, z);
            Assert.That(willow.ChunkX, Is.EqualTo(chunkX));
            Assert.That(willow.ChunkZ, Is.EqualTo(chunkZ));
            Assert.That(willow.Yaw, Is.EqualTo(yaw).Within(0.000001d));
            Assert.That(willow.Scale, Is.EqualTo(scale).Within(0.000001d));
            Assert.That(willow.Biome, Is.EqualTo(biome));
            Assert.That(willow.Variant, Is.EqualTo(variant).Within(0.000000000001d));
        }

        private static void AssertVector(Vector3 actual, double x, double y, double z)
        {
            Assert.That(actual.x, Is.EqualTo(x).Within(0.0001d));
            Assert.That(actual.y, Is.EqualTo(y).Within(0.0001d));
            Assert.That(actual.z, Is.EqualTo(z).Within(0.0001d));
        }
    }
}
