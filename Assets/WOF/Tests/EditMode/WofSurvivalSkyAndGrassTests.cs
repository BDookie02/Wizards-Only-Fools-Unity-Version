using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests.EditMode
{
    public sealed class WofSurvivalSkyAndGrassTests
    {
        [Test]
        public void ReactSkyCycleConstantsRemainExact()
        {
            Assert.That(WofSurvivalSkyRuntime.CycleSeconds, Is.EqualTo(600f));
            Assert.That(WofSurvivalSkyRuntime.ForcedDaySeconds, Is.EqualTo(42f));
            Assert.That(WofSurvivalSkyRuntime.ForcedNightSeconds, Is.EqualTo(342f));
        }

        [Test]
        public void ForcedDayAndNightResolveToOppositeLightingStates()
        {
            var day = WofSurvivalSkyRuntime.Evaluate(WofSurvivalSkyRuntime.ForcedDaySeconds);
            var night = WofSurvivalSkyRuntime.Evaluate(WofSurvivalSkyRuntime.ForcedNightSeconds);
            Assert.That(day.DayAmount, Is.GreaterThan(0.99f));
            Assert.That(day.NightAmount, Is.LessThan(0.01f));
            Assert.That(night.DayAmount, Is.LessThan(0.01f));
            Assert.That(night.NightAmount, Is.GreaterThan(0.99f));
        }

        [Test]
        public void TerrainTintMatchesExactReactDayAndNightColors()
        {
            var day = WofSurvivalSkyRuntime.EvaluateTerrainTint(
                WofSurvivalSkyRuntime.Evaluate(WofSurvivalSkyRuntime.ForcedDaySeconds));
            var night = WofSurvivalSkyRuntime.EvaluateTerrainTint(
                WofSurvivalSkyRuntime.Evaluate(WofSurvivalSkyRuntime.ForcedNightSeconds));
            Assert.That(day.r, Is.EqualTo(1f).Within(0.001f));
            Assert.That(day.g, Is.EqualTo(1f).Within(0.001f));
            Assert.That(day.b, Is.EqualTo(1f).Within(0.001f));
            Assert.That(night.r, Is.EqualTo(0x3f / 255f).Within(0.001f));
            Assert.That(night.g, Is.EqualTo(0x4f / 255f).Within(0.001f));
            Assert.That(night.b, Is.EqualTo(0x45 / 255f).Within(0.001f));
        }

        [Test]
        public void BotwGrassDensityAndStreamingConstantsMatchReact()
        {
            Assert.That(WofSurvivalBotwGrassRuntime.Radius, Is.EqualTo(224f));
            Assert.That(WofSurvivalBotwGrassRuntime.EdgeFade, Is.EqualTo(34f));
            Assert.That(WofSurvivalBotwGrassRuntime.CenterStep, Is.EqualTo(96f));
            Assert.That(WofSurvivalBotwGrassRuntime.RecenterDistance, Is.EqualTo(64f));
            Assert.That(WofSurvivalBotwGrassRuntime.BladeCount, Is.EqualTo(56000));
            Assert.That(WofSurvivalBotwGrassRuntime.FlowerCount, Is.EqualTo(760));
            Assert.That(WofSurvivalBotwGrassRuntime.CandidateCount, Is.EqualTo(71680));
        }

        [Test]
        public void GrassHashIsDeterministicAndBounded()
        {
            var first = WofSurvivalBotwGrassRuntime.Hash01(12f, -8f, 1900f);
            var second = WofSurvivalBotwGrassRuntime.Hash01(12f, -8f, 1900f);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.GreaterThanOrEqualTo(0f));
            Assert.That(first, Is.LessThan(1f));
        }
    }
}
