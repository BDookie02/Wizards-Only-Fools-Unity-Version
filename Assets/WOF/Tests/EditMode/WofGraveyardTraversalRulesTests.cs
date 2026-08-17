using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofGraveyardTraversalRulesTests
    {
        [Test]
        public void ChapelRouteUsesSouthAndNorthWestReactOpenings()
        {
            var route = WofGraveyardTraversalRules.BuildChapelRoute();
            var origin = WofGraveyardVillageLayout.WorldOrigin;

            Assert.That(route, Has.Length.EqualTo(9));
            Assert.That(route[0].x - origin.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(route[0].z - origin.z, Is.GreaterThan(125f));
            Assert.That(route[2].x - origin.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(route[2].z - origin.z, Is.LessThan(82f));
            Assert.That(route[6].x - origin.x, Is.EqualTo(-33f).Within(0.001f));
            Assert.That(route[6].z - origin.z, Is.GreaterThan(-82f));
            Assert.That(route[^1].x - origin.x, Is.EqualTo(-33f).Within(0.001f));
            Assert.That(route[^1].z - origin.z, Is.LessThan(-125f));
        }

        [Test]
        public void ChapelRouteKeepsCenterAisleClearOfPewBanks()
        {
            var route = WofGraveyardTraversalRules.BuildChapelRoute();
            var origin = WofGraveyardVillageLayout.WorldOrigin;

            for (var index = 1; index <= 4; index++)
            {
                Assert.That(route[index].x - origin.x, Is.EqualTo(0f).Within(0.001f));
            }
            Assert.That(route[4].z - origin.z, Is.LessThan(-32f));
            Assert.That(route[5].x - origin.x, Is.LessThan(-17.2f));
            Assert.That(route[5].z - origin.z, Is.LessThan(-44f));
        }

        [Test]
        public void RouteSegmentsStayShortEnoughForProgressWatchdog()
        {
            var route = WofGraveyardTraversalRules.BuildChapelRoute();
            for (var index = 1; index < route.Length; index++)
            {
                Assert.That(
                    WofGraveyardTraversalRules.HorizontalDistance(route[index - 1], route[index]),
                    Is.InRange(20f, 61f),
                    $"segment {index - 1}->{index}");
            }
        }

        [Test]
        public void HorizontalSegmentDistanceClampsBeforeAndAfterEndpoints()
        {
            var from = new Vector3(0f, 10f, 0f);
            var to = new Vector3(0f, 20f, 10f);

            Assert.That(
                WofGraveyardTraversalRules.HorizontalDistanceToSegment(
                    new Vector3(3f, -100f, 5f),
                    from,
                    to),
                Is.EqualTo(3f).Within(0.0001f));
            Assert.That(
                WofGraveyardTraversalRules.HorizontalDistanceToSegment(
                    new Vector3(0f, 0f, -4f),
                    from,
                    to),
                Is.EqualTo(4f).Within(0.0001f));
            Assert.That(
                WofGraveyardTraversalRules.HorizontalDistanceToSegment(
                    new Vector3(0f, 0f, 14f),
                    from,
                    to),
                Is.EqualTo(4f).Within(0.0001f));
        }
    }
}
