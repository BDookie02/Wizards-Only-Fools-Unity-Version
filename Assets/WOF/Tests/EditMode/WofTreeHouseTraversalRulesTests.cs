using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofTreeHouseTraversalRulesTests
    {
        [Test]
        public void ReactSpiralRequiresHigherStepOffsetThanTheNormalController()
        {
            var steps = WofTreeHouseVillageLayout.BuildSpiralSteps();
            var rise = steps[1].Position.y - steps[0].Position.y;

            Assert.That(rise, Is.EqualTo(15f / 29f).Within(0.0001f));
            Assert.That(rise, Is.GreaterThan(0.35f));
            Assert.That(WofTreeHouseTraversalRules.AssistedStepOffset, Is.GreaterThan(rise));

            var mobileSteps = WofTreeHouseVillageLayout.BuildSpiralSteps(
                steps: WofTreeHouseVillageLayout.MobileSpiralStepCount);
            var mobileRise = mobileSteps[1].Position.y - mobileSteps[0].Position.y;
            Assert.That(WofTreeHouseTraversalRules.MobileAssistedStepOffset, Is.GreaterThan(mobileRise));
        }

        [Test]
        public void AssistIsLimitedToTheExactTreeHouseTraversalGeometry()
        {
            var tree = WofTreeHouseVillageLayout.Trees[0];
            var spiralPoint = tree.Position + WofTreeHouseVillageLayout.BuildSpiralSteps()[15].Position;
            Assert.That(
                WofTreeHouseTraversalRules.RequiresTraversalAssist(spiralPoint),
                Is.True);
            Assert.That(
                WofTreeHouseTraversalRules.RequiresTraversalAssist(new Vector3(80f, 0f, 80f)),
                Is.False);
            Assert.That(
                WofTreeHouseTraversalRules.RequiresTraversalAssist(tree.Position + new Vector3(0f, 8f, 0f)),
                Is.False,
                "Standing inside the trunk must not alter the controller merely because a staircase is nearby.");
        }

        [Test]
        public void EveryReactSpiralStepOnEveryTreeReceivesAssist()
        {
            AssertEverySpiralStepReceivesAssist(
                WofTreeHouseVillageLayout.BuildSpiralSteps(),
                mobilePerformanceMode: false);
            AssertEverySpiralStepReceivesAssist(
                WofTreeHouseVillageLayout.BuildSpiralSteps(
                    steps: WofTreeHouseVillageLayout.MobileSpiralStepCount),
                mobilePerformanceMode: true);
        }

        [Test]
        public void SpiralCollisionAssistResolvesOnlyTheMatchingTree()
        {
            var steps = WofTreeHouseVillageLayout.BuildSpiralSteps();
            for (var treeIndex = 0; treeIndex < WofTreeHouseVillageLayout.Trees.Count; treeIndex++)
            {
                var tree = WofTreeHouseVillageLayout.Trees[treeIndex];
                var rotation = Quaternion.Euler(0f, tree.YawRadians * Mathf.Rad2Deg, 0f);
                var spiralPoint = tree.Position + rotation * steps[1].Position;
                Assert.That(
                    WofTreeHouseTraversalRules.TryResolveSpiralTreeIndex(
                        spiralPoint,
                        mobilePerformanceMode: false,
                        out var resolvedTreeIndex),
                    Is.True);
                Assert.That(resolvedTreeIndex, Is.EqualTo(treeIndex));
            }

            Assert.That(
                WofTreeHouseTraversalRules.TryResolveSpiralTreeIndex(
                    WofTreeHouseVillageLayout.Trees[0].Position + new Vector3(0f, 8f, 0f),
                    mobilePerformanceMode: false,
                    out var nonSpiralTreeIndex),
                Is.False);
            Assert.That(nonSpiralTreeIndex, Is.EqualTo(-1));
        }

        [Test]
        public void SpiralSurfacePriorityDoesNotTreatTheBridgeBelowAsAStair()
        {
            var steps = WofTreeHouseVillageLayout.BuildSpiralSteps();
            var tree = WofTreeHouseVillageLayout.Trees[0];
            var rotation = Quaternion.Euler(0f, tree.YawRadians * Mathf.Rad2Deg, 0f);
            var exactSurface = tree.Position + rotation * steps[26].Position + Vector3.up * 0.11f;
            Assert.That(
                WofTreeHouseTraversalRules.TryResolveSpiralSurfaceTreeIndex(
                    exactSurface,
                    mobilePerformanceMode: false,
                    out var surfaceTreeIndex),
                Is.True);
            Assert.That(surfaceTreeIndex, Is.EqualTo(0));

            Assert.That(
                WofTreeHouseTraversalRules.TryResolveSpiralSurfaceTreeIndex(
                    new Vector3(23.26f, 11.83f, 12.04f),
                    mobilePerformanceMode: false,
                    out _),
                Is.False,
                "The lower bridge deck must not inherit the overhead spiral's collision priority.");
        }

        [Test]
        public void ContinuousSupportBridgesEveryReactTreadWithoutChangingItsVisibleLayout()
        {
            var steps = WofTreeHouseVillageLayout.BuildSpiralSteps();
            WofTreeHouseTraversalRules.BuildContinuousSpiralSupport(
                steps,
                out var vertices,
                out var triangles);

            Assert.That(vertices, Has.Length.EqualTo(steps.Count * 2));
            Assert.That(triangles, Has.Length.EqualTo((steps.Count - 1) * 6));
            for (var index = 0; index < steps.Count; index++)
            {
                var inner = vertices[index * 2];
                var outer = vertices[index * 2 + 1];
                Assert.That(
                    Vector3.Distance(inner, outer),
                    Is.EqualTo(WofTreeHouseTraversalRules.SpiralSupportHalfWidth * 2f).Within(0.0001f));
                Assert.That(
                    (inner.y + outer.y) * 0.5f,
                    Is.EqualTo(steps[index].Position.y + WofTreeHouseTraversalRules.SpiralSupportSurfaceLift)
                        .Within(0.0001f));
            }

            for (var triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                var a = vertices[triangles[triangle]];
                var b = vertices[triangles[triangle + 1]];
                var c = vertices[triangles[triangle + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void GroundRopeAndBridgeSegmentsReceiveSlopeAssist()
        {
            for (var treeIndex = 0; treeIndex < WofTreeHouseVillageLayout.Trees.Count; treeIndex++)
            {
                AssertSpanReceivesAssist(
                    WofTreeHouseVillageLayout.GetTreeBasePosition(treeIndex),
                    WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, 0),
                    $"ground rope for tree {treeIndex}");

                foreach (var connection in WofTreeHouseVillageLayout.InternalRopes)
                {
                    AssertSpanReceivesAssist(
                        WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, connection.StartHouse),
                        WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, connection.EndHouse),
                        $"internal rope {connection.StartHouse}-{connection.EndHouse} for tree {treeIndex}");
                }
            }

            for (var bridgeIndex = 0; bridgeIndex < WofTreeHouseVillageLayout.Bridges.Count; bridgeIndex++)
            {
                var bridge = WofTreeHouseVillageLayout.Bridges[bridgeIndex];
                AssertSpanReceivesAssist(
                    WofTreeHouseVillageLayout.GetHouseBalconyPosition(bridge.StartTree, bridge.StartHouse),
                    WofTreeHouseVillageLayout.GetHouseBalconyPosition(bridge.EndTree, bridge.EndHouse),
                    $"bridge {bridgeIndex}");
            }
        }

        [Test]
        public void BridgeCollisionAssistTargetsOnlyItsEndpointTrees()
        {
            for (var bridgeIndex = 0; bridgeIndex < WofTreeHouseVillageLayout.Bridges.Count; bridgeIndex++)
            {
                var bridge = WofTreeHouseVillageLayout.Bridges[bridgeIndex];
                var start = WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                    bridge.StartTree,
                    bridge.StartHouse);
                var end = WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                    bridge.EndTree,
                    bridge.EndHouse);
                var midpoint = Vector3.Lerp(start, end, 0.5f);
                var mask = WofTreeHouseTraversalRules.ResolveStructuralCollisionAssistTreeMask(midpoint);
                var bridgeMask = WofTreeHouseTraversalRules.ResolveBridgeEndpointTreeMask(midpoint);

                Assert.That(mask & (1 << bridge.StartTree), Is.Not.Zero, $"bridge={bridgeIndex} start");
                Assert.That(mask & (1 << bridge.EndTree), Is.Not.Zero, $"bridge={bridgeIndex} end");
                Assert.That(bridgeMask & (1 << bridge.StartTree), Is.Not.Zero, $"bridge={bridgeIndex} bridge-start");
                Assert.That(bridgeMask & (1 << bridge.EndTree), Is.Not.Zero, $"bridge={bridgeIndex} bridge-end");
            }

            Assert.That(
                WofTreeHouseTraversalRules.ResolveStructuralCollisionAssistTreeMask(
                    new Vector3(80f, 0f, 80f)),
                Is.Zero);
            Assert.That(
                WofTreeHouseTraversalRules.ResolveBridgeEndpointTreeMask(new Vector3(80f, 0f, 80f)),
                Is.Zero);
        }

        [Test]
        public void SegmentDistanceClampsToBothEndpoints()
        {
            var start = Vector3.zero;
            var end = new Vector3(0f, 0f, 10f);
            Assert.That(WofTreeHouseTraversalRules.DistanceToSegment(new Vector3(3f, 0f, 5f), start, end),
                Is.EqualTo(3f).Within(0.0001f));
            Assert.That(WofTreeHouseTraversalRules.DistanceToSegment(new Vector3(0f, 0f, 14f), start, end),
                Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void AuthoritativeServerAndPredictingOwnerUseTheSameTraversalRule()
        {
            Assert.That(WofTreeHouseTraversalRules.RunsControllerSimulation(isServer: true, isOwner: false), Is.True);
            Assert.That(WofTreeHouseTraversalRules.RunsControllerSimulation(isServer: false, isOwner: true), Is.True);
            Assert.That(WofTreeHouseTraversalRules.RunsControllerSimulation(isServer: true, isOwner: true), Is.True);
            Assert.That(WofTreeHouseTraversalRules.RunsControllerSimulation(isServer: false, isOwner: false), Is.False);
        }

        private static void AssertEverySpiralStepReceivesAssist(
            System.Collections.Generic.IReadOnlyList<WofTreeHouseSpiralStep> steps,
            bool mobilePerformanceMode)
        {
            for (var treeIndex = 0; treeIndex < WofTreeHouseVillageLayout.Trees.Count; treeIndex++)
            {
                var tree = WofTreeHouseVillageLayout.Trees[treeIndex];
                var rotation = Quaternion.Euler(0f, tree.YawRadians * Mathf.Rad2Deg, 0f);
                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    var worldPoint = tree.Position + rotation * steps[stepIndex].Position;
                    Assert.That(
                        WofTreeHouseTraversalRules.RequiresTraversalAssist(worldPoint, mobilePerformanceMode),
                        Is.True,
                        $"tree={treeIndex} step={stepIndex} mobile={mobilePerformanceMode}");
                }
            }
        }

        private static void AssertSpanReceivesAssist(Vector3 start, Vector3 end, string label)
        {
            foreach (var t in new[] { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1f })
            {
                Assert.That(
                    WofTreeHouseTraversalRules.RequiresTraversalAssist(Vector3.Lerp(start, end, t)),
                    Is.True,
                    $"{label} at t={t:F2}");
            }
        }
    }
}
