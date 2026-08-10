using UnityEngine;
using UnityEngine.UI;

namespace WOF
{
    public sealed class WofMapCursorGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.42f;
            var thickness = Mathf.Max(2f, radius * 0.14f);
            var gap = radius * 0.38f;
            var color = new Color32(207, 250, 254, 255);
            var shadow = new Color32(0, 0, 0, 220);

            AddCross(vertexHelper, center + new Vector2(1.5f, -1.5f), radius, thickness + 2f, gap, shadow);
            AddCross(vertexHelper, center, radius, thickness, gap, color);
        }

        private static void AddCross(
            VertexHelper helper,
            Vector2 center,
            float radius,
            float thickness,
            float gap,
            Color32 color)
        {
            AddQuad(helper, new Rect(center.x - thickness * 0.5f, center.y + gap, thickness, radius - gap), color);
            AddQuad(helper, new Rect(center.x - thickness * 0.5f, center.y - radius, thickness, radius - gap), color);
            AddQuad(helper, new Rect(center.x + gap, center.y - thickness * 0.5f, radius - gap, thickness), color);
            AddQuad(helper, new Rect(center.x - radius, center.y - thickness * 0.5f, radius - gap, thickness), color);
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
