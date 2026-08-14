using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    internal readonly struct WofSurvivalRockOutcropRecord
    {
        public WofSurvivalRockOutcropRecord(
            string key,
            int chunkX,
            int chunkZ,
            Vector3 position,
            float scale,
            float yaw,
            Color32 color,
            int paletteIndex,
            bool spire)
        {
            Key = key;
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Position = position;
            Scale = scale;
            Yaw = yaw;
            Color = color;
            PaletteIndex = paletteIndex;
            Spire = spire;
        }

        public string Key { get; }
        public int ChunkX { get; }
        public int ChunkZ { get; }
        public Vector3 Position { get; }
        public float Scale { get; }
        public float Yaw { get; }
        public Color32 Color { get; }
        public int PaletteIndex { get; }
        public bool Spire { get; }

        public Matrix4x4 Matrix => Matrix4x4.TRS(
            Position + Vector3.up * (Scale * 0.42f),
            Quaternion.Euler(0f, Yaw * Mathf.Rad2Deg, 0f),
            new Vector3(Scale * 1.35f, Scale * (Spire ? 1.95f : 0.75f), Scale));
    }

    internal static class WofSurvivalRockOutcropRules
    {
        internal const int NearJungleCount = 5;
        internal const int NearDefaultCount = 4;
        internal const int MidCount = 1;
        internal const float MinimumNormalY = 0.62f;
        internal const float MaximumHeightRange = 7.8f;
        internal const float FootprintRadius = 5.6f;
        internal const float SurfaceSampleDistance = 4.8f;
        internal const float WaterClearance = 0.24f;
        internal const float VillageClearance = 26f;

        internal static bool ShouldShowRuntime(bool survivalSession)
        {
            // Unlike birds, bushes, and world willows, React intentionally keeps the
            // rock-outcrop layer present during the grass-inspection view.
            return survivalSession;
        }

        private const double DesktopStageOneDelayMilliseconds = 180d;
        private const double MobileStageOneDelayMilliseconds = 280d;
        private const double DesktopDistanceDelayMilliseconds = 280d;
        private const double MobileDistanceDelayMilliseconds = 460d;
        private const double DesktopStageJitterMilliseconds = 520d;
        private const double MobileStageJitterMilliseconds = 760d;
        private const int TreeLoadStageSalt = 9011;

        private static readonly Color32[] DefaultPalette =
        {
            Hex("#777a62"), Hex("#8a866e"), Hex("#5e6652")
        };

        private static readonly Color32[] SwampPalette =
        {
            Hex("#48513a"), Hex("#5c6549"), Hex("#343829")
        };

        internal static bool ShouldGenerateChunk(
            bool survivalSession,
            int chunkX,
            int chunkZ,
            int distance)
        {
            if (!survivalSession || distance < 0 || distance > WofSurvivalTerrainMath.NearRadius)
                return false;
            if (WofSurvivalTerrainMath.IsAuthoredChunk(chunkX, chunkZ)) return false;
            var biome = WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ);
            if (biome == WofSurvivalBiome.Desert || biome == WofSurvivalBiome.Tallgrass) return false;
            return !WofSurvivalTerrainMath.IsWaterSuppressed(
                chunkX * (double)WofSurvivalTerrainMath.BlockSize,
                chunkZ * (double)WofSurvivalTerrainMath.BlockSize,
                WofSurvivalTerrainMath.BlockSize * 0.58d);
        }

        internal static float GetReadyDelaySeconds(int chunkX, int chunkZ, int distance, bool mobile)
        {
            var delay = (mobile ? MobileStageOneDelayMilliseconds : DesktopStageOneDelayMilliseconds) +
                        Math.Max(0, distance) *
                        (mobile ? MobileDistanceDelayMilliseconds : DesktopDistanceDelayMilliseconds) +
                        WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, TreeLoadStageSalt) *
                        (mobile ? MobileStageJitterMilliseconds : DesktopStageJitterMilliseconds);
            return (float)(delay / 1000d);
        }

        internal static WofSurvivalRockOutcropRecord[] MakeChunk(int chunkX, int chunkZ, int distance)
        {
            if (!ShouldGenerateChunk(true, chunkX, chunkZ, distance))
                return Array.Empty<WofSurvivalRockOutcropRecord>();

            var biome = WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ);
            var targetCount = distance == 0
                ? biome == WofSurvivalBiome.Jungle ? NearJungleCount : NearDefaultCount
                : MidCount;
            var palette = biome == WofSurvivalBiome.Swamp ? SwampPalette : DefaultPalette;
            var paletteOffset = biome == WofSurvivalBiome.Swamp ? DefaultPalette.Length : 0;
            var generated = new List<WofSurvivalRockOutcropRecord>(targetCount);
            var attempts = targetCount * 4;
            var sampler = new RenderedTerrainSampler();
            var chunkWorldX = chunkX * (double)WofSurvivalTerrainMath.BlockSize;
            var chunkWorldZ = chunkZ * (double)WofSurvivalTerrainMath.BlockSize;
            var renderSegments = WofSurvivalTerrainMath.GetRenderSegments(distance);

            for (var index = 0; index < attempts && generated.Count < targetCount; index++)
            {
                var localX = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 910 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.86d;
                var localZ = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 960 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.86d;
                var worldX = chunkWorldX + localX;
                var worldZ = chunkWorldZ + localZ;
                var surface = sampler.GetSurfaceQuality(
                    chunkX,
                    chunkZ,
                    renderSegments,
                    localX,
                    localZ,
                    FootprintRadius,
                    SurfaceSampleDistance);
                if (surface.NormalY < MinimumNormalY || surface.HeightRange > MaximumHeightRange) continue;
                if (surface.Y < WofSurvivalTerrainMath.GetWaterLevelAtWorld(worldX, worldZ) + WaterClearance)
                    continue;

                var variant = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 990 + index);
                var localPaletteIndex = (int)Math.Floor(variant * palette.Length) % palette.Length;
                generated.Add(new WofSurvivalRockOutcropRecord(
                    $"{chunkX}:{chunkZ}-rock-{index}",
                    chunkX,
                    chunkZ,
                    new Vector3((float)worldX, (float)surface.Y, (float)worldZ),
                    (float)(1.8d + variant * 3.6d),
                    (float)(WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 1020 + index) * Math.PI * 2d),
                    palette[localPaletteIndex],
                    paletteOffset + localPaletteIndex,
                    variant > 0.76d));
            }

            return generated.ToArray();
        }

        internal static Color32 GetPaletteColor(int paletteIndex)
        {
            return paletteIndex < DefaultPalette.Length
                ? DefaultPalette[Mathf.Clamp(paletteIndex, 0, DefaultPalette.Length - 1)]
                : SwampPalette[Mathf.Clamp(paletteIndex - DefaultPalette.Length, 0, SwampPalette.Length - 1)];
        }

        internal static int PaletteColorCount => DefaultPalette.Length + SwampPalette.Length;

        private static Color32 Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out var color)) return new Color32(255, 255, 255, 255);
            return color;
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
                int chunkX,
                int chunkZ,
                int segments,
                double localX,
                double localZ,
                double footprintRadius,
                double sampleDistance)
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
                    chunkX,
                    chunkZ,
                    WofSurvivalTerrainMath.GetRenderSegments(0),
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
                        chunkX,
                        chunkZ,
                        -half + xIndex * step,
                        -half + zIndex * step);
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
