using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WOF
{
    /// <summary>A small code-native waypoint glyph that stays legible over either live map.</summary>
    public sealed class WofWizardHatWaypointGraphic : MaskableGraphic
    {
        private static readonly Color32 HighlightRed = new(248, 32, 64, 255);
        private static readonly Color32 ShadowRed = new(112, 8, 24, 245);
        private static readonly Color32 HatPurple = new(126, 34, 206, 255);
        private static readonly Color32 HatLight = new(216, 180, 254, 255);

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            var size = Mathf.Min(rect.width, rect.height);

            AddRing(helper, center, size * 0.48f, size * 0.39f, HighlightRed);
            AddHat(helper, center + new Vector2(size * 0.025f, -size * 0.025f), size * 1.08f, ShadowRed);
            AddHat(helper, center, size, HatPurple);

            // A single bright fold keeps the symbol recognizable at compact-map size.
            AddTriangle(
                helper,
                center + new Vector2(-size * 0.08f, -size * 0.03f),
                center + new Vector2(size * 0.02f, size * 0.29f),
                center + new Vector2(size * 0.055f, -size * 0.02f),
                HatLight);
        }

        private static void AddHat(VertexHelper helper, Vector2 center, float size, Color32 color)
        {
            var crown = new List<Vector2>(7)
            {
                center + new Vector2(-0.26f, -0.09f) * size,
                center + new Vector2(-0.16f, 0.13f) * size,
                center + new Vector2(-0.04f, 0.39f) * size,
                center + new Vector2(0.03f, 0.19f) * size,
                center + new Vector2(0.24f, 0.13f) * size,
                center + new Vector2(0.13f, 0.02f) * size,
                center + new Vector2(0.24f, -0.09f) * size
            };
            AddConvexPolygon(helper, crown, color);
            AddQuad(
                helper,
                new Rect(center.x - size * 0.38f, center.y - size * 0.17f, size * 0.76f, size * 0.12f),
                color);
        }

        private static void AddRing(VertexHelper helper, Vector2 center, float outer, float inner, Color32 color)
        {
            const int segments = 24;
            var first = helper.currentVertCount;
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                helper.AddVert(center + direction * outer, color, Vector2.zero);
                helper.AddVert(center + direction * inner, color, Vector2.zero);
            }
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                var outerA = first + index * 2;
                var innerA = outerA + 1;
                var outerB = first + next * 2;
                var innerB = outerB + 1;
                helper.AddTriangle(outerA, outerB, innerB);
                helper.AddTriangle(outerA, innerB, innerA);
            }
        }

        private static void AddConvexPolygon(VertexHelper helper, IReadOnlyList<Vector2> points, Color32 color)
        {
            if (points.Count < 3) return;
            var center = Vector2.zero;
            for (var index = 0; index < points.Count; index++) center += points[index];
            center /= points.Count;
            var first = helper.currentVertCount;
            helper.AddVert(center, color, Vector2.zero);
            for (var index = 0; index < points.Count; index++) helper.AddVert(points[index], color, Vector2.zero);
            for (var index = 0; index < points.Count; index++)
            {
                helper.AddTriangle(first, first + 1 + index, first + 1 + (index + 1) % points.Count);
            }
        }

        private static void AddTriangle(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            var first = helper.currentVertCount;
            helper.AddVert(a, color, Vector2.zero);
            helper.AddVert(b, color, Vector2.zero);
            helper.AddVert(c, color, Vector2.zero);
            helper.AddTriangle(first, first + 1, first + 2);
        }

        private static void AddQuad(VertexHelper helper, Rect rect, Color32 color)
        {
            var first = helper.currentVertCount;
            helper.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            helper.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
            helper.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
            helper.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first, first + 2, first + 3);
        }
    }
}
