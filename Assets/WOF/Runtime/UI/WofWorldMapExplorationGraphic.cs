using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace WOF
{
    public sealed class WofWorldMapExplorationGraphic : MaskableGraphic
    {
        public const int ColumnCount = 11;
        public const int RowCount = 8;
        private static readonly Color32 FogColor = new(10, 8, 18, 188);
        private static readonly Color32 GridColor = new(210, 238, 255, 34);
        private static readonly Color32 CurrentFillColor = new(255, 214, 70, 34);
        private static readonly Color32 CurrentBorderColor = new(255, 214, 70, 255);

        private readonly HashSet<int> _exploredCells = new();
        private int _currentColumn = -1;
        private int _currentRow = -1;

        public int ExploredCount => _exploredCells.Count;

        public bool SetWorldPosition(float worldX, float worldZ)
        {
            if (!TryGetCell(worldX, worldZ, out var column, out var row))
            {
                return false;
            }

            var changed = _currentColumn != column || _currentRow != row;
            _currentColumn = column;
            _currentRow = row;
            var revealed = _exploredCells.Add(GetCellKey(column, row));
            if (changed || revealed) SetVerticesDirty();
            return revealed;
        }

        public bool IsExplored(int column, int row)
        {
            return IsValidCell(column, row) && _exploredCells.Contains(GetCellKey(column, row));
        }

        public void ImportExploredCells(string serialized)
        {
            _exploredCells.Clear();
            if (!string.IsNullOrWhiteSpace(serialized))
            {
                foreach (var token in serialized.Split(';'))
                {
                    var pair = token.Split(',');
                    if (pair.Length != 2 ||
                        !int.TryParse(pair[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var column) ||
                        !int.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row) ||
                        !IsValidCell(column, row))
                    {
                        continue;
                    }
                    _exploredCells.Add(GetCellKey(column, row));
                }
            }
            SetVerticesDirty();
        }

        public string ExportExploredCells()
        {
            return string.Join(";", _exploredCells
                .OrderBy(key => key)
                .Select(key => $"{key % ColumnCount},{key / ColumnCount}"));
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var pixelRect = GetPixelAdjustedRect();
            for (var row = 0; row < RowCount; row++)
            {
                for (var column = 0; column < ColumnCount; column++)
                {
                    if (IsExplored(column, row)) continue;
                    AddQuad(vertexHelper, ToPixelRect(pixelRect, GetCellNormalizedRect(column, row)), FogColor);
                }
            }

            var gridThickness = Mathf.Max(1f, Mathf.Min(pixelRect.width, pixelRect.height) * 0.0016f);
            for (var column = 1; column < ColumnCount; column++)
            {
                var x = pixelRect.xMin + pixelRect.width * column / ColumnCount;
                AddQuad(vertexHelper, new Rect(x - gridThickness * 0.5f, pixelRect.yMin, gridThickness, pixelRect.height), GridColor);
            }
            for (var row = 1; row < RowCount; row++)
            {
                var y = pixelRect.yMin + pixelRect.height * row / RowCount;
                AddQuad(vertexHelper, new Rect(pixelRect.xMin, y - gridThickness * 0.5f, pixelRect.width, gridThickness), GridColor);
            }

            if (!IsValidCell(_currentColumn, _currentRow)) return;
            var currentRect = ToPixelRect(pixelRect, GetCellNormalizedRect(_currentColumn, _currentRow));
            AddQuad(vertexHelper, currentRect, CurrentFillColor);
            var border = Mathf.Max(2f, Mathf.Min(pixelRect.width, pixelRect.height) * 0.005f);
            AddBorder(vertexHelper, currentRect, border, CurrentBorderColor);
        }

        internal static bool TryGetCell(float worldX, float worldZ, out int column, out int row)
        {
            if (worldX < WofMapFastTravel.MapMinX || worldX > WofMapFastTravel.MapMaxX ||
                worldZ < WofMapFastTravel.MapMinZ || worldZ > WofMapFastTravel.MapMaxZ)
            {
                column = -1;
                row = -1;
                return false;
            }
            column = Mathf.Min(
                ColumnCount - 1,
                Mathf.FloorToInt((worldX - WofMapFastTravel.MapMinX) / WofLilyCoilLayout.SurvivalBlockSize));
            row = Mathf.Min(
                RowCount - 1,
                Mathf.FloorToInt((worldZ - WofMapFastTravel.MapMinZ) / WofLilyCoilLayout.SurvivalBlockSize));
            return IsValidCell(column, row);
        }

        internal static Vector2 GetMarkerNormalized(float worldX, float worldZ)
        {
            var x = Mathf.InverseLerp(WofMapFastTravel.MapMinX, WofMapFastTravel.MapMaxX, worldX);
            var zFromTop = Mathf.InverseLerp(WofMapFastTravel.MapMinZ, WofMapFastTravel.MapMaxZ, worldZ);
            return new Vector2(x, 1f - zFromTop);
        }

        internal static Vector2 GetWorldPosition(Vector2 normalized)
        {
            return new Vector2(
                Mathf.Lerp(WofMapFastTravel.MapMinX, WofMapFastTravel.MapMaxX, Mathf.Clamp01(normalized.x)),
                Mathf.Lerp(WofMapFastTravel.MapMaxZ, WofMapFastTravel.MapMinZ, Mathf.Clamp01(normalized.y)));
        }

        internal static Rect GetCellNormalizedRect(int column, int row)
        {
            var xMin = column / (float)ColumnCount;
            var xMax = (column + 1f) / ColumnCount;
            var yMin = 1f - (row + 1f) / RowCount;
            var yMax = 1f - row / (float)RowCount;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static bool IsValidCell(int column, int row)
        {
            return column >= 0 && column < ColumnCount && row >= 0 && row < RowCount;
        }

        private static int GetCellKey(int column, int row)
        {
            return row * ColumnCount + column;
        }

        private static Rect ToPixelRect(Rect pixelRect, Rect normalized)
        {
            return Rect.MinMaxRect(
                pixelRect.xMin + normalized.xMin * pixelRect.width,
                pixelRect.yMin + normalized.yMin * pixelRect.height,
                pixelRect.xMin + normalized.xMax * pixelRect.width,
                pixelRect.yMin + normalized.yMax * pixelRect.height);
        }

        private static void AddBorder(VertexHelper helper, Rect rect, float thickness, Color32 color)
        {
            AddQuad(helper, new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            AddQuad(helper, new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            AddQuad(helper, new Rect(rect.xMin, rect.yMin + thickness, thickness, Mathf.Max(0f, rect.height - thickness * 2f)), color);
            AddQuad(helper, new Rect(rect.xMax - thickness, rect.yMin + thickness, thickness, Mathf.Max(0f, rect.height - thickness * 2f)), color);
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
