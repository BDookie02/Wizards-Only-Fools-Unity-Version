using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    internal enum WofSurvivalDetailScatterKind
    {
        Tree,
        Cactus,
        Tumbleweed
    }

    internal readonly struct WofSurvivalDetailScatterRecord
    {
        public WofSurvivalDetailScatterRecord(
            int sourceIndex,
            WofSurvivalDetailScatterKind kind,
            WofSurvivalBiome biome,
            Vector3 position,
            float scale,
            float variant)
        {
            SourceIndex = sourceIndex;
            Kind = kind;
            Biome = biome;
            Position = position;
            Scale = scale;
            Variant = variant;
        }

        public int SourceIndex { get; }
        public WofSurvivalDetailScatterKind Kind { get; }
        public WofSurvivalBiome Biome { get; }
        public Vector3 Position { get; }
        public float Scale { get; }
        public float Variant { get; }
    }

    internal static class WofSurvivalDetailScatterRules
    {
        internal const float MinimumNormalY = 0.72f;
        internal const float MaximumHeightRange = 7.4f;
        internal const float FootprintRadius = 8.8f;
        internal const float SurfaceSampleDistance = 5.2f;
        internal const float WaterClearance = 0.18f;
        internal const float RouteHalfWidth = 34f;
        internal const float TumbleweedThreshold = 0.56f;
        internal const int TreeLoadStageSalt = 9011;
        internal const float DesktopStageFiveDelaySeconds = 3.6f;
        internal const float DesktopDistanceDelaySeconds = 0.28f;
        internal const float DesktopStageJitterSeconds = 0.52f;

        internal static bool ShouldShowRuntime(
            bool survivalSession,
            bool mobilePerformanceMode,
            bool grassInspectionView) =>
            survivalSession && !mobilePerformanceMode && !grassInspectionView;

        internal static float GetReadyDelaySeconds(int chunkX, int chunkZ, int initialDistance)
        {
            return DesktopStageFiveDelaySeconds +
                   Mathf.Max(0, initialDistance) * DesktopDistanceDelaySeconds +
                   (float)WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, TreeLoadStageSalt) *
                   DesktopStageJitterSeconds;
        }

        internal static WofSurvivalDetailScatterRecord[] MakeChunk(int chunkX, int chunkZ)
        {
            var biome = WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ);
            var baseCount = biome switch
            {
                WofSurvivalBiome.Desert => 9,
                WofSurvivalBiome.Jungle => 5,
                WofSurvivalBiome.Swamp => 4,
                WofSurvivalBiome.Mushroom => 3,
                _ => 4
            };
            var result = new List<WofSurvivalDetailScatterRecord>(baseCount);
            var sampler = new RenderedTerrainSampler();
            var chunkWorldX = chunkX * (double)WofSurvivalTerrainMath.BlockSize;
            var chunkWorldZ = chunkZ * (double)WofSurvivalTerrainMath.BlockSize;
            var attempts = baseCount * 8;

            for (var index = 0; index < attempts && result.Count < baseCount; index++)
            {
                var localX = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 20 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.78d;
                var localZ = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 60 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.78d;
                if (WofSurvivalTerrainMath.IsAuthoredChunk(chunkX, chunkZ) &&
                    Math.Max(Math.Abs(localX), Math.Abs(localZ)) < 284d) continue;
                if (biome != WofSurvivalBiome.Desert &&
                    Math.Min(Math.Abs(localX), Math.Abs(localZ)) < RouteHalfWidth) continue;

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
                if (surface.NormalY < MinimumNormalY || surface.HeightRange > MaximumHeightRange) continue;
                if (surface.Y < WofSurvivalTerrainMath.GetWaterLevelAtWorld(worldX, worldZ) + WaterClearance)
                    continue;

                var scaleRange = biome == WofSurvivalBiome.Jungle ? 2.95d :
                    biome == WofSurvivalBiome.Swamp ? 2.45d : 2.1d;
                var scale = 1.35d + WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 90 + index) * scaleRange;
                var variant = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 120 + index);
                if (biome != WofSurvivalBiome.Desert)
                {
                    var minimumSpacing = biome == WofSurvivalBiome.Jungle ? 118d :
                        biome == WofSurvivalBiome.Swamp ? 96d :
                        biome == WofSurvivalBiome.Mushroom ? 82d : 88d;
                    var minimumSpacingSquared = minimumSpacing * minimumSpacing;
                    var tooClose = false;
                    foreach (var existing in result)
                    {
                        var deltaX = existing.Position.x - worldX;
                        var deltaZ = existing.Position.z - worldZ;
                        if (deltaX * deltaX + deltaZ * deltaZ >= minimumSpacingSquared) continue;
                        tooClose = true;
                        break;
                    }
                    if (tooClose) continue;
                }

                var kind = biome != WofSurvivalBiome.Desert
                    ? WofSurvivalDetailScatterKind.Tree
                    : variant > TumbleweedThreshold
                        ? WofSurvivalDetailScatterKind.Tumbleweed
                        : WofSurvivalDetailScatterKind.Cactus;
                result.Add(new WofSurvivalDetailScatterRecord(
                    index,
                    kind,
                    biome,
                    new Vector3((float)worldX, (float)surface.Y, (float)worldZ),
                    (float)scale,
                    (float)variant));
            }

            return result.ToArray();
        }

        internal static float GetTreeVisualScale(WofSurvivalBiome biome, float propScale)
        {
            const float giantScale = 5f;
            return biome switch
            {
                WofSurvivalBiome.Jungle => Mathf.Min(18.75f, (1.24f + propScale * 0.62f) * giantScale),
                WofSurvivalBiome.Swamp => Mathf.Min(15.25f, (1.12f + propScale * 0.54f) * giantScale),
                WofSurvivalBiome.Mushroom => Mathf.Min(15f, (1.05f + propScale * 0.48f) * giantScale),
                _ => Mathf.Min(13.75f, (1.05f + propScale * 0.48f) * giantScale)
            };
        }

        internal static float GetTreeFootprintScale(WofSurvivalBiome biome, float visualScale)
        {
            return biome switch
            {
                WofSurvivalBiome.Jungle => visualScale * 0.24f,
                WofSurvivalBiome.Swamp => visualScale * 0.3f,
                WofSurvivalBiome.Mushroom => visualScale * 0.42f,
                _ => visualScale * 0.32f
            };
        }

        internal static float GetTumbleweedScale(WofSurvivalDetailScatterRecord record) => record.Scale * 1.25f;

        internal static Vector3 GetTumbleweedPosition(WofSurvivalDetailScatterRecord record)
        {
            var tumbleScale = GetTumbleweedScale(record);
            return record.Position + new Vector3(
                Mathf.Sin(record.Variant * 6.28f) * 2.2f,
                1.35f * tumbleScale,
                0f);
        }

        internal static Vector3 GetTumbleweedRotationRadians(WofSurvivalDetailScatterRecord record) =>
            new(record.Variant * Mathf.PI * 2f, record.Variant * Mathf.PI, record.Variant * Mathf.PI * 1.3f);

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
