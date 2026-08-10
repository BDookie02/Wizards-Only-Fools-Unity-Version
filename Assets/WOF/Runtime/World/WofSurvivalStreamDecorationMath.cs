using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    internal readonly struct WofSurvivalStreamTreePlacement
    {
        public WofSurvivalStreamTreePlacement(
            int meshIndex,
            Vector3 position,
            Vector3 rotationRadians,
            Vector3 scale)
        {
            MeshIndex = meshIndex;
            Position = position;
            RotationRadians = rotationRadians;
            Scale = scale;
        }

        public int MeshIndex { get; }
        public Vector3 Position { get; }
        public Vector3 RotationRadians { get; }
        public Vector3 Scale { get; }
    }

    internal sealed class WofSurvivalStreamWaterMeshData
    {
        public WofSurvivalStreamWaterMeshData(
            Vector3[] vertices,
            Color[] colors,
            int[] indices,
            int pondCount,
            int lilyCount,
            int riverTriangleCount)
        {
            Vertices = vertices;
            Colors = colors;
            Indices = indices;
            PondCount = pondCount;
            LilyCount = lilyCount;
            RiverTriangleCount = riverTriangleCount;
        }

        public Vector3[] Vertices { get; }
        public Color[] Colors { get; }
        public int[] Indices { get; }
        public int PondCount { get; }
        public int LilyCount { get; }
        public int RiverTriangleCount { get; }
    }

    internal sealed class WofSurvivalStreamDecorationData
    {
        public WofSurvivalStreamDecorationData(
            WofSurvivalStreamTreePlacement[] trees,
            WofSurvivalStreamWaterMeshData water)
        {
            Trees = trees;
            Water = water;
        }

        public WofSurvivalStreamTreePlacement[] Trees { get; }
        public WofSurvivalStreamWaterMeshData Water { get; }
    }

    internal static class WofSurvivalStreamDecorationMath
    {
        private const double RiverMaskThreshold = 0.2d;
        private const double RiverHalfSize = WofSurvivalTerrainMath.BlockSize * 0.54d;
        private const double WholeChunkWaterSuppressionRadius = WofSurvivalTerrainMath.BlockSize * 1.36d;

        internal static WofSurvivalStreamDecorationData Generate(int cx, int cz, int distance)
        {
            if (distance > WofSurvivalTerrainMath.NearRadius)
                return new WofSurvivalStreamDecorationData(
                    Array.Empty<WofSurvivalStreamTreePlacement>(),
                    null);

            var sampler = new RenderedTerrainSampler();
            return new WofSurvivalStreamDecorationData(
                GenerateTrees(cx, cz, distance, sampler),
                GenerateWater(cx, cz, distance));
        }

        private static WofSurvivalStreamTreePlacement[] GenerateTrees(
            int cx,
            int cz,
            int distance,
            RenderedTerrainSampler sampler)
        {
            var biome = WofSurvivalTerrainMath.GetBiome(cx, cz);
            var mid = distance > 0;
            var density = mid ? 0.055d : 0.92d;
            var baseCount = biome switch
            {
                WofSurvivalBiome.Jungle => 44,
                WofSurvivalBiome.Swamp => 38,
                WofSurvivalBiome.Mushroom => 34,
                WofSurvivalBiome.Desert => 22,
                _ => 36
            };
            var count = Math.Max(mid ? 1 : 8, (int)Math.Floor(baseCount * density + 0.5d));
            var attempts = count * 12;
            var footprintRadius = mid ? 11.5d : 8.5d;
            var sampleDistance = mid ? 7.2d : 5.2d;
            var minimumNormalY = mid ? 0.86d : 0.72d;
            var maximumHeightRange = mid ? 3.2d : 7.4d;
            var middleDistanceScale = mid ? 0.58d : 1d;
            var renderSegments = WofSurvivalTerrainMath.GetRenderSegments(distance);
            var chunkWorldX = cx * (double)WofSurvivalTerrainMath.BlockSize;
            var chunkWorldZ = cz * (double)WofSurvivalTerrainMath.BlockSize;
            var generated = new List<Vector2>(count);
            var placements = new List<WofSurvivalStreamTreePlacement>(count);

            for (var index = 0; index < attempts && generated.Count < count; index++)
            {
                var localX = (WofSurvivalTerrainMath.Hash01(cx, cz, 2310 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.94d;
                var localZ = (WofSurvivalTerrainMath.Hash01(cx, cz, 2350 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.94d;
                if (biome != WofSurvivalBiome.Desert && Math.Min(Math.Abs(localX), Math.Abs(localZ)) < 22d)
                    continue;

                var surface = sampler.GetSurfaceQuality(
                    cx,
                    cz,
                    renderSegments,
                    localX,
                    localZ,
                    footprintRadius,
                    sampleDistance);
                if (surface.NormalY < minimumNormalY || surface.HeightRange > maximumHeightRange) continue;

                var worldX = chunkWorldX + localX;
                var worldZ = chunkWorldZ + localZ;
                if (surface.Y < WofSurvivalTerrainMath.GetWaterLevelAtWorld(worldX, worldZ) + 0.2d) continue;

                var baseSpacing = biome switch
                {
                    WofSurvivalBiome.Jungle => 22d,
                    WofSurvivalBiome.Swamp => 20d,
                    WofSurvivalBiome.Desert => 28d,
                    _ => 23d
                };
                var spacing = mid ? baseSpacing * 2.35d : baseSpacing;
                var spacingSquared = spacing * spacing;
                var tooClose = false;
                foreach (var tree in generated)
                {
                    var dx = tree.x - localX;
                    var dz = tree.y - localZ;
                    if (dx * dx + dz * dz >= spacingSquared) continue;
                    tooClose = true;
                    break;
                }
                if (tooClose) continue;

                var variant = WofSurvivalTerrainMath.Hash01(cx, cz, 2390 + index);
                GetTreeProfile(biome, variant, out var trunkHeight, out var canopyRadius);
                var geometryVariant = (int)Math.Floor(
                    WofSurvivalTerrainMath.Hash01(cx, cz, 2465 + index) * 4d) % 4;
                var treeIndex = generated.Count;
                var lean = (variant - 0.5d) * (0.08d + geometryVariant * 0.018d) * (mid ? 0.36d : 1d);
                var heightStretch = mid
                    ? 0.72d + WofSurvivalTerrainMath.Hash01(cx + treeIndex, cz - treeIndex, 8220) * 0.22d
                    : 0.82d + WofSurvivalTerrainMath.Hash01(cx + treeIndex, cz - treeIndex, 8220) * 0.46d;
                var radiusStretch = mid
                    ? 0.82d + WofSurvivalTerrainMath.Hash01(cx - treeIndex, cz + treeIndex, 8230) * 0.2d
                    : 1.12d + WofSurvivalTerrainMath.Hash01(cx - treeIndex, cz + treeIndex, 8230) * 0.52d;
                var depthStretch = mid
                    ? 0.74d + WofSurvivalTerrainMath.Hash01(
                        cx + geometryVariant,
                        cz - geometryVariant,
                        8240 + treeIndex) * 0.18d
                    : 0.82d + WofSurvivalTerrainMath.Hash01(
                        cx + geometryVariant,
                        cz - geometryVariant,
                        8240 + treeIndex) * 0.42d;
                var radiusScale = canopyRadius * middleDistanceScale * radiusStretch;
                placements.Add(new WofSurvivalStreamTreePlacement(
                    (int)biome * 4 + geometryVariant,
                    new Vector3((float)worldX, (float)(surface.Y + 0.04d), (float)worldZ),
                    new Vector3(
                        (float)lean,
                        (float)(WofSurvivalTerrainMath.Hash01(cx, cz, 2430 + index) * Math.PI * 2d),
                        (float)(-lean * 0.62d)),
                    new Vector3(
                        (float)radiusScale,
                        (float)(trunkHeight * middleDistanceScale * heightStretch),
                        (float)(radiusScale * depthStretch))));
                generated.Add(new Vector2((float)localX, (float)localZ));
            }

            return placements.ToArray();
        }

        private static WofSurvivalStreamWaterMeshData GenerateWater(
            int cx,
            int cz,
            int distance)
        {
            var biome = WofSurvivalTerrainMath.GetBiome(cx, cz);
            var chunkWorldX = cx * (double)WofSurvivalTerrainMath.BlockSize;
            var chunkWorldZ = cz * (double)WofSurvivalTerrainMath.BlockSize;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            var waterColor = GetWaterColor(biome, GetWaterOpacity(biome));
            var suppressWholeChunk = WofSurvivalTerrainMath.IsWaterSuppressed(
                chunkWorldX,
                chunkWorldZ,
                WholeChunkWaterSuppressionRadius);
            var riverTriangleCount = 0;

            if (WofSurvivalTerrainMath.HasRiver(cx, cz) && !suppressWholeChunk)
            {
                var segments = distance == 0 ? 48 : 24;
                var gridSize = segments + 1;
                var firstVertex = vertices.Count;
                var span = RiverHalfSize * 2d;
                for (var zIndex = 0; zIndex <= segments; zIndex++)
                for (var xIndex = 0; xIndex <= segments; xIndex++)
                {
                    var worldX = chunkWorldX - RiverHalfSize + xIndex / (double)segments * span;
                    var worldZ = chunkWorldZ - RiverHalfSize + zIndex / (double)segments * span;
                    vertices.Add(new Vector3(
                        (float)(worldX - chunkWorldX),
                        (float)(WofSurvivalTerrainMath.GetWaterLevelAtWorld(worldX, worldZ) + 0.16d),
                        (float)(worldZ - chunkWorldZ)));
                    colors.Add(waterColor);
                }

                for (var zIndex = 0; zIndex < segments; zIndex++)
                for (var xIndex = 0; xIndex < segments; xIndex++)
                {
                    var centerWorldX = chunkWorldX - RiverHalfSize + (xIndex + 0.5d) / segments * span;
                    var centerWorldZ = chunkWorldZ - RiverHalfSize + (zIndex + 0.5d) / segments * span;
                    if (WofSurvivalTerrainMath.IsWaterSuppressed(
                            centerWorldX,
                            centerWorldZ,
                            WofSurvivalTerrainMath.GetRiverWidthForBiome(biome) * 0.62d +
                            WofSurvivalTerrainMath.BlockSize * 0.42d))
                        continue;
                    if (WofSurvivalTerrainMath.GetChunkRiverMask(cx, cz, centerWorldX, centerWorldZ) <
                        RiverMaskThreshold)
                        continue;
                    var centerLocalX = centerWorldX - chunkWorldX;
                    var centerLocalZ = centerWorldZ - chunkWorldZ;
                    var waterY = WofSurvivalTerrainMath.GetWaterLevelAtWorld(centerWorldX, centerWorldZ) + 0.16d;
                    var terrainY = WofSurvivalTerrainMath.GetTerrainHeight(
                        cx,
                        cz,
                        centerLocalX,
                        centerLocalZ);
                    if (terrainY > waterY + 0.48d) continue;

                    var a = firstVertex + zIndex * gridSize + xIndex;
                    var b = a + 1;
                    var c = a + gridSize + 1;
                    var d = a + gridSize;
                    indices.Add(a);
                    indices.Add(b);
                    indices.Add(c);
                    indices.Add(a);
                    indices.Add(c);
                    indices.Add(d);
                    riverTriangleCount += 2;
                }
            }

            var ponds = suppressWholeChunk ? Array.Empty<Pond>() : MakePonds(cx, cz, biome);
            var shoreColor = biome == WofSurvivalBiome.Desert
                ? FromRgb(0xd5, 0xbb, 0x76, GetShoreOpacity(biome))
                : FromRgb(0x4a, 0x6d, 0x3a, GetShoreOpacity(biome));
            var pondWaterColor = GetWaterColor(biome, Math.Min(0.7f, GetWaterOpacity(biome) + 0.08f));
            foreach (var pond in ponds)
            {
                AddDisk(
                    vertices,
                    colors,
                    indices,
                    (float)pond.LocalX,
                    (float)(pond.Y - 0.04d),
                    (float)pond.LocalZ,
                    (float)(pond.RadiusX + 7d),
                    (float)(pond.RadiusZ + 7d),
                    0f,
                    shoreColor,
                    16);
                AddDisk(
                    vertices,
                    colors,
                    indices,
                    (float)pond.LocalX,
                    (float)pond.Y,
                    (float)pond.LocalZ,
                    (float)pond.RadiusX,
                    (float)pond.RadiusZ,
                    0f,
                    pondWaterColor,
                    16);
            }

            var lilies = MakeLilies(cx, cz, biome, distance);
            var lilyColor = FromRgb(0x69, 0xa3, 0x3a, 1f);
            foreach (var lily in lilies)
            {
                var worldX = chunkWorldX + lily.LocalX;
                var worldZ = chunkWorldZ + lily.LocalZ;
                AddDisk(
                    vertices,
                    colors,
                    indices,
                    (float)lily.LocalX,
                    (float)(WofSurvivalTerrainMath.GetWaterLevelAtWorld(worldX, worldZ) + 0.24d),
                    (float)lily.LocalZ,
                    (float)(lily.Scale * 1.35d),
                    (float)lily.Scale,
                    (float)(WofSurvivalTerrainMath.Hash01(cx, cz, lily.Scale) * Math.PI),
                    lilyColor,
                    10);
            }

            if (indices.Count == 0) return null;
            return new WofSurvivalStreamWaterMeshData(
                vertices.ToArray(),
                colors.ToArray(),
                indices.ToArray(),
                ponds.Length,
                lilies.Length,
                riverTriangleCount);
        }

        private static Pond[] MakePonds(int cx, int cz, WofSurvivalBiome biome)
        {
            var roll = WofSurvivalTerrainMath.Hash01(cx, cz, 155);
            var count = biome switch
            {
                WofSurvivalBiome.Swamp => 3,
                WofSurvivalBiome.Jungle => roll > 0.72d ? 1 : 0,
                WofSurvivalBiome.Tallgrass => roll > 0.9d ? 1 : 0,
                WofSurvivalBiome.Plains => roll > 0.94d ? 1 : 0,
                WofSurvivalBiome.Mushroom => roll > 0.96d ? 1 : 0,
                _ => roll > 0.985d ? 1 : 0
            };
            if (count == 0) return Array.Empty<Pond>();
            var result = new List<Pond>(count);
            var chunkWorldX = cx * (double)WofSurvivalTerrainMath.BlockSize;
            var chunkWorldZ = cz * (double)WofSurvivalTerrainMath.BlockSize;
            for (var index = 0; index < count; index++)
            {
                var localX = (WofSurvivalTerrainMath.Hash01(cx, cz, 160 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.62d;
                var localZ = (WofSurvivalTerrainMath.Hash01(cx, cz, 180 + index) - 0.5d) *
                             WofSurvivalTerrainMath.BlockSize * 0.62d;
                var radiusX = 20d + WofSurvivalTerrainMath.Hash01(cx, cz, 210 + index) *
                    (biome == WofSurvivalBiome.Swamp ? 42d : 24d);
                var radiusZ = 16d + WofSurvivalTerrainMath.Hash01(cx, cz, 240 + index) *
                    (biome == WofSurvivalBiome.Swamp ? 36d : 18d);
                var worldX = chunkWorldX + localX;
                var worldZ = chunkWorldZ + localZ;
                if (WofSurvivalTerrainMath.IsWaterSuppressed(
                        worldX,
                        worldZ,
                        Math.Max(radiusX, radiusZ) + 24d))
                    continue;
                result.Add(new Pond(
                    localX,
                    localZ,
                    radiusX,
                    radiusZ,
                    WofSurvivalTerrainMath.GetWaterLevelAtWorld(worldX, worldZ) + 0.12d));
            }
            return result.ToArray();
        }

        private static Lily[] MakeLilies(int cx, int cz, WofSurvivalBiome biome, int distance)
        {
            if (biome != WofSurvivalBiome.Swamp) return Array.Empty<Lily>();
            var count = distance == 0 ? 12 : 4;
            var result = new Lily[count];
            for (var index = 0; index < count; index++)
            {
                result[index] = new Lily(
                    (WofSurvivalTerrainMath.Hash01(cx, cz, 300 + index) - 0.5d) *
                    WofSurvivalTerrainMath.BlockSize * 0.86d,
                    (WofSurvivalTerrainMath.Hash01(cx, cz, 330 + index) - 0.5d) *
                    WofSurvivalTerrainMath.BlockSize * 0.86d,
                    2.4d + WofSurvivalTerrainMath.Hash01(cx, cz, 360 + index) * 3.2d);
            }
            return result;
        }

        private static void AddDisk(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> indices,
            float localX,
            float y,
            float localZ,
            float radiusX,
            float radiusZ,
            float yaw,
            Color color,
            int segments)
        {
            var first = vertices.Count;
            vertices.Add(new Vector3(localX, y, localZ));
            colors.Add(color);
            var yawCos = Math.Cos(yaw);
            var yawSin = Math.Sin(yaw);
            for (var index = 0; index <= segments; index++)
            {
                var angle = index / (double)segments * Math.PI * 2d;
                var scaledX = Math.Cos(angle) * radiusX;
                var scaledY = Math.Sin(angle) * radiusZ;
                var x = yawCos * scaledX - yawSin * scaledY;
                var z = -(yawSin * scaledX + yawCos * scaledY);
                vertices.Add(new Vector3(localX + (float)x, y, localZ + (float)z));
                colors.Add(color);
            }
            for (var index = 0; index < segments; index++)
            {
                indices.Add(first);
                indices.Add(first + index + 1);
                indices.Add(first + index + 2);
            }
        }

        private static void GetTreeProfile(
            WofSurvivalBiome biome,
            double variant,
            out double trunkHeight,
            out double canopyRadius)
        {
            switch (biome)
            {
                case WofSurvivalBiome.Jungle:
                    trunkHeight = 88d + variant * 72d;
                    canopyRadius = 13d + variant * 10d;
                    return;
                case WofSurvivalBiome.Swamp:
                    trunkHeight = 54d + variant * 42d;
                    canopyRadius = 9d + variant * 7d;
                    return;
                case WofSurvivalBiome.Mushroom:
                    trunkHeight = 26d + variant * 22d;
                    canopyRadius = 9d + variant * 8d;
                    return;
                case WofSurvivalBiome.Desert:
                    trunkHeight = 34d + variant * 28d;
                    canopyRadius = 6.5d + variant * 5d;
                    return;
                default:
                    trunkHeight = 46d + variant * 38d;
                    canopyRadius = 9.5d + variant * 7d;
                    return;
            }
        }

        private static float GetWaterOpacity(WofSurvivalBiome biome)
        {
            if (biome == WofSurvivalBiome.Swamp) return 0.66f;
            return biome == WofSurvivalBiome.Desert ? 0.34f : 0.44f;
        }

        private static float GetShoreOpacity(WofSurvivalBiome biome)
        {
            if (biome == WofSurvivalBiome.Swamp) return 0.26f;
            return biome == WofSurvivalBiome.Desert ? 0.16f : 0.12f;
        }

        private static Color GetWaterColor(WofSurvivalBiome biome, float alpha)
        {
            return biome switch
            {
                WofSurvivalBiome.Plains => FromRgb(0x2e, 0x72, 0xa8, alpha),
                WofSurvivalBiome.Jungle => FromRgb(0x19, 0x6d, 0x6f, alpha),
                WofSurvivalBiome.Desert => FromRgb(0x4e, 0xa7, 0xb6, alpha),
                WofSurvivalBiome.Swamp => FromRgb(0x24, 0x5f, 0x62, alpha),
                WofSurvivalBiome.Mushroom => FromRgb(0x49, 0x6e, 0xb6, alpha),
                WofSurvivalBiome.Tallgrass => FromRgb(0x3a, 0x8f, 0x9e, alpha),
                _ => Color.clear
            };
        }

        private static Color FromRgb(byte r, byte g, byte b, float alpha)
        {
            return new Color(r / 255f, g / 255f, b / 255f, alpha);
        }

        private readonly struct Pond
        {
            public Pond(double localX, double localZ, double radiusX, double radiusZ, double y)
            {
                LocalX = localX;
                LocalZ = localZ;
                RadiusX = radiusX;
                RadiusZ = radiusZ;
                Y = y;
            }

            public double LocalX { get; }
            public double LocalZ { get; }
            public double RadiusX { get; }
            public double RadiusZ { get; }
            public double Y { get; }
        }

        private readonly struct Lily
        {
            public Lily(double localX, double localZ, double scale)
            {
                LocalX = localX;
                LocalZ = localZ;
                Scale = scale;
            }

            public double LocalX { get; }
            public double LocalZ { get; }
            public double Scale { get; }
        }

        private readonly struct SurfaceQuality
        {
            public SurfaceQuality(double y, double normalY, double heightRange)
            {
                Y = y;
                NormalY = normalY;
                HeightRange = heightRange;
            }

            public double Y { get; }
            public double NormalY { get; }
            public double HeightRange { get; }
        }

        private sealed class RenderedTerrainSampler
        {
            private readonly Dictionary<GridKey, float[]> _grids = new();

            public SurfaceQuality GetSurfaceQuality(
                int cx,
                int cz,
                int segments,
                double localX,
                double localZ,
                double footprintRadius,
                double sampleDistance)
            {
                var terrainY = GetRenderedHeight(cx, cz, segments, localX, localZ);
                var left = GetRenderedHeight(cx, cz, segments, localX - sampleDistance, localZ);
                var right = GetRenderedHeight(cx, cz, segments, localX + sampleDistance, localZ);
                var down = GetRenderedHeight(cx, cz, segments, localX, localZ - sampleDistance);
                var up = GetRenderedHeight(cx, cz, segments, localX, localZ + sampleDistance);
                var normalX = left - right;
                var normalY = sampleDistance * 2d;
                var normalZ = down - up;
                var inverseLength = 1d / Math.Sqrt(normalX * normalX + normalY * normalY + normalZ * normalZ);
                normalY *= inverseLength;

                var worldX = cx * (double)WofSurvivalTerrainMath.BlockSize + localX;
                var worldZ = cz * (double)WofSurvivalTerrainMath.BlockSize + localZ;
                var footprint = GetFootprintStats(worldX, worldZ, footprintRadius);
                return new SurfaceQuality(Math.Min(terrainY, footprint.BaseY), normalY, footprint.HeightRange);
            }

            public double GetRenderedHeight(int cx, int cz, int segments, double localX, double localZ)
            {
                var grid = GetGrid(cx, cz, segments);
                var clampedX = Clamp01((localX + WofSurvivalTerrainMath.BlockSize * 0.5d) /
                                       WofSurvivalTerrainMath.BlockSize) * segments;
                var clampedZ = Clamp01((localZ + WofSurvivalTerrainMath.BlockSize * 0.5d) /
                                       WofSurvivalTerrainMath.BlockSize) * segments;
                var x0 = (int)Math.Floor(clampedX);
                var z0 = (int)Math.Floor(clampedZ);
                var x1 = Math.Min(segments, x0 + 1);
                var z1 = Math.Min(segments, z0 + 1);
                var tx = clampedX - x0;
                var tz = clampedZ - z0;
                var size = segments + 1;
                var h00 = grid[z0 * size + x0];
                var h10 = grid[z0 * size + x1];
                var h01 = grid[z1 * size + x0];
                var h11 = grid[z1 * size + x1];
                return Lerp(Lerp(h00, h10, tx), Lerp(h01, h11, tx), tz);
            }

            private FootprintStats GetFootprintStats(double worldX, double worldZ, double radius)
            {
                var sampleRadius = Math.Max(0.16d, radius);
                var diagonalRadius = sampleRadius * 0.72d;
                var centerY = GetGrassHeightAtWorld(worldX, worldZ);
                var minimum = centerY;
                var maximum = centerY;
                Sample(worldX + sampleRadius, worldZ, ref minimum, ref maximum);
                Sample(worldX - sampleRadius, worldZ, ref minimum, ref maximum);
                Sample(worldX, worldZ + sampleRadius, ref minimum, ref maximum);
                Sample(worldX, worldZ - sampleRadius, ref minimum, ref maximum);
                Sample(worldX + diagonalRadius, worldZ + diagonalRadius, ref minimum, ref maximum);
                Sample(worldX - diagonalRadius, worldZ + diagonalRadius, ref minimum, ref maximum);
                Sample(worldX + diagonalRadius, worldZ - diagonalRadius, ref minimum, ref maximum);
                Sample(worldX - diagonalRadius, worldZ - diagonalRadius, ref minimum, ref maximum);
                var range = maximum - minimum;
                var slopeTuck = SmoothstepRange(0.08d, 1.55d, range);
                var tuckedCenterY = centerY + Lerp(0.004d, -0.035d, slopeTuck);
                return new FootprintStats(Math.Max(minimum - 0.025d, tuckedCenterY), range);
            }

            private void Sample(double worldX, double worldZ, ref double minimum, ref double maximum)
            {
                var y = GetGrassHeightAtWorld(worldX, worldZ);
                minimum = Math.Min(minimum, y);
                maximum = Math.Max(maximum, y);
            }

            private double GetGrassHeightAtWorld(double worldX, double worldZ)
            {
                var cx = WofSurvivalTerrainMath.GetChunkCoordinate(worldX);
                var cz = WofSurvivalTerrainMath.GetChunkCoordinate(worldZ);
                return GetRenderedHeight(
                    cx,
                    cz,
                    WofSurvivalTerrainMath.GetRenderSegments(0),
                    worldX - cx * (double)WofSurvivalTerrainMath.BlockSize,
                    worldZ - cz * (double)WofSurvivalTerrainMath.BlockSize);
            }

            private float[] GetGrid(int cx, int cz, int segments)
            {
                var key = new GridKey(cx, cz, segments);
                if (_grids.TryGetValue(key, out var existing)) return existing;
                var size = segments + 1;
                var result = new float[size * size];
                var step = WofSurvivalTerrainMath.BlockSize / (double)segments;
                var half = WofSurvivalTerrainMath.BlockSize * 0.5d;
                for (var zIndex = 0; zIndex <= segments; zIndex++)
                for (var xIndex = 0; xIndex <= segments; xIndex++)
                {
                    result[zIndex * size + xIndex] = (float)WofSurvivalTerrainMath.GetTerrainHeight(
                        cx,
                        cz,
                        -half + xIndex * step,
                        -half + zIndex * step);
                }
                _grids.Add(key, result);
                return result;
            }

            private static double SmoothstepRange(double minimum, double maximum, double value)
            {
                var amount = Clamp01((value - minimum) / (maximum - minimum));
                return amount * amount * (3d - 2d * amount);
            }

            private static double Clamp01(double value)
            {
                return value < 0d ? 0d : value > 1d ? 1d : value;
            }

            private static double Lerp(double a, double b, double amount)
            {
                return a + (b - a) * amount;
            }
        }

        private readonly struct FootprintStats
        {
            public FootprintStats(double baseY, double heightRange)
            {
                BaseY = baseY;
                HeightRange = heightRange;
            }

            public double BaseY { get; }
            public double HeightRange { get; }
        }

        private readonly struct GridKey : IEquatable<GridKey>
        {
            public GridKey(int x, int z, int segments)
            {
                X = x;
                Z = z;
                Segments = segments;
            }

            private int X { get; }
            private int Z { get; }
            private int Segments { get; }
            public bool Equals(GridKey other) => X == other.X && Z == other.Z && Segments == other.Segments;
            public override bool Equals(object obj) => obj is GridKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(X, Z, Segments);
        }
    }
}
