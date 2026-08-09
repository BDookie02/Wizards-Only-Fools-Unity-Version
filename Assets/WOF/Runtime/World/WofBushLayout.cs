using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    public readonly struct WofBushInstance
    {
        public WofBushInstance(double x, double y, double z, double widthScale, double heightScale, double yaw, double variant)
        {
            X = x;
            Y = y;
            Z = z;
            WidthScale = widthScale;
            HeightScale = heightScale;
            Yaw = yaw;
            Variant = variant;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double WidthScale { get; }
        public double HeightScale { get; }
        public double Yaw { get; }
        public double Variant { get; }
    }

    public readonly struct WofBushLobe
    {
        public WofBushLobe(
            int bushIndex,
            int lobeIndex,
            int colorIndex,
            double x,
            double y,
            double z,
            double pitch,
            double yaw,
            double roll,
            double width,
            double height,
            double depth)
        {
            BushIndex = bushIndex;
            LobeIndex = lobeIndex;
            ColorIndex = colorIndex;
            X = x;
            Y = y;
            Z = z;
            Pitch = pitch;
            Yaw = yaw;
            Roll = roll;
            Width = width;
            Height = height;
            Depth = depth;
        }

        public int BushIndex { get; }
        public int LobeIndex { get; }
        public int ColorIndex { get; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double Pitch { get; }
        public double Yaw { get; }
        public double Roll { get; }
        public double Width { get; }
        public double Height { get; }
        public double Depth { get; }
    }

    internal sealed class WofSeededRandom
    {
        private uint _seed;

        public WofSeededRandom(string seedText)
        {
            _seed = 2166136261u;
            foreach (var character in seedText ?? string.Empty)
            {
                _seed ^= character;
                _seed = unchecked(_seed * 16777619u);
            }
        }

        public double NextDouble()
        {
            _seed = unchecked(_seed + 0x6D2B79F5u);
            var value = _seed;
            value = unchecked((value ^ (value >> 15)) * (value | 1u));
            value ^= unchecked(value + unchecked((value ^ (value >> 7)) * (value | 61u)));
            return (value ^ (value >> 14)) / 4294967296d;
        }
    }

    public static class WofBushLayout
    {
        public const int DesktopAttempts = 600;
        public const int MobileAttempts = 150;
        public const int LobeCount = 5;
        public const double MapSize = 510d;

        private static readonly (double X, double Z)[] TreeBlockers =
        {
            (0d, 0d),
            (25d, 20d),
            (-28d, 15d),
            (18d, -26d),
            (-22d, -24d)
        };

        public static IReadOnlyList<WofBushInstance> BuildBushes(int attempts = DesktopAttempts, double mapSize = MapSize)
        {
            var random = new WofSeededRandom($"base-village-bushes:{mapSize:0}");
            var bushes = new List<WofBushInstance>(attempts);
            for (var index = 0; index < attempts; index++)
            {
                var x = (random.NextDouble() - 0.5d) * mapSize;
                var z = (random.NextDouble() - 0.5d) * mapSize;
                var y = WofBaseVillageLayout.GetTerrainHeight(x, z);
                if (y > 2f || y < -0.5f) continue;

                var absoluteX = Math.Abs(x);
                var absoluteZ = Math.Abs(z);
                var radius = Math.Sqrt(x * x + z * z);
                var isRoad = absoluteX < 12d || absoluteZ < 12d;
                var isMoat = (radius > 42d && radius < 58d) || (radius > 125d && radius < 145d);
                var isCentralPlaza = radius < 35d;
                var isPath = ((absoluteX >= 32d && absoluteX < 40d) ||
                              (absoluteZ >= 32d && absoluteZ < 40d)) &&
                             radius > 60d && radius < 125d;
                if (isRoad || isMoat || isCentralPlaza || isPath) continue;

                var floorX = Math.Floor(x / 16d) * 16d;
                var ceilX = Math.Ceiling(x / 16d) * 16d;
                var floorZ = Math.Floor(z / 16d) * 16d;
                var ceilZ = Math.Ceiling(z / 16d) * 16d;
                if (IsInsideHutBlocker(x, z, floorX, floorZ) ||
                    IsInsideHutBlocker(x, z, floorX, ceilZ) ||
                    IsInsideHutBlocker(x, z, ceilX, floorZ) ||
                    IsInsideHutBlocker(x, z, ceilX, ceilZ) ||
                    IsInsideTreeBlocker(x, z))
                {
                    continue;
                }

                var heightScale = 3d + random.NextDouble() * 4d;
                var widthScale = heightScale * (1.5d + random.NextDouble() * 2d);
                bushes.Add(new WofBushInstance(
                    x,
                    y,
                    z,
                    widthScale,
                    heightScale,
                    random.NextDouble() * Math.PI * 2d,
                    random.NextDouble()));
            }
            return bushes;
        }

        public static IReadOnlyList<WofBushLobe> BuildLobes(IReadOnlyList<WofBushInstance> bushes)
        {
            var lobes = new List<WofBushLobe>(bushes.Count * LobeCount);
            for (var bushIndex = 0; bushIndex < bushes.Count; bushIndex++)
            {
                var bush = bushes[bushIndex];
                for (var lobeIndex = 0; lobeIndex < LobeCount; lobeIndex++)
                {
                    var center = lobeIndex == 0;
                    var angle = bush.Yaw + lobeIndex / (double)LobeCount * Math.PI * 2d + bush.Variant * 0.45d;
                    var spread = center ? 0d : bush.WidthScale * (0.12d + lobeIndex * 0.018d);
                    var width = bush.WidthScale * (center ? 0.56d : 0.28d + (lobeIndex + bushIndex) % 3 * 0.055d);
                    var height = bush.HeightScale * (center ? 0.82d : 0.44d + (lobeIndex + bushIndex) % 4 * 0.07d);
                    var depth = bush.HeightScale * (center ? 0.66d : 0.38d + (lobeIndex + bushIndex) % 3 * 0.08d);
                    var y = bush.Y + height * (center ? 0.5d : 0.42d + lobeIndex * 0.025d);
                    lobes.Add(new WofBushLobe(
                        bushIndex,
                        lobeIndex,
                        (bushIndex + lobeIndex) % 3,
                        bush.X + Math.Sin(angle) * spread,
                        y,
                        bush.Z + Math.Cos(angle) * spread,
                        center ? 0d : (bush.Variant - 0.5d) * 0.16d,
                        angle,
                        center ? 0d : Math.Sin(angle) * 0.18d,
                        width,
                        height,
                        depth));
                }
            }
            return lobes;
        }

        public static Matrix4x4 ToThreeJsMatrix(WofBushLobe lobe)
        {
            var a = Math.Cos(lobe.Pitch);
            var b = Math.Sin(lobe.Pitch);
            var c = Math.Cos(lobe.Yaw);
            var d = Math.Sin(lobe.Yaw);
            var e = Math.Cos(lobe.Roll);
            var f = Math.Sin(lobe.Roll);
            var ae = a * e;
            var af = a * f;
            var be = b * e;
            var bf = b * f;

            var matrix = Matrix4x4.identity;
            matrix.m00 = (float)(c * e * lobe.Width);
            matrix.m10 = (float)((af + be * d) * lobe.Width);
            matrix.m20 = (float)((bf - ae * d) * lobe.Width);
            matrix.m01 = (float)(-c * f * lobe.Height);
            matrix.m11 = (float)((ae - bf * d) * lobe.Height);
            matrix.m21 = (float)((be + af * d) * lobe.Height);
            matrix.m02 = (float)(d * lobe.Depth);
            matrix.m12 = (float)(-b * c * lobe.Depth);
            matrix.m22 = (float)(a * c * lobe.Depth);
            matrix.m03 = (float)lobe.X;
            matrix.m13 = (float)lobe.Y;
            matrix.m23 = (float)lobe.Z;
            return matrix;
        }

        private static bool IsInsideHutBlocker(double x, double z, double hutX, double hutZ)
        {
            if (!WofBaseVillageLayout.IsBlockingCell(hutX, hutZ)) return false;
            var deltaX = x - hutX;
            var deltaZ = z - hutZ;
            return deltaX * deltaX + deltaZ * deltaZ < 400d;
        }

        private static bool IsInsideTreeBlocker(double x, double z)
        {
            foreach (var blocker in TreeBlockers)
            {
                var deltaX = x - blocker.X;
                var deltaZ = z - blocker.Z;
                if (deltaX * deltaX + deltaZ * deltaZ < 400d) return true;
            }
            return false;
        }
    }
}
