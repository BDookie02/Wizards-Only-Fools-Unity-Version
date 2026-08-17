using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofSwampTraversalRulesTests
    {
        [Test]
        public void RouteCrossesNorthRampCentralPlatformAndEastRamp()
        {
            var route = WofSwampTraversalRules.BuildNorthToEastRoute();
            var origin = WofSwampVillageLayout.WorldOrigin;

            Assert.That(route, Has.Length.EqualTo(11));
            Assert.That(route[0].z - origin.z, Is.LessThan(-284f));
            Assert.That(route[2].z - origin.z, Is.EqualTo(-216f).Within(0.001f));
            Assert.That(route[3].z - origin.z, Is.EqualTo(-107f).Within(0.001f));
            Assert.That(WofSwampTraversalRules.IsCentralPlatformApproach(route[4]), Is.True);
            Assert.That(route[5].x - origin.x, Is.EqualTo(30f).Within(0.001f));
            Assert.That(route[7].x - origin.x, Is.EqualTo(107f).Within(0.001f));
            Assert.That(route[8].x - origin.x, Is.EqualTo(216f).Within(0.001f));
            Assert.That(WofSwampTraversalRules.IsEastRampExit(route[^1]), Is.True);
        }

        [Test]
        public void RampEndpointsUseExactReactHighAndLowElevations()
        {
            var route = WofSwampTraversalRules.BuildNorthToEastRoute();
            var origin = WofSwampVillageLayout.WorldOrigin;
            var playerLift = 1.4f;

            Assert.That(route[0].y - origin.y, Is.EqualTo(WofSwampTraversalRules.RampLowY + playerLift).Within(0.001f));
            Assert.That(route[1].y - origin.y, Is.EqualTo((WofSwampTraversalRules.RampLowY + WofSwampVillageLayout.ReactPlatformY) * 0.5f + playerLift).Within(0.001f));
            Assert.That(route[2].y - origin.y, Is.EqualTo(WofSwampVillageLayout.ReactPlatformY + playerLift).Within(0.001f));
            Assert.That(route[^1].y - origin.y, Is.EqualTo(WofSwampTraversalRules.RampLowY + playerLift).Within(0.001f));
        }

        [Test]
        public void RouteSegmentsRemainBoundedForProgressWatchdog()
        {
            var route = WofSwampTraversalRules.BuildNorthToEastRoute();
            for (var index = 1; index < route.Length; index++)
            {
                Assert.That(
                    WofSwampTraversalRules.HorizontalDistance(route[index - 1], route[index]),
                    Is.InRange(30f, 110f),
                    $"segment {index - 1}->{index}");
            }
        }
    }
}
