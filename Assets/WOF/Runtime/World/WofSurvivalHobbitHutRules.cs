using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    internal readonly struct WofSurvivalHobbitHutRecord
    {
        public WofSurvivalHobbitHutRecord(
            int sourceIndex,
            WofSurvivalBiome biome,
            Vector3 position,
            float yawRadians,
            float scale,
            float variant)
        {
            SourceIndex = sourceIndex;
            Biome = biome;
            Position = position;
            YawRadians = yawRadians;
            Scale = scale;
            Variant = variant;
        }

        public int SourceIndex { get; }
        public WofSurvivalBiome Biome { get; }
        public Vector3 Position { get; }
        public float YawRadians { get; }
        public float Scale { get; }
        public float Variant { get; }
    }

    internal static class WofSurvivalHobbitHutRules
    {
        internal const int SpawnSalt = 7310;
        internal const float FootprintRadius = 13.5f;
        internal const float SurfaceSampleDistance = 7.2f;
        internal const float MinimumNormalY = 0.82f;
        internal const float MaximumSurfaceHeightRange = 5.8f;
        internal const float RouteHalfWidth = 54f;
        internal const float WaterClearance = 0.42f;
        internal const float CardinalSampleDistance = 7f;
        internal const float MaximumCardinalHeightRange = 7.5f;
        internal const float DesktopStageFiveDelaySeconds = 3.6f;
        internal const float DesktopDistanceDelaySeconds = 0.28f;
        internal const float DesktopStageJitterSeconds = 0.52f;
        internal const int TreeLoadStageSalt = 9011;
        internal static readonly Vector3 ColliderCenter = new(0f, 2.9f, 0.8f);
        internal static readonly Vector3 ColliderSize = new(13.6f, 6.4f, 9.2f);

        internal static bool ShouldShowRuntime(
            bool survivalSession,
            bool mobilePerformanceMode,
            bool grassInspectionView) =>
            survivalSession && !mobilePerformanceMode && !grassInspectionView;

        internal static bool SupportsRoofForest(WofSurvivalBiome biome) =>
            biome == WofSurvivalBiome.Plains ||
            biome == WofSurvivalBiome.Jungle ||
            biome == WofSurvivalBiome.Mushroom;

        internal static float GetSpawnThreshold(WofSurvivalBiome biome) => biome switch
        {
            WofSurvivalBiome.Jungle => 0.68f,
            WofSurvivalBiome.Mushroom => 0.72f,
            _ => 0.74f
        };

        internal static float GetReadyDelaySeconds(int chunkX, int chunkZ, int initialDistance)
        {
            return DesktopStageFiveDelaySeconds +
                   Mathf.Max(0, initialDistance) * DesktopDistanceDelaySeconds +
                   (float)WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, TreeLoadStageSalt) *
                   DesktopStageJitterSeconds;
        }

        internal static float GetRenderedTerrainHeightAtWorld(double worldX, double worldZ)
        {
            return (float)new RenderedTerrainSampler().GetHeightAtWorld(worldX, worldZ);
        }

        internal static WofSurvivalHobbitHutRecord[] MakeChunk(int chunkX, int chunkZ)
        {
            var biome = WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ);
            if (!SupportsRoofForest(biome) || WofSurvivalTerrainMath.IsAuthoredChunk(chunkX, chunkZ))
                return Array.Empty<WofSurvivalHobbitHutRecord>();
            var spawnRoll = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, SpawnSalt);
            if (spawnRoll <= GetSpawnThreshold(biome)) return Array.Empty<WofSurvivalHobbitHutRecord>();

            var sampler = new RenderedTerrainSampler();
            var chunkWorldX = chunkX * (double)WofSurvivalTerrainMath.BlockSize;
            var chunkWorldZ = chunkZ * (double)WofSurvivalTerrainMath.BlockSize;
            for (var index = 0; index < 10; index++)
            {
                var localX = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 7360 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.72d;
                var localZ = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 7410 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.72d;
                if (Math.Min(Math.Abs(localX), Math.Abs(localZ)) < RouteHalfWidth) continue;

                var worldX = chunkWorldX + localX;
                var worldZ = chunkWorldZ + localZ;
                var surface = sampler.GetSurfaceQuality(
                    chunkX,
                    chunkZ,
                    WofSurvivalTerrainMath.GetRenderSegments(0),
                    localX,
                    localZ,
                    FootprintRadius,
                    SurfaceSampleDistance);
                if (surface.NormalY < MinimumNormalY || surface.HeightRange > MaximumSurfaceHeightRange)
                    continue;
                if (surface.Y < WofSurvivalTerrainMath.GetWaterLevelAtWorld(worldX, worldZ) + WaterClearance)
                    continue;

                var north = WofSurvivalTerrainMath.GetTerrainHeight(
                    chunkX, chunkZ, localX, localZ - CardinalSampleDistance);
                var south = WofSurvivalTerrainMath.GetTerrainHeight(
                    chunkX, chunkZ, localX, localZ + CardinalSampleDistance);
                var east = WofSurvivalTerrainMath.GetTerrainHeight(
                    chunkX, chunkZ, localX + CardinalSampleDistance, localZ);
                var west = WofSurvivalTerrainMath.GetTerrainHeight(
                    chunkX, chunkZ, localX - CardinalSampleDistance, localZ);
                var minimum = Math.Min(Math.Min(north, south), Math.Min(east, west));
                var maximum = Math.Max(Math.Max(north, south), Math.Max(east, west));
                if (maximum - minimum > MaximumCardinalHeightRange) continue;

                return new[]
                {
                    new WofSurvivalHobbitHutRecord(
                        index,
                        biome,
                        new Vector3((float)worldX, (float)surface.Y, (float)worldZ),
                        (float)(WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 7460 + index) * Math.PI * 2d),
                        (float)(1.12d + WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 7510 + index) * 0.38d),
                        (float)WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 7560 + index))
                };
            }

            return Array.Empty<WofSurvivalHobbitHutRecord>();
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
                var centerY = GetHeightAtWorld(worldX, worldZ);
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
                var y = GetHeightAtWorld(worldX, worldZ);
                minimum = Math.Min(minimum, y);
                maximum = Math.Max(maximum, y);
            }

            internal double GetHeightAtWorld(double worldX, double worldZ)
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
