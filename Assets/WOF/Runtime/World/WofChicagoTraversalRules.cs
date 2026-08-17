using UnityEngine;

namespace WOF
{
    /// <summary>
    /// Controller route along the east sidewalk of React's western-center
    /// avenue, through the authored Bean Park entrance, and back to the north
    /// boundary. The sidewalk offset keeps the player out of live traffic and
    /// the park stop stays outside the Bean's physical silhouette.
    /// </summary>
    public static class WofChicagoTraversalRules
    {
        public const float ArrivalRadius = 0.95f;
        public const float MaximumCrossTrackError = 4f;
        public const float MinimumGroundedRatio = 0.65f;

        private static readonly Vector2[] RouteLocal =
        {
            new(0f, -214f),
            new(-65f, -214f),
            new(-65f, -150f),
            new(-65f, -75f),
            new(-65f, 0f),
            new(-65f, 75f),
            new(-65f, 145f),
            new(-36f, 150f),
            new(-65f, 145f),
            new(-65f, 214f)
        };

        public static Vector3[] BuildBeanParkRoute()
        {
            var route = new Vector3[RouteLocal.Length];
            for (var index = 0; index < RouteLocal.Length; index++)
            {
                route[index] = WofChicagoCityLayout.WorldOrigin +
                               new Vector3(
                                   RouteLocal[index].x,
                                   WofChicagoCityLayout.ReactBaseHeight + 1.4f,
                                   RouteLocal[index].y);
            }
            return route;
        }

        public static bool IsExactRoadCoordinate(float coordinate)
        {
            return Mathf.Approximately(coordinate, -150f) ||
                   Mathf.Approximately(coordinate, -75f) ||
                   Mathf.Approximately(coordinate, 75f) ||
                   Mathf.Approximately(coordinate, 150f);
        }

        public static bool IsBeanParkApproach(Vector3 worldPosition)
        {
            var local = worldPosition - WofChicagoCityLayout.WorldOrigin;
            return Mathf.Abs(local.x + 36f) <= 1f && Mathf.Abs(local.z - 150f) <= 1f;
        }

        public static bool IsNorthBoundary(Vector3 worldPosition)
        {
            var local = worldPosition - WofChicagoCityLayout.WorldOrigin;
            return Mathf.Abs(local.x + 65f) <= 1.5f && local.z >= 212f;
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
