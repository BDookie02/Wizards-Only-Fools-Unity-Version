using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    public enum WofHutType
    {
        Mushroom = 0,
        GrassMound = 1,
        Log = 2,
        DirtAndStone = 3
    }

    public readonly struct WofHutPlacement
    {
        public WofHutPlacement(
            int x,
            float y,
            int z,
            WofHutType hutType,
            int colorIndex,
            float yawRadians,
            bool hasPath,
            float pathYawRadians)
        {
            X = x;
            Y = y;
            Z = z;
            HutType = hutType;
            ColorIndex = colorIndex;
            YawRadians = yawRadians;
            HasPath = hasPath;
            PathYawRadians = pathYawRadians;
        }

        public int X { get; }
        public float Y { get; }
        public int Z { get; }
        public WofHutType HutType { get; }
        public int ColorIndex { get; }
        public float YawRadians { get; }
        public bool HasPath { get; }
        public float PathYawRadians { get; }
    }

    /// <summary>
    /// Deterministic port of the canonical React base-village terrain and hut-layout math.
    /// Keep this free of scene state so golden fixtures can guard the migration.
    /// </summary>
    public static class WofBaseVillageLayout
    {
        public const int MapSize = 512;
        public const int TerrainSegments = 128;
        public const int CollisionSegments = 64;
        public const float WallCenterOffset = 238f;
        public const float WallHeight = 12f;
        public const float WallThickness = 8f;
        public const float CampfireX = 8f;
        public const float CampfireZ = 30f;
        public const float CampfireDamagePerSecond = 2f;
        public const float CampfireDamageRadiusSquared = 6.25f;
        public const float CampfireDamageTickSeconds = 0.1f;
        public const float CampfireDamagePerTick = CampfireDamagePerSecond * CampfireDamageTickSeconds;

        private const double TerrainBlendFeather = 6d;

        public static float GetTerrainHeight(double x, double z)
        {
            var absX = Math.Abs(x);
            var absZ = Math.Abs(z);
            var radiusSquared = x * x + z * z;
            var isRoad = absX < 12d || absZ < 12d;

            if (isRoad)
            {
                if (radiusSquared < 35d * 35d) return -0.5f;
                if (radiusSquared >= 35d * 35d && radiusSquared <= 42d * 42d) return 0f;
                if (radiusSquared > 42d * 42d && radiusSquared < 58d * 58d) return 0.5f;
                if (radiusSquared >= 58d * 58d && radiusSquared <= 125d * 125d) return 1f;
                if (radiusSquared > 125d * 125d && radiusSquared < 145d * 145d) return 1.5f;
                return 2f;
            }

            if ((radiusSquared > 42d * 42d && radiusSquared < 58d * 58d) ||
                (radiusSquared > 125d * 125d && radiusSquared < 145d * 145d))
            {
                return -1.5f;
            }

            if (radiusSquared < 35d * 35d) return -0.5f;
            if (radiusSquared >= 35d * 35d && radiusSquared <= 42d * 42d) return 0f;
            if (radiusSquared >= 58d * 58d && radiusSquared <= 125d * 125d) return 1f;
            return 2f;
        }

        public static bool IsTerrainHutCell(double x, double z)
        {
            var absX = Math.Abs(x);
            var absZ = Math.Abs(z);
            var radiusSquared = x * x + z * z;

            if (absX >= 240d || absZ >= 240d) return true;

            var isRoad = absX < 12d || absZ < 12d;
            var isMoat = (radiusSquared > 42d * 42d && radiusSquared < 58d * 58d) ||
                         (radiusSquared > 125d * 125d && radiusSquared < 145d * 145d);
            var isCentralPlaza = radiusSquared < 35d * 35d;
            var isPath = (absX >= 32d && absX < 40d && radiusSquared > 60d * 60d && radiusSquared < 125d * 125d) ||
                         (absZ >= 32d && absZ < 40d && radiusSquared > 60d * 60d && radiusSquared < 125d * 125d);

            if (isRoad || isMoat || isCentralPlaza || isPath) return false;

            var cellX = Math.Floor((x + 256d) / 16d);
            var cellZ = Math.Floor((z + 256d) / 16d);
            return Fraction(Math.Sin(cellX * 12.9898d + cellZ * 78.233d) * 43758.5453d) <= 0.7d;
        }

        public static bool IsRoadCell(double x, double z)
        {
            var absX = Math.Abs(x);
            var absZ = Math.Abs(z);
            var radius = Math.Sqrt(x * x + z * z);
            var isRoad = absX < 24d || absZ < 24d;
            var isCentralPlaza = radius < 45d;
            var isPath = (absX >= 24d && absX < 48d && radius > 60d && radius < 125d) ||
                         (absZ >= 24d && absZ < 48d && radius > 60d && radius < 125d);
            return isRoad || isCentralPlaza || isPath;
        }

        public static bool IsBlockingCell(double x, double z)
        {
            var absX = Math.Abs(x);
            var absZ = Math.Abs(z);
            var radius = Math.Sqrt(x * x + z * z);

            if (absX >= 230d || absZ >= 230d) return false;
            if ((radius > 30d && radius < 70d) || (radius > 112d && radius < 158d)) return false;
            if (IsRoadCell(x, z)) return false;

            var treePositions = new (double X, double Z)[]
            {
                (0d, 0d),
                (25d, 20d),
                (-28d, 15d),
                (18d, -26d),
                (-22d, -24d)
            };
            foreach (var tree in treePositions)
            {
                if (Math.Abs(x - tree.X) < 20d && Math.Abs(z - tree.Z) < 20d) return false;
            }

            var cellX = Math.Floor((x + 256d) / 16d);
            var cellZ = Math.Floor((z + 256d) / 16d);
            return Fraction(Math.Sin(cellX * 12.9898d + cellZ * 78.233d) * 43758.5453d) <= 0.65d;
        }

        public static IReadOnlyList<WofHutPlacement> BuildHutPlacements()
        {
            var placements = new List<WofHutPlacement>(320);
            for (var x = -240; x <= 240; x += 16)
            {
                for (var z = -240; z <= 240; z += 16)
                {
                    if (!IsBlockingCell(x, z)) continue;

                    var cellX = Math.Floor((x + 256d) / 16d);
                    var cellZ = Math.Floor((z + 256d) / 16d);
                    var uniqueHash = Math.Sin(cellX * 3.123d + cellZ * 4.412d) * 1000d;
                    var hashValue = Fraction(uniqueHash);
                    var hutType = hashValue > 0.5d
                        ? WofHutType.Mushroom
                        : hashValue > 0.33d
                            ? WofHutType.GrassMound
                            : hashValue > 0.16d
                                ? WofHutType.Log
                                : WofHutType.DirtAndStone;
                    var colorIndex = (int)Math.Floor(Math.Abs(uniqueHash * 1000d)) % 4;

                    var validRotations = new List<float>(4);
                    AddRotationWhenOpen(validRotations, x, z + 16, 0f);
                    AddRotationWhenOpen(validRotations, x + 16, z, Mathf.PI * 0.5f);
                    AddRotationWhenOpen(validRotations, x, z - 16, Mathf.PI);
                    AddRotationWhenOpen(validRotations, x - 16, z, -Mathf.PI * 0.5f);
                    if (validRotations.Count == 0) continue;

                    var roadRotations = new List<float>(4);
                    AddRotationWhenRoad(roadRotations, x, z + 16, 0f);
                    AddRotationWhenRoad(roadRotations, x + 16, z, Mathf.PI * 0.5f);
                    AddRotationWhenRoad(roadRotations, x, z - 16, Mathf.PI);
                    AddRotationWhenRoad(roadRotations, x - 16, z, -Mathf.PI * 0.5f);

                    var rotations = roadRotations.Count > 0 ? roadRotations : validRotations;
                    var rotationIndex = (int)Math.Floor(Math.Abs(uniqueHash * 100d)) % rotations.Count;
                    var rotation = rotations[rotationIndex];
                    var hasPath = roadRotations.Count > 0;

                    placements.Add(new WofHutPlacement(
                        x,
                        GetTerrainHeight(x, z),
                        z,
                        hutType,
                        colorIndex,
                        rotation,
                        hasPath,
                        hasPath ? rotation : 0f));
                }
            }

            return placements;
        }

        public static Color GetTerrainColor(double x, double z)
        {
            var height = GetTerrainHeight(x, z);
            var absX = Math.Abs(x);
            var absZ = Math.Abs(z);
            var radius = Math.Sqrt(x * x + z * z);
            var roadMask = Math.Max(1d - SmoothStepRange(10d, 18d, absX), 1d - SmoothStepRange(10d, 18d, absZ));
            var innerMoatMask = GetSoftBandMask(radius, 42d, 58d) * (1d - roadMask);
            var outerMoatMask = GetSoftBandMask(radius, 125d, 145d) * (1d - roadMask);
            var moatMask = Clamp01(innerMoatMask + outerMoatMask);
            var plazaMask = 1d - SmoothStepRange(28d, 42d, radius);
            var pathMask = Math.Max(
                GetSoftBandMask(absX, 32d, 40d, 3.2d) * GetSoftBandMask(radius, 60d, 125d, 5d),
                GetSoftBandMask(absZ, 32d, 40d, 3.2d) * GetSoftBandMask(radius, 60d, 125d, 5d));
            var hutNoise = Math.Sin(x * 0.3d + z * 0.4d) * Math.Cos(x * 0.2d + z * 0.5d);
            var hutDirtMask = IsTerrainHutCell(x, z) ? SmoothStepRange(-0.2d, 1d, hutNoise) : 0d;
            var grassNoise = (Math.Sin(x * 0.038d + z * 0.021d) + Math.Cos(z * 0.031d - x * 0.017d)) * 0.5d;

            var color = Color.Lerp(
                new Color32(79, 135, 48, 255),
                new Color32(120, 185, 79, 255),
                (float)(0.22d + SmoothStepRange(-0.75d, 0.82d, grassNoise) * 0.28d));
            color = Color.Lerp(color, new Color32(92, 64, 51, 255), (float)(hutDirtMask * 0.44d));
            color = Color.Lerp(color, new Color32(194, 160, 119, 255), (float)Clamp01(pathMask * 0.72d + roadMask * 0.82d));
            color = Color.Lerp(color, new Color32(184, 137, 98, 255), (float)(plazaMask * 0.68d));
            color = Color.Lerp(color, new Color32(76, 61, 43, 255), (float)(moatMask * 0.92d));
            var brightness = (float)Lerp(0.94d, 1.08d, SmoothStepRange(-1.5d, 2d, height));
            color.r = Mathf.Clamp01(color.r * brightness);
            color.g = Mathf.Clamp01(color.g * brightness);
            color.b = Mathf.Clamp01(color.b * brightness);
            color.a = 1f;
            return color;
        }

        public static bool IsWithinCampfireDamageRadius(Vector3 point)
        {
            var campfire = new Vector3(CampfireX, GetTerrainHeight(CampfireX, CampfireZ), CampfireZ);
            return (point - campfire).sqrMagnitude < CampfireDamageRadiusSquared;
        }

        public static (float Intensity, float LightY) GetCampfireFlicker(float elapsedSeconds)
        {
            var quick = Mathf.Sin(elapsedSeconds * 19.7f) * 0.16f;
            var slow = Mathf.Sin(elapsedSeconds * 7.1f + 1.4f) * 0.1f;
            var ember = Mathf.Sin(elapsedSeconds * 31.3f + 0.7f) * 0.05f;
            var pulse = quick + slow + ember;
            return (2.18f + pulse, 1.08f + pulse * 0.18f);
        }

        private static void AddRotationWhenOpen(List<float> rotations, int x, int z, float rotation)
        {
            if (!IsBlockingCell(x, z)) rotations.Add(rotation);
        }

        private static void AddRotationWhenRoad(List<float> rotations, int x, int z, float rotation)
        {
            if (IsRoadCell(x, z)) rotations.Add(rotation);
        }

        private static double GetSoftBandMask(double value, double inner, double outer, double feather = TerrainBlendFeather)
        {
            return SmoothStepRange(inner - feather, inner + feather, value) *
                   (1d - SmoothStepRange(outer - feather, outer + feather, value));
        }

        private static double SmoothStepRange(double edge0, double edge1, double value)
        {
            var t = Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3d - 2d * t);
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * Clamp01(t);
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0d, Math.Min(1d, value));
        }

        private static double Fraction(double value)
        {
            return value - Math.Floor(value);
        }
    }
}
