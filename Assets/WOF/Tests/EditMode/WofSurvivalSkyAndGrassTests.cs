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
            Assert.That(WofSurvivalBotwGrassRuntime.FlowerStemMinimum, Is.InRange(1f, 1.1f));
            Assert.That(WofSurvivalBotwGrassRuntime.FlowerStemMaximum,
                Is.GreaterThan(WofSurvivalBotwGrassRuntime.FlowerStemMinimum));
            Assert.That(WofSurvivalBotwGrassRuntime.FlowerBloomMinimum, Is.InRange(0.5f, 0.65f));
            Assert.That(WofSurvivalBotwGrassRuntime.FlowerBloomMaximum,
                Is.GreaterThan(WofSurvivalBotwGrassRuntime.FlowerBloomMinimum));
            Assert.That(WofSurvivalBotwGrassRuntime.BladeAlphaCutoff, Is.EqualTo(0.14f).Within(0.001f));
            Assert.That(WofSurvivalBotwGrassRuntime.MaxCandidatesPerFrameDesktop, Is.LessThanOrEqualTo(384));
            Assert.That(WofSurvivalBotwGrassRuntime.MaxCandidatesPerFrameMobile, Is.LessThanOrEqualTo(256));
            Assert.That(WofSurvivalBotwGrassRuntime.DesktopBuildBudgetMilliseconds, Is.LessThanOrEqualTo(4d));
            Assert.That(WofSurvivalBotwGrassRuntime.MobileBuildBudgetMilliseconds, Is.LessThanOrEqualTo(2d));
            Assert.That(WofSurvivalBotwGrassRuntime.BuildBudgetCheckInterval, Is.LessThanOrEqualTo(4));
            Assert.That(WofSurvivalBotwGrassRuntime.CanopyLodNearDistance,
                Is.LessThan(WofSurvivalBotwGrassRuntime.CanopyLodFarDistance));
            Assert.That(WofSurvivalBotwGrassRuntime.CanopyFarScale, Is.InRange(3f, 5f));
            Assert.That(WofSurvivalBotwGrassRuntime.TerrainGrassDetailStrength, Is.InRange(0.2f, 0.35f));
            Assert.That(WofSurvivalBotwGrassRuntime.TerrainGrassDetailScale, Is.InRange(0.15f, 0.3f));
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
        public void GrassClusterRetainsAnUpwardCanopyFromOverheadViews()
        {
            var mesh = WofSurvivalBotwGrassRuntime.CreateGrassClusterMesh();
            try
            {
                var texturedVertexCount = WofSurvivalBotwGrassRuntime.GrassClusterCardCount * 4;
                Assert.That(mesh.vertexCount, Is.EqualTo(
                    texturedVertexCount + WofSurvivalBotwGrassRuntime.GrassClusterCanopyBladeCount * 3));
                Assert.That(mesh.uv2, Has.Length.EqualTo(mesh.vertexCount));
                Assert.That(mesh.triangles, Has.Length.EqualTo(
                    WofSurvivalBotwGrassRuntime.GrassClusterCardCount * 6 +
                    WofSurvivalBotwGrassRuntime.GrassClusterCanopyBladeCount * 3));

                var vertices = mesh.vertices;
                var flags = mesh.uv2;
                var canopyVertices = 0;
                var outwardTips = 0;
                for (var index = texturedVertexCount; index < vertices.Length; index++)
                {
                    if (flags[index].x > 0.5f) canopyVertices++;
                    if (vertices[index].y > 0.7f &&
                        new Vector2(vertices[index].x, vertices[index].z).magnitude > 0.3f)
                        outwardTips++;
                }

                Assert.That(canopyVertices, Is.EqualTo(WofSurvivalBotwGrassRuntime.GrassClusterCanopyBladeCount * 3));
                Assert.That(outwardTips, Is.EqualTo(WofSurvivalBotwGrassRuntime.GrassClusterCanopyBladeCount));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
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
            Assert.That(WofSurvivalFoliageRuntime.ExactReactDenseTreeCount, Is.EqualTo(2526));
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
            var foliageMaterial = serialized.FindProperty("foliageMaterial").objectReferenceValue as Material;
            Assert.That(foliageMaterial, Is.Not.Null);
            Assert.That(foliageMaterial.shader.name, Is.EqualTo("WOF/Instanced Foliage"));
            Assert.That(foliageMaterial.enableInstancing, Is.True);
            Assert.That(scene.isLoaded, Is.True);
        }
    }
}
