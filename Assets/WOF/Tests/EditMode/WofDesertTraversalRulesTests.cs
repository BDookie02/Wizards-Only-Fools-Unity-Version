using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofDesertTraversalRulesTests
    {
        [Test]
        public void ExpansionIsExactlyCentralChunkPlusFiveSurroundingChunks()
        {
            Assert.That(WofDesertTraversalRules.CountExpansionChunks(), Is.EqualTo(6));
            for (var chunkX = 3; chunkX <= 5; chunkX++)
            {
                for (var chunkZ = -4; chunkZ <= -3; chunkZ++)
                {
                    Assert.That(
                        WofSurvivalTerrainMath.IsDesertVillageExpansionChunk(chunkX, chunkZ),
                        Is.True,
                        $"expected desert expansion at {chunkX}:{chunkZ}");
                }
            }
            Assert.That(WofSurvivalTerrainMath.IsDesertVillageExpansionChunk(2, -4), Is.False);
            Assert.That(WofSurvivalTerrainMath.IsDesertVillageExpansionChunk(6, -4), Is.False);
            Assert.That(WofSurvivalTerrainMath.IsDesertVillageExpansionChunk(4, -5), Is.False);
            Assert.That(WofSurvivalTerrainMath.IsDesertVillageExpansionChunk(4, -2), Is.False);
        }

        [Test]
        public void NorthRoadRouteClearsWellAndCrossesIntoExpansionChunk()
        {
            var route = WofDesertTraversalRules.BuildNorthGateRoute();
            var origin = WofDesertVillageLayout.WorldOrigin;

            Assert.That(route, Has.Length.EqualTo(10));
            Assert.That(route[0].z - origin.z, Is.EqualTo(-214f).Within(0.001f));
            Assert.That(route[2].x - origin.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(route[3].x - origin.x, Is.EqualTo(40f).Within(0.001f));
            Assert.That(route[4].x - origin.x, Is.EqualTo(40f).Within(0.001f));
            Assert.That(route[7].x - origin.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(route[7].z - origin.z, Is.EqualTo(214f).Within(0.001f));
            Assert.That(WofDesertTraversalRules.IsNorthExpansionPoint(route[^1]), Is.True);
        }

        [Test]
        public void RouteSegmentsRemainBoundedForProgressWatchdog()
        {
            var route = WofDesertTraversalRules.BuildNorthGateRoute();
            for (var index = 1; index < route.Length; index++)
            {
                Assert.That(
                    WofDesertTraversalRules.HorizontalDistance(route[index - 1], route[index]),
                    Is.InRange(35f, 125f),
                    $"segment {index - 1}->{index}");
            }
        }

        [Test]
        public void VillageFoundationMeetsEveryAdjacentChunkWithoutAHeightGap()
        {
            var baseHeight = WofDesertVillageLayout.ReactBaseHeight;
            var half = WofDesertVillageLayout.SurvivalBlockSize * 0.5d;

            Assert.That(
                WofSurvivalTerrainMath.GetTerrainHeight(4, -4, 0d, half),
                Is.EqualTo(baseHeight).Within(0.0001d));
            Assert.That(
                WofSurvivalTerrainMath.GetTerrainHeight(4, -3, 0d, -half),
                Is.EqualTo(baseHeight).Within(0.0001d));
            Assert.That(
                WofSurvivalTerrainMath.GetTerrainHeight(3, -4, half, 0d),
                Is.EqualTo(baseHeight).Within(0.0001d));
            Assert.That(
                WofSurvivalTerrainMath.GetTerrainHeight(5, -4, -half, 0d),
                Is.EqualTo(baseHeight).Within(0.0001d));
            Assert.That(
                WofSurvivalTerrainMath.GetTerrainHeight(4, -5, 0d, half),
                Is.EqualTo(baseHeight).Within(0.0001d));
        }

        [Test]
        public void FoundationFeatherIsFlatAtVillageAndEndsAtConfiguredDistance()
        {
            var origin = WofDesertVillageLayout.WorldOrigin;
            var half = WofDesertVillageLayout.SurvivalBlockSize * 0.5d;

            Assert.That(
                WofSurvivalTerrainMath.GetDesertVillageFoundationMaskAtWorld(origin.x, origin.z),
                Is.EqualTo(1d).Within(0.000001d));
            Assert.That(
                WofSurvivalTerrainMath.GetDesertVillageFoundationMaskAtWorld(origin.x, origin.z + half),
                Is.EqualTo(1d).Within(0.000001d));
            Assert.That(
                WofSurvivalTerrainMath.GetDesertVillageFoundationMaskAtWorld(origin.x, origin.z + half + 192d),
                Is.EqualTo(0d).Within(0.000001d));
        }

        [Test]
        public void NorthRoadRouteCrossesTheFeatherWithoutACliff()
        {
            var route = WofDesertTraversalRules.BuildNorthGateRoute();
            for (var index = 8; index < route.Length; index++)
            {
                Assert.That(
                    Mathf.Abs(route[index].y - route[index - 1].y),
                    Is.LessThan(3f),
                    $"height delta at route point {index}");
            }
        }

        [Test]
        public void RuntimeFoundationCoversTheFullAuthoredChunk()
        {
            var mesh = WofDesertVillageFoundationRuntime.BuildFoundationMeshForTests();
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(289));
                Assert.That(mesh.bounds.min.x, Is.EqualTo(1792f).Within(0.001f));
                Assert.That(mesh.bounds.max.x, Is.EqualTo(2304f).Within(0.001f));
                Assert.That(mesh.bounds.min.z, Is.EqualTo(-2304f).Within(0.001f));
                Assert.That(mesh.bounds.max.z, Is.EqualTo(-1792f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
