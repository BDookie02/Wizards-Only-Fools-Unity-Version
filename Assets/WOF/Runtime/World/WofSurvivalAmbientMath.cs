using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    internal enum WofAmbientInsectKind
    {
        Butterfly,
        Bee
    }

    internal readonly struct WofAmbientInsectRecord
    {
        public WofAmbientInsectRecord(
            Vector3 position,
            float orbitRadius,
            float height,
            float speed,
            float phase,
            float size,
            float wobble)
        {
            Position = position;
            OrbitRadius = orbitRadius;
            Height = height;
            Speed = speed;
            Phase = phase;
            Size = size;
            Wobble = wobble;
        }

        public Vector3 Position { get; }
        public float OrbitRadius { get; }
        public float Height { get; }
        public float Speed { get; }
        public float Phase { get; }
        public float Size { get; }
        public float Wobble { get; }
    }

    public readonly struct WofManaFlowerRecord
    {
        public WofManaFlowerRecord(
            string id,
            int chunkX,
            int chunkZ,
            int index,
            Vector3 position,
            float radius,
            float stemHeight,
            float headScale)
        {
            Id = id;
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Index = index;
            Position = position;
            Radius = radius;
            StemHeight = stemHeight;
            HeadScale = headScale;
        }

        public string Id { get; }
        public int ChunkX { get; }
        public int ChunkZ { get; }
        public int Index { get; }
        public Vector3 Position { get; }
        public float Radius { get; }
        public float StemHeight { get; }
        public float HeadScale { get; }
    }

    internal static class WofSurvivalAmbientMath
    {
        internal const int ManaFlowersPerChunk = 8;

        internal static int GetAmbientInsectTargetCount(
            WofSurvivalBiome biome,
            bool mobile,
            WofAmbientInsectKind kind)
        {
            var multiplier = biome switch
            {
                WofSurvivalBiome.Desert => 0.45d,
                WofSurvivalBiome.Swamp => 0.72d,
                WofSurvivalBiome.Tallgrass => 1.35d,
                _ => 1d
            };
            var baseCount = kind == WofAmbientInsectKind.Butterfly ? 8d : 10d;
            return Math.Max(0, (int)Math.Floor(baseCount * multiplier * (mobile ? 0.62d : 1d) + 0.5d));
        }

        internal static WofAmbientInsectRecord[] MakeAmbientInsects(
            int chunkX,
            int chunkZ,
            bool mobile,
            WofAmbientInsectKind kind)
        {
            if (WofSurvivalTerrainMath.IsAuthoredChunk(chunkX, chunkZ))
                return Array.Empty<WofAmbientInsectRecord>();
            var biome = WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ);
            var targetCount = GetAmbientInsectTargetCount(biome, mobile, kind);
            var result = new List<WofAmbientInsectRecord>(targetCount);
            var attempts = targetCount * 6;
            var butterfly = kind == WofAmbientInsectKind.Butterfly;
            for (var index = 0; index < attempts && result.Count < targetCount; index++)
            {
                var localX = (WofSurvivalTerrainMath.Hash01(
                                  chunkX, chunkZ, (butterfly ? 7000 : 7400) + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.84d;
                var localZ = (WofSurvivalTerrainMath.Hash01(
                                  chunkX, chunkZ, (butterfly ? 7100 : 7500) + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.84d;
                var worldX = chunkX * (double)WofSurvivalTerrainMath.BlockSize + localX;
                var worldZ = chunkZ * (double)WofSurvivalTerrainMath.BlockSize + localZ;
                if (IsInsideMountainShoulder(worldX, worldZ)) continue;
                var terrainY = WofSurvivalTerrainMath.GetTerrainHeight(chunkX, chunkZ, localX, localZ);
                if (terrainY < WofSurvivalTerrainMath.GetWaterLevelAtWorld(worldX, worldZ) + 0.12d) continue;
                result.Add(new WofAmbientInsectRecord(
                    new Vector3((float)worldX, (float)terrainY, (float)worldZ),
                    (float)((butterfly ? 2.6d : 1.8d) + WofSurvivalTerrainMath.Hash01(
                        chunkX, chunkZ, 7300 + index) * (butterfly ? 5.2d : 3.4d)),
                    (float)((butterfly ? 1.6d : 1d) + WofSurvivalTerrainMath.Hash01(
                        chunkX, chunkZ, 7350 + index) * (butterfly ? 3.4d : 2.1d)),
                    (float)((butterfly ? 0.45d : 0.86d) + WofSurvivalTerrainMath.Hash01(
                        chunkX, chunkZ, 7360 + index) * (butterfly ? 0.54d : 0.92d)),
                    (float)(WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 7370 + index) * Math.PI * 2d),
                    (float)((butterfly ? 1.05d : 0.58d) + WofSurvivalTerrainMath.Hash01(
                        chunkX, chunkZ, 7380 + index) * (butterfly ? 0.82d : 0.34d)),
                    (float)(WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 7390 + index) * Math.PI * 2d)));
            }
            return result.ToArray();
        }

        internal static WofManaFlowerRecord[] GetNearbyManaFlowers(int centerX, int centerZ)
        {
            var result = new List<WofManaFlowerRecord>(ManaFlowersPerChunk * 9);
            for (var dz = -1; dz <= 1; dz++)
            for (var dx = -1; dx <= 1; dx++)
            for (var index = 0; index < ManaFlowersPerChunk; index++)
                if (TryGetManaFlower(centerX + dx, centerZ + dz, index, out var flower)) result.Add(flower);
            return result.ToArray();
        }

        public static bool TryGetManaFlower(
            int chunkX,
            int chunkZ,
            int index,
            out WofManaFlowerRecord flower)
        {
            if (index < 0 || index >= ManaFlowersPerChunk)
            {
                flower = default;
                return false;
            }
            var localX = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 9100 + index * 17) - 0.5d) *
                         WofSurvivalTerrainMath.BlockSize * 0.86d;
            var localZ = (WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 9300 + index * 19) - 0.5d) *
                         WofSurvivalTerrainMath.BlockSize * 0.86d;
            var worldX = chunkX * (double)WofSurvivalTerrainMath.BlockSize + localX;
            var worldZ = chunkZ * (double)WofSurvivalTerrainMath.BlockSize + localZ;
            if (!IsWildernessSpawnAllowed(chunkX, chunkZ, localX, localZ) ||
                WofSurvivalTerrainMath.GetTownRouteMaskAtWorld(worldX, worldZ) > 0.18d)
            {
                flower = default;
                return false;
            }
            var y = WofSurvivalTerrainMath.GetTerrainHeight(chunkX, chunkZ, localX, localZ);
            if (y < WofSurvivalTerrainMath.GetWaterLevelAtWorld(worldX, worldZ) + 0.55d)
            {
                flower = default;
                return false;
            }
            var slope = Math.Max(
                Math.Max(
                    Math.Abs(WofSurvivalTerrainMath.GetTerrainHeight(chunkX, chunkZ, localX + 4d, localZ) - y),
                    Math.Abs(WofSurvivalTerrainMath.GetTerrainHeight(chunkX, chunkZ, localX - 4d, localZ) - y)),
                Math.Max(
                    Math.Abs(WofSurvivalTerrainMath.GetTerrainHeight(chunkX, chunkZ, localX, localZ + 4d) - y),
                    Math.Abs(WofSurvivalTerrainMath.GetTerrainHeight(chunkX, chunkZ, localX, localZ - 4d) - y)));
            if (slope > 4.8d)
            {
                flower = default;
                return false;
            }
            flower = new WofManaFlowerRecord(
                $"mana-flower-{chunkX}:{chunkZ}:{index}",
                chunkX,
                chunkZ,
                index,
                new Vector3((float)worldX, (float)y, (float)worldZ),
                2.15f,
                (float)(1.35d + WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 9500 + index) * 0.7d),
                (float)(0.72d + WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 9700 + index) * 0.28d));
            return true;
        }

        private static bool IsWildernessSpawnAllowed(
            int chunkX,
            int chunkZ,
            double localX,
            double localZ)
        {
            var worldX = chunkX * (double)WofSurvivalTerrainMath.BlockSize + localX;
            var worldZ = chunkZ * (double)WofSurvivalTerrainMath.BlockSize + localZ;
            if (IsInsideMountainShoulder(worldX, worldZ)) return false;
            if (!WofSurvivalTerrainMath.IsAuthoredChunk(chunkX, chunkZ)) return true;
            var maxAbs = Math.Max(Math.Abs(localX), Math.Abs(localZ));
            var radiusSquared = localX * localX + localZ * localZ;
            if (chunkX == WofLilyCoilLayout.ChunkX && chunkZ == WofLilyCoilLayout.ChunkZ)
            {
                var radius = WofSurvivalTerrainMath.BlockSize * 0.72d;
                return radiusSquared >= radius * radius;
            }
            if (chunkX == WofMountainVillageLayout.ChunkX && chunkZ == WofMountainVillageLayout.ChunkZ)
            {
                var radius = WofMountainVillageLayout.PerimeterShoulderRadius;
                return radiusSquared >= radius * radius;
            }
            if (chunkX == WofGraveyardVillageLayout.ChunkX && chunkZ == WofGraveyardVillageLayout.ChunkZ)
            {
                var radius = WofGraveyardVillageLayout.FenceRadius + 62d;
                return radiusSquared >= radius * radius;
            }
            if (chunkX == WofDarrelGroveLayout.ChunkX && chunkZ == WofDarrelGroveLayout.ChunkZ)
                return maxAbs >= WofDarrelGroveLayout.HalfSize + 62d;
            return maxAbs >= 318d;
        }

        private static bool IsInsideMountainShoulder(double worldX, double worldZ)
        {
            var dx = worldX - WofMountainVillageLayout.WorldOrigin.x;
            var dz = worldZ - WofMountainVillageLayout.WorldOrigin.z;
            var radius = WofMountainVillageLayout.PerimeterShoulderRadius;
            return dx * dx + dz * dz < radius * radius;
        }
    }
}
