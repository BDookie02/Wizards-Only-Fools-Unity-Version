using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    internal enum WofSurvivalDesertLandmarkKind
    {
        Pyramid,
        Obelisk
    }

    internal readonly struct WofSurvivalDesertLandmarkRecord
    {
        public WofSurvivalDesertLandmarkRecord(
            int chunkX,
            int chunkZ,
            int sourceIndex,
            WofSurvivalDesertLandmarkKind kind,
            Vector3 position,
            float localX,
            float localZ,
            float scale,
            float yawRadians,
            float variant)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            SourceIndex = sourceIndex;
            Kind = kind;
            Position = position;
            LocalX = localX;
            LocalZ = localZ;
            Scale = scale;
            YawRadians = yawRadians;
            Variant = variant;
        }

        public int ChunkX { get; }
        public int ChunkZ { get; }
        public int SourceIndex { get; }
        public WofSurvivalDesertLandmarkKind Kind { get; }
        public Vector3 Position { get; }
        public float LocalX { get; }
        public float LocalZ { get; }
        public float Scale { get; }
        public float YawRadians { get; }
        public float Variant { get; }
        public string Key => $"{ChunkX}:{ChunkZ}-desert-landmark-{SourceIndex}";
    }

    internal readonly struct WofSurvivalDesertPyramidMetrics
    {
        public WofSurvivalDesertPyramidMetrics(WofSurvivalDesertLandmarkRecord landmark)
        {
            StepCount = landmark.Variant > 0.72f ? 7 : 6;
            StepHeight = 2.1f * landmark.Scale;
            BaseSize = 31f * landmark.Scale;
            PyramidYawRadians = landmark.YawRadians + Mathf.PI * 0.25f;
            DoorWidth = 5.4f * landmark.Scale;
            DoorHeight = 6.3f * landmark.Scale;
            WallThickness = Mathf.Max(1.7f * landmark.Scale, BaseSize * 0.075f);
            Height = StepHeight * StepCount + 4.2f * landmark.Scale;
        }

        public int StepCount { get; }
        public float StepHeight { get; }
        public float BaseSize { get; }
        public float PyramidYawRadians { get; }
        public float DoorWidth { get; }
        public float DoorHeight { get; }
        public float WallThickness { get; }
        public float Height { get; }
    }

    internal readonly struct WofSurvivalDesertFootprintStats
    {
        public WofSurvivalDesertFootprintStats(float minimum, float maximum, float average)
        {
            Minimum = minimum;
            Maximum = maximum;
            Average = average;
        }

        public float Minimum { get; }
        public float Maximum { get; }
        public float Average { get; }
        public float Range => Maximum - Minimum;
    }

    internal static class WofSurvivalDesertLandmarkRules
    {
        internal const float RouteHalfWidth = 62f;
        internal const float WaterClearance = 0.5f;
        internal const int TreeLoadStageSalt = 9011;
        internal const float DesktopStageTwoDelaySeconds = 0.56f;
        internal const float MobileStageTwoDelaySeconds = 0.82f;
        internal const float DesktopDistanceDelaySeconds = 0.28f;
        internal const float MobileDistanceDelaySeconds = 0.46f;
        internal const float DesktopStageJitterSeconds = 0.52f;
        internal const float MobileStageJitterSeconds = 0.76f;
        private static readonly double[] FootprintSamples = { -1d, -0.5d, 0d, 0.5d, 1d };

        internal static bool ShouldShowRuntime(bool survivalSession) => survivalSession;

        internal static bool ShouldGenerateChunk(bool survivalSession, int chunkX, int chunkZ, int distance)
        {
            return survivalSession &&
                   distance >= 0 && distance <= WofSurvivalTerrainMath.NearRadius &&
                   WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ) == WofSurvivalBiome.Desert &&
                   !WofSurvivalTerrainMath.IsAuthoredChunk(chunkX, chunkZ);
        }

        internal static int GetTargetCount(int distance) => distance == 0 ? 2 : distance == 1 ? 1 : 0;

        internal static float GetReadyDelaySeconds(int chunkX, int chunkZ, int initialDistance, bool mobile)
        {
            var stageDelay = mobile ? MobileStageTwoDelaySeconds : DesktopStageTwoDelaySeconds;
            var distanceDelay = mobile ? MobileDistanceDelaySeconds : DesktopDistanceDelaySeconds;
            var jitter = mobile ? MobileStageJitterSeconds : DesktopStageJitterSeconds;
            return stageDelay + Mathf.Max(0, initialDistance) * distanceDelay +
                   (float)WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, TreeLoadStageSalt) * jitter;
        }

        internal static WofSurvivalDesertPyramidMetrics GetPyramidMetrics(
            WofSurvivalDesertLandmarkRecord landmark) => new(landmark);

        internal static WofSurvivalDesertLandmarkRecord[] MakeChunk(int chunkX, int chunkZ, int distance)
        {
            if (!ShouldGenerateChunk(true, chunkX, chunkZ, distance))
                return Array.Empty<WofSurvivalDesertLandmarkRecord>();

            var targetCount = GetTargetCount(distance);
            var records = new List<WofSurvivalDesertLandmarkRecord>(targetCount);
            var sampler = new RenderedTerrainSampler(
                WofSurvivalTerrainMath.GetRenderSegments(distance),
                true);
            var chunkWorldX = chunkX * (double)WofSurvivalTerrainMath.BlockSize;
            var chunkWorldZ = chunkZ * (double)WofSurvivalTerrainMath.BlockSize;
            var attempts = targetCount * 14;

            for (var index = 0; index < attempts && records.Count < targetCount; index++)
            {
                var localX = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 510 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.76d;
                var localZ = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 540 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.76d;
                if (Math.Min(Math.Abs(localX), Math.Abs(localZ)) < RouteHalfWidth) continue;

                var worldX = chunkWorldX + localX;
                var worldZ = chunkWorldZ + localZ;
                var y = sampler.GetRenderedHeight(chunkX, chunkZ, localX, localZ);
                if (y < WofSurvivalTerrainMath.GetReactWaterLevelAtWorld(worldX, worldZ) + WaterClearance) continue;

                var variant = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 570 + index);
                var scale = 0.86d + WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 600 + index) * 0.72d;
                var yaw = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 630 + index) * Math.PI * 2d;
                var kind = variant > 0.32d
                    ? WofSurvivalDesertLandmarkKind.Pyramid
                    : WofSurvivalDesertLandmarkKind.Obelisk;
                var landmarkY = y;
                if (kind == WofSurvivalDesertLandmarkKind.Pyramid)
                {
                    var sourceBaseSize = 31d * scale;
                    var sourcePyramidYaw = yaw + Math.PI * 0.25d;
                    var footprint = sampler.GetRotatedFootprintStats(
                        chunkX, chunkZ, localX, localZ,
                        sourceBaseSize * 0.58d,
                        sourcePyramidYaw);
                    var maximumSlopeRange = Math.Max(4.75d, sourceBaseSize * 0.12d);
                    if (footprint.Range > maximumSlopeRange) continue;
                    landmarkY = Math.Max(y, footprint.Maximum + 0.08d);
                }

                records.Add(new WofSurvivalDesertLandmarkRecord(
                    chunkX, chunkZ, index, kind,
                    new Vector3((float)worldX, (float)landmarkY, (float)worldZ),
                    (float)localX, (float)localZ, (float)scale, (float)yaw, (float)variant));
            }

            return records.ToArray();
        }

        internal static WofSurvivalDesertFootprintStats GetPyramidFootprintStats(
            WofSurvivalDesertLandmarkRecord landmark,
            int distance)
        {
            var metrics = GetPyramidMetrics(landmark);
            return new RenderedTerrainSampler(WofSurvivalTerrainMath.GetRenderSegments(distance))
                .GetRotatedFootprintStats(
                    landmark.ChunkX,
                    landmark.ChunkZ,
                    landmark.LocalX,
                    landmark.LocalZ,
                    metrics.BaseSize * 0.58d,
                    metrics.PyramidYawRadians);
        }

        internal static WofSurvivalDesertFootprintStats GetUnityPyramidFootprintStats(
            WofSurvivalDesertLandmarkRecord landmark,
            int distance)
        {
            var metrics = GetPyramidMetrics(landmark);
            return new RenderedTerrainSampler(WofSurvivalTerrainMath.GetRenderSegments(distance), false)
                .GetRotatedFootprintStats(
                    landmark.ChunkX,
                    landmark.ChunkZ,
                    landmark.LocalX,
                    landmark.LocalZ,
                    metrics.BaseSize * 0.58d,
                    metrics.PyramidYawRadians);
        }

        internal static float GetRenderedTerrainHeightAtWorld(double worldX, double worldZ)
        {
            var chunkX = WofSurvivalTerrainMath.GetChunkCoordinate(worldX);
            var chunkZ = WofSurvivalTerrainMath.GetChunkCoordinate(worldZ);
            return (float)new RenderedTerrainSampler(WofSurvivalTerrainMath.GetRenderSegments(0), false)
                .GetRenderedHeight(
                    chunkX,
                    chunkZ,
                    worldX - chunkX * (double)WofSurvivalTerrainMath.BlockSize,
                    worldZ - chunkZ * (double)WofSurvivalTerrainMath.BlockSize);
        }

        private sealed class RenderedTerrainSampler
        {
            private readonly Dictionary<GridKey, float[]> _grids = new();
            private readonly int _segments;
            private readonly bool _reactTerrain;

            public RenderedTerrainSampler(int segments, bool reactTerrain = true)
            {
                _segments = segments;
                _reactTerrain = reactTerrain;
            }

            public double GetRenderedHeight(int chunkX, int chunkZ, double localX, double localZ)
            {
                // Desert landmarks call React's continuous terrain resolver directly;
                // unlike several other scatter systems, they do not interpolate the
                // rendered chunk vertex grid. Unity-contact queries still use the grid.
                if (_reactTerrain)
                    return WofSurvivalTerrainMath.GetReactTerrainHeight(chunkX, chunkZ, localX, localZ);
                var grid = GetGrid(chunkX, chunkZ);
                var x = Clamp01((localX + WofSurvivalTerrainMath.BlockSize * 0.5d) /
                                WofSurvivalTerrainMath.BlockSize) * _segments;
                var z = Clamp01((localZ + WofSurvivalTerrainMath.BlockSize * 0.5d) /
                                WofSurvivalTerrainMath.BlockSize) * _segments;
                var x0 = (int)Math.Floor(x);
                var z0 = (int)Math.Floor(z);
                var x1 = Math.Min(_segments, x0 + 1);
                var z1 = Math.Min(_segments, z0 + 1);
                var tx = x - x0;
                var tz = z - z0;
                var size = _segments + 1;
                var h00 = grid[z0 * size + x0];
                var h10 = grid[z0 * size + x1];
                var h01 = grid[z1 * size + x0];
                var h11 = grid[z1 * size + x1];
                return Lerp(Lerp(h00, h10, tx), Lerp(h01, h11, tx), tz);
            }

            public WofSurvivalDesertFootprintStats GetRotatedFootprintStats(
                int chunkX,
                int chunkZ,
                double localX,
                double localZ,
                double halfSize,
                double yaw)
            {
                var cosine = Math.Cos(yaw);
                var sine = Math.Sin(yaw);
                var minimum = double.PositiveInfinity;
                var maximum = double.NegativeInfinity;
                var sum = 0d;
                var count = 0;
                foreach (var sampleX in FootprintSamples)
                foreach (var sampleZ in FootprintSamples)
                {
                    var offsetX = sampleX * halfSize;
                    var offsetZ = sampleZ * halfSize;
                    var rotatedX = offsetX * cosine - offsetZ * sine;
                    var rotatedZ = offsetX * sine + offsetZ * cosine;
                    var height = GetRenderedHeight(chunkX, chunkZ, localX + rotatedX, localZ + rotatedZ);
                    minimum = Math.Min(minimum, height);
                    maximum = Math.Max(maximum, height);
                    sum += height;
                    count++;
                }

                if (count == 0 || !double.IsFinite(minimum) || !double.IsFinite(maximum))
                {
                    var fallback = GetRenderedHeight(chunkX, chunkZ, localX, localZ);
                    return new WofSurvivalDesertFootprintStats((float)fallback, (float)fallback, (float)fallback);
                }
                return new WofSurvivalDesertFootprintStats(
                    (float)minimum,
                    (float)maximum,
                    (float)(sum / count));
            }

            private float[] GetGrid(int chunkX, int chunkZ)
            {
                var key = new GridKey(chunkX, chunkZ);
                if (_grids.TryGetValue(key, out var existing)) return existing;
                var size = _segments + 1;
                var result = new float[size * size];
                var step = WofSurvivalTerrainMath.BlockSize / (double)_segments;
                var half = WofSurvivalTerrainMath.BlockSize * 0.5d;
                for (var zIndex = 0; zIndex <= _segments; zIndex++)
                for (var xIndex = 0; xIndex <= _segments; xIndex++)
                {
                    result[zIndex * size + xIndex] = (float)(_reactTerrain
                        ? WofSurvivalTerrainMath.GetReactTerrainHeight(
                            chunkX,
                            chunkZ,
                            -half + xIndex * step,
                            -half + zIndex * step)
                        : WofSurvivalTerrainMath.GetTerrainHeight(
                            chunkX,
                            chunkZ,
                            -half + xIndex * step,
                            -half + zIndex * step));
                }
                _grids.Add(key, result);
                return result;
            }

            private static double Clamp01(double value) => value < 0d ? 0d : value > 1d ? 1d : value;
            private static double Lerp(double left, double right, double amount) => left + (right - left) * amount;
        }

        private readonly struct GridKey : IEquatable<GridKey>
        {
            public GridKey(int x, int z)
            {
                X = x;
                Z = z;
            }

            private int X { get; }
            private int Z { get; }
            public bool Equals(GridKey other) => X == other.X && Z == other.Z;
            public override bool Equals(object obj) => obj is GridKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(X, Z);
        }
    }
}
