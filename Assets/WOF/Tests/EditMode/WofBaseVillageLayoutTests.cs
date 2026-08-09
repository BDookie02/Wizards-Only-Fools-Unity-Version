using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofBaseVillageLayoutTests
    {
        [Test]
        public void ClassicWorldLightMatchesReactDirectionAndIntensity()
        {
            var forward = WofGameWorldLightingLayout.GetDirectionalLightRotation() * Vector3.forward;
            var expected = -WofGameWorldLightingLayout.DirectionalLightPosition.normalized;

            Assert.That(Vector3.Distance(forward, expected), Is.LessThan(0.00001f));
            Assert.That(WofGameWorldLightingLayout.ClassicAmbientIntensity, Is.EqualTo(0.4f));
            Assert.That(WofGameWorldLightingLayout.ClassicDirectionalIntensity, Is.EqualTo(1.5f));
            Assert.That(WofGameWorldLightingLayout.GetClassicAmbientColor(), Is.EqualTo(Color.white * 0.4f));
        }

        [Test]
        public void MobileClassicWorldLightsMatchReactConfiguration()
        {
            Assert.That(WofGameWorldLightingLayout.MobileAmbientIntensity, Is.EqualTo(1.25f));
            Assert.That(WofGameWorldLightingLayout.MobileDirectionalIntensity, Is.EqualTo(2.55f));
            Assert.That(WofGameWorldLightingLayout.MobileHemisphereIntensity, Is.EqualTo(1.18f));
            Assert.That(WofGameWorldLightingLayout.MobileHemisphereSkyColor, Is.EqualTo(Color.white));
            Assert.That(ColorDistance(
                    WofGameWorldLightingLayout.MobileHemisphereGroundColor,
                    new Color32(163, 125, 82, 255)),
                Is.LessThan(0.00001f));

            var expectedSky = new Color(2.43f, 2.43f, 2.43f, 1f);
            var expectedGround = new Color(
                1.25f + 163f / 255f * 1.18f,
                1.25f + 125f / 255f * 1.18f,
                1.25f + 82f / 255f * 1.18f,
                1f);
            Assert.That(ColorDistance(WofGameWorldLightingLayout.GetMobileAmbientSkyColor(), expectedSky),
                Is.LessThan(0.00001f));
            Assert.That(ColorDistance(WofGameWorldLightingLayout.GetMobileAmbientGroundColor(), expectedGround),
                Is.LessThan(0.00001f));
        }

        [TestCase(0, 0, -0.5f)]
        [TestCase(0, 38, 0f)]
        [TestCase(0, 50, 0.5f)]
        [TestCase(0, 100, 1f)]
        [TestCase(0, 135, 1.5f)]
        [TestCase(0, 200, 2f)]
        [TestCase(30, 30, -1.5f)]
        [TestCase(80, 80, 1f)]
        [TestCase(100, 100, -1.5f)]
        [TestCase(200, 200, 2f)]
        public void TerrainHeightMatchesReactGoldenZones(double x, double z, float expected)
        {
            Assert.That(WofBaseVillageLayout.GetTerrainHeight(x, z), Is.EqualTo(expected));
        }

        [Test]
        public void HutPlacementsMatchReactGoldenInventory()
        {
            var placements = WofBaseVillageLayout.BuildHutPlacements();

            Assert.That(placements, Has.Count.EqualTo(307));
            Assert.That(placements.Count(item => item.HasPath), Is.EqualTo(45));
            Assert.That(placements.Count(item => item.HutType == WofHutType.Mushroom), Is.EqualTo(156));
            Assert.That(placements.Count(item => item.HutType == WofHutType.GrassMound), Is.EqualTo(57));
            Assert.That(placements.Count(item => item.HutType == WofHutType.Log), Is.EqualTo(54));
            Assert.That(placements.Count(item => item.HutType == WofHutType.DirtAndStone), Is.EqualTo(40));
        }

        [Test]
        public void FirstHutPlacementMatchesReactGoldenFixture()
        {
            var placement = WofBaseVillageLayout.BuildHutPlacements()[0];

            Assert.That(placement.X, Is.EqualTo(-224));
            Assert.That(placement.Y, Is.EqualTo(2f));
            Assert.That(placement.Z, Is.EqualTo(-224));
            Assert.That(placement.HutType, Is.EqualTo(WofHutType.Mushroom));
            Assert.That(placement.ColorIndex, Is.EqualTo(0));
            Assert.That(placement.YawRadians, Is.EqualTo(Mathf.PI).Within(0.00001f));
            Assert.That(placement.HasPath, Is.False);
        }

        [Test]
        public void RoadsAndBlockersRetainReactBoundarySemantics()
        {
            Assert.That(WofBaseVillageLayout.IsRoadCell(0, 180), Is.True);
            Assert.That(WofBaseVillageLayout.IsRoadCell(32, 100), Is.True);
            Assert.That(WofBaseVillageLayout.IsRoadCell(80, 80), Is.False);
            Assert.That(WofBaseVillageLayout.IsBlockingCell(240, 240), Is.False);
            Assert.That(WofBaseVillageLayout.IsBlockingCell(0, 0), Is.False);
        }

        [Test]
        public void CampfireRadiusUsesStrictReactBoundary()
        {
            var center = new Vector3(
                WofBaseVillageLayout.CampfireX,
                WofBaseVillageLayout.GetTerrainHeight(WofBaseVillageLayout.CampfireX, WofBaseVillageLayout.CampfireZ),
                WofBaseVillageLayout.CampfireZ);

            Assert.That(WofBaseVillageLayout.IsWithinCampfireDamageRadius(center + Vector3.right * 2.49f), Is.True);
            Assert.That(WofBaseVillageLayout.IsWithinCampfireDamageRadius(center + Vector3.right * 2.5f), Is.False);
        }

        private static float ColorDistance(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) +
                   Mathf.Abs(left.g - right.g) +
                   Mathf.Abs(left.b - right.b) +
                   Mathf.Abs(left.a - right.a);
        }
    }
}
