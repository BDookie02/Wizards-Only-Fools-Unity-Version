using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    internal readonly struct WofDesertCactusPlacement
    {
        public WofDesertCactusPlacement(int chunkX, int chunkZ, Vector3 position, float yawDegrees, float scale)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Position = position;
            YawDegrees = yawDegrees;
            Scale = scale;
        }

        public int ChunkX { get; }
        public int ChunkZ { get; }
        public Vector3 Position { get; }
        public float YawDegrees { get; }
        public float Scale { get; }
    }

    [DisallowMultipleComponent]
    public sealed class WofSurvivalDesertCactusRuntime : MonoBehaviour
    {
        public const int SurroundingChunkCount = 5;
        public const int CactiPerChunk = 6;
        public const int TotalCactusCount = SurroundingChunkCount * CactiPerChunk;
        public const float VisibleRadius = 1120f;

        private static readonly Vector2Int[] SurroundingChunks =
        {
            new(3, -4), new(5, -4), new(3, -3), new(4, -3), new(5, -3)
        };

        private readonly List<CactusBatch> _batches = new();
        private Mesh _mesh;
        private Material _material;
        private Transform _viewer;
        private float _nextViewerResolveAt;

        private void Awake()
        {
            if (!SystemInfo.supportsInstancing)
            {
                enabled = false;
                return;
            }

            var shader = Shader.Find("WOF/Instanced Foliage");
            if (shader == null)
            {
                Debug.LogError("[WOF-AUTOMATION] DESERT_CACTUS_FAILED reason=missing-foliage-shader");
                enabled = false;
                return;
            }

            _mesh = CreateCactusMesh();
            _material = new Material(shader)
            {
                name = "ReactDesertCactusRuntimeMaterial",
                enableInstancing = true
            };
            _material.SetColor("_Color", Color.white);

            foreach (var placement in CreatePlacements())
            {
                var batch = GetOrCreateBatch(placement.ChunkX, placement.ChunkZ);
                batch.Matrices[batch.Count++] = Matrix4x4.TRS(
                    placement.Position,
                    Quaternion.Euler(0f, placement.YawDegrees, 0f),
                    Vector3.one * placement.Scale);
            }

            Debug.Log($"[WOF-AUTOMATION] DESERT_CACTUS_READY chunks={SurroundingChunkCount} cacti={TotalCactusCount}");
        }

        private void Update()
        {
            ResolveViewer();
            if (_viewer == null) return;
            var visibleRadiusSquared = VisibleRadius * VisibleRadius;
            foreach (var batch in _batches)
            {
                var dx = batch.Center.x - _viewer.position.x;
                var dz = batch.Center.z - _viewer.position.z;
                if (dx * dx + dz * dz > visibleRadiusSquared) continue;
                Graphics.DrawMeshInstanced(
                    _mesh,
                    0,
                    _material,
                    batch.Matrices,
                    batch.Count,
                    batch.Properties,
                    ShadowCastingMode.Off,
                    false,
                    gameObject.layer,
                    null,
                    LightProbeUsage.Off);
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        private void ResolveViewer()
        {
            if (_viewer != null || Time.unscaledTime < _nextViewerResolveAt) return;
            _nextViewerResolveAt = Time.unscaledTime + 0.25f;
            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (playerObject != null) _viewer = playerObject.transform;
        }

        private CactusBatch GetOrCreateBatch(int chunkX, int chunkZ)
        {
            foreach (var batch in _batches)
            {
                if (batch.ChunkX == chunkX && batch.ChunkZ == chunkZ) return batch;
            }
            var created = new CactusBatch(chunkX, chunkZ);
            _batches.Add(created);
            return created;
        }

        internal static WofDesertCactusPlacement[] CreatePlacements()
        {
            var result = new WofDesertCactusPlacement[TotalCactusCount];
            var output = 0;
            foreach (var chunk in SurroundingChunks)
            {
                for (var index = 0; index < CactiPerChunk; index++)
                {
                    var localX = (WofSurvivalTerrainMath.Hash01(chunk.x, chunk.y, 7100 + index * 17) - 0.5d) *
                                 WofSurvivalTerrainMath.BlockSize * 0.78d;
                    var localZ = (WofSurvivalTerrainMath.Hash01(chunk.x, chunk.y, 7200 + index * 23) - 0.5d) *
                                 WofSurvivalTerrainMath.BlockSize * 0.78d;
                    var worldX = chunk.x * (double)WofSurvivalTerrainMath.BlockSize + localX;
                    var worldZ = chunk.y * (double)WofSurvivalTerrainMath.BlockSize + localZ;
                    var worldY = WofSurvivalTerrainMath.GetTerrainHeight(chunk.x, chunk.y, localX, localZ) + 0.04d;
                    var yaw = (float)(WofSurvivalTerrainMath.Hash01(chunk.x, chunk.y, 7300 + index * 29) * 360d);
                    var scale = (float)(1.28d +
                                        WofSurvivalTerrainMath.Hash01(chunk.x, chunk.y, 90 + index) * 1.15d);
                    result[output++] = new WofDesertCactusPlacement(
                        chunk.x,
                        chunk.y,
                        new Vector3((float)worldX, (float)worldY, (float)worldZ),
                        yaw,
                        scale);
                }
            }
            return result;
        }

        internal static Mesh CreateCactusMesh()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            var trunk = new Color32(0x2f, 0x82, 0x49, 0xff);
            var light = new Color32(0x58, 0xac, 0x62, 0xff);
            var dark = new Color32(0x18, 0x4d, 0x2d, 0xff);

            // A deliberately heavy saguaro silhouette: broad tapered body,
            // thick elbows, and two substantial upturned arms. Alternating
            // vertex colors create readable low-poly ribs without thin geometry.
            AppendTaperedCylinder(vertices, normals, colors, indices,
                Vector3.zero, new Vector3(0f, 10.6f, 0f), 1.22f, 0.98f, trunk, light, dark, 8);
            AppendHemisphere(vertices, normals, colors, indices,
                new Vector3(0f, 10.6f, 0f), 0.98f, light, 8);

            AppendTaperedCylinder(vertices, normals, colors, indices,
                new Vector3(-0.5f, 4.15f, 0f), new Vector3(-3.25f, 4.15f, 0f),
                0.72f, 0.64f, trunk, light, dark, 7);
            AppendTaperedCylinder(vertices, normals, colors, indices,
                new Vector3(-3.25f, 3.95f, 0f), new Vector3(-3.25f, 8.35f, 0f),
                0.72f, 0.54f, trunk, light, dark, 7);
            AppendHemisphere(vertices, normals, colors, indices,
                new Vector3(-3.25f, 8.35f, 0f), 0.54f, light, 7);

            AppendTaperedCylinder(vertices, normals, colors, indices,
                new Vector3(0.52f, 6.05f, 0f), new Vector3(2.65f, 6.05f, 0f),
                0.6f, 0.54f, trunk, light, dark, 7);
            AppendTaperedCylinder(vertices, normals, colors, indices,
                new Vector3(2.65f, 5.9f, 0f), new Vector3(2.65f, 9.25f, 0f),
                0.6f, 0.46f, trunk, light, dark, 7);
            AppendHemisphere(vertices, normals, colors, indices,
                new Vector3(2.65f, 9.25f, 0f), 0.46f, light, 7);

            var mesh = new Mesh
            {
                name = "ReactDesertSaguaroCactus",
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                colors = colors.ToArray(),
                triangles = indices.ToArray()
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendTaperedCylinder(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> indices,
            Vector3 start,
            Vector3 end,
            float startRadius,
            float endRadius,
            Color bodyColor,
            Color lightColor,
            Color darkColor,
            int sides)
        {
            var axis = (end - start).normalized;
            var reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.94f ? Vector3.right : Vector3.up;
            var right = Vector3.Cross(axis, reference).normalized;
            var forward = Vector3.Cross(right, axis).normalized;
            var first = vertices.Count;
            for (var ring = 0; ring < 2; ring++)
            {
                var center = ring == 0 ? start : end;
                var radius = ring == 0 ? startRadius : endRadius;
                for (var side = 0; side < sides; side++)
                {
                    var angle = side / (float)sides * Mathf.PI * 2f;
                    var radial = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                    vertices.Add(center + radial * radius);
                    normals.Add(radial);
                    colors.Add(side % 3 == 0 ? lightColor : side % 3 == 1 ? bodyColor : darkColor);
                }
            }
            for (var side = 0; side < sides; side++)
            {
                var next = (side + 1) % sides;
                indices.Add(first + side);
                indices.Add(first + sides + side);
                indices.Add(first + next);
                indices.Add(first + next);
                indices.Add(first + sides + side);
                indices.Add(first + sides + next);
            }
        }

        private static void AppendHemisphere(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> indices,
            Vector3 center,
            float radius,
            Color color,
            int sides)
        {
            var first = vertices.Count;
            const int rings = 3;
            for (var ring = 0; ring <= rings; ring++)
            {
                var latitude = ring / (float)rings * Mathf.PI * 0.5f;
                var ringRadius = Mathf.Cos(latitude) * radius;
                var y = Mathf.Sin(latitude) * radius;
                for (var side = 0; side < sides; side++)
                {
                    var angle = side / (float)sides * Mathf.PI * 2f;
                    var normal = new Vector3(
                        Mathf.Cos(angle) * Mathf.Cos(latitude),
                        Mathf.Sin(latitude),
                        Mathf.Sin(angle) * Mathf.Cos(latitude));
                    vertices.Add(center + new Vector3(Mathf.Cos(angle) * ringRadius, y, Mathf.Sin(angle) * ringRadius));
                    normals.Add(normal);
                    colors.Add(color);
                }
            }
            for (var ring = 0; ring < rings; ring++)
            for (var side = 0; side < sides; side++)
            {
                var next = (side + 1) % sides;
                var currentRing = first + ring * sides;
                var nextRing = currentRing + sides;
                indices.Add(currentRing + side);
                indices.Add(nextRing + side);
                indices.Add(currentRing + next);
                indices.Add(currentRing + next);
                indices.Add(nextRing + side);
                indices.Add(nextRing + next);
            }
        }

        private sealed class CactusBatch
        {
            public CactusBatch(int chunkX, int chunkZ)
            {
                ChunkX = chunkX;
                ChunkZ = chunkZ;
                Center = new Vector3(
                    chunkX * WofSurvivalTerrainMath.BlockSize,
                    0f,
                    chunkZ * WofSurvivalTerrainMath.BlockSize);
                Matrices = new Matrix4x4[CactiPerChunk];
                var instanceColors = new Vector4[CactiPerChunk];
                for (var index = 0; index < instanceColors.Length; index++) instanceColors[index] = Vector4.one;
                Properties.SetVectorArray("_InstanceColor", instanceColors);
            }

            public int ChunkX { get; }
            public int ChunkZ { get; }
            public Vector3 Center { get; }
            public Matrix4x4[] Matrices { get; }
            public MaterialPropertyBlock Properties { get; } = new();
            public int Count { get; set; }
        }
    }
}
