using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofChicagoTraversalRulesTests
    {
        [Test]
        public void RouteUsesTrafficSafeSidewalkAndBeanEntrance()
        {
            var route = WofChicagoTraversalRules.BuildBeanParkRoute();
            var origin = WofChicagoCityLayout.WorldOrigin;

            Assert.That(route, Has.Length.EqualTo(10));
            Assert.That(route[1].z - origin.z, Is.EqualTo(-214f).Within(0.001f));
            for (var index = 1; index <= 6; index++)
            {
                Assert.That(route[index].x - origin.x, Is.EqualTo(-65f).Within(0.001f));
            }
            Assert.That(route[7].x - origin.x, Is.EqualTo(-36f).Within(0.001f));
            Assert.That(route[7].z - origin.z, Is.EqualTo(150f).Within(0.001f));
            Assert.That(WofChicagoTraversalRules.IsBeanParkApproach(route[7]), Is.True);
            Assert.That(WofChicagoTraversalRules.IsNorthBoundary(route[^1]), Is.True);
        }

        [Test]
        public void ReactRoadCoordinateContractContainsOnlyFourRoads()
        {
            foreach (var coordinate in new[] { -150f, -75f, 75f, 150f })
            {
                Assert.That(WofChicagoTraversalRules.IsExactRoadCoordinate(coordinate), Is.True);
            }
            foreach (var coordinate in new[] { -214f, -36f, 0f, 118f, 214f })
            {
                Assert.That(WofChicagoTraversalRules.IsExactRoadCoordinate(coordinate), Is.False);
            }
        }

        [Test]
        public void RouteSegmentsRemainBoundedForProgressWatchdog()
        {
            var route = WofChicagoTraversalRules.BuildBeanParkRoute();
            for (var index = 1; index < route.Length; index++)
            {
                Assert.That(
                    WofChicagoTraversalRules.HorizontalDistance(route[index - 1], route[index]),
                    Is.InRange(28f, 78f),
                    $"segment {index - 1}->{index}");
            }
        }
    }
}
