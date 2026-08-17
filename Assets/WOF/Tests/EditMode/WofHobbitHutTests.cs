using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofHobbitHutTests
    {
        [Test]
        public void RecordsMatchIndependentReactOracleInEverySupportedBiome()
        {
            var plains = WofSurvivalHobbitHutRules.MakeChunk(2, -1);
            Assert.That(plains, Has.Length.EqualTo(1));
            AssertRecord(plains[0], 1, WofSurvivalBiome.Plains,
                952.7664478108287d, 21.574429797477073d, -380.4389726886153d,
                1.0172927494577606d, 1.2909211089181192d, 0.5384004570150864d);

            var jungle = WofSurvivalHobbitHutRules.MakeChunk(-4, 0);
            Assert.That(jungle, Has.Length.EqualTo(1));
            AssertRecord(jungle[0], 0, WofSurvivalBiome.Jungle,
                -2129.372651338875d, 56.84271649743468d, -123.99846867710352d,
                2.8390064275789273d, 1.3972691371655674d, 0.8345903094732421d);

            var mushroom = WofSurvivalHobbitHutRules.MakeChunk(2, -4);
            Assert.That(mushroom, Has.Length.EqualTo(1));
            // X/Z, index, yaw, scale, and variant come from React. Unity's approved
            // rendered mushroom surface is exactly 0.0182489 m above the source sample.
            AssertRecord(mushroom[0], 2, WofSurvivalBiome.Mushroom,
                1154.6182071506978d, 32.0267181d, -1917.2537604880333d,
                1.032182900526606d, 1.4318345999675512d, 0.08319656629464589d);
        }

        [Test]
        public void VisibilityBiomeSpawnAndAuthoredChunkGatesMatchReact()
        {
            Assert.That(WofSurvivalHobbitHutRules.ShouldShowRuntime(true, false, false), Is.True);
            Assert.That(WofSurvivalHobbitHutRules.ShouldShowRuntime(true, true, false), Is.False);
            Assert.That(WofSurvivalHobbitHutRules.ShouldShowRuntime(true, false, true), Is.False);
            Assert.That(WofSurvivalHobbitHutRules.ShouldShowRuntime(false, false, false), Is.False);
            Assert.That(WofSurvivalHobbitHutRules.SupportsRoofForest(WofSurvivalBiome.Plains), Is.True);
            Assert.That(WofSurvivalHobbitHutRules.SupportsRoofForest(WofSurvivalBiome.Jungle), Is.True);
            Assert.That(WofSurvivalHobbitHutRules.SupportsRoofForest(WofSurvivalBiome.Mushroom), Is.True);
            Assert.That(WofSurvivalHobbitHutRules.SupportsRoofForest(WofSurvivalBiome.Swamp), Is.False);
            Assert.That(WofSurvivalHobbitHutRules.SupportsRoofForest(WofSurvivalBiome.Desert), Is.False);
            Assert.That(WofSurvivalHobbitHutRules.GetSpawnThreshold(WofSurvivalBiome.Plains), Is.EqualTo(0.74f));
            Assert.That(WofSurvivalHobbitHutRules.GetSpawnThreshold(WofSurvivalBiome.Jungle), Is.EqualTo(0.68f));
            Assert.That(WofSurvivalHobbitHutRules.GetSpawnThreshold(WofSurvivalBiome.Mushroom), Is.EqualTo(0.72f));
            Assert.That(WofSurvivalHobbitHutRules.MakeChunk(-1, -1), Is.Empty,
                "A supported roof biome still obeys React's sparse spawn roll and surface gates.");
            Assert.That(WofSurvivalHobbitHutRules.MakeChunk(7, 4), Is.Empty);
            Assert.That(WofSurvivalHobbitHutRules.MakeChunk(4, -3), Is.Empty);
            Assert.That(WofSurvivalHobbitHutRules.MakeChunk(0, 0), Is.Empty,
                "React omits the authored base from its streamed chunk list; Unity must not add a hut there.");
        }

        [Test]
        public void StageFiveTimingColliderAndRetainedWindowMatchSourceContract()
        {
            var ready = WofSurvivalHobbitHutRules.GetReadyDelaySeconds(2, -1, 0);
            Assert.That(ready, Is.InRange(3.6f, 4.12f));
            Assert.That(WofSurvivalHobbitHutRules.GetReadyDelaySeconds(2, -1, 3) - ready,
                Is.EqualTo(0.84f).Within(0.00001f));
            Assert.That(WofSurvivalHobbitHutRules.ColliderCenter,
                Is.EqualTo(new Vector3(0f, 2.9f, 0.8f)));
            Assert.That(WofSurvivalHobbitHutRules.ColliderSize,
                Is.EqualTo(new Vector3(13.6f, 6.4f, 9.2f)));

            var gameObject = new GameObject("HobbitHutWindowRetentionTest");
            try
            {
                var runtime = gameObject.AddComponent<WofSurvivalHobbitHutRuntime>();
                var runtimeType = typeof(WofSurvivalHobbitHutRuntime);
                var awake = GetPrivateMethod(runtimeType, "Awake");
                var rebuild = GetPrivateMethod(runtimeType, "RebuildStageWindow");
                var stagesField = GetPrivateField(runtimeType, "_visibleStages");
                awake.Invoke(runtime, null);
                rebuild.Invoke(runtime, new object[] { 0, 0 });
                var stages = (IDictionary)stagesField.GetValue(runtime);
                Assert.That(stages.Count, Is.EqualTo(37));
                var retainedKey = ((long)0 << 32) ^ (uint)0;
                var retained = stages[retainedKey];
                var readyAt = (float)retained.GetType().GetProperty("ReadyAt").GetValue(retained);
                rebuild.Invoke(runtime, new object[] { 1, 0 });
                stages = (IDictionary)stagesField.GetValue(runtime);
                Assert.That(stages.Count, Is.EqualTo(37));
                var retainedAfter = stages[retainedKey];
                Assert.That((float)retainedAfter.GetType().GetProperty("ReadyAt").GetValue(retainedAfter),
                    Is.EqualTo(readyAt), "Moving must not restart an overlapping chunk's stage-five timer.");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RuntimeBuildUsesExactVisualPartAndDodecahedronContracts()
        {
            var gameObject = new GameObject("HobbitHutVisualContractTest");
            var content = new GameObject("HobbitHutVisualContent");
            content.transform.SetParent(gameObject.transform, false);
            try
            {
                var runtime = gameObject.AddComponent<WofSurvivalHobbitHutRuntime>();
                var runtimeType = typeof(WofSurvivalHobbitHutRuntime);
                GetPrivateMethod(runtimeType, "Awake").Invoke(runtime, null);
                var detailZero = (Mesh)GetPrivateField(runtimeType, "_dodecaDetailZeroMesh").GetValue(runtime);
                var detailOne = (Mesh)GetPrivateField(runtimeType, "_dodecaDetailOneMesh").GetValue(runtime);
                Assert.That(detailZero.name, Is.EqualTo("ReactHobbitHutDodecaDetail0"));
                Assert.That(detailZero.triangles, Has.Length.EqualTo(108));
                Assert.That(detailOne.name, Is.EqualTo("ReactHobbitHutDodecaDetail1"));
                Assert.That(detailOne.triangles, Has.Length.EqualTo(432));

                var record = WofSurvivalHobbitHutRules.MakeChunk(2, -1)[0];
                GetPrivateMethod(runtimeType, "BuildHut").Invoke(runtime, new object[] { content.transform, record });
                var root = content.transform.GetChild(0);
                Assert.That(root.position.x, Is.EqualTo(record.Position.x).Within(0.001f));
                Assert.That(root.position.y, Is.EqualTo(record.Position.y).Within(0.001f));
                Assert.That(root.position.z, Is.EqualTo(record.Position.z).Within(0.001f));
                Assert.That(root.localScale, Is.EqualTo(Vector3.one * record.Scale));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(24));
                var colliders = root.GetComponentsInChildren<BoxCollider>(true);
                Assert.That(colliders, Has.Length.EqualTo(1));
                Assert.That(colliders[0].center, Is.EqualTo(WofSurvivalHobbitHutRules.ColliderCenter));
                Assert.That(colliders[0].size, Is.EqualTo(WofSurvivalHobbitHutRules.ColliderSize));
                Assert.That(colliders[0].isTrigger, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static MethodInfo GetPrivateMethod(System.Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method {name}.");
            return method;
        }

        private static FieldInfo GetPrivateField(System.Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {name}.");
            return field;
        }

        private static void AssertRecord(
            WofSurvivalHobbitHutRecord record,
            int sourceIndex,
            WofSurvivalBiome biome,
            double x,
            double y,
            double z,
            double yaw,
            double scale,
            double variant)
        {
            Assert.That(record.SourceIndex, Is.EqualTo(sourceIndex));
            Assert.That(record.Biome, Is.EqualTo(biome));
            Assert.That(record.Position.x, Is.EqualTo(x).Within(0.001d));
            Assert.That(record.Position.y, Is.EqualTo(y).Within(0.001d));
            Assert.That(record.Position.z, Is.EqualTo(z).Within(0.001d));
            Assert.That(record.YawRadians, Is.EqualTo(yaw).Within(0.00001d));
            Assert.That(record.Scale, Is.EqualTo(scale).Within(0.00001d));
            Assert.That(record.Variant, Is.EqualTo(variant).Within(0.00001d));
        }
    }
}
