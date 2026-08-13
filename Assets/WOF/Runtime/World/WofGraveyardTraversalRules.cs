using UnityEngine;

namespace WOF
{
    /// <summary>
    /// Source-aligned controller route through the graveyard chapel. The route
    /// stays in the center aisle, avoids both pew banks and the altar, then uses
    /// the React-authored north-west rear opening and ramp.
    /// </summary>
    public static class WofGraveyardTraversalRules
    {
        public const float ArrivalRadius = 0.9f;
        public const float MaximumCrossTrackError = 3.5f;
        public const float MinimumGroundedRatio = 0.65f;

        private static readonly Vector3[] ChapelRouteLocal =
        {
            new(0f, 1.6f, 128f),
            new(0f, 1.3f, 102f),
            new(0f, 1.2f, 76f),
            new(0f, 1.2f, 24f),
            new(0f, 1.2f, -36f),
            new(-24f, 1.2f, -50f),
            new(-33f, 1.2f, -72f),
            new(-33f, 1.3f, -98f),
            new(-33f, 1.6f, -128f)
        };

        public static Vector3[] BuildChapelRoute()
        {
            var route = new Vector3[ChapelRouteLocal.Length];
            var floorOffset = Vector3.up * WofGraveyardVillageLayout.ReactBaseHeight;
            for (var index = 0; index < ChapelRouteLocal.Length; index++)
            {
                route[index] = WofGraveyardVillageLayout.WorldOrigin + floorOffset +
                               ChapelRouteLocal[index];
            }
            return route;
        }

        public static float ResolveHeading(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
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
            if (denominator <= 0.0001f)
            {
                return Vector2.Distance(candidate, start);
            }

            var t = Mathf.Clamp01(Vector2.Dot(candidate - start, segment) / denominator);
            return Vector2.Distance(candidate, start + segment * t);
        }
    }
}
