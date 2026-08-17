using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofDesertLandmarkTests
    {
        [Test]
        public void NearAndMidRecordsMatchIndependentReactOracle()
        {
            var near = WofSurvivalDesertLandmarkRules.MakeChunk(3, -3, 0);
            Assert.That(near, Has.Length.EqualTo(2));
            AssertRecord(near[0], 3, WofSurvivalDesertLandmarkKind.Pyramid,
                1353.1553506737948d, 15.498468004166451d, -1443.745401577074d,
                1.402944693514728d, 4.220188865668552d, 0.7439086598315043d);
            AssertRecord(near[1], 14, WofSurvivalDesertLandmarkKind.Obelisk,
                1641.6745195131004d, 19.897892690784587d, -1399.9642258714139d,
                1.3527421091170981d, 2.361995971527329d, 0.18396906471025432d);

            var mid = WofSurvivalDesertLandmarkRules.MakeChunk(3, -3, 1);
            Assert.That(mid, Has.Length.EqualTo(1));
            AssertRecord(mid[0], 3, WofSurvivalDesertLandmarkKind.Pyramid,
                1353.1553506737948d, 15.498468004166451d, -1443.745401577074d,
                1.402944693514728d, 4.220188865668552d, 0.7439086598315043d);

            var lateNear = WofSurvivalDesertLandmarkRules.MakeChunk(4, -3, 0);
            Assert.That(lateNear.Select(record => record.SourceIndex), Is.EqualTo(new[] { 19, 24 }));
            Assert.That(WofSurvivalDesertLandmarkRules.MakeChunk(4, -3, 1), Is.Empty,
                "React mid LOD makes only fourteen attempts, so its first accepted index is out of range.");
        }

        [Test]
        public void GatesStageTwoTimingAndPyramidMetricsMatchReact()
        {
            Assert.That(WofSurvivalDesertLandmarkRules.ShouldShowRuntime(true), Is.True);
            Assert.That(WofSurvivalDesertLandmarkRules.ShouldShowRuntime(false), Is.False);
            Assert.That(WofSurvivalDesertLandmarkRules.ShouldGenerateChunk(true, 3, -3, 0), Is.True);
            Assert.That(WofSurvivalDesertLandmarkRules.ShouldGenerateChunk(true, 3, -3, 1), Is.True);
            Assert.That(WofSurvivalDesertLandmarkRules.ShouldGenerateChunk(true, 3, -3, 2), Is.False);
            Assert.That(WofSurvivalDesertLandmarkRules.ShouldGenerateChunk(true, 4, -4, 0), Is.False,
                "The authored desert village suppresses streamed landmarks.");
            Assert.That(WofSurvivalDesertLandmarkRules.ShouldGenerateChunk(true, 0, 0, 0), Is.False);
            Assert.That(WofSurvivalDesertLandmarkRules.GetTargetCount(0), Is.EqualTo(2));
            Assert.That(WofSurvivalDesertLandmarkRules.GetTargetCount(1), Is.EqualTo(1));
            Assert.That(WofSurvivalDesertLandmarkRules.GetTargetCount(2), Is.EqualTo(0));

            var desktop = WofSurvivalDesertLandmarkRules.GetReadyDelaySeconds(3, -3, 0, false);
            var mobile = WofSurvivalDesertLandmarkRules.GetReadyDelaySeconds(3, -3, 0, true);
            Assert.That(desktop, Is.InRange(0.56f, 1.08f));
            Assert.That(mobile, Is.InRange(0.82f, 1.58f));
            Assert.That(WofSurvivalDesertLandmarkRules.GetReadyDelaySeconds(3, -3, 1, false) - desktop,
                Is.EqualTo(0.28f).Within(0.00001f));
            Assert.That(WofSurvivalDesertLandmarkRules.GetReadyDelaySeconds(3, -3, 1, true) - mobile,
                Is.EqualTo(0.46f).Within(0.00001f));

            var pyramid = WofSurvivalDesertLandmarkRules.MakeChunk(3, -3, 0)[0];
            var metrics = WofSurvivalDesertLandmarkRules.GetPyramidMetrics(pyramid);
            Assert.That(metrics.StepCount, Is.EqualTo(7));
            Assert.That(metrics.StepHeight, Is.EqualTo(2.9461838563809288d).Within(0.00001d));
            Assert.That(metrics.BaseSize, Is.EqualTo(43.49128549895657d).Within(0.00001d));
            Assert.That(metrics.PyramidYawRadians, Is.EqualTo(5.005587029065999d).Within(0.00001d));
            Assert.That(metrics.DoorWidth, Is.EqualTo(7.575901344979531d).Within(0.00001d));
            Assert.That(metrics.DoorHeight, Is.EqualTo(8.838551569142786d).Within(0.00001d));
            Assert.That(metrics.Height, Is.EqualTo(26.515654707428357d).Within(0.00002d));
            var footprint = WofSurvivalDesertLandmarkRules.GetPyramidFootprintStats(pyramid, 0);
            Assert.That(footprint.Range, Is.LessThanOrEqualTo(Mathf.Max(4.75f, metrics.BaseSize * 0.12f)));
            Assert.That(pyramid.Position.y, Is.EqualTo(footprint.Maximum + 0.08f).Within(0.0001f));
        }

        [Test]
        public void StageWindowRetainsOverlappingReactTimers()
        {
            var gameObject = new GameObject("DesertLandmarkWindowRetentionTest");
            try
            {
                var runtime = gameObject.AddComponent<WofSurvivalDesertLandmarkRuntime>();
                var runtimeType = typeof(WofSurvivalDesertLandmarkRuntime);
                GetPrivateMethod(runtimeType, "Awake").Invoke(runtime, null);
                var rebuild = GetPrivateMethod(runtimeType, "RebuildWindow");
                var stagesField = GetPrivateField(runtimeType, "_visibleStages");
                rebuild.Invoke(runtime, new object[] { 3, -3 });
                var stages = (IDictionary)stagesField.GetValue(runtime);
                Assert.That(stages.Count, Is.EqualTo(37));
                var retainedKey = ((long)3 << 32) ^ unchecked((uint)-3);
                var retained = stages[retainedKey];
                var readyAt = (float)retained.GetType().GetProperty("ReadyAt").GetValue(retained);
                rebuild.Invoke(runtime, new object[] { 4, -3 });
                stages = (IDictionary)stagesField.GetValue(runtime);
                Assert.That(stages.Count, Is.EqualTo(37));
                var retainedAfter = stages[retainedKey];
                Assert.That((float)retainedAfter.GetType().GetProperty("ReadyAt").GetValue(retainedAfter),
                    Is.EqualTo(readyAt), "Moving must not restart an overlapping chunk's React stage-two timer.");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RuntimeBuildHasExactLandmarkPartsCollidersAndDuneVillagers()
        {
            var gameObject = new GameObject("DesertLandmarkVisualContractTest");
            try
            {
                var runtime = gameObject.AddComponent<WofSurvivalDesertLandmarkRuntime>();
                var runtimeType = typeof(WofSurvivalDesertLandmarkRuntime);
                GetPrivateMethod(runtimeType, "Awake").Invoke(runtime, null);
                GetPrivateMethod(runtimeType, "BuildChunk").Invoke(runtime, new object[] { 3, -3, 0 });

                var pyramid = gameObject.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name.EndsWith("-pyramid"));
                var obelisk = gameObject.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name.EndsWith("-obelisk"));
                Assert.That(pyramid.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(49));
                Assert.That(pyramid.GetComponentsInChildren<BoxCollider>(true), Has.Length.EqualTo(8));
                Assert.That(obelisk.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(5));
                Assert.That(obelisk.GetComponentsInChildren<Collider>(true), Is.Empty);
                var manager = gameObject.GetComponentInChildren<WofVillagerManager>(true);
                Assert.That(manager, Is.Not.Null);
                Assert.That(manager.VillagerCount, Is.EqualTo(3));
                var billboards = gameObject.GetComponentsInChildren<WofVillagerBillboard>(true);
                Assert.That(billboards, Has.Length.EqualTo(3));
                Assert.That(billboards[0].VillagerId,
                    Is.EqualTo("3:-3-desert-landmark-3-egyptian-villager-0"));
                Assert.That(billboards[0].ReactDisplayName, Is.EqualTo("Dune Villager 1"));
                Assert.That(billboards[0].ReactTownId, Is.EqualTo("survival-pyramid-villagers-3:-3"));
                var archiveField = GetPrivateField(typeof(WofVillagerBillboard), "archiveFile");
                StringAssert.IsMatch("^desert-[0-9]{2}\\.wofavatar$", (string)archiveField.GetValue(billboards[0]));
                Assert.That(runtime.LandmarkCount, Is.EqualTo(2));
                Assert.That(runtime.PyramidCount, Is.EqualTo(1));
                Assert.That(runtime.ObeliskCount, Is.EqualTo(1));
                Assert.That(runtime.VillagerCount, Is.EqualTo(3));
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
            WofSurvivalDesertLandmarkRecord record,
            int sourceIndex,
            WofSurvivalDesertLandmarkKind kind,
            double x,
            double y,
            double z,
            double scale,
            double yaw,
            double variant)
        {
            Assert.That(record.SourceIndex, Is.EqualTo(sourceIndex));
            Assert.That(record.Kind, Is.EqualTo(kind));
            Assert.That(record.Position.x, Is.EqualTo(x).Within(0.001d));
            Assert.That(record.Position.y, Is.EqualTo(y).Within(0.001d));
            Assert.That(record.Position.z, Is.EqualTo(z).Within(0.001d));
            Assert.That(record.Scale, Is.EqualTo(scale).Within(0.00001d));
            Assert.That(record.YawRadians, Is.EqualTo(yaw).Within(0.00001d));
            Assert.That(record.Variant, Is.EqualTo(variant).Within(0.00001d));
        }
    }
}
