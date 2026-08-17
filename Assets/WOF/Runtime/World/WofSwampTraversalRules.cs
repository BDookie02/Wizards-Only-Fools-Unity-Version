using UnityEngine;

namespace WOF
{
    /// <summary>
    /// Source-aligned route from the north terrain ramp, across the exact main
    /// dock cross and around the giant toad on the central platform, then down
    /// the east terrain ramp.
    /// </summary>
    public static class WofSwampTraversalRules
    {
        public const float ArrivalRadius = 0.9f;
        public const float MaximumCrossTrackError = 3.5f;
        public const float MinimumGroundedRatio = 0.65f;
        public const float RampLength = 76f;
        public const float RampLowY = 4.002989536349784f;

        private static readonly Vector3[] RouteLocal =
        {
            new(0f, RampLowY + 1.4f, -286f),
            new(0f, (RampLowY + WofSwampVillageLayout.ReactPlatformY) * 0.5f + 1.4f, -252f),
            new(0f, WofSwampVillageLayout.ReactPlatformY + 1.4f, -216f),
            new(0f, WofSwampVillageLayout.ReactPlatformY + 1.4f, -107f),
            new(0f, WofSwampVillageLayout.ReactPlatformY + 1.4f, -30f),
            new(30f, WofSwampVillageLayout.ReactPlatformY + 1.4f, -30f),
            new(30f, WofSwampVillageLayout.ReactPlatformY + 1.4f, 0f),
            new(107f, WofSwampVillageLayout.ReactPlatformY + 1.4f, 0f),
            new(216f, WofSwampVillageLayout.ReactPlatformY + 1.4f, 0f),
            new(252f, (RampLowY + WofSwampVillageLayout.ReactPlatformY) * 0.5f + 1.4f, 0f),
            new(286f, RampLowY + 1.4f, 0f)
        };

        public static Vector3[] BuildNorthToEastRoute()
        {
            var route = new Vector3[RouteLocal.Length];
            for (var index = 0; index < RouteLocal.Length; index++)
                route[index] = WofSwampVillageLayout.WorldOrigin + RouteLocal[index];
            return route;
        }

        public static bool IsCentralPlatformApproach(Vector3 worldPosition)
        {
            var local = worldPosition - WofSwampVillageLayout.WorldOrigin;
            return Mathf.Abs(local.x) <= 1f && Mathf.Abs(local.z + 30f) <= 1f;
        }

        public static bool IsEastRampExit(Vector3 worldPosition)
        {
            var local = worldPosition - WofSwampVillageLayout.WorldOrigin;
            return local.x >= 284f && Mathf.Abs(local.z) <= 1.5f;
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
    }
}
