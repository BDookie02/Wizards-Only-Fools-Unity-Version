using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofSurvivalWaterfallTests
    {
        [Test]
        public void NearbyBiomeRecordsMatchIndependentReactOracle()
        {
            AssertRecord(
                WofSurvivalWaterfallRules.MakeChunk(1, -2, 0).Single(),
                1,
                -2,
                3,
                558.2809453977384d,
                26.54071546762149d,
                -1095.0452194617335d,
                10.848004305418964d,
                5.491443717579386d,
                3.5776713922139214d,
                543.8145632257156d,
                21.196713314912007d,
                -1101.7865388320777d,
                15.959665026632138d);
            AssertRecord(
                WofSurvivalWaterfallRules.MakeChunk(4, -2, 0).Single(),
                4,
                -2,
                4,
                1965.9240493867612d,
                13.723209188184057d,
                -1147.5179575730792d,
                8.63901007206745d,
                6.298637947015232d,
                4.130460367242145d,
                1957.1518567607209d,
                9.483704152150333d,
                -1160.8510081066834d,
                11.29055017058272d);
            AssertRecord(
                WofSurvivalWaterfallRules.MakeChunk(-3, 0, 0).Single(),
                -3,
                0,
                5,
                -1466.7736366843837d,
                11.036074745003209d,
                24.257952105022014d,
                13.641312077790452d,
                7.0591004737536425d,
                3.9498768172010124d,
                -1477.7978498878797d,
                4.295418706107982d,
                12.717227550477403d,
                12.524175700178603d);
        }

        [Test]
        public void SourceGatesKeepWaterfallsNearAndOutOfDesertSwampAndAuthoredChunks()
        {
            Assert.That(WofSurvivalWaterfallRules.ShouldShowRuntime(true), Is.True);
            Assert.That(WofSurvivalWaterfallRules.ShouldShowRuntime(false), Is.False);
            Assert.That(WofSurvivalWaterfallRules.ShouldGenerateChunk(true, 1, -2, 0), Is.True);
            Assert.That(WofSurvivalWaterfallRules.ShouldGenerateChunk(true, 1, -2, 1), Is.False);
            Assert.That(WofSurvivalWaterfallRules.ShouldGenerateChunk(false, 1, -2, 0), Is.False);
            Assert.That(WofSurvivalWaterfallRules.ShouldGenerateChunk(true, 3, -3, 0), Is.False);
            Assert.That(WofSurvivalWaterfallRules.ShouldGenerateChunk(true, 0, -3, 0), Is.False);
            Assert.That(WofSurvivalWaterfallRules.ShouldGenerateChunk(true, 0, 0, 0), Is.False);
            Assert.That(WofSurvivalWaterfallRules.MakeChunk(1, -2, 1), Is.Empty);
            Assert.That(WofSurvivalWaterfallRules.MakeChunk(3, -3, 0), Is.Empty);
            Assert.That(WofSurvivalWaterfallRules.GetDesiredCount(1, -2, 0), Is.EqualTo(1));
            Assert.That(WofSurvivalWaterfallRules.GetDesiredCount(1, -2, 1), Is.EqualTo(0));
        }

        [Test]
        public void RuntimeBuildMatchesThreePartTransparentSourceContract()
        {
            var gameObject = new GameObject("SurvivalWaterfallVisualContractTest");
            try
            {
                var runtime = gameObject.AddComponent<WofSurvivalWaterfallRuntime>();
                var runtimeType = typeof(WofSurvivalWaterfallRuntime);
                GetPrivateMethod(runtimeType, "Awake").Invoke(runtime, null);
                GetPrivateMethod(runtimeType, "RebuildCurrentChunk").Invoke(runtime, new object[] { 1, -2 });

                Assert.That(runtime.WaterfallCount, Is.EqualTo(1));
                var root = gameObject.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "survival-waterfall-1:-2-waterfall-3");
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers, Has.Length.EqualTo(3));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(renderers.Single(item => item.name == "WaterfallMain").sharedMaterial.color.a,
                    Is.EqualTo(0.42f).Within(0.0001f));
                Assert.That(renderers.Single(item => item.name == "WaterfallHighlight").sharedMaterial.color.a,
                    Is.EqualTo(0.22f).Within(0.0001f));
                Assert.That(renderers.Single(item => item.name == "WaterfallPool").sharedMaterial.color.a,
                    Is.EqualTo(0.62f).Within(0.0001f));
                var pool = root.GetComponentsInChildren<MeshFilter>(true)
                    .Single(item => item.name == "WaterfallPool");
                Assert.That(pool.sharedMesh.vertexCount, Is.EqualTo(19));
                Assert.That(pool.sharedMesh.triangles, Has.Length.EqualTo(54));

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

        private static void AssertRecord(
            WofSurvivalWaterfallRecord record,
            int chunkX,
            int chunkZ,
            int sourceIndex,
            double x,
            double y,
            double z,
            double height,
            double width,
            double yaw,
            double poolX,
            double poolY,
            double poolZ,
            double poolScale)
        {
            Assert.That(record.ChunkX, Is.EqualTo(chunkX));
            Assert.That(record.ChunkZ, Is.EqualTo(chunkZ));
            Assert.That(record.SourceIndex, Is.EqualTo(sourceIndex));
            Assert.That(record.Position.x, Is.EqualTo(x).Within(0.001d));
            Assert.That(record.Position.y, Is.EqualTo(y).Within(0.001d));
            Assert.That(record.Position.z, Is.EqualTo(z).Within(0.001d));
            Assert.That(record.Height, Is.EqualTo(height).Within(0.0001d));
            Assert.That(record.Width, Is.EqualTo(width).Within(0.0001d));
            Assert.That(record.YawRadians, Is.EqualTo(yaw).Within(0.0001d));
            Assert.That(record.PoolPosition.x, Is.EqualTo(poolX).Within(0.001d));
            Assert.That(record.PoolPosition.y, Is.EqualTo(poolY).Within(0.001d));
            Assert.That(record.PoolPosition.z, Is.EqualTo(poolZ).Within(0.001d));
            Assert.That(record.PoolScale, Is.EqualTo(poolScale).Within(0.0001d));
        }
    }
}
