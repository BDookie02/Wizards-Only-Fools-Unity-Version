using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    public enum WofManaSourceKind
    {
        BaseInfinite = 0,
        DesertWell = 1,
        HutRune = 2
    }

    public readonly struct WofManaSourceRecord
    {
        public WofManaSourceRecord(string id, WofManaSourceKind kind, Vector3 position, float radius, int sourceIndex)
        {
            Id = id;
            Kind = kind;
            Position = position;
            Radius = radius;
            SourceIndex = sourceIndex;
        }

        public string Id { get; }
        public WofManaSourceKind Kind { get; }
        public Vector3 Position { get; }
        public float Radius { get; }
        public int SourceIndex { get; }
    }

    /// <summary>
    /// Pure, deterministic port of the React base-rune and infinite-mana source contract.
    /// Runtime rendering and Netcode authority intentionally live elsewhere.
    /// </summary>
    public static class WofManaSourceRules
    {
        public const double RuneCycleSeconds = 15d;
        public const double InfiniteSourceDebounceSeconds = 0.55d;
        public const float BaseSourceRadius = 2.6f;
        public const float DesertWellRadius = 34f;
        public const float HutRuneCollectionRadius = 0.9f;
        public const float BaseSourceX = 11.5f;
        public const float BaseSourceZ = 31.5f;
        public const float HutRuneVisualLift = 0.6f;
        public const float PickupPulseSeconds = 0.95f;

        private static IReadOnlyList<WofHutPlacement> _hutPlacements;

        public static IReadOnlyList<WofHutPlacement> HutPlacements =>
            _hutPlacements ??= WofBaseVillageLayout.BuildHutPlacements();

        public static WofManaSourceRecord BaseSource => new(
            "bonfire-mana-spawner",
            WofManaSourceKind.BaseInfinite,
            new Vector3(BaseSourceX, WofBaseVillageLayout.GetTerrainHeight(BaseSourceX, BaseSourceZ), BaseSourceZ),
            BaseSourceRadius,
            -1);

        public static WofManaSourceRecord DesertWell => new(
            $"desert-well-mana-{WofDesertVillageLayout.ChunkX}:{WofDesertVillageLayout.ChunkZ}",
            WofManaSourceKind.DesertWell,
            WofDesertVillageLayout.WorldOrigin + Vector3.up * (WofDesertVillageLayout.ReactBaseHeight + 7.35f),
            DesertWellRadius,
            -1);

        public static long GetRuneCycle(double serverSeconds)
        {
            if (double.IsNaN(serverSeconds) || double.IsInfinity(serverSeconds) || serverSeconds <= 0d) return 0L;
            return (long)Math.Floor(serverSeconds / RuneCycleSeconds);
        }

        public static int GetActiveRuneCount(int hutCount)
        {
            return Math.Max(0, hutCount * 2 / 3);
        }

        public static int[] BuildActiveRuneIndices(long cycle)
        {
            var huts = HutPlacements;
            var ranked = new RuneRank[huts.Count];
            for (var index = 0; index < ranked.Length; index++)
                ranked[index] = new RuneRank(index, HashRune(cycle, index));
            Array.Sort(ranked, CompareRanks);
            var result = new int[GetActiveRuneCount(ranked.Length)];
            for (var index = 0; index < result.Length; index++) result[index] = ranked[index].Index;
            Array.Sort(result);
            return result;
        }

        public static bool IsRuneActive(int sourceIndex, long cycle)
        {
            var active = BuildActiveRuneIndices(cycle);
            return Array.BinarySearch(active, sourceIndex) >= 0;
        }

        public static bool TryGetHutRune(int sourceIndex, out WofManaSourceRecord source)
        {
            var huts = HutPlacements;
            if (sourceIndex < 0 || sourceIndex >= huts.Count)
            {
                source = default;
                return false;
            }

            var hut = huts[sourceIndex];
            source = new WofManaSourceRecord(
                $"{hut.X}-{hut.Z}",
                WofManaSourceKind.HutRune,
                new Vector3(hut.X, hut.Y, hut.Z),
                HutRuneCollectionRadius,
                sourceIndex);
            return true;
        }

        public static bool ShouldShowBaseSources(bool isSurvival, Vector3 playerPosition)
        {
            if (!isSurvival) return true;
            return WofSurvivalTerrainMath.GetChunkCoordinate(playerPosition.x) == 0 &&
                   WofSurvivalTerrainMath.GetChunkCoordinate(playerPosition.z) == 0;
        }

        public static bool ShouldShowDesertWell(bool isSurvival, Vector3 playerPosition)
        {
            if (!isSurvival) return false;
            var chunkX = WofSurvivalTerrainMath.GetChunkCoordinate(playerPosition.x);
            var chunkZ = WofSurvivalTerrainMath.GetChunkCoordinate(playerPosition.z);
            return Math.Abs(chunkX - WofDesertVillageLayout.ChunkX) <= 1 &&
                   Math.Abs(chunkZ - WofDesertVillageLayout.ChunkZ) <= 1;
        }

        public static bool IsWithinHorizontalRadius(Vector3 playerPosition, WofManaSourceRecord source)
        {
            var dx = playerPosition.x - source.Position.x;
            var dz = playerPosition.z - source.Position.z;
            return dx * dx + dz * dz < source.Radius * source.Radius;
        }

        private static int CompareRanks(RuneRank left, RuneRank right)
        {
            var hash = left.Hash.CompareTo(right.Hash);
            return hash != 0 ? hash : left.Index.CompareTo(right.Index);
        }

        private static uint HashRune(long cycle, int index)
        {
            unchecked
            {
                var value = (uint)cycle ^ (uint)(cycle >> 32) ^ ((uint)index + 0x9e3779b9u);
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                return value ^ value >> 16;
            }
        }

        private readonly struct RuneRank
        {
            public RuneRank(int index, uint hash)
            {
                Index = index;
                Hash = hash;
            }

            public int Index { get; }
            public uint Hash { get; }
        }
    }
}
