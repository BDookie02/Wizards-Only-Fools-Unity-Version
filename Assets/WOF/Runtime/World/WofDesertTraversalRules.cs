using UnityEngine;

namespace WOF
{
    /// <summary>
    /// Source-aligned north-road route through the desert village and into its
    /// north expansion chunk. The small east offset clears the exact central
    /// well while remaining inside React's intentionally open road corridor.
    /// </summary>
    public static class WofDesertTraversalRules
    {
        public const float ArrivalRadius = 0.95f;
        public const float MaximumCrossTrackError = 4f;
        public const float MinimumGroundedRatio = 0.65f;

        private static readonly Vector2[] RouteLocal =
        {
            new(0f, -214f),
            new(0f, -120f),
            new(0f, -42f),
            new(40f, -42f),
            new(40f, 42f),
            new(0f, 42f),
            new(0f, 120f),
            new(0f, 214f),
            new(0f, 270f),
            new(0f, 330f)
        };

        public static Vector3[] BuildNorthGateRoute()
        {
            var route = new Vector3[RouteLocal.Length];
            for (var index = 0; index < RouteLocal.Length; index++)
            {
                var local = RouteLocal[index];
                var height = local.y <= 256f
                    ? WofDesertVillageLayout.ReactBaseHeight
                    : ResolveNorthExpansionHeight(local.y);
                route[index] = WofDesertVillageLayout.WorldOrigin +
                               new Vector3(local.x, height + 1.4f, local.y);
            }
            return route;
        }

        public static bool IsNorthExpansionPoint(Vector3 worldPosition)
        {
            return WofSurvivalTerrainMath.IsDesertVillageExpansionChunk(
                ResolveChunkCoordinate(worldPosition.x),
                ResolveChunkCoordinate(worldPosition.z));
        }

        public static int CountExpansionChunks()
        {
            var count = 0;
            for (var chunkX = 2; chunkX <= 6; chunkX++)
            {
                for (var chunkZ = -5; chunkZ <= -2; chunkZ++)
                {
                    if (WofSurvivalTerrainMath.IsDesertVillageExpansionChunk(chunkX, chunkZ))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        public static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            return Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z));
        }

        public static float HorizontalDistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            var start = new Vector2(from.x, from.z);
            var end = new Vector2(to.x, to.z);
            var candidate = new Vector2(point.x, point.z);
            var segment = end - start;
            var denominator = segment.sqrMagnitude;
            if (denominator <= 0.0001f) return Vector2.Distance(candidate, start);
            var t = Mathf.Clamp01(Vector2.Dot(candidate - start, segment) / denominator);
            return Vector2.Distance(candidate, start + segment * t);
        }

        private static float ResolveNorthExpansionHeight(float desertLocalZ)
        {
            var worldZ = WofDesertVillageLayout.WorldOrigin.z + desertLocalZ;
            const int northChunkZ = WofDesertVillageLayout.ChunkZ + 1;
            var northLocalZ = worldZ - northChunkZ * WofDesertVillageLayout.SurvivalBlockSize;
            return (float)WofSurvivalTerrainMath.GetTerrainHeight(
                WofDesertVillageLayout.ChunkX,
                northChunkZ,
                0d,
                northLocalZ);
        }

        private static int ResolveChunkCoordinate(float worldCoordinate)
        {
            return Mathf.FloorToInt(
                (worldCoordinate + WofDesertVillageLayout.SurvivalBlockSize * 0.5f) /
                WofDesertVillageLayout.SurvivalBlockSize);
        }
    }
}
