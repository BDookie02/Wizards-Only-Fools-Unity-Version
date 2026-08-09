using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofTreeHouseVillageLayoutTests
    {
        [Test]
        public void InventoryMatchesReactTreeHouseRuntime()
        {
            Assert.That(WofTreeHouseVillageLayout.Trees.Count, Is.EqualTo(5));
            Assert.That(WofTreeHouseVillageLayout.Houses.Count, Is.EqualTo(4));
            Assert.That(WofTreeHouseVillageLayout.InternalRopes.Count, Is.EqualTo(3));
            Assert.That(WofTreeHouseVillageLayout.Bridges.Count, Is.EqualTo(13));
        }

        [Test]
        public void PlayerSpawnMatchesReactSurvivalDefault()
        {
            Assert.That(WofTreeHouseVillageLayout.DefaultPlayerSpawn, Is.EqualTo(new Vector3(0f, 5f, 30f)));
            Assert.That(WofTreeHouseVillageLayout.DefaultPlayerYawDegrees, Is.EqualTo(180f));
        }

        [Test]
        public void FirstBalconyMatchesReactGoldenPosition()
        {
            var balcony = WofTreeHouseVillageLayout.GetHouseBalconyPosition(0, 0);

            Assert.That(balcony.x, Is.EqualTo(6.5f).Within(0.00001f));
            Assert.That(balcony.y, Is.EqualTo(11.5f).Within(0.00001f));
            Assert.That(balcony.z, Is.EqualTo(6.5f).Within(0.00001f));
        }

        [Test]
        public void RotatedSatelliteBalconyMatchesReactAxisAngleMath()
        {
            var balcony = WofTreeHouseVillageLayout.GetHouseBalconyPosition(1, 0);

            Assert.That(balcony.x, Is.EqualTo(33.413f).Within(0.002f));
            Assert.That(balcony.y, Is.EqualTo(11.5f).Within(0.00001f));
            Assert.That(balcony.z, Is.EqualTo(16.298f).Within(0.002f));
        }

        [Test]
        public void SpiralStaircaseMatchesReactEndpoints()
        {
            var steps = WofTreeHouseVillageLayout.BuildSpiralSteps();

            Assert.That(steps.Count, Is.EqualTo(WofTreeHouseVillageLayout.DesktopSpiralStepCount));
            Assert.That(steps[0].Position, Is.EqualTo(new Vector3(6.5f, 0f, 0f)));
            Assert.That(steps[^1].Position.x, Is.EqualTo(6.5f).Within(0.0001f));
            Assert.That(steps[^1].Position.y, Is.EqualTo(15f).Within(0.0001f));
            Assert.That(steps[^1].Position.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(steps[^1].YawRadians, Is.EqualTo(-Mathf.PI * 4f).Within(0.0001f));
        }

        [Test]
        public void MobileSpiralStaircaseMatchesReactCountAndEndpoints()
        {
            var steps = WofTreeHouseVillageLayout.BuildSpiralSteps(
                steps: WofTreeHouseVillageLayout.MobileSpiralStepCount);

            Assert.That(steps.Count, Is.EqualTo(16));
            Assert.That(steps[0].Position, Is.EqualTo(new Vector3(6.5f, 0f, 0f)));
            Assert.That(steps[^1].Position.x, Is.EqualTo(6.5f).Within(0.0001f));
            Assert.That(steps[^1].Position.y, Is.EqualTo(15f).Within(0.0001f));
            Assert.That(steps[^1].Position.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(steps[^1].YawRadians, Is.EqualTo(-Mathf.PI * 4f).Within(0.0001f));
        }

        [Test]
        public void RopeRungsAndSpanMatchReactRules()
        {
            var rungs = WofTreeHouseVillageLayout.BuildRopeRungs(3.9f);
            var span = WofTreeHouseVillageLayout.GetSpan(Vector3.zero, new Vector3(3f, 4f, 0f));

            Assert.That(rungs.Count, Is.EqualTo(3));
            Assert.That(rungs[0].Position.z, Is.EqualTo(-1.45f).Within(0.0001f));
            Assert.That(span.Length, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(span.Position, Is.EqualTo(new Vector3(1.5f, 2f, 0f)));
            var forward = span.Rotation * Vector3.forward;
            Assert.That(forward.x, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(forward.y, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(forward.z, Is.EqualTo(0f).Within(0.0001f));
        }


        [Test]
        public void MobileRopeRungsMatchReactReducedStepRule()
        {
            var rungs = WofTreeHouseVillageLayout.BuildRopeRungs(
                3.9f,
                WofTreeHouseVillageLayout.MobileRopeRungStep);

            Assert.That(rungs.Count, Is.EqualTo(1));
            Assert.That(rungs[0].Position.z, Is.EqualTo(-0.95f).Within(0.0001f));
        }
    }
}
