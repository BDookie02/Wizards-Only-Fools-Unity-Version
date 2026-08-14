using System.Collections.Generic;
using System.IO;
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
        public void ReactHorizonCylinderConstantsAndOpenGeometryRemainExact()
        {
            Assert.That(WofSurvivalSkyRuntime.HorizonRadius, Is.EqualTo(2816f));
            Assert.That(WofSurvivalSkyRuntime.HorizonHeight, Is.EqualTo(2200f));
            Assert.That(WofSurvivalSkyRuntime.HorizonY, Is.EqualTo(330f));
            Assert.That(WofSurvivalSkyRuntime.HorizonSegments, Is.EqualTo(96));

            var mesh = WofSurvivalSkyRuntime.CreateHorizonCylinderMesh(
                WofSurvivalSkyRuntime.HorizonRadius,
                WofSurvivalSkyRuntime.HorizonHeight,
                WofSurvivalSkyRuntime.HorizonSegments);
            try
            {
                Assert.That(mesh.name, Is.EqualTo("ReactHorizonCylinderMesh"));
                Assert.That(mesh.vertexCount, Is.EqualTo((WofSurvivalSkyRuntime.HorizonSegments + 1) * 2));
                Assert.That(mesh.triangles, Has.Length.EqualTo(WofSurvivalSkyRuntime.HorizonSegments * 6));
                Assert.That(mesh.uv[0], Is.EqualTo(Vector2.zero));
                Assert.That(mesh.uv[^1], Is.EqualTo(Vector2.one));
                Assert.That(Vector3.Distance(mesh.vertices[0], mesh.vertices[^2]), Is.LessThan(0.01f));
                Assert.That(mesh.bounds.size.y, Is.EqualTo(WofSurvivalSkyRuntime.HorizonHeight).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ReactCustomLobbyUsesTheSeparateClassicSkyContract()
        {
            var desktop = WofSurvivalSkyRuntime.ResolvePresentationLayout(false, false);
            Assert.That(desktop.HorizonRadius, Is.EqualTo(400f));
            Assert.That(desktop.HorizonHeight, Is.EqualTo(250f));
            Assert.That(desktop.HorizonY, Is.EqualTo(40f));
            Assert.That(desktop.HorizonSegments, Is.EqualTo(64));
            Assert.That(desktop.FollowsCamera, Is.False);
            Assert.That(desktop.FogEnabled, Is.False);
            Assert.That(desktop.SurvivalSpritesVisible, Is.False);
            Assert.That(desktop.ClassicAtmosphereVisible, Is.True);

            var mobile = WofSurvivalSkyRuntime.ResolvePresentationLayout(false, true);
            Assert.That(mobile.ClassicAtmosphereVisible, Is.False);

            var survival = WofSurvivalSkyRuntime.ResolvePresentationLayout(true, false);
            Assert.That(survival.HorizonRadius, Is.EqualTo(2816f));
            Assert.That(survival.HorizonHeight, Is.EqualTo(2200f));
            Assert.That(survival.HorizonY, Is.EqualTo(330f));
            Assert.That(survival.HorizonSegments, Is.EqualTo(96));
            Assert.That(survival.FollowsCamera, Is.True);
            Assert.That(survival.FogEnabled, Is.True);
            Assert.That(survival.SurvivalSpritesVisible, Is.True);
            Assert.That(survival.ClassicAtmosphereVisible, Is.False);
        }

        [Test]
        public void ClassicSkyShaderRetainsTheExactReactAtmosphereInputs()
        {
            const string shaderPath = "Assets/WOF/Shaders/WofSkyUnlit.shader";
            var source = File.ReadAllText(shaderPath);
            StringAssert.Contains("_UseClassicAtmosphere", source);
            StringAssert.Contains("_ClassicTurbidity", source);
            StringAssert.Contains("_ClassicRayleigh", source);
            StringAssert.Contains("_ClassicMieCoefficient", source);
            StringAssert.Contains("_ClassicMieDirectionalG", source);
            StringAssert.Contains("0.9999566769464484", source);

            const string runtimePath = "Assets/WOF/Runtime/World/WofSurvivalSkyRuntime.cs";
            var runtime = File.ReadAllText(runtimePath);
            StringAssert.Contains("SetFloat(\"_ClassicTurbidity\", 0.3f)", runtime);
            StringAssert.Contains("SetFloat(\"_ClassicRayleigh\", 0.5f)", runtime);
            StringAssert.Contains("SetFloat(\"_ClassicMieCoefficient\", 0.005f)", runtime);
            StringAssert.Contains("SetFloat(\"_ClassicMieDirectionalG\", 0.8f)", runtime);
            StringAssert.Contains("new Vector4(50f, 20f, 50f, 0f)", runtime);
        }

        [Test]
        public void ReactHorizonTextureRetainsSeededLayersTreesAndWrapping()
        {
            var texture = WofSurvivalSkyTextures.CreateHorizonHills();
            try
            {
                Assert.That(texture.width, Is.EqualTo(2048));
                Assert.That(texture.height, Is.EqualTo(1024));
                Assert.That(texture.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
                Assert.That(texture.wrapModeV, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
                Assert.That(texture.mipmapCount, Is.GreaterThan(1));
                Assert.That(texture.GetPixel(1024, 1023).a, Is.EqualTo(0f).Within(0.001f));
                Assert.That(texture.GetPixel(1024, 0), Is.EqualTo((Color)new Color32(71, 137, 45, 255)));
                Assert.That(System.Array.Exists(
                    texture.GetPixels32(),
                    pixel => pixel.Equals(new Color32(53, 111, 34, 255))), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
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
        public void HorizonTintMatchesReactDayAndNightCycleColors()
        {
            var day = WofSurvivalSkyRuntime.EvaluateHorizonTint(
                WofSurvivalSkyRuntime.Evaluate(WofSurvivalSkyRuntime.ForcedDaySeconds));
            var night = WofSurvivalSkyRuntime.EvaluateHorizonTint(
                WofSurvivalSkyRuntime.Evaluate(WofSurvivalSkyRuntime.ForcedNightSeconds));
            Assert.That(day, Is.EqualTo(Color.white));
            Assert.That(night.r, Is.EqualTo(0x4b / 255f).Within(0.001f));
            Assert.That(night.g, Is.EqualTo(0x54 / 255f).Within(0.001f));
            Assert.That(night.b, Is.EqualTo(0x7c / 255f).Within(0.001f));
        }

        [Test]
        public void SharedSkyShaderKeepsReactHorizonFogOptInAndIsolated()
        {
            const string shaderPath = "Assets/WOF/Shaders/WofSkyUnlit.shader";
            var source = File.ReadAllText(shaderPath);
            StringAssert.Contains("_UseFog (\"Use Fog\", Float) = 0", source);
            StringAssert.Contains("#pragma multi_compile_fog", source);
            StringAssert.Contains("ComputeFogFactor(output.positionHCS.z)", source);
            StringAssert.Contains("MixFog(color.rgb, input.fogFactor)", source);

            const string runtimePath = "Assets/WOF/Runtime/World/WofSurvivalSkyRuntime.cs";
            var runtime = File.ReadAllText(runtimePath);
            StringAssert.Contains("_horizonMaterial.SetFloat(\"_UseFog\", 1f);", runtime);
            Assert.That(runtime.Split(new[] { "SetFloat(\"_UseFog\"" }, System.StringSplitOptions.None), Has.Length.EqualTo(2));
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
        public void BotwGrassShaderAppliesTheSynchronizedSurvivalCycleTint()
        {
            const string shaderPath = "Assets/WOF/Shaders/WofBotwGrass.shader";
            var source = File.ReadAllText(shaderPath);
            StringAssert.Contains("half4 _WofSurvivalTerrainTint;", source);
            StringAssert.Contains("* cycleTint", source);

            var day = WofSurvivalSkyRuntime.EvaluateTerrainTint(
                WofSurvivalSkyRuntime.Evaluate(WofSurvivalSkyRuntime.ForcedDaySeconds));
            var night = WofSurvivalSkyRuntime.EvaluateTerrainTint(
                WofSurvivalSkyRuntime.Evaluate(WofSurvivalSkyRuntime.ForcedNightSeconds));
            Assert.That(day.maxColorComponent, Is.EqualTo(1f).Within(0.001f));
            Assert.That(night.maxColorComponent, Is.LessThan(0.35f));
        }

        [Test]
        public void AstralVeilUsesTheExactReactCanvasDimensionsAndVisibleDetail()
        {
            var texture = WofSurvivalSkyTextures.CreateAstralVeil();
            try
            {
                Assert.That(texture.width, Is.EqualTo(192));
                Assert.That(texture.height, Is.EqualTo(128));
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Bilinear));
                Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(texture.GetPixel(96, 64).a, Is.GreaterThan(0.01f));
                Assert.That(texture.GetPixel(0, 0).a, Is.GreaterThan(0.25f));
                Assert.That(System.Array.Exists(
                    texture.GetPixels(),
                    pixel => pixel.r > 0.8f && pixel.b > 0.8f && pixel.a > 0.08f), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
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
            Assert.That(WofSurvivalBotwGrassRuntime.FlowerStemMinimum, Is.InRange(1.3f, 1.5f));
            Assert.That(WofSurvivalBotwGrassRuntime.FlowerStemMaximum,
                Is.GreaterThan(WofSurvivalBotwGrassRuntime.FlowerStemMinimum));
            Assert.That(WofSurvivalBotwGrassRuntime.FlowerBloomMinimum, Is.InRange(0.6f, 0.7f));
            Assert.That(WofSurvivalBotwGrassRuntime.FlowerBloomMaximum,
                Is.GreaterThan(WofSurvivalBotwGrassRuntime.FlowerBloomMinimum));
            Assert.That(WofSurvivalBotwGrassRuntime.BladeAlphaCutoff, Is.EqualTo(0.14f).Within(0.001f));
            Assert.That(WofSurvivalBotwGrassRuntime.MaxCandidatesPerFrameDesktop, Is.LessThanOrEqualTo(384));
            Assert.That(WofSurvivalBotwGrassRuntime.MaxCandidatesPerFrameMobile, Is.LessThanOrEqualTo(256));
            Assert.That(WofSurvivalBotwGrassRuntime.DesktopBuildBudgetMilliseconds, Is.LessThanOrEqualTo(4d));
            Assert.That(WofSurvivalBotwGrassRuntime.MobileBuildBudgetMilliseconds, Is.LessThanOrEqualTo(2d));
            Assert.That(WofSurvivalBotwGrassRuntime.BuildBudgetCheckInterval, Is.LessThanOrEqualTo(4));
            Assert.That(WofSurvivalBotwGrassRuntime.GrassCardsPerTuft, Is.EqualTo(4));
            Assert.That(WofSurvivalBotwGrassRuntime.BladeTextureInfluence, Is.EqualTo(1f));
            Assert.That(WofSurvivalBotwGrassRuntime.SlopeUprightBlend, Is.InRange(0.75f, 0.9f));
            Assert.That(WofSurvivalBotwGrassRuntime.TerrainGrassDetailStrength, Is.InRange(0.1f, 0.18f));
            Assert.That(WofSurvivalBotwGrassRuntime.TerrainGrassDetailScale, Is.InRange(0.15f, 0.3f));
            Assert.That(WofSurvivalBotwGrassRuntime.IsAuthoredSurfaceBlocked(
                WofMountainVillageLayout.WorldOrigin.x,
                WofMountainVillageLayout.WorldOrigin.z), Is.True);
            Assert.That(WofSurvivalBotwGrassRuntime.IsAuthoredSurfaceBlocked(
                WofMountainVillageLayout.WorldOrigin.x + WofMountainVillageLayout.PerimeterShoulderRadius + 20f,
                WofMountainVillageLayout.WorldOrigin.z), Is.False);
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
        public void AmbientLifeSkipsInstancedDrawsOnHeadlessGraphicsDevices()
        {
            Assert.That(WofSurvivalAmbientLifeRuntime.ShouldDrawInstances(false, 64), Is.False);
            Assert.That(WofSurvivalAmbientLifeRuntime.ShouldDrawInstances(true, 0), Is.False);
            Assert.That(WofSurvivalAmbientLifeRuntime.ShouldDrawInstances(true, 64), Is.True);
        }

        [Test]
        public void GrassDistributionUsesDeterministicHashScatterWithoutSpiralRows()
        {
            var first = WofSurvivalBotwGrassRuntime.GetIrregularDistributionPoint(100, 24, -16);
            var repeated = WofSurvivalBotwGrassRuntime.GetIrregularDistributionPoint(100, 24, -16);
            var next = WofSurvivalBotwGrassRuntime.GetIrregularDistributionPoint(101, 24, -16);

            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(first.x, Is.InRange(-1f, 1f));
            Assert.That(first.y, Is.InRange(-1f, 1f));
            Assert.That(first.magnitude, Is.LessThanOrEqualTo(1.01f));
            Assert.That(Vector2.Distance(first, next), Is.GreaterThan(0.01f));

            var centroid = Vector2.zero;
            var meanRadiusSquared = 0f;
            const int sampleCount = 2048;
            for (var index = 0; index < sampleCount; index++)
            {
                var point = WofSurvivalBotwGrassRuntime.GetIrregularDistributionPoint(index, 24, -16);
                centroid += point;
                meanRadiusSquared += point.sqrMagnitude;
            }
            centroid /= sampleCount;
            meanRadiusSquared /= sampleCount;
            Assert.That(centroid.magnitude, Is.LessThan(0.06f));
            Assert.That(meanRadiusSquared, Is.InRange(0.45f, 0.55f));
        }

        [Test]
        public void GrassRotationRootsFlushToTheSlopeLikeTheReactField()
        {
            var surfaceNormal = new Vector3(0.42f, 0.82f, -0.39f).normalized;
            var rotation = WofSurvivalBotwGrassRuntime.GetSurfaceAlignedRotation(surfaceNormal, 137f);
            var growth = rotation * Vector3.up;
            Assert.That(Vector3.Angle(growth, surfaceNormal), Is.LessThan(0.001f));
        }

        [Test]
        public void GrassTuftRestoresTheExactFourCardReactBotwCluster()
        {
            var mesh = WofSurvivalBotwGrassRuntime.CreateGrassClusterMesh();
            try
            {
                Assert.That(mesh.name, Is.EqualTo("ReactBotwGrassCluster"));
                Assert.That(WofSurvivalBotwGrassRuntime.GrassCardsPerTuft, Is.EqualTo(4));
                Assert.That(mesh.vertexCount, Is.EqualTo(
                    WofSurvivalBotwGrassRuntime.GrassCardsPerTuft *
                    WofSurvivalBotwGrassRuntime.GrassCardVertices));
                Assert.That(mesh.triangles, Has.Length.EqualTo(
                    WofSurvivalBotwGrassRuntime.GrassCardsPerTuft *
                    WofSurvivalBotwGrassRuntime.GrassCardTriangles * 3));
                var vertices = mesh.vertices;
                var rootCenters = new List<Vector3>();
                for (var card = 0; card < WofSurvivalBotwGrassRuntime.GrassCardsPerTuft; card++)
                {
                    var offset = card * WofSurvivalBotwGrassRuntime.GrassCardVertices;
                    Assert.That(vertices[offset].y, Is.EqualTo(0f).Within(0.001f));
                    Assert.That(vertices[offset + 1].y, Is.EqualTo(0f).Within(0.001f));
                    Assert.That(vertices[offset + 2].y, Is.EqualTo(1f).Within(0.001f));
                    Assert.That(vertices[offset + 3].y, Is.EqualTo(vertices[offset + 2].y).Within(0.001f));
                    Assert.That(Vector3.Distance(vertices[offset], vertices[offset + 1]),
                        Is.EqualTo(card % 2 == 0 ? 1.44f : 1.16f).Within(0.001f));
                    Assert.That(Vector3.Distance(vertices[offset + 2], vertices[offset + 3]),
                        Is.LessThan(Vector3.Distance(vertices[offset], vertices[offset + 1])));
                    rootCenters.Add((vertices[offset] + vertices[offset + 1]) * 0.5f);
                }
                Assert.That(rootCenters, Has.All.EqualTo(Vector3.zero));
                Assert.That(mesh.uv[0].y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(mesh.uv[2].y, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void GrassTuftKeepsTheExactReactRootToTipColorGradient()
        {
            var mesh = WofSurvivalBotwGrassRuntime.CreateGrassClusterMesh();
            try
            {
                var colors = mesh.colors;
                var root = colors[0];
                var tip = colors[2];
                Assert.That(root.g, Is.LessThan(tip.g));
                Assert.That(root.r, Is.EqualTo(0x85 / 255f).Within(0.001f));
                Assert.That(root.g, Is.EqualTo(0xd2 / 255f).Within(0.001f));
                Assert.That(root.b, Is.EqualTo(0x4a / 255f).Within(0.001f));
                Assert.That(tip.r, Is.EqualTo(0xf0 / 255f).Within(0.001f));
                Assert.That(tip.g, Is.EqualTo(1f).Within(0.001f));
                Assert.That(tip.b, Is.EqualTo(0x90 / 255f).Within(0.001f));
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
        public void DesertSurroundUsesRecognizableCactiAcrossExactlyFiveNeighborChunks()
        {
            var placements = WofSurvivalDesertCactusRuntime.CreatePlacements();
            Assert.That(placements, Has.Length.EqualTo(WofSurvivalDesertCactusRuntime.TotalCactusCount));
            var chunks = new HashSet<string>();
            foreach (var placement in placements)
            {
                chunks.Add($"{placement.ChunkX}:{placement.ChunkZ}");
                Assert.That((placement.ChunkX, placement.ChunkZ), Is.Not.EqualTo((4, -4)));
                Assert.That(WofSurvivalTerrainMath.IsDesertVillageExpansionChunk(
                    placement.ChunkX, placement.ChunkZ), Is.True);
                Assert.That(WofSurvivalTerrainMath.GetDesertVillageExpansionMaskAtWorld(
                    placement.Position.x, placement.Position.z), Is.EqualTo(1d).Within(0.000001d));
                Assert.That(placement.Scale, Is.InRange(1.28f, 2.43f));
            }
            Assert.That(chunks, Has.Count.EqualTo(WofSurvivalDesertCactusRuntime.SurroundingChunkCount));

            var mesh = WofSurvivalDesertCactusRuntime.CreateCactusMesh();
            try
            {
                Assert.That(mesh.name, Is.EqualTo("ReactDesertSaguaroCactus"));
                Assert.That(mesh.vertexCount, Is.GreaterThan(140));
                Assert.That(mesh.triangles.Length, Is.GreaterThan(500));
                Assert.That(mesh.bounds.max.y, Is.GreaterThan(11.4f));
                Assert.That(mesh.bounds.size.x, Is.GreaterThan(6.8f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void DesertExpansionUsesCactiInsteadOfDenseBiomeTrees()
        {
            var placement = new WofSurvivalFoliagePlacement
            {
                x = 3f * WofSurvivalTerrainMath.BlockSize,
                z = -4f * WofSurvivalTerrainMath.BlockSize,
                meshIndex = 0
            };
            Assert.That(WofSurvivalFoliageRuntime.ShouldRenderPlacement(placement), Is.False);
            placement.meshIndex = 8;
            Assert.That(WofSurvivalFoliageRuntime.ShouldRenderPlacement(placement), Is.False);
            placement.x = 0f;
            placement.z = 0f;
            placement.meshIndex = 0;
            Assert.That(WofSurvivalFoliageRuntime.ShouldRenderPlacement(placement), Is.True);
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
