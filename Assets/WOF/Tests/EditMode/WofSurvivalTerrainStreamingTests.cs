using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF.Tests
{
    public sealed class WofSurvivalTerrainStreamingTests
    {
        [Test]
        public void RuntimeMathMatchesReactStreamingOracle()
        {
            var document = LoadOracle();
            var oracle = document.streamingOracle;
            Assert.That(oracle.renderRadius, Is.EqualTo(WofSurvivalTerrainMath.RenderRadius));
            Assert.That(oracle.nearRadius, Is.EqualTo(WofSurvivalTerrainMath.NearRadius));
            Assert.That(oracle.collisionRadius, Is.EqualTo(WofSurvivalTerrainMath.CollisionRadius));
            Assert.That(oracle.centerHysteresis, Is.EqualTo(WofSurvivalTerrainMath.CenterHysteresis).Within(0.001d));

            foreach (var fixture in oracle.chunkCoordinates)
                Assert.That(WofSurvivalTerrainMath.GetChunkCoordinate(fixture.value), Is.EqualTo(fixture.chunk),
                    $"React chunk coordinate mismatch at {fixture.value}.");

            var offsets = WofSurvivalTerrainStreamingRuntime.GetOrderedOffsetsForTests();
            Assert.That(offsets.Length, Is.EqualTo(37));
            Assert.That(oracle.window.Length, Is.EqualTo(offsets.Length));
            for (var index = 0; index < offsets.Length; index++)
            {
                var expected = oracle.window[index];
                Assert.That(offsets[index].x, Is.EqualTo(expected.dx), $"Window x mismatch at {index}.");
                Assert.That(offsets[index].z, Is.EqualTo(expected.dz), $"Window z mismatch at {index}.");
                Assert.That(offsets[index].distance, Is.EqualTo(expected.distance), $"Window distance mismatch at {index}.");
                Assert.That(WofSurvivalTerrainMath.GetRenderSegments(expected.distance),
                    Is.EqualTo(expected.renderSegments), $"Render LOD mismatch at {index}.");
                Assert.That(WofSurvivalTerrainMath.GetCollisionSegments(expected.distance),
                    Is.EqualTo(expected.collisionSegments), $"Collision LOD mismatch at {index}.");
            }

            foreach (var fixture in oracle.chunks)
            {
                var biome = WofSurvivalTerrainMath.GetBiome(fixture.cx, fixture.cz);
                Assert.That(WofSurvivalTerrainMath.GetBiomeName(biome), Is.EqualTo(fixture.biome),
                    $"Biome mismatch at {fixture.cx}:{fixture.cz}.");
                Assert.That(WofSurvivalTerrainMath.HasRiver(fixture.cx, fixture.cz), Is.EqualTo(fixture.hasRiver),
                    $"River mismatch at {fixture.cx}:{fixture.cz}.");
                Assert.That(WofSurvivalTerrainMath.IsRiverVertical(fixture.cx, fixture.cz),
                    Is.EqualTo(fixture.riverVertical), $"River direction mismatch at {fixture.cx}:{fixture.cz}.");
                foreach (var sample in fixture.samples)
                {
                    var height = WofSurvivalTerrainMath.GetTerrainHeight(
                        fixture.cx, fixture.cz, sample.localX, sample.localZ);
                    Assert.That(height, Is.EqualTo(sample.height).Within(0.002d),
                        $"Height mismatch at {fixture.cx}:{fixture.cz} local {sample.localX},{sample.localZ}.");
                    var worldX = fixture.cx * (double)WofSurvivalTerrainMath.BlockSize + sample.localX;
                    var worldZ = fixture.cz * (double)WofSurvivalTerrainMath.BlockSize + sample.localZ;
                    var color = WofSurvivalTerrainMath.GetRenderedTerrainColor(worldX, worldZ, height);
                    Assert.That(color.r, Is.EqualTo(sample.colorR).Within(0.002f));
                    Assert.That(color.g, Is.EqualTo(sample.colorG).Within(0.002f));
                    Assert.That(color.b, Is.EqualTo(sample.colorB).Within(0.002f));
                }
            }
        }

        [Test]
        public void RuntimeMeshesUseReactRenderAndCollisionLods()
        {
            var centerRender = WofSurvivalTerrainStreamingRuntime.BuildTerrainMeshForTests(7, 4, 0, false);
            var nearRender = WofSurvivalTerrainStreamingRuntime.BuildTerrainMeshForTests(8, 4, 1, false);
            var farRender = WofSurvivalTerrainStreamingRuntime.BuildTerrainMeshForTests(9, 4, 2, false);
            var farCollision = WofSurvivalTerrainStreamingRuntime.BuildTerrainMeshForTests(9, 4, 2, true);
            var noCollision = WofSurvivalTerrainStreamingRuntime.BuildTerrainMeshForTests(10, 4, 3, true);
            try
            {
                AssertMesh(centerRender, 1089, 6144, true);
                AssertMesh(nearRender, 169, 864, true);
                AssertMesh(farRender, 25, 96, true);
                AssertMesh(farCollision, 1089, 6144, false);
                Assert.That(noCollision, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(centerRender);
                UnityEngine.Object.DestroyImmediate(nearRender);
                UnityEngine.Object.DestroyImmediate(farRender);
                UnityEngine.Object.DestroyImmediate(farCollision);
            }
        }

        [Test]
        public void GeneratedBootstrapInstallsOneConfiguredStreamingRuntimeWithoutSceneMutation()
        {
            const string scenePath = "Assets/WOF/Generated/Scenes/WofBootstrap.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var runtime = WofSurvivalTerrainStreamingRuntime.InstallIfNeeded();
                Assert.That(runtime, Is.Not.Null);
                var runtimes = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<WofSurvivalTerrainStreamingRuntime>(true))
                    .ToArray();
                Assert.That(runtimes.Length, Is.EqualTo(1));
                var serialized = new UnityEditor.SerializedObject(runtimes[0]);
                Assert.That(serialized.FindProperty("terrainMaterial").objectReferenceValue, Is.Not.Null);
                UnityEngine.Object.DestroyImmediate(runtime.gameObject);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void RecenterAndWorldExceptionsMatchReactRules()
        {
            Assert.That(WofSurvivalTerrainMath.RecenterCoordinate(0, 368.64d), Is.EqualTo(0));
            Assert.That(WofSurvivalTerrainMath.RecenterCoordinate(0, 368.641d), Is.EqualTo(1));
            Assert.That(WofSurvivalTerrainMath.RecenterCoordinate(0, -368.64d), Is.EqualTo(0));
            Assert.That(WofSurvivalTerrainMath.RecenterCoordinate(0, -368.641d), Is.EqualTo(-1));
            Assert.That(WofSurvivalTerrainMath.IsInsideBakedAtlas(-4, -4), Is.True);
            Assert.That(WofSurvivalTerrainMath.IsInsideBakedAtlas(7, 4), Is.False);
            Assert.That(WofSurvivalTerrainMath.IsAuthoredChunk(12, -12), Is.True);
            Assert.That(WofSurvivalTerrainMath.IsLilyRealmCenter(47, -49), Is.True);
            Assert.That(WofSurvivalTerrainMath.IsLilyRealmCenter(46, -48), Is.False);
        }

        private static void AssertMesh(Mesh mesh, int vertices, int indices, bool hasRenderData)
        {
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.EqualTo(vertices));
            Assert.That(mesh.triangles.Length, Is.EqualTo(indices));
            Assert.That(mesh.vertices.All(value => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z)), Is.True);
            Assert.That(mesh.normals.Length, Is.EqualTo(vertices));
            Assert.That(mesh.normals.All(value => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z)), Is.True);
            Assert.That(mesh.colors.Length, Is.EqualTo(hasRenderData ? vertices : 0));
            Assert.That(mesh.uv.Length, Is.EqualTo(hasRenderData ? vertices : 0));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static OracleDocument LoadOracle()
        {
            var path = Path.GetFullPath("Assets/WOF/Art/Generated/React/SurvivalTerrain/base-region.json");
            var document = JsonUtility.FromJson<OracleDocument>(File.ReadAllText(path));
            Assert.That(document?.streamingOracle, Is.Not.Null);
            return document;
        }

        [Serializable]
        private sealed class OracleDocument { public StreamingOracle streamingOracle; }
        [Serializable]
        private sealed class StreamingOracle
        {
            public int renderRadius;
            public int nearRadius;
            public int collisionRadius;
            public double centerHysteresis;
            public ChunkCoordinate[] chunkCoordinates;
            public WindowChunk[] window;
            public ChunkFixture[] chunks;
        }
        [Serializable]
        private sealed class ChunkCoordinate { public double value; public int chunk; }
        [Serializable]
        private sealed class WindowChunk
        {
            public int dx;
            public int dz;
            public int distance;
            public int renderSegments;
            public int collisionSegments;
        }
        [Serializable]
        private sealed class ChunkFixture
        {
            public int cx;
            public int cz;
            public string biome;
            public bool hasRiver;
            public bool riverVertical;
            public TerrainSample[] samples;
        }
        [Serializable]
        private sealed class TerrainSample
        {
            public double localX;
            public double localZ;
            public double height;
            public float colorR;
            public float colorG;
            public float colorB;
        }
    }
}
