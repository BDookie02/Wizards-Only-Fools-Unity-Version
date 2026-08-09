using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    public readonly struct WofTreeHouseSpec
    {
        public WofTreeHouseSpec(Vector3 position, float yawRadians, float scale)
        {
            Position = position;
            YawRadians = yawRadians;
            Scale = scale;
        }

        public Vector3 Position { get; }
        public float YawRadians { get; }
        public float Scale { get; }
    }

    public readonly struct WofTreeHouseTreePlacement
    {
        public WofTreeHouseTreePlacement(Vector3 position, float yawRadians)
        {
            Position = position;
            YawRadians = yawRadians;
        }

        public Vector3 Position { get; }
        public float YawRadians { get; }
    }

    public readonly struct WofTreeHouseSpanConnection
    {
        public WofTreeHouseSpanConnection(int startTree, int startHouse, int endTree, int endHouse)
        {
            StartTree = startTree;
            StartHouse = startHouse;
            EndTree = endTree;
            EndHouse = endHouse;
        }

        public int StartTree { get; }
        public int StartHouse { get; }
        public int EndTree { get; }
        public int EndHouse { get; }
    }

    public readonly struct WofTreeHouseSpan
    {
        public WofTreeHouseSpan(float length, Vector3 position, Quaternion rotation)
        {
            Length = length;
            Position = position;
            Rotation = rotation;
        }

        public float Length { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }

    public readonly struct WofTreeHouseSpiralStep
    {
        public WofTreeHouseSpiralStep(int index, Vector3 position, float yawRadians)
        {
            Index = index;
            Position = position;
            YawRadians = yawRadians;
        }

        public int Index { get; }
        public Vector3 Position { get; }
        public float YawRadians { get; }
    }

    public readonly struct WofTreeHouseRopeRung
    {
        public WofTreeHouseRopeRung(int index, Vector3 position)
        {
            Index = index;
            Position = position;
        }

        public int Index { get; }
        public Vector3 Position { get; }
    }

    /// <summary>
    /// Exact, scene-independent port of treeHouseVillageRuntime.ts. The editor
    /// consumes these fixtures when generating the authored central village.
    /// </summary>
    public static class WofTreeHouseVillageLayout
    {
        public static readonly Vector3 DefaultPlayerSpawn = new(0f, 5f, 30f);
        public const float DefaultPlayerYawDegrees = 180f;
        public const int DesktopSpiralStepCount = 30;
        public const int MobileSpiralStepCount = 16;
        public const float DesktopRopeRungStep = 1f;
        public const float MobileRopeRungStep = 2f;

        private static readonly WofTreeHouseSpec[] HouseSpecs =
        {
            new(new Vector3(6.5f, 15f, 6.5f), Mathf.PI * 0.25f, 1.2f),
            new(new Vector3(-7f, 22f, 5f), -Mathf.PI / 6f, 1f),
            new(new Vector3(-2f, 28f, -7.5f), Mathf.PI, 1.5f),
            new(new Vector3(8f, 25f, -4f), Mathf.PI * 0.5f, 0.9f)
        };

        private static readonly WofTreeHouseTreePlacement[] TreePlacements =
        {
            new(new Vector3(0f, -0.5f, 0f), 0f),
            new(new Vector3(25f, -0.5f, 20f), 1.2f),
            new(new Vector3(-28f, -0.5f, 15f), -0.5f),
            new(new Vector3(18f, -0.5f, -26f), 2.1f),
            new(new Vector3(-22f, -0.5f, -24f), 0.8f)
        };

        private static readonly (int StartHouse, int EndHouse)[] InternalRopeConnections =
        {
            (0, 1),
            (1, 3),
            (3, 2)
        };

        private static readonly WofTreeHouseSpanConnection[] BridgeConnections =
        {
            new(0, 0, 1, 0),
            new(0, 0, 2, 0),
            new(0, 0, 3, 0),
            new(0, 0, 4, 0),
            new(1, 0, 2, 0),
            new(2, 0, 4, 0),
            new(4, 0, 3, 0),
            new(3, 0, 1, 0),
            new(0, 2, 1, 1),
            new(1, 2, 3, 3),
            new(0, 1, 2, 0),
            new(2, 2, 4, 3),
            new(0, 3, 4, 1)
        };

        public static IReadOnlyList<WofTreeHouseSpec> Houses => HouseSpecs;
        public static IReadOnlyList<WofTreeHouseTreePlacement> Trees => TreePlacements;
        public static IReadOnlyList<(int StartHouse, int EndHouse)> InternalRopes => InternalRopeConnections;
        public static IReadOnlyList<WofTreeHouseSpanConnection> Bridges => BridgeConnections;

        public static Vector3 GetHouseBalconyPosition(int treeIndex, int houseIndex)
        {
            var tree = TreePlacements[treeIndex];
            var house = HouseSpecs[houseIndex];
            var rotated = Quaternion.Euler(0f, tree.YawRadians * Mathf.Rad2Deg, 0f) * house.Position;
            rotated += tree.Position;
            rotated.y -= 2.5f * house.Scale;
            return rotated;
        }

        public static Vector3 GetTreeBasePosition(int treeIndex)
        {
            var tree = TreePlacements[treeIndex].Position;
            return new Vector3(tree.x, 0f, tree.z);
        }

        public static WofTreeHouseSpan GetSpan(Vector3 start, Vector3 end)
        {
            var direction = end - start;
            var length = direction.magnitude;
            var rotation = length > 0.00001f
                ? Quaternion.LookRotation(direction / length, Vector3.up)
                : Quaternion.identity;
            return new WofTreeHouseSpan(length, (start + end) * 0.5f, rotation);
        }

        public static IReadOnlyList<WofTreeHouseSpiralStep> BuildSpiralSteps(
            float radius = 6.5f,
            float height = 15f,
            int steps = DesktopSpiralStepCount)
        {
            var safeSteps = Mathf.Max(1, steps);
            var denominator = Mathf.Max(1, safeSteps - 1);
            var result = new WofTreeHouseSpiralStep[safeSteps];
            for (var index = 0; index < safeSteps; index++)
            {
                var t = index / (float)denominator;
                var angle = t * Mathf.PI * 4f;
                result[index] = new WofTreeHouseSpiralStep(
                    index,
                    new Vector3(Mathf.Cos(angle) * radius, t * height, Mathf.Sin(angle) * radius),
                    -angle);
            }
            return result;
        }

        public static IReadOnlyList<WofTreeHouseRopeRung> BuildRopeRungs(
            float length,
            float rungStep = DesktopRopeRungStep)
        {
            var safeLength = float.IsFinite(length) ? Mathf.Max(0f, length) : 0f;
            var safeStep = float.IsFinite(rungStep) ? Mathf.Max(0.001f, rungStep) : 1f;
            var count = Mathf.FloorToInt(safeLength / safeStep);
            var result = new WofTreeHouseRopeRung[count];
            for (var index = 0; index < count; index++)
            {
                result[index] = new WofTreeHouseRopeRung(
                    index,
                    new Vector3(0f, 0f, -safeLength * 0.5f + index * safeStep + safeStep * 0.5f));
            }
            return result;
        }
    }
}
