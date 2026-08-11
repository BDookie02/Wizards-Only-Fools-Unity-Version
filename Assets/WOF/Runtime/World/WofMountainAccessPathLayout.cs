using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    internal static class WofMountainAccessPathLayout
    {
        internal const float Width = 5.5f;
        internal const float MaximumGrade = 0.24f;
        internal const float DeckClearance = 0.24f;
        internal const float DensifySegmentLength = 12f;
        internal const float MaximumSegmentLength = 14f;

        private static readonly Vector2[] SwitchbackControls =
        {
            new(0f, 320f),
            new(-220f, 280f),
            new(220f, 240f),
            new(-210f, 195f),
            new(180f, 145f),
            new(0f, 88f)
        };

        internal static Vector2[] BuildHorizontalPoints()
        {
            var points = new List<Vector2>(SwitchbackControls);
            for (var pass = 0; pass < 2; pass++) points = RoundCorners(points);
            return Densify(points).ToArray();
        }

        private static List<Vector2> Densify(IReadOnlyList<Vector2> source)
        {
            var result = new List<Vector2>(source.Count * 8) { source[0] };
            for (var index = 0; index < source.Count - 1; index++)
            {
                var current = source[index];
                var next = source[index + 1];
                var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(current, next) / DensifySegmentLength));
                for (var step = 1; step <= steps; step++)
                    result.Add(Vector2.Lerp(current, next, step / (float)steps));
            }
            return result;
        }

        private static List<Vector2> RoundCorners(IReadOnlyList<Vector2> source)
        {
            var result = new List<Vector2>(source.Count * 2) { source[0] };
            for (var index = 0; index < source.Count - 1; index++)
            {
                var current = source[index];
                var next = source[index + 1];
                result.Add(Vector2.Lerp(current, next, 0.25f));
                result.Add(Vector2.Lerp(current, next, 0.75f));
            }
            result.Add(source[source.Count - 1]);
            return result;
        }
    }
}
