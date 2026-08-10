using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
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

        [Test]
        public void EmptyBlockedVillageGrassBuildDoesNotRestartEveryFrame()
        {
            Assert.That(WofSurvivalBotwGrassRuntime.ShouldStartBuild(false, false, 0f), Is.True);
            Assert.That(WofSurvivalBotwGrassRuntime.ShouldStartBuild(false, true, 0f), Is.False);
            Assert.That(WofSurvivalBotwGrassRuntime.ShouldStartBuild(false, true, 64f), Is.True);
            Assert.That(WofSurvivalBotwGrassRuntime.ShouldStartBuild(true, true, 128f), Is.False);
        }

        [Test]
        public void GrassDistributionUsesIndependentIrregularAxesInsteadOfVisibleSpiralRows()
        {
            var first = WofSurvivalBotwGrassRuntime.GetIrregularDistributionPoint(100, 24, -16);
            var repeated = WofSurvivalBotwGrassRuntime.GetIrregularDistributionPoint(100, 24, -16);
            var next = WofSurvivalBotwGrassRuntime.GetIrregularDistributionPoint(101, 24, -16);

            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(first.x, Is.InRange(-1f, 1f));
            Assert.That(first.y, Is.InRange(-1f, 1f));
            Assert.That(Vector2.Distance(first, next), Is.GreaterThan(0.01f));
        }

        [Test]
        public void GrassRotationRootsToTheSlopeButRetainsNaturalVerticalGrowth()
        {
            var surfaceNormal = new Vector3(0.42f, 0.82f, -0.39f).normalized;
            var rotation = WofSurvivalBotwGrassRuntime.GetSurfaceAlignedRotation(surfaceNormal, 137f);
            var growth = rotation * Vector3.up;
            Assert.That(Vector3.Angle(growth, Vector3.up), Is.GreaterThan(0.1f));
            Assert.That(Vector3.Angle(growth, Vector3.up),
                Is.LessThan(Vector3.Angle(surfaceNormal, Vector3.up)));
            Assert.That(Vector3.Angle(growth, surfaceNormal),
                Is.LessThan(Vector3.Angle(Vector3.up, surfaceNormal)));
        }

        [Test]
        public void TerrainVertexNormalsInterpolateSmoothlyAcrossTriangleInteriors()
        {
            var first = new Vector3(-0.2f, 1f, 0f).normalized;
            var second = new Vector3(0.25f, 1f, 0.1f).normalized;
            var third = new Vector3(0f, 1f, -0.3f).normalized;
            var barycentric = new Vector3(0.2f, 0.35f, 0.45f);
            var expected = (first * barycentric.x + second * barycentric.y + third * barycentric.z).normalized;
            var actual = WofSurvivalBotwGrassRuntime.InterpolateSurfaceNormal(first, second, third, barycentric);
            Assert.That(Vector3.Angle(actual, expected), Is.LessThan(0.001f));
            Assert.That(actual, Is.Not.EqualTo(first));
            Assert.That(actual, Is.Not.EqualTo(second));
            Assert.That(actual, Is.Not.EqualTo(third));
        }

        [Test]
        public void GeneratedOpenWorldContainsEveryExactReactDenseBiomeTree()
        {
            Assert.That(WofSurvivalFoliageRuntime.ExactReactMeshCount, Is.EqualTo(24));
            Assert.That(WofSurvivalFoliageRuntime.ExactReactDenseTreeCount, Is.EqualTo(2591));
            Assert.That(AssetDatabase.FindAssets(
                    "t:Mesh",
                    new[] { "Assets/WOF/Generated/Geometry/SurvivalTerrain/Foliage" }).Length,
                Is.EqualTo(WofSurvivalFoliageRuntime.ExactReactMeshCount));

            var scene = EditorSceneManager.OpenScene(
                "Assets/WOF/Generated/Scenes/WofBootstrap.unity",
                OpenSceneMode.Single);
            var runtime = Object.FindFirstObjectByType<WofSurvivalFoliageRuntime>();
            Assert.That(runtime, Is.Not.Null);
            var serialized = new SerializedObject(runtime);
            Assert.That(serialized.FindProperty("meshes").arraySize,
                Is.EqualTo(WofSurvivalFoliageRuntime.ExactReactMeshCount));
            Assert.That(serialized.FindProperty("placements").arraySize,
                Is.EqualTo(WofSurvivalFoliageRuntime.ExactReactDenseTreeCount));
            Assert.That(scene.isLoaded, Is.True);
        }
    }
}
