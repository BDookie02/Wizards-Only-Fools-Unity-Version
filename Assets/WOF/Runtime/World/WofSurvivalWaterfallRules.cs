using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    internal readonly struct WofSurvivalWaterfallRecord
    {
        public WofSurvivalWaterfallRecord(
            int chunkX,
            int chunkZ,
            int sourceIndex,
            Vector3 position,
            float height,
            float width,
            float yawRadians,
            Vector3 poolPosition,
            float poolScale)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            SourceIndex = sourceIndex;
            Position = position;
            Height = height;
            Width = width;
            YawRadians = yawRadians;
            PoolPosition = poolPosition;
            PoolScale = poolScale;
        }

        public int ChunkX { get; }
        public int ChunkZ { get; }
        public int SourceIndex { get; }
        public Vector3 Position { get; }
        public float Height { get; }
        public float Width { get; }
        public float YawRadians { get; }
        public Vector3 PoolPosition { get; }
        public float PoolScale { get; }
        public string Key => $"{ChunkX}:{ChunkZ}-waterfall-{SourceIndex}";
    }

    internal static class WofSurvivalWaterfallRules
    {
        internal const int AttemptCount = 8;
        internal const float MinimumDrop = 8f;
        internal const float MaximumDrop = 34f;
        internal const float MaximumRenderedHeight = 26f;

        internal static bool ShouldShowRuntime(bool survivalSession) => survivalSession;

        internal static bool ShouldGenerateChunk(bool survivalSession, int chunkX, int chunkZ, int distance)
        {
            if (!survivalSession || distance != 0 || WofSurvivalTerrainMath.IsAuthoredChunk(chunkX, chunkZ))
                return false;
            var biome = WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ);
            return biome != WofSurvivalBiome.Desert && biome != WofSurvivalBiome.Swamp;
        }

        internal static int GetDesiredCount(int chunkX, int chunkZ, int distance)
        {
            if (distance != 0) return 0;
            return WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ) == WofSurvivalBiome.Jungle ||
                   WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 188) > 0.72d
                ? 1
                : 0;
        }

        internal static WofSurvivalWaterfallRecord[] MakeChunk(int chunkX, int chunkZ, int distance)
        {
            if (!ShouldGenerateChunk(true, chunkX, chunkZ, distance))
                return Array.Empty<WofSurvivalWaterfallRecord>();

            var desired = GetDesiredCount(chunkX, chunkZ, distance);
            if (desired == 0) return Array.Empty<WofSurvivalWaterfallRecord>();
            var generated = new List<WofSurvivalWaterfallRecord>(desired);
            var chunkWorldX = chunkX * (double)WofSurvivalTerrainMath.BlockSize;
            var chunkWorldZ = chunkZ * (double)WofSurvivalTerrainMath.BlockSize;

            for (var index = 0; index < desired * AttemptCount && generated.Count < desired; index++)
            {
                var localX = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 1200 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.72d;
                var localZ = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 1240 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.72d;
                var angle = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 1280 + index) * Math.PI * 2d;
                var dropX = Math.Cos(angle) * 42d;
                var dropZ = Math.Sin(angle) * 42d;
                var topY = WofSurvivalTerrainMath.GetReactTerrainHeight(chunkX, chunkZ, localX, localZ) + 3.5d;
                var bottomTerrainY = WofSurvivalTerrainMath.GetReactTerrainHeight(
                    chunkX,
                    chunkZ,
                    localX + dropX,
                    localZ + dropZ);
                var worldX = chunkWorldX + localX;
                var worldZ = chunkWorldZ + localZ;
                var waterY = WofSurvivalTerrainMath.GetReactWaterLevelAtWorld(worldX, worldZ);
                var bottomY = Math.Max(waterY + 0.45d, bottomTerrainY + 0.8d);
                var drop = topY - bottomY;
                if (drop < MinimumDrop || drop > MaximumDrop || topY < waterY + 9d) continue;

                var poolX = worldX + dropX * 0.72d;
                var poolZ = worldZ + dropZ * 0.72d;
                var poolScale = 9d + WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 1360 + index) * 8d;
                if (WofSurvivalTerrainMath.IsWaterSuppressed(worldX, worldZ, 58d) ||
                    WofSurvivalTerrainMath.IsWaterSuppressed(poolX, poolZ, poolScale * 1.5d + 18d))
                    continue;

                generated.Add(new WofSurvivalWaterfallRecord(
                    chunkX,
                    chunkZ,
                    index,
                    new Vector3(
                        (float)(worldX + dropX * 0.34d),
                        (float)(bottomY + drop * 0.5d),
                        (float)(worldZ + dropZ * 0.34d)),
                    (float)Math.Min(MaximumRenderedHeight, drop),
                    (float)(3.2d + WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 1320 + index) * 4.8d),
                    (float)angle,
                    new Vector3((float)poolX, (float)(bottomY + 0.08d), (float)poolZ),
                    (float)poolScale));
            }

            return generated.ToArray();
        }
    }
}
