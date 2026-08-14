using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    internal readonly struct WofSurvivalBushLobeRecord
    {
        public WofSurvivalBushLobeRecord(
            int sourceIndex, int lobeIndex, Vector3 position, Vector3 rotationRadians,
            Vector3 scale, int colorIndex)
        {
            SourceIndex = sourceIndex;
            LobeIndex = lobeIndex;
            Position = position;
            RotationRadians = rotationRadians;
            Scale = scale;
            ColorIndex = colorIndex;
        }

        public int SourceIndex { get; }
        public int LobeIndex { get; }
        public Vector3 Position { get; }
        public Vector3 RotationRadians { get; }
        public Vector3 Scale { get; }
        public int ColorIndex { get; }
        public Matrix4x4 Matrix => WofSurvivalUnderbrushRules.MakeThreeJsMatrix(
            Position, RotationRadians, Scale);
    }

    internal readonly struct WofSurvivalFernRecord
    {
        public WofSurvivalFernRecord(
            int sourceIndex, Vector3 position, Vector3 rotationRadians,
            Vector2 scale, int colorIndex)
        {
            SourceIndex = sourceIndex;
            Position = position;
            RotationRadians = rotationRadians;
            Scale = scale;
            ColorIndex = colorIndex;
        }

        public int SourceIndex { get; }
        public Vector3 Position { get; }
        public Vector3 RotationRadians { get; }
        public Vector2 Scale { get; }
        public int ColorIndex { get; }
        public Matrix4x4 Matrix => WofSurvivalUnderbrushRules.MakeThreeJsMatrix(
            Position + Vector3.up * (Scale.y * 0.5f),
            RotationRadians,
            new Vector3(Scale.x, Scale.y, 1f));
    }

    internal sealed class WofSurvivalUnderbrushChunk
    {
        public WofSurvivalUnderbrushChunk(
            int bushClusterCount,
            WofSurvivalBushLobeRecord[] bushLobes,
            WofSurvivalFernRecord[] ferns)
        {
            BushClusterCount = bushClusterCount;
            BushLobes = bushLobes;
            Ferns = ferns;
        }

        public int BushClusterCount { get; }
        public WofSurvivalBushLobeRecord[] BushLobes { get; }
        public WofSurvivalFernRecord[] Ferns { get; }
    }

    internal static class WofSurvivalUnderbrushRules
    {
        internal const float BushMinimumNormalY = 0.68f;
        internal const float BushMaximumHeightRange = 7.4f;
        internal const float BushWaterClearance = 0.18f;
        internal const float FernMinimumNormalY = 0.78f;
        internal const float FernMaximumHeightRange = 2.4f;
        internal const float FernWaterClearance = 0.08f;
        internal const float FernRouteMaskMaximum = 0.12f;
        internal const float FernOpacity = 0.88f;
        private const float DesktopNearDensity = 0.42f;
        private const float DesktopMidDensity = 0.12f;
        private const float MobileDensityMultiplier = 0.58f;
        private const int TreeLoadStageSalt = 9011;
        private const double DesktopStageTwoDelayMilliseconds = 560d;
        private const double MobileStageTwoDelayMilliseconds = 820d;
        private const double DesktopDistanceDelayMilliseconds = 280d;
        private const double MobileDistanceDelayMilliseconds = 460d;
        private const double DesktopStageJitterMilliseconds = 520d;
        private const double MobileStageJitterMilliseconds = 760d;

        private static readonly Color32[,] BushPalettes =
        {
            { Hex("#416f2f"), Hex("#5e9341"), Hex("#7aad55") },
            { Hex("#174d26"), Hex("#246b31"), Hex("#3a8e45") },
            { Hex("#8a7139"), Hex("#b68e43"), Hex("#d0ad62") },
            { Hex("#33441f"), Hex("#526126"), Hex("#687337") },
            { Hex("#4f6d3c"), Hex("#745699"), Hex("#a976bf") },
            { Hex("#5c7d2f"), Hex("#7d9539"), Hex("#a9a84c") }
        };

        private static readonly Color32[,] FernPalettes =
        {
            { Hex("#3f8f35"), Hex("#67a843"), Hex("#87bd52") },
            { Hex("#0f6b34"), Hex("#168342"), Hex("#25a05a") },
            { Hex("#8b7437"), Hex("#b59145"), Hex("#cfab5f") },
            { Hex("#334c23"), Hex("#54642d"), Hex("#6d793a") },
            { Hex("#586f43"), Hex("#885caa"), Hex("#b478d0") },
            { Hex("#627f2d"), Hex("#8e9f3a"), Hex("#b0a849") }
        };

        internal static bool ShouldGenerateChunk(
            bool survivalSession,
            bool grassInspectionView,
            int chunkX,
            int chunkZ,
            int distance)
        {
            if (!survivalSession || grassInspectionView || distance < 0 ||
                distance > WofSurvivalTerrainMath.NearRadius)
                return false;
            if (WofSurvivalTerrainMath.IsAuthoredChunk(chunkX, chunkZ)) return false;
            var biome = WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ);
            return biome == WofSurvivalBiome.Desert ||
                   (biome != WofSurvivalBiome.Tallgrass &&
                    !WofSurvivalTerrainMath.IsWaterSuppressed(
                        chunkX * (double)WofSurvivalTerrainMath.BlockSize,
                        chunkZ * (double)WofSurvivalTerrainMath.BlockSize,
                        WofSurvivalTerrainMath.BlockSize * 0.58d));
        }

        internal static float GetReadyDelaySeconds(int chunkX, int chunkZ, int distance, bool mobile)
        {
            var delay = (mobile ? MobileStageTwoDelayMilliseconds : DesktopStageTwoDelayMilliseconds) +
                        Math.Max(0, distance) *
                        (mobile ? MobileDistanceDelayMilliseconds : DesktopDistanceDelayMilliseconds) +
                        WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, TreeLoadStageSalt) *
                        (mobile ? MobileStageJitterMilliseconds : DesktopStageJitterMilliseconds);
            return (float)(delay / 1000d);
        }

        internal static WofSurvivalUnderbrushChunk MakeChunk(
            int chunkX,
            int chunkZ,
            int distance,
            bool mobile)
        {
            if (!ShouldGenerateChunk(true, false, chunkX, chunkZ, distance))
                return EmptyChunk();
            var sampler = new RenderedTerrainSampler();
            var bushLobes = MakeBushes(chunkX, chunkZ, distance, mobile, sampler, out var bushClusters);
            var ferns = MakeFerns(chunkX, chunkZ, distance, mobile, sampler);
            return new WofSurvivalUnderbrushChunk(bushClusters, bushLobes, ferns);
        }

        internal static Color32 GetBushColor(WofSurvivalBiome biome, int colorIndex) =>
            BushPalettes[(int)biome, Mathf.Clamp(colorIndex, 0, 2)];

        internal static Color32 GetFernColor(WofSurvivalBiome biome, int colorIndex) =>
            FernPalettes[(int)biome, Mathf.Clamp(colorIndex, 0, 2)];

        internal static Color32 GetBushEdgeColor(WofSurvivalBiome biome, int colorIndex)
        {
            var fill = (Color)GetBushColor(biome, colorIndex);
            var luminance = fill.r * 0.2126f + fill.g * 0.7152f + fill.b * 0.0722f;
            var target = (Color)Hex(luminance < 0.38f ? "#d9f99d" : "#17250f");
            var result = Color.Lerp(fill, target, luminance < 0.38f ? 0.74f : 0.78f);
            result.a = 0.74f;
            return result;
        }

        internal static Matrix4x4 MakeThreeJsMatrix(
            Vector3 position,
            Vector3 rotationRadians,
            Vector3 scale)
        {
            var a = Math.Cos(rotationRadians.x);
            var b = Math.Sin(rotationRadians.x);
            var c = Math.Cos(rotationRadians.y);
            var d = Math.Sin(rotationRadians.y);
            var e = Math.Cos(rotationRadians.z);
            var f = Math.Sin(rotationRadians.z);
            var ae = a * e;
            var af = a * f;
            var be = b * e;
            var bf = b * f;
            var matrix = Matrix4x4.identity;
            matrix.m00 = (float)(c * e * scale.x);
            matrix.m10 = (float)((af + be * d) * scale.x);
            matrix.m20 = (float)((bf - ae * d) * scale.x);
            matrix.m01 = (float)(-c * f * scale.y);
            matrix.m11 = (float)((ae - bf * d) * scale.y);
            matrix.m21 = (float)((be + af * d) * scale.y);
            matrix.m02 = (float)(d * scale.z);
            matrix.m12 = (float)(-b * c * scale.z);
            matrix.m22 = (float)(a * c * scale.z);
            matrix.m03 = position.x;
            matrix.m13 = position.y;
            matrix.m23 = position.z;
            return matrix;
        }

        private static WofSurvivalBushLobeRecord[] MakeBushes(
            int chunkX, int chunkZ, int distance, bool mobile,
            RenderedTerrainSampler sampler, out int clusterCount)
        {
            var biome = WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ);
            var baseCount = biome switch
            {
                WofSurvivalBiome.Jungle => 50,
                WofSurvivalBiome.Swamp => 44,
                WofSurvivalBiome.Mushroom => 38,
                WofSurvivalBiome.Desert => 26,
                _ => 40
            };
            var density = (distance > 0 ? DesktopMidDensity : DesktopNearDensity) *
                          (mobile ? MobileDensityMultiplier : 1f);
            var targetCount = RoundPositive(baseCount * density);
            var attempts = targetCount * 3;
            var generated = new List<WofSurvivalBushLobeRecord>(targetCount * 5);
            var chunkWorldX = chunkX * (double)WofSurvivalTerrainMath.BlockSize;
            var chunkWorldZ = chunkZ * (double)WofSurvivalTerrainMath.BlockSize;
            var segments = WofSurvivalTerrainMath.GetRenderSegments(distance);
            clusterCount = 0;

            for (var index = 0; index < attempts && clusterCount < targetCount; index++)
            {
                var localX = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 1810 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.9d;
                var localZ = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 1850 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.9d;
                var worldX = chunkWorldX + localX;
                var worldZ = chunkWorldZ + localZ;
                var surface = sampler.GetSurfaceQuality(
                    chunkX, chunkZ, segments, localX, localZ, 6.6d, 4.8d);
                if (surface.NormalY < BushMinimumNormalY ||
                    surface.HeightRange > BushMaximumHeightRange ||
                    surface.Y < WofSurvivalTerrainMath.GetWaterLevelAtWorld(worldX, worldZ) +
                    BushWaterClearance)
                    continue;

                var shape = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 1890 + index);
                var biomeScale = biome switch
                {
                    WofSurvivalBiome.Jungle => 2.2d,
                    WofSurvivalBiome.Swamp => 1.9d,
                    WofSurvivalBiome.Desert => 1.05d,
                    _ => 1.55d
                };
                var height = (1.5d + shape * 3.2d) * biomeScale;
                var width = height * (1.28d + WofSurvivalTerrainMath.Hash01(
                    chunkX, chunkZ, 1930 + index) * 1.22d);
                var lobeCount = 3 + (int)Math.Floor(WofSurvivalTerrainMath.Hash01(
                    chunkX, chunkZ, 1970 + index) * 4d);
                var yaw = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 2010 + index) * Math.PI * 2d;

                for (var lobeIndex = 0; lobeIndex < lobeCount; lobeIndex++)
                {
                    var angle = yaw + lobeIndex / (double)lobeCount * Math.PI * 2d +
                                (WofSurvivalTerrainMath.Hash01(index, lobeIndex, 2030) - 0.5d) * 0.78d;
                    var spread = width * (0.12d + WofSurvivalTerrainMath.Hash01(index, lobeIndex, 2040) * 0.24d);
                    var lobeHeight = height * (0.58d + WofSurvivalTerrainMath.Hash01(index, lobeIndex, 2050) * 0.72d);
                    var lobeWidth = width * (0.34d + WofSurvivalTerrainMath.Hash01(index, lobeIndex, 2060) * 0.5d);
                    var lobeDepth = height * (0.44d + WofSurvivalTerrainMath.Hash01(index, lobeIndex, 2070) * 0.58d);
                    generated.Add(new WofSurvivalBushLobeRecord(
                        index,
                        lobeIndex,
                        new Vector3(
                            (float)(worldX + Math.Sin(angle) * spread),
                            (float)(surface.Y + lobeHeight *
                                (0.42d + WofSurvivalTerrainMath.Hash01(index, lobeIndex, 2080) * 0.14d)),
                            (float)(worldZ + Math.Cos(angle) * spread)),
                        new Vector3(
                            (float)((WofSurvivalTerrainMath.Hash01(index, lobeIndex, 2100) - 0.5d) * 0.18d),
                            (float)(angle + WofSurvivalTerrainMath.Hash01(index, lobeIndex, 2090) * 0.7d),
                            (float)((WofSurvivalTerrainMath.Hash01(index, lobeIndex, 2110) - 0.5d) * 0.28d)),
                        new Vector3((float)lobeWidth, (float)lobeHeight, (float)lobeDepth),
                        (int)Math.Floor(WofSurvivalTerrainMath.Hash01(
                            chunkX, chunkZ, 2120 + index + lobeIndex * 17) * 3d) % 3));
                }
                clusterCount++;
            }
            return generated.ToArray();
        }

        private static WofSurvivalFernRecord[] MakeFerns(
            int chunkX, int chunkZ, int distance, bool mobile,
            RenderedTerrainSampler sampler)
        {
            var biome = WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ);
            if (biome == WofSurvivalBiome.Desert) return Array.Empty<WofSurvivalFernRecord>();
            var mid = distance > 0;
            var baseCount = mid
                ? biome == WofSurvivalBiome.Jungle ? 34 : biome == WofSurvivalBiome.Swamp ? 26 : 24
                : biome == WofSurvivalBiome.Jungle ? 140 : biome == WofSurvivalBiome.Swamp ? 105 :
                  biome == WofSurvivalBiome.Mushroom ? 100 : 105;
            var targetCount = Math.Max(mid ? 3 : 18, RoundPositive(
                baseCount * (mid ? 0.42d : 0.5d) * (mobile ? MobileDensityMultiplier : 1d)));
            var generated = new List<WofSurvivalFernRecord>(targetCount);
            var chunkWorldX = chunkX * (double)WofSurvivalTerrainMath.BlockSize;
            var chunkWorldZ = chunkZ * (double)WofSurvivalTerrainMath.BlockSize;
            var segments = WofSurvivalTerrainMath.GetRenderSegments(distance);

            for (var index = 0; index < targetCount * 3 && generated.Count < targetCount; index++)
            {
                var localX = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 2610 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.93d;
                var localZ = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 2650 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.93d;
                var worldX = chunkWorldX + localX;
                var worldZ = chunkWorldZ + localZ;
                if (WofSurvivalTerrainMath.GetTownRouteMaskAtWorld(worldX, worldZ) >
                    FernRouteMaskMaximum) continue;
                var surface = sampler.GetSurfaceQuality(
                    chunkX, chunkZ, segments, localX, localZ, 2.6d, 3.4d);
                if (surface.NormalY < FernMinimumNormalY ||
                    surface.HeightRange > FernMaximumHeightRange ||
                    surface.Y < WofSurvivalTerrainMath.GetWaterLevelAtWorld(worldX, worldZ) +
                    FernWaterClearance)
                    continue;

                var variant = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 2690 + index);
                var biomeScale = biome == WofSurvivalBiome.Jungle ? 1.75d :
                    biome == WofSurvivalBiome.Swamp ? 1.45d : 1.1d;
                var yaw = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 2730 + index) * Math.PI * 2d;
                generated.Add(new WofSurvivalFernRecord(
                    index,
                    new Vector3((float)worldX, (float)(surface.Y + 0.08d), (float)worldZ),
                    new Vector3(
                        (float)(-0.3d + WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 2770 + index) * 0.58d),
                        (float)yaw,
                        (float)(Math.Sin(yaw) * 0.18d)),
                    new Vector2(
                        (float)((0.42d + variant * 0.68d) * biomeScale),
                        (float)((2.1d + variant * 4.2d) * biomeScale)),
                    (int)Math.Floor(WofSurvivalTerrainMath.Hash01(
                        chunkX, chunkZ, 2810 + index) * 3d) % 3));
            }
            return generated.ToArray();
        }

        private static int RoundPositive(double value) => (int)Math.Floor(value + 0.5d);
        private static WofSurvivalUnderbrushChunk EmptyChunk() => new(
            0, Array.Empty<WofSurvivalBushLobeRecord>(), Array.Empty<WofSurvivalFernRecord>());

        private static Color32 Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color)
                ? color
                : new Color32(255, 255, 255, 255);
        }

        private readonly struct SurfaceQuality
        {
            public SurfaceQuality(double y, double normalY, double heightRange)
            {
                Y = y;
                NormalY = normalY;
                HeightRange = heightRange;
            }
            public double Y { get; }
            public double NormalY { get; }
            public double HeightRange { get; }
        }

        private sealed class RenderedTerrainSampler
        {
            private readonly Dictionary<GridKey, float[]> _grids = new();

            public SurfaceQuality GetSurfaceQuality(
                int chunkX, int chunkZ, int segments, double localX, double localZ,
                double footprintRadius, double sampleDistance)
            {
                var terrainY = GetRenderedHeight(chunkX, chunkZ, segments, localX, localZ);
                var left = GetRenderedHeight(chunkX, chunkZ, segments, localX - sampleDistance, localZ);
                var right = GetRenderedHeight(chunkX, chunkZ, segments, localX + sampleDistance, localZ);
                var down = GetRenderedHeight(chunkX, chunkZ, segments, localX, localZ - sampleDistance);
                var up = GetRenderedHeight(chunkX, chunkZ, segments, localX, localZ + sampleDistance);
                var normalX = left - right;
                var normalY = sampleDistance * 2d;
                var normalZ = down - up;
                normalY /= Math.Sqrt(normalX * normalX + normalY * normalY + normalZ * normalZ);
                var worldX = chunkX * (double)WofSurvivalTerrainMath.BlockSize + localX;
                var worldZ = chunkZ * (double)WofSurvivalTerrainMath.BlockSize + localZ;
                var footprint = GetFootprintStats(worldX, worldZ, footprintRadius);
                return new SurfaceQuality(Math.Min(terrainY, footprint.BaseY), normalY, footprint.HeightRange);
            }

            private double GetRenderedHeight(int chunkX, int chunkZ, int segments, double localX, double localZ)
            {
                var grid = GetGrid(chunkX, chunkZ, segments);
                var x = Clamp01((localX + WofSurvivalTerrainMath.BlockSize * 0.5d) /
                                WofSurvivalTerrainMath.BlockSize) * segments;
                var z = Clamp01((localZ + WofSurvivalTerrainMath.BlockSize * 0.5d) /
                                WofSurvivalTerrainMath.BlockSize) * segments;
                var x0 = (int)Math.Floor(x);
                var z0 = (int)Math.Floor(z);
                var x1 = Math.Min(segments, x0 + 1);
                var z1 = Math.Min(segments, z0 + 1);
                var tx = x - x0;
                var tz = z - z0;
                var size = segments + 1;
                var h00 = grid[z0 * size + x0];
                var h10 = grid[z0 * size + x1];
                var h01 = grid[z1 * size + x0];
                var h11 = grid[z1 * size + x1];
                return Lerp(Lerp(h00, h10, tx), Lerp(h01, h11, tx), tz);
            }

            private FootprintStats GetFootprintStats(double worldX, double worldZ, double radius)
            {
                var sampleRadius = Math.Max(0.16d, radius);
                var diagonal = sampleRadius * 0.72d;
                var centerY = GetGrassHeightAtWorld(worldX, worldZ);
                var minimum = centerY;
                var maximum = centerY;
                Sample(worldX + sampleRadius, worldZ, ref minimum, ref maximum);
                Sample(worldX - sampleRadius, worldZ, ref minimum, ref maximum);
                Sample(worldX, worldZ + sampleRadius, ref minimum, ref maximum);
                Sample(worldX, worldZ - sampleRadius, ref minimum, ref maximum);
                Sample(worldX + diagonal, worldZ + diagonal, ref minimum, ref maximum);
                Sample(worldX - diagonal, worldZ + diagonal, ref minimum, ref maximum);
                Sample(worldX + diagonal, worldZ - diagonal, ref minimum, ref maximum);
                Sample(worldX - diagonal, worldZ - diagonal, ref minimum, ref maximum);
                var range = maximum - minimum;
                var slopeTuck = SmoothstepRange(0.08d, 1.55d, range);
                var tuckedCenterY = centerY + Lerp(0.004d, -0.035d, slopeTuck);
                return new FootprintStats(Math.Max(minimum - 0.025d, tuckedCenterY), range);
            }

            private void Sample(double worldX, double worldZ, ref double minimum, ref double maximum)
            {
                var y = GetGrassHeightAtWorld(worldX, worldZ);
                minimum = Math.Min(minimum, y);
                maximum = Math.Max(maximum, y);
            }

            private double GetGrassHeightAtWorld(double worldX, double worldZ)
            {
                var chunkX = WofSurvivalTerrainMath.GetChunkCoordinate(worldX);
                var chunkZ = WofSurvivalTerrainMath.GetChunkCoordinate(worldZ);
                return GetRenderedHeight(
                    chunkX, chunkZ, WofSurvivalTerrainMath.GetRenderSegments(0),
                    worldX - chunkX * (double)WofSurvivalTerrainMath.BlockSize,
                    worldZ - chunkZ * (double)WofSurvivalTerrainMath.BlockSize);
            }

            private float[] GetGrid(int chunkX, int chunkZ, int segments)
            {
                var key = new GridKey(chunkX, chunkZ, segments);
                if (_grids.TryGetValue(key, out var existing)) return existing;
                var size = segments + 1;
                var result = new float[size * size];
                var step = WofSurvivalTerrainMath.BlockSize / (double)segments;
                var half = WofSurvivalTerrainMath.BlockSize * 0.5d;
                for (var zIndex = 0; zIndex <= segments; zIndex++)
                for (var xIndex = 0; xIndex <= segments; xIndex++)
                {
                    result[zIndex * size + xIndex] = (float)WofSurvivalTerrainMath.GetTerrainHeight(
                        chunkX, chunkZ, -half + xIndex * step, -half + zIndex * step);
                }
                _grids.Add(key, result);
                return result;
            }

            private static double Clamp01(double value) => value < 0d ? 0d : value > 1d ? 1d : value;
            private static double Lerp(double a, double b, double amount) => a + (b - a) * amount;
            private static double SmoothstepRange(double minimum, double maximum, double value)
            {
                var amount = Clamp01((value - minimum) / (maximum - minimum));
                return amount * amount * (3d - 2d * amount);
            }
        }

        private readonly struct FootprintStats
        {
            public FootprintStats(double baseY, double heightRange)
            {
                BaseY = baseY;
                HeightRange = heightRange;
            }
            public double BaseY { get; }
            public double HeightRange { get; }
        }

        private readonly struct GridKey : IEquatable<GridKey>
        {
            public GridKey(int x, int z, int segments)
            {
                X = x;
                Z = z;
                Segments = segments;
            }
            private int X { get; }
            private int Z { get; }
            private int Segments { get; }
            public bool Equals(GridKey other) => X == other.X && Z == other.Z && Segments == other.Segments;
            public override bool Equals(object obj) => obj is GridKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(X, Z, Segments);
        }
    }
}
