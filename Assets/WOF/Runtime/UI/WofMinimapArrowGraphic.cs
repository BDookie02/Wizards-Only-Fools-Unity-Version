using UnityEngine;
using UnityEngine.UI;

namespace WOF
{
    public sealed class WofMinimapArrowGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            var halfWidth = rect.width * 0.34f;
            var halfHeight = rect.height * 0.46f;
            var tip = center + Vector2.up * halfHeight;
            var right = center + new Vector2(halfWidth, -halfHeight);
            var notch = center + new Vector2(0f, -halfHeight * 0.52f);
            var left = center + new Vector2(-halfWidth, -halfHeight);
            var inner = new Color32(255, 235, 59, 255);
            var outline = new Color32(0, 0, 0, 255);

            AddQuad(vertexHelper, tip + Vector2.up * 2f, right + new Vector2(2f, -2f), notch, left + new Vector2(-2f, -2f), outline);
            AddQuad(vertexHelper, tip, right, notch, left, inner);
        }

        private static void AddQuad(
            VertexHelper helper,
            Vector2 tip,
            Vector2 right,
            Vector2 notch,
            Vector2 left,
            Color color)
        {
            var start = helper.currentVertCount;
            helper.AddVert(tip, color, Vector2.zero);
            helper.AddVert(right, color, Vector2.zero);
            helper.AddVert(notch, color, Vector2.zero);
            helper.AddVert(left, color, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
