using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    /// <summary>
    /// Keeps Unity's controller compatible with the exact React tree-house
    /// collision layout without changing movement anywhere else in the world.
    /// </summary>
    public static class WofTreeHouseTraversalRules
    {
        public const float AssistedStepOffset = 0.58f;
        public const float MobileAssistedStepOffset = 1.08f;
        public const float AssistedSlopeLimit = 60f;
        public const float SpiralAssistRadius = 2.25f;
        public const float SpiralSurfacePriorityRadius = 0.9f;
        public const float RopeAssistRadius = 1.1f;
        public const float BridgeAssistRadius = 2.75f;
        public const float SpiralSupportHalfWidth = 1.35f;
        public const float SpiralSupportSurfaceLift = 0.11f;

        private static readonly IReadOnlyList<WofTreeHouseSpiralStep> DesktopSpiralSteps =
            WofTreeHouseVillageLayout.BuildSpiralSteps();
        private static readonly IReadOnlyList<WofTreeHouseSpiralStep> MobileSpiralSteps =
            WofTreeHouseVillageLayout.BuildSpiralSteps(
                steps: WofTreeHouseVillageLayout.MobileSpiralStepCount);
        private static readonly Bounds TraversalBounds = BuildTraversalBounds();

        public static bool RunsControllerSimulation(bool isServer, bool isOwner)
        {
            return isServer || isOwner;
        }

        public static bool RequiresTraversalAssist(Vector3 worldPosition, bool mobilePerformanceMode = false)
        {
            if (!TraversalBounds.Contains(worldPosition))
            {
                return false;
            }

            var spiralSteps = mobilePerformanceMode ? MobileSpiralSteps : DesktopSpiralSteps;
            for (var treeIndex = 0; treeIndex < WofTreeHouseVillageLayout.Trees.Count; treeIndex++)
            {
                var tree = WofTreeHouseVillageLayout.Trees[treeIndex];
                var localPosition = Quaternion.Euler(0f, -tree.YawRadians * Mathf.Rad2Deg, 0f) *
                                    (worldPosition - tree.Position);
                if (IsNearSpiralPath(localPosition, spiralSteps))
                {
                    return true;
                }

                var groundStart = WofTreeHouseVillageLayout.GetTreeBasePosition(treeIndex);
                var groundEnd = WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, 0);
                if (DistanceToSegment(worldPosition, groundStart, groundEnd) <= RopeAssistRadius)
                {
                    return true;
                }

                for (var connectionIndex = 0;
                     connectionIndex < WofTreeHouseVillageLayout.InternalRopes.Count;
                     connectionIndex++)
                {
                    var connection = WofTreeHouseVillageLayout.InternalRopes[connectionIndex];
                    var start = WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, connection.StartHouse);
                    var end = WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, connection.EndHouse);
                    if (DistanceToSegment(worldPosition, start, end) <= RopeAssistRadius)
                    {
                        return true;
                    }
                }
            }

            for (var connectionIndex = 0;
                 connectionIndex < WofTreeHouseVillageLayout.Bridges.Count;
                 connectionIndex++)
            {
                var connection = WofTreeHouseVillageLayout.Bridges[connectionIndex];
                var start = WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                    connection.StartTree,
                    connection.StartHouse);
                var end = WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                    connection.EndTree,
                    connection.EndHouse);
                if (DistanceToSegment(worldPosition, start, end) <= BridgeAssistRadius)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryResolveSpiralTreeIndex(
            Vector3 worldPosition,
            bool mobilePerformanceMode,
            out int treeIndex)
        {
            treeIndex = -1;
            if (!TraversalBounds.Contains(worldPosition))
            {
                return false;
            }

            var spiralSteps = mobilePerformanceMode ? MobileSpiralSteps : DesktopSpiralSteps;
            for (var index = 0; index < WofTreeHouseVillageLayout.Trees.Count; index++)
            {
                var tree = WofTreeHouseVillageLayout.Trees[index];
                var localPosition = Quaternion.Euler(0f, -tree.YawRadians * Mathf.Rad2Deg, 0f) *
                                    (worldPosition - tree.Position);
                if (!IsNearSpiralPath(localPosition, spiralSteps))
                {
                    continue;
                }

                treeIndex = index;
                return true;
            }

            return false;
        }

        public static int ResolveStructuralCollisionAssistTreeMask(
            Vector3 worldPosition,
            bool mobilePerformanceMode = false)
        {
            var mask = 0;
            if (TryResolveSpiralTreeIndex(
                    worldPosition,
                    mobilePerformanceMode,
                    out var spiralTreeIndex))
            {
                mask |= 1 << spiralTreeIndex;
            }

            for (var treeIndex = 0; treeIndex < WofTreeHouseVillageLayout.Trees.Count; treeIndex++)
            {
                var groundStart = WofTreeHouseVillageLayout.GetTreeBasePosition(treeIndex);
                var groundEnd = WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, 0);
                if (DistanceToSegment(worldPosition, groundStart, groundEnd) <= RopeAssistRadius)
                {
                    mask |= 1 << treeIndex;
                }

                foreach (var connection in WofTreeHouseVillageLayout.InternalRopes)
                {
                    var start = WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                        treeIndex,
                        connection.StartHouse);
                    var end = WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                        treeIndex,
                        connection.EndHouse);
                    if (DistanceToSegment(worldPosition, start, end) <= RopeAssistRadius)
                    {
                        mask |= 1 << treeIndex;
                    }
                }
            }

            foreach (var connection in WofTreeHouseVillageLayout.Bridges)
            {
                var start = WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                    connection.StartTree,
                    connection.StartHouse);
                var end = WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                    connection.EndTree,
                    connection.EndHouse);
                if (DistanceToSegment(worldPosition, start, end) <= BridgeAssistRadius)
                {
                    mask |= 1 << connection.StartTree;
                    mask |= 1 << connection.EndTree;
                }
            }

            return mask;
        }

        public static int ResolveBridgeEndpointTreeMask(Vector3 worldPosition)
        {
            var mask = 0;
            foreach (var connection in WofTreeHouseVillageLayout.Bridges)
            {
                var start = WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                    connection.StartTree,
                    connection.StartHouse);
                var end = WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                    connection.EndTree,
                    connection.EndHouse);
                if (DistanceToSegment(worldPosition, start, end) > BridgeAssistRadius)
                {
                    continue;
                }

                mask |= 1 << connection.StartTree;
                mask |= 1 << connection.EndTree;
            }

            return mask;
        }

        public static bool TryResolveSpiralSurfaceTreeIndex(
            Vector3 worldPosition,
            bool mobilePerformanceMode,
            out int treeIndex)
        {
            treeIndex = -1;
            var spiralSteps = mobilePerformanceMode ? MobileSpiralSteps : DesktopSpiralSteps;
            for (var index = 0; index < WofTreeHouseVillageLayout.Trees.Count; index++)
            {
                var tree = WofTreeHouseVillageLayout.Trees[index];
                var localPosition = Quaternion.Euler(0f, -tree.YawRadians * Mathf.Rad2Deg, 0f) *
                                    (worldPosition - tree.Position);
                if (!IsNearSpiralPath(localPosition, spiralSteps, SpiralSurfacePriorityRadius))
                {
                    continue;
                }

                treeIndex = index;
                return true;
            }

            return false;
        }

        public static float ResolveAssistedStepOffset(bool mobilePerformanceMode)
        {
            return mobilePerformanceMode ? MobileAssistedStepOffset : AssistedStepOffset;
        }

        public static void BuildContinuousSpiralSupport(
            IReadOnlyList<WofTreeHouseSpiralStep> steps,
            out Vector3[] vertices,
            out int[] triangles)
        {
            if (steps == null || steps.Count < 2)
            {
                vertices = System.Array.Empty<Vector3>();
                triangles = System.Array.Empty<int>();
                return;
            }

            vertices = new Vector3[steps.Count * 2];
            triangles = new int[(steps.Count - 1) * 6];
            for (var index = 0; index < steps.Count; index++)
            {
                var center = steps[index].Position + Vector3.up * SpiralSupportSurfaceLift;
                var radial = new Vector3(center.x, 0f, center.z).normalized;
                vertices[index * 2] = center - radial * SpiralSupportHalfWidth;
                vertices[index * 2 + 1] = center + radial * SpiralSupportHalfWidth;

                if (index >= steps.Count - 1)
                {
                    continue;
                }

                var vertex = index * 2;
                var triangle = index * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }
        }

        public static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
            {
                return Vector3.Distance(point, start);
            }

            var t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
            return Vector3.Distance(point, start + segment * t);
        }

        private static bool IsNearSpiralPath(
            Vector3 localPosition,
            IReadOnlyList<WofTreeHouseSpiralStep> steps,
            float radius = SpiralAssistRadius)
        {
            for (var index = 0; index < steps.Count - 1; index++)
            {
                if (DistanceToSegment(localPosition, steps[index].Position, steps[index + 1].Position) <= radius)
                {
                    return true;
                }
            }

            return steps.Count > 0 &&
                   Vector3.Distance(localPosition, steps[steps.Count - 1].Position) <= radius;
        }

        private static Bounds BuildTraversalBounds()
        {
            var initialized = false;
            var bounds = new Bounds();
            void Include(Vector3 point)
            {
                if (!initialized)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    initialized = true;
                    return;
                }
                bounds.Encapsulate(point);
            }

            for (var treeIndex = 0; treeIndex < WofTreeHouseVillageLayout.Trees.Count; treeIndex++)
            {
                var tree = WofTreeHouseVillageLayout.Trees[treeIndex];
                var rotation = Quaternion.Euler(0f, tree.YawRadians * Mathf.Rad2Deg, 0f);
                for (var stepIndex = 0; stepIndex < DesktopSpiralSteps.Count; stepIndex++)
                {
                    Include(tree.Position + rotation * DesktopSpiralSteps[stepIndex].Position);
                }

                Include(WofTreeHouseVillageLayout.GetTreeBasePosition(treeIndex));
                Include(WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, 0));
                foreach (var connection in WofTreeHouseVillageLayout.InternalRopes)
                {
                    Include(WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, connection.StartHouse));
                    Include(WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, connection.EndHouse));
                }
            }

            foreach (var connection in WofTreeHouseVillageLayout.Bridges)
            {
                Include(WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                    connection.StartTree,
                    connection.StartHouse));
                Include(WofTreeHouseVillageLayout.GetHouseBalconyPosition(
                    connection.EndTree,
                    connection.EndHouse));
            }

            var padding = Mathf.Max(SpiralAssistRadius, Mathf.Max(RopeAssistRadius, BridgeAssistRadius));
            bounds.Expand(Vector3.one * (padding * 2f + 0.1f));
            return bounds;
        }
    }
}
