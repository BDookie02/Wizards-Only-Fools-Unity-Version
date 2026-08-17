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
        public void RockOutcropRecordsMatchExactReactOracle()
        {
            var plains = WofSurvivalRockOutcropRules.MakeChunk(-1, -1, 0);
            Assert.That(plains, Has.Length.EqualTo(4));
            AssertRock(plains[0], "-1:-1-rock-0", -433.95021008990705d, 45.00388116110742d,
                -357.94758341805078d, 3.673254155478207d, 5.4334731618066385d, 1, false);
            AssertRock(plains[1], "-1:-1-rock-1", -451.35406307548285d, 54.66388811320902d,
                -492.6342642901838d, 4.630492193603277d, 1.7388210708427787d, 2, true);
            AssertRock(plains[2], "-1:-1-rock-2", -577.5918675721437d, 62.232763933622145d,
                -307.75246151454746d, 3.9979437456800957d, 0.3388028972777374d, 1, false);
            AssertRock(plains[3], "-1:-1-rock-3", -325.2709291406721d, 31.726573805034345d,
                -636.2882562758774d, 4.913873229679303d, 4.688349526816385d, 2, true);

            var jungle = WofSurvivalRockOutcropRules.MakeChunk(-4, 0, 0);
            Assert.That(jungle, Has.Length.EqualTo(5));
            AssertRock(jungle[0], "-4:0-rock-0", -1903.6356101697125d, 29.872904708853333d,
                -72.0592223785445d, 5.299054981542577d, 2.296501136753142d, 2, true);

            var swamp = WofSurvivalRockOutcropRules.MakeChunk(7, 4, 0);
            Assert.That(swamp, Has.Length.EqualTo(4));
            AssertRock(swamp[0], "7:4-rock-0", 3712.664902947657d, 75.01392288285268d,
                2169.727358831838d, 5.26654601755763d, 0.07871350264190505d, 5, true);
        }

        [Test]
        public void RockOutcropLodBiomeAndOwnershipGatesMatchReact()
        {
            Assert.That(WofSurvivalRockOutcropRules.MakeChunk(-1, -1, 1), Has.Length.EqualTo(1));
            Assert.That(WofSurvivalRockOutcropRules.MakeChunk(-1, -1, 2), Is.Empty);
            Assert.That(WofSurvivalRockOutcropRules.MakeChunk(1, 0, 0), Is.Empty,
                "React excludes every desert rock outcrop.");
            Assert.That(WofSurvivalRockOutcropRules.MakeChunk(6, -3, 0), Is.Empty,
                "React gives tallgrass/restored-meadow detail ownership to the grass system.");
            Assert.That(WofSurvivalRockOutcropRules.MakeChunk(0, 0, 0), Is.Empty,
                "Authored villages own their complete decoration layers.");
            Assert.That(WofSurvivalRockOutcropRules.ShouldGenerateChunk(false, -1, -1, 0), Is.False);
            Assert.That(WofSurvivalRockOutcropRules.ShouldGenerateChunk(true, -1, -1, 0), Is.True);
            Assert.That(WofSurvivalRockOutcropRules.ShouldGenerateChunk(true, -1, -1, 2), Is.False);
            Assert.That(WofSurvivalRockOutcropRules.ShouldShowRuntime(true), Is.True,
                "React leaves rock outcrops visible during its grass-inspection view.");
            Assert.That(WofSurvivalRockOutcropRules.ShouldShowRuntime(false), Is.False);
        }

        [Test]
        public void RockOutcropStagingAndGeometryMatchReactContract()
        {
            var desktopNear = WofSurvivalRockOutcropRules.GetReadyDelaySeconds(-1, -1, 0, false);
            var desktopMid = WofSurvivalRockOutcropRules.GetReadyDelaySeconds(-1, -1, 1, false);
            var mobileNear = WofSurvivalRockOutcropRules.GetReadyDelaySeconds(-1, -1, 0, true);
            Assert.That(desktopMid - desktopNear, Is.EqualTo(0.28f).Within(0.000001f));
            Assert.That(mobileNear, Is.GreaterThan(desktopNear));
            Assert.That(WofSurvivalRockOutcropRules.MinimumNormalY, Is.EqualTo(0.62f));
            Assert.That(WofSurvivalRockOutcropRules.MaximumHeightRange, Is.EqualTo(7.8f));
            Assert.That(WofSurvivalRockOutcropRules.WaterClearance, Is.EqualTo(0.24f));

            var boulder = WofSurvivalRockOutcropRules.MakeChunk(-1, -1, 0)[0];
            var boulderScale = boulder.Matrix.lossyScale;
            Assert.That(boulderScale.x, Is.EqualTo(boulder.Scale * 1.35f).Within(0.0001f));
            Assert.That(boulderScale.y, Is.EqualTo(boulder.Scale * 0.75f).Within(0.0001f));
            var spire = WofSurvivalRockOutcropRules.MakeChunk(-1, -1, 0)[1];
            Assert.That(spire.Matrix.lossyScale.y, Is.EqualTo(spire.Scale * 1.95f).Within(0.0001f));
        }

        [Test]
        public void UnderbrushRecordsMatchExactReactOracle()
        {
            var plains = WofSurvivalUnderbrushRules.MakeChunk(-1, -1, 0, false);
            Assert.That(plains.BushClusterCount, Is.EqualTo(17));
            Assert.That(plains.BushLobes, Has.Length.EqualTo(72));
            Assert.That(plains.Ferns, Has.Length.EqualTo(53));
            AssertBush(plains.BushLobes[0], 0, 0,
                -703.9989589073432d, 65.27536534413332d, -357.19027570288875d,
                3.7688444992766614d, 0.08864323878369759d, 0.015896436681614435d,
                2.30856181748631d, 4.302213309332986d, 2.6500302616447953d, 0);
            AssertFern(plains.Ferns[0], 0,
                -630.7097423482873d, 64.5828686664674d, -520.9502058040723d,
                5.970936545839625d, 0.23242924419158956d,
                0.6695338278047276d, 3.591826583499788d, 0);

            var jungle = WofSurvivalUnderbrushRules.MakeChunk(-4, 0, 0, false);
            Assert.That(jungle.BushClusterCount, Is.EqualTo(21));
            Assert.That(jungle.BushLobes, Has.Length.EqualTo(87));
            Assert.That(jungle.Ferns, Has.Length.EqualTo(70));
            Assert.That(jungle.BushLobes[0].Position.x, Is.EqualTo(-2028.1840203509864d).Within(0.001d));
            Assert.That(jungle.Ferns[0].Position.y, Is.EqualTo(20.731329037414273d).Within(0.001d));

            var swamp = WofSurvivalUnderbrushRules.MakeChunk(7, 4, 0, false);
            Assert.That(swamp.BushClusterCount, Is.EqualTo(18));
            Assert.That(swamp.BushLobes, Has.Length.EqualTo(76));
            Assert.That(swamp.Ferns, Has.Length.EqualTo(53));
            Assert.That(swamp.BushLobes[0].ColorIndex, Is.EqualTo(2));
            Assert.That(swamp.Ferns[0].SourceIndex, Is.EqualTo(1));
        }

        [Test]
        public void UnderbrushLodMobileAndOwnershipGatesMatchReact()
        {
            var mid = WofSurvivalUnderbrushRules.MakeChunk(-1, -1, 1, false);
            Assert.That(mid.BushClusterCount, Is.EqualTo(5));
            Assert.That(mid.BushLobes, Has.Length.EqualTo(23));
            Assert.That(mid.Ferns, Has.Length.EqualTo(10));
            var mobile = WofSurvivalUnderbrushRules.MakeChunk(-1, -1, 0, true);
            Assert.That(mobile.BushClusterCount, Is.EqualTo(10));
            Assert.That(mobile.BushLobes, Has.Length.EqualTo(44));
            Assert.That(mobile.Ferns, Has.Length.EqualTo(30));
            var desert = WofSurvivalUnderbrushRules.MakeChunk(1, 0, 0, false);
            Assert.That(desert.BushClusterCount, Is.EqualTo(11));
            Assert.That(desert.BushLobes, Has.Length.EqualTo(51));
            Assert.That(desert.Ferns, Is.Empty);
            Assert.That(WofSurvivalUnderbrushRules.MakeChunk(-1, -1, 2, false).BushLobes, Is.Empty);
            Assert.That(WofSurvivalUnderbrushRules.MakeChunk(6, -3, 0, false).BushLobes, Is.Empty,
                "React gives tallgrass/restored-meadow underbrush ownership to local grass.");
            Assert.That(WofSurvivalUnderbrushRules.MakeChunk(0, 0, 0, false).BushLobes, Is.Empty,
                "Authored villages own their complete decoration layers.");
            Assert.That(WofSurvivalUnderbrushRules.ShouldGenerateChunk(false, false, -1, -1, 0), Is.False);
            Assert.That(WofSurvivalUnderbrushRules.ShouldGenerateChunk(true, true, -1, -1, 0), Is.False,
                "React hides underbrush in grass-inspection view.");
            Assert.That(WofSurvivalUnderbrushRules.ShouldGenerateChunk(true, false, -1, -1, 0), Is.True);
        }

        [Test]
        public void UnderbrushStagingPaletteAndGeometryMatchReactContract()
        {
            var desktopNear = WofSurvivalUnderbrushRules.GetReadyDelaySeconds(-1, -1, 0, false);
            var desktopMid = WofSurvivalUnderbrushRules.GetReadyDelaySeconds(-1, -1, 1, false);
            var mobileNear = WofSurvivalUnderbrushRules.GetReadyDelaySeconds(-1, -1, 0, true);
            Assert.That(desktopMid - desktopNear, Is.EqualTo(0.28f).Within(0.000001f));
            Assert.That(mobileNear, Is.GreaterThan(desktopNear));
            Assert.That(WofSurvivalUnderbrushRules.BushMinimumNormalY, Is.EqualTo(0.68f));
            Assert.That(WofSurvivalUnderbrushRules.BushMaximumHeightRange, Is.EqualTo(7.4f));
            Assert.That(WofSurvivalUnderbrushRules.FernMinimumNormalY, Is.EqualTo(0.78f));
            Assert.That(WofSurvivalUnderbrushRules.FernMaximumHeightRange, Is.EqualTo(2.4f));
            Assert.That(WofSurvivalUnderbrushRules.FernRouteMaskMaximum, Is.EqualTo(0.12f));
            Assert.That(WofSurvivalUnderbrushRules.FernOpacity, Is.EqualTo(0.88f));
            Assert.That(WofSurvivalUnderbrushRules.GetBushColor(WofSurvivalBiome.Plains, 0),
                Is.EqualTo(new Color32(0x41, 0x6f, 0x2f, 0xff)));
            Assert.That(WofSurvivalUnderbrushRules.GetFernColor(WofSurvivalBiome.Swamp, 2),
                Is.EqualTo(new Color32(0x6d, 0x79, 0x3a, 0xff)));

            var lobe = WofSurvivalUnderbrushRules.MakeChunk(-1, -1, 0, false).BushLobes[0];
            Assert.That(lobe.Matrix.GetColumn(3).x, Is.EqualTo(lobe.Position.x).Within(0.0001f));
            Assert.That(lobe.Matrix.GetColumn(3).y, Is.EqualTo(lobe.Position.y).Within(0.0001f));
            var fern = WofSurvivalUnderbrushRules.MakeChunk(-1, -1, 0, false).Ferns[0];
            Assert.That(fern.Matrix.GetColumn(3).y,
                Is.EqualTo(fern.Position.y + fern.Scale.y * 0.5f).Within(0.0001f));

            var shader = Resources.Load<Shader>("Shaders/WofUnderbrushFaceted");
            Assert.That(shader, Is.Not.Null, "The build must retain the underbrush-only faceted shader.");
            Assert.That(shader.name, Is.EqualTo("WOF/Underbrush Faceted"));
            var meshFactory = typeof(WofSurvivalUnderbrushRuntime).GetMethod(
                "CreateDodecaMesh",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(meshFactory, Is.Not.Null);
            var mesh = (Mesh)meshFactory.Invoke(null, new object[] { 0.5f, "UnderbrushContractProbe" });
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(108),
                    "Three.js toNonIndexed emits one barycentric triplet per triangle vertex.");
                var barycentric = new List<Vector3>();
                mesh.GetUVs(1, barycentric);
                Assert.That(barycentric, Has.Count.EqualTo(108));
                Assert.That(barycentric[0], Is.EqualTo(new Vector3(1f, 0f, 0f)));
                Assert.That(barycentric[1], Is.EqualTo(new Vector3(0f, 1f, 0f)));
                Assert.That(barycentric[2], Is.EqualTo(new Vector3(0f, 0f, 1f)));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void UnderbrushWindowRetainsOverlappingReadyChunksAndStagesOnlyNewFringe()
        {
            var gameObject = new GameObject("UnderbrushWindowRetentionTest");
            try
            {
                var runtime = gameObject.AddComponent<WofSurvivalUnderbrushRuntime>();
                var runtimeType = typeof(WofSurvivalUnderbrushRuntime);
                var activeType = runtimeType.GetNestedType(
                    "ActiveChunk", System.Reflection.BindingFlags.NonPublic);
                var activeField = runtimeType.GetField(
                    "_activeChunks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var pendingField = runtimeType.GetField(
                    "_pendingChunks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var awake = runtimeType.GetMethod(
                    "Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var rebuildWindow = runtimeType.GetMethod(
                    "RebuildWindow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(activeType, Is.Not.Null);
                Assert.That(activeField, Is.Not.Null);
                Assert.That(pendingField, Is.Not.Null);
                Assert.That(awake, Is.Not.Null);
                Assert.That(rebuildWindow, Is.Not.Null);
                awake.Invoke(runtime, null);

                const int previousCenterX = -10;
                const int previousCenterZ = -10;
                const int nextCenterX = -9;
                const int nextCenterZ = -10;
                var previousKeys = new HashSet<long>();
                var nextKeys = new HashSet<long>();
                var active = (System.Collections.IDictionary)activeField.GetValue(runtime);
                for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var chunkX = previousCenterX + dx;
                    var chunkZ = previousCenterZ + dz;
                    var distance = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz));
                    if (!WofSurvivalUnderbrushRules.ShouldGenerateChunk(true, false, chunkX, chunkZ, distance))
                        continue;
                    var key = ((long)chunkX << 32) | (uint)chunkZ;
                    previousKeys.Add(key);
                    var chunk = WofSurvivalUnderbrushRules.MakeChunk(chunkX, chunkZ, distance, false);
                    var activeValue = System.Activator.CreateInstance(
                        activeType,
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic,
                        null,
                        new object[] { chunkX, chunkZ, distance, chunk },
                        null);
                    active.Add(key, activeValue);
                }
                for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var chunkX = nextCenterX + dx;
                    var chunkZ = nextCenterZ + dz;
                    var distance = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz));
                    if (WofSurvivalUnderbrushRules.ShouldGenerateChunk(true, false, chunkX, chunkZ, distance))
                        nextKeys.Add(((long)chunkX << 32) | (uint)chunkZ);
                }

                var expectedRetained = 0;
                foreach (var key in previousKeys)
                    if (nextKeys.Contains(key)) expectedRetained++;
                rebuildWindow.Invoke(runtime, new object[] { nextCenterX, nextCenterZ });

                Assert.That(active.Count, Is.EqualTo(expectedRetained));
                var pending = (System.Collections.IList)pendingField.GetValue(runtime);
                Assert.That(pending.Count, Is.EqualTo(nextKeys.Count - expectedRetained));
                foreach (var key in previousKeys)
                    Assert.That(active.Contains(key), Is.EqualTo(nextKeys.Contains(key)));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DetailScatterRecordsMatchIndependentReactOracle()
        {
            var plains = WofSurvivalDetailScatterRules.MakeChunk(-1, -1);
            Assert.That(plains, Has.Length.EqualTo(4));
            AssertDetail(plains[0], 0, WofSurvivalDetailScatterKind.Tree, WofSurvivalBiome.Plains,
                -676.3834219428338d, 66.58193218213847d, -546.5675836268556d,
                3.3226080804823144d, 0.15171720300713787d);
            AssertDetail(plains[3], 5, WofSurvivalDetailScatterKind.Tree, WofSurvivalBiome.Plains,
                -464.5536900033057d, 52.45128294630732d, -435.85430162956936d,
                2.690502712875241d, 0.6550689798168605d);

            var jungle = WofSurvivalDetailScatterRules.MakeChunk(-4, 0);
            Assert.That(jungle, Has.Length.EqualTo(5));
            AssertDetail(jungle[0], 1, WofSurvivalDetailScatterKind.Tree, WofSurvivalBiome.Jungle,
                -1903.4202450914681d, 29.627371801790204d, -84.57465503543615d,
                3.8591589127437147d, 0.9073762310872553d);
            AssertDetail(jungle[4], 9, WofSurvivalDetailScatterKind.Tree, WofSurvivalBiome.Jungle,
                -1996.5095991875976d, 37.39285346726079d, 186.38972910672427d,
                4.256907378554024d, 0.5963996467617108d);

            var desert = WofSurvivalDetailScatterRules.MakeChunk(4, -3);
            Assert.That(desert, Has.Length.EqualTo(9));
            foreach (var record in desert)
                Assert.That(record.Kind, Is.EqualTo(record.Variant > 0.56f
                    ? WofSurvivalDetailScatterKind.Tumbleweed
                    : WofSurvivalDetailScatterKind.Cactus));
            var sourceTumbleweed = System.Array.Find(desert, record => record.SourceIndex == 1);
            var sourceCactus = System.Array.Find(desert, record => record.SourceIndex == 9);
            // X/Z, indices, scale, and variants come from React; Y is the exact
            // approved smoothed-desert surface rather than React's former seam.
            AssertDetail(sourceTumbleweed, 1, WofSurvivalDetailScatterKind.Tumbleweed, WofSurvivalBiome.Desert,
                2077.793862410486d, 21.9626694d, -1629.0929528412223d,
                1.4915801306155119d, 0.8757774386685924d);
            AssertDetail(sourceCactus, 9, WofSurvivalDetailScatterKind.Cactus, WofSurvivalBiome.Desert,
                2061.353465262279d, 29.3072872d, -1537.0753559077532d,
                2.3722367013164334d, 0.22846357117668958d);
        }

        [Test]
        public void DetailScatterStagingVisualScaleAndCactusOverrideMatchSourceContract()
        {
            Assert.That(WofSurvivalDetailScatterRules.ShouldShowRuntime(true, false, false), Is.True);
            Assert.That(WofSurvivalDetailScatterRules.ShouldShowRuntime(true, true, false), Is.False);
            Assert.That(WofSurvivalDetailScatterRules.ShouldShowRuntime(true, false, true), Is.False);
            Assert.That(WofSurvivalDetailScatterRules.ShouldShowRuntime(false, false, false), Is.False);
            var ready = WofSurvivalDetailScatterRules.GetReadyDelaySeconds(-1, -1, 0);
            Assert.That(ready, Is.InRange(3.6f, 4.12f));
            Assert.That(WofSurvivalDetailScatterRules.GetReadyDelaySeconds(-1, -1, 3) - ready,
                Is.EqualTo(0.84f).Within(0.00001f));
            Assert.That(WofSurvivalDetailScatterRules.GetTreeVisualScale(
                WofSurvivalBiome.Jungle, 3.8591588f), Is.EqualTo(18.163393f).Within(0.0001f));
            Assert.That(WofSurvivalDetailScatterRules.GetTreeFootprintScale(
                WofSurvivalBiome.Jungle, 18.163393f), Is.EqualTo(4.359214f).Within(0.0001f));
            Assert.That(WofSurvivalDetailScatterRules.TumbleweedThreshold, Is.EqualTo(0.56f));
            Assert.That(WofSurvivalDesertCactusRuntime.TotalCactusCount, Is.EqualTo(30),
                "The approved thick-cactus runtime must remain the rendered cactus layer.");
        }

        [Test]
        public void DetailScatterWindowRetainsReactStageTimersAndUsesExactDodecaTopology()
        {
            var gameObject = new GameObject("DetailScatterWindowRetentionTest");
            try
            {
                var runtime = gameObject.AddComponent<WofSurvivalDetailScatterRuntime>();
                var runtimeType = typeof(WofSurvivalDetailScatterRuntime);
                var awake = runtimeType.GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var rebuild = runtimeType.GetMethod("RebuildStageWindow",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var stagesField = runtimeType.GetField("_visibleStages",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var solidField = runtimeType.GetField("_dodecaMesh",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var wireField = runtimeType.GetField("_dodecaWireMesh",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(awake, Is.Not.Null);
                awake.Invoke(runtime, null);
                Assert.That(rebuild, Is.Not.Null);
                Assert.That(stagesField, Is.Not.Null);
                rebuild.Invoke(runtime, new object[] { 0, 0 });
                var stages = (System.Collections.IDictionary)stagesField.GetValue(runtime);
                Assert.That(stages.Count, Is.EqualTo(37));
                var retainedKey = ((long)0 << 32) ^ (uint)0;
                var retained = stages[retainedKey];
                var readyAt = (float)retained.GetType().GetProperty("ReadyAt").GetValue(retained);
                rebuild.Invoke(runtime, new object[] { 1, 0 });
                stages = (System.Collections.IDictionary)stagesField.GetValue(runtime);
                Assert.That(stages.Count, Is.EqualTo(37));
                var retainedAfter = stages[retainedKey];
                Assert.That((float)retainedAfter.GetType().GetProperty("ReadyAt").GetValue(retainedAfter),
                    Is.EqualTo(readyAt), "Moving the player must not restart an overlapping chunk's timer.");

                var solid = (Mesh)solidField.GetValue(runtime);
                var wire = (Mesh)wireField.GetValue(runtime);
                Assert.That(solid.name, Is.EqualTo("ReactDetailScatterDodeca"));
                Assert.That(solid.triangles, Has.Length.EqualTo(108));
                Assert.That(wire.name, Is.EqualTo("ReactDetailScatterDodecaWire"));
                Assert.That(wire.GetIndexCount(0), Is.EqualTo(216));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
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

        private static void AssertRock(
            WofSurvivalRockOutcropRecord rock,
            string key,
            double x,
            double y,
            double z,
            double scale,
            double yaw,
            int paletteIndex,
            bool spire)
        {
            Assert.That(rock.Key, Is.EqualTo(key));
            Assert.That(rock.Position.x, Is.EqualTo(x).Within(0.001d));
            Assert.That(rock.Position.y, Is.EqualTo(y).Within(0.001d));
            Assert.That(rock.Position.z, Is.EqualTo(z).Within(0.001d));
            Assert.That(rock.Scale, Is.EqualTo(scale).Within(0.00001d));
            Assert.That(rock.Yaw, Is.EqualTo(yaw).Within(0.00001d));
            Assert.That(rock.PaletteIndex, Is.EqualTo(paletteIndex));
            Assert.That(rock.Spire, Is.EqualTo(spire));
        }

        private static void AssertBush(
            WofSurvivalBushLobeRecord bush,
            int sourceIndex,
            int lobeIndex,
            double x,
            double y,
            double z,
            double yaw,
            double pitch,
            double roll,
            double width,
            double height,
            double depth,
            int colorIndex)
        {
            Assert.That(bush.SourceIndex, Is.EqualTo(sourceIndex));
            Assert.That(bush.LobeIndex, Is.EqualTo(lobeIndex));
            Assert.That(bush.Position.x, Is.EqualTo(x).Within(0.001d));
            Assert.That(bush.Position.y, Is.EqualTo(y).Within(0.001d));
            Assert.That(bush.Position.z, Is.EqualTo(z).Within(0.001d));
            Assert.That(bush.RotationRadians.y, Is.EqualTo(yaw).Within(0.00001d));
            Assert.That(bush.RotationRadians.x, Is.EqualTo(pitch).Within(0.00001d));
            Assert.That(bush.RotationRadians.z, Is.EqualTo(roll).Within(0.00001d));
            Assert.That(bush.Scale.x, Is.EqualTo(width).Within(0.00001d));
            Assert.That(bush.Scale.y, Is.EqualTo(height).Within(0.00001d));
            Assert.That(bush.Scale.z, Is.EqualTo(depth).Within(0.00001d));
            Assert.That(bush.ColorIndex, Is.EqualTo(colorIndex));
        }

        private static void AssertFern(
            WofSurvivalFernRecord fern,
            int sourceIndex,
            double x,
            double y,
            double z,
            double yaw,
            double tilt,
            double width,
            double height,
            int colorIndex)
        {
            Assert.That(fern.SourceIndex, Is.EqualTo(sourceIndex));
            Assert.That(fern.Position.x, Is.EqualTo(x).Within(0.001d));
            Assert.That(fern.Position.y, Is.EqualTo(y).Within(0.001d));
            Assert.That(fern.Position.z, Is.EqualTo(z).Within(0.001d));
            Assert.That(fern.RotationRadians.y, Is.EqualTo(yaw).Within(0.00001d));
            Assert.That(fern.RotationRadians.x, Is.EqualTo(tilt).Within(0.00001d));
            Assert.That(fern.Scale.x, Is.EqualTo(width).Within(0.00001d));
            Assert.That(fern.Scale.y, Is.EqualTo(height).Within(0.00001d));
            Assert.That(fern.ColorIndex, Is.EqualTo(colorIndex));
        }

        private static void AssertDetail(
            WofSurvivalDetailScatterRecord record,
            int sourceIndex,
            WofSurvivalDetailScatterKind kind,
            WofSurvivalBiome biome,
            double x,
            double y,
            double z,
            double scale,
            double variant)
        {
            Assert.That(record.SourceIndex, Is.EqualTo(sourceIndex));
            Assert.That(record.Kind, Is.EqualTo(kind));
            Assert.That(record.Biome, Is.EqualTo(biome));
            Assert.That(record.Position.x, Is.EqualTo(x).Within(0.001d));
            Assert.That(record.Position.y, Is.EqualTo(y).Within(0.001d));
            Assert.That(record.Position.z, Is.EqualTo(z).Within(0.001d));
            Assert.That(record.Scale, Is.EqualTo(scale).Within(0.00001d));
            Assert.That(record.Variant, Is.EqualTo(variant).Within(0.00001d));
        }

        private static void AssertVector(Vector3 actual, double x, double y, double z)
        {
            Assert.That(actual.x, Is.EqualTo(x).Within(0.0001d));
            Assert.That(actual.y, Is.EqualTo(y).Within(0.0001d));
            Assert.That(actual.z, Is.EqualTo(z).Within(0.0001d));
        }
    }
}
