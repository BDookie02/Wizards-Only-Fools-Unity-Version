using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalRockOutcropRuntime : MonoBehaviour
    {
        private const string ProbeArgument = "--wof-rock-outcrop-probe";
        private readonly Dictionary<string, WofSurvivalRockOutcropRecord[]> _recordCache = new(StringComparer.Ordinal);
        private readonly List<PendingChunk> _pendingChunks = new();
        private readonly RockBatch[] _batches = new RockBatch[WofSurvivalRockOutcropRules.PaletteColorCount * 2];
        private WofPlayerController _player;
        private Mesh _boulderMesh;
        private Mesh _spireMesh;
        private float _nextResolveAt;
        private int _centerX = int.MinValue;
        private int _centerZ = int.MinValue;
        private bool _probe;
        private bool _probeViewPrepared;
        private bool _probePositioned;

        public int RockCount { get; private set; }
        public int BoulderCount { get; private set; }
        public int SpireCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            if (FindFirstObjectByType<WofSurvivalRockOutcropRuntime>() != null) return;
            new GameObject("ReactSurvivalRockOutcropRuntime").AddComponent<WofSurvivalRockOutcropRuntime>();
        }

        private void Awake()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals(ProbeArgument, StringComparison.OrdinalIgnoreCase)) _probe = true;
            }

            var rockMeshes = CreateDodecaMeshes();
            _boulderMesh = rockMeshes.Solid;
            Destroy(rockMeshes.Edges);
            _boulderMesh.name = "ReactSurvivalRockBoulder";
            _spireMesh = CreateConeMesh(5, 2.1f, "ReactSurvivalRockSpire");
            for (var paletteIndex = 0; paletteIndex < WofSurvivalRockOutcropRules.PaletteColorCount; paletteIndex++)
            {
                var color = WofSurvivalRockOutcropRules.GetPaletteColor(paletteIndex);
                _batches[paletteIndex * 2] = new RockBatch(_boulderMesh, MakeMaterial($"ReactRockBoulder-{paletteIndex}", color));
                _batches[paletteIndex * 2 + 1] = new RockBatch(_spireMesh, MakeMaterial($"ReactRockSpire-{paletteIndex}", color));
            }
        }

        private void OnDestroy()
        {
            foreach (var batch in _batches) batch?.Dispose();
            Destroy(_boulderMesh);
            Destroy(_spireMesh);
        }

        private void Update()
        {
            var survival = WofBootstrap.Instance != null && WofBootstrap.Instance.IsSurvivalSession;
            if (!WofSurvivalRockOutcropRules.ShouldShowRuntime(survival))
            {
                ClearRuntimeState();
                return;
            }

            ResolvePlayer();
            if (_player == null) return;
            if (_probe && !_probeViewPrepared) PrepareProbeView();

            var centerX = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.x);
            var centerZ = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.z);
            if (centerX != _centerX || centerZ != _centerZ) RebuildWindow(centerX, centerZ);
            ContinueStagedGeneration();
            DrawBatches();
            TryReportProbeReady();
        }

        private void ResolvePlayer()
        {
            if (_player != null && _player.IsSpawned && _player.IsOwner) return;
            if (Time.unscaledTime < _nextResolveAt) return;
            _nextResolveAt = Time.unscaledTime + 0.25f;
            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var candidate = playerObject == null ? null : playerObject.GetComponent<WofPlayerController>();
            _player = candidate != null && candidate.IsSpawned && candidate.IsOwner ? candidate : null;
        }

        private void PrepareProbeView()
        {
            var rocks = WofSurvivalRockOutcropRules.MakeChunk(-1, -1, 0);
            if (rocks.Length < 2) return;
            var target = rocks[1];
            var viewPosition = target.Position + new Vector3(0f, target.Scale * 0.72f, -18f);
            if (!_player.PrepareForAutomationStaticViewProbe(viewPosition, 0f, -7f)) return;
            var camera = _player.GetComponentInChildren<Camera>(true);
            if (camera != null) camera.farClipPlane = 2500f;
            _probeViewPrepared = true;
        }

        private void RebuildWindow(int centerX, int centerZ)
        {
            _centerX = centerX;
            _centerZ = centerZ;
            _pendingChunks.Clear();
            foreach (var batch in _batches) batch.Count = 0;
            RockCount = 0;
            BoulderCount = 0;
            SpireCount = 0;

            for (var dz = -WofSurvivalTerrainMath.NearRadius; dz <= WofSurvivalTerrainMath.NearRadius; dz++)
            for (var dx = -WofSurvivalTerrainMath.NearRadius; dx <= WofSurvivalTerrainMath.NearRadius; dx++)
            {
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dz));
                var chunkX = centerX + dx;
                var chunkZ = centerZ + dz;
                if (!WofSurvivalRockOutcropRules.ShouldGenerateChunk(true, chunkX, chunkZ, distance)) continue;
                _pendingChunks.Add(new PendingChunk(
                    chunkX,
                    chunkZ,
                    distance,
                    Time.unscaledTime + WofSurvivalRockOutcropRules.GetReadyDelaySeconds(
                        chunkX,
                        chunkZ,
                        distance,
                        WofPerformanceModeRuntime.IsMobilePerformanceMode)));
            }
            _pendingChunks.Sort((left, right) =>
            {
                var ready = left.ReadyAt.CompareTo(right.ReadyAt);
                if (ready != 0) return ready;
                var distance = left.Distance.CompareTo(right.Distance);
                if (distance != 0) return distance;
                var x = left.ChunkX.CompareTo(right.ChunkX);
                return x != 0 ? x : left.ChunkZ.CompareTo(right.ChunkZ);
            });
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_ROCK_OUTCROPS_WINDOW center={centerX}:{centerZ} queued={_pendingChunks.Count}");
        }

        private void ContinueStagedGeneration()
        {
            // One due chunk per frame mirrors React's staged background work and avoids
            // a terrain-sampling spike when the player crosses a chunk boundary.
            if (_pendingChunks.Count == 0 || _pendingChunks[0].ReadyAt > Time.unscaledTime) return;
            var pending = _pendingChunks[0];
            _pendingChunks.RemoveAt(0);
            var key = $"{pending.ChunkX}:{pending.ChunkZ}:{pending.Distance}";
            if (!_recordCache.TryGetValue(key, out var records))
            {
                records = WofSurvivalRockOutcropRules.MakeChunk(
                    pending.ChunkX,
                    pending.ChunkZ,
                    pending.Distance);
                _recordCache[key] = records;
            }
            foreach (var record in records)
            {
                var batchIndex = record.PaletteIndex * 2 + (record.Spire ? 1 : 0);
                _batches[batchIndex].Add(record.Matrix);
                RockCount++;
                if (record.Spire) SpireCount++;
                else BoulderCount++;
            }
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_ROCK_OUTCROPS_CHUNK_READY chunk={pending.ChunkX}:{pending.ChunkZ} distance={pending.Distance} rocks={records.Length}");
        }

        private void DrawBatches()
        {
            if (!SystemInfo.supportsInstancing) return;
            foreach (var batch in _batches) batch.Draw(gameObject.layer);
        }

        private void TryReportProbeReady()
        {
            if (!_probeViewPrepared || _probePositioned || _centerX != -1 || _centerZ != -1) return;
            if (_pendingChunks.Exists(chunk => chunk.ChunkX == -1 && chunk.ChunkZ == -1 && chunk.Distance == 0))
                return;
            var rocks = WofSurvivalRockOutcropRules.MakeChunk(-1, -1, 0);
            if (rocks.Length < 2 || RockCount < rocks.Length) return;
            var target = rocks[1];
            _probePositioned = true;
            Debug.Log($"[WOF-AUTOMATION] ROCK_OUTCROP_PROBE_POSITIONED key={target.Key} chunk=-1:-1 rocks={rocks.Length} boulders={BoulderCount} spires={SpireCount} position={target.Position} scale={target.Scale:F6}");
            Debug.Log($"[WOF-AUTOMATION] ROCK_OUTCROP_PROBE_PASS key={target.Key} palette={target.PaletteIndex} spire={target.Spire}");
        }

        private void ClearRuntimeState()
        {
            _pendingChunks.Clear();
            foreach (var batch in _batches) if (batch != null) batch.Count = 0;
            RockCount = 0;
            BoulderCount = 0;
            SpireCount = 0;
            _centerX = int.MinValue;
            _centerZ = int.MinValue;
        }

        private static Material MakeMaterial(string name, Color32 color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = name, color = color, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }

        private static Mesh CreateConeMesh(int segments, float height, string name)
        {
            var vertices = new List<Vector3>(segments + 2) { Vector3.up * (height * 0.5f), Vector3.down * (height * 0.5f) };
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Sin(angle), -height * 0.5f, Mathf.Cos(angle)));
            }
            var triangles = new List<int>(segments * 6);
            for (var index = 0; index < segments; index++)
            {
                var current = 2 + index;
                var next = 2 + (index + 1) % segments;
                triangles.Add(0); triangles.Add(current); triangles.Add(next);
                triangles.Add(1); triangles.Add(next); triangles.Add(current);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static (Mesh Solid, Mesh Edges) CreateDodecaMeshes()
        {
            var phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var inverse = 1f / phi;
            var vertices = new List<Vector3>(20);
            foreach (var x in new[] { -1f, 1f }) foreach (var y in new[] { -1f, 1f }) foreach (var z in new[] { -1f, 1f })
                vertices.Add(new Vector3(x, y, z).normalized);
            foreach (var y in new[] { -inverse, inverse }) foreach (var z in new[] { -phi, phi }) vertices.Add(new Vector3(0f, y, z).normalized);
            foreach (var x in new[] { -inverse, inverse }) foreach (var y in new[] { -phi, phi }) vertices.Add(new Vector3(x, y, 0f).normalized);
            foreach (var x in new[] { -phi, phi }) foreach (var z in new[] { -inverse, inverse }) vertices.Add(new Vector3(x, 0f, z).normalized);
            var faces = new List<int[]>();
            for (var a = 0; a < vertices.Count - 2; a++) for (var b = a + 1; b < vertices.Count - 1; b++) for (var c = b + 1; c < vertices.Count; c++)
            {
                var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (normal.sqrMagnitude < 0.000001f) continue;
                normal.Normalize();
                var positive = false; var negative = false;
                foreach (var point in vertices) { var side = Vector3.Dot(normal, point - vertices[a]); if (side > 0.0001f) positive = true; if (side < -0.0001f) negative = true; }
                if (positive && negative) continue;
                if (positive) normal = -normal;
                var distance = Vector3.Dot(normal, vertices[a]);
                if (distance < 0f) { normal = -normal; distance = -distance; }
                var duplicate = false;
                foreach (var face in faces) { var center = Vector3.zero; foreach (var i in face) center += vertices[i]; center /= face.Length; if (Mathf.Abs(Vector3.Dot(normal, center) - distance) < 0.001f) { duplicate = true; break; } }
                if (duplicate) continue;
                var indices = new List<int>();
                for (var i = 0; i < vertices.Count; i++) if (Mathf.Abs(Vector3.Dot(normal, vertices[i]) - distance) < 0.001f) indices.Add(i);
                if (indices.Count != 5) continue;
                var centerPoint = Vector3.zero; foreach (var i in indices) centerPoint += vertices[i]; centerPoint /= indices.Count;
                var axis = (vertices[indices[0]] - centerPoint).normalized;
                var tangent = Vector3.Cross(normal, axis).normalized;
                indices.Sort((left, right) => Mathf.Atan2(Vector3.Dot(vertices[left] - centerPoint, tangent), Vector3.Dot(vertices[left] - centerPoint, axis)).CompareTo(Mathf.Atan2(Vector3.Dot(vertices[right] - centerPoint, tangent), Vector3.Dot(vertices[right] - centerPoint, axis))));
                if (Vector3.Dot(Vector3.Cross(vertices[indices[1]] - vertices[indices[0]], vertices[indices[2]] - vertices[indices[0]]), normal) < 0f) indices.Reverse();
                faces.Add(indices.ToArray());
            }
            if (faces.Count != 12) throw new InvalidOperationException($"Expected 12 dodecahedron faces, generated {faces.Count}.");
            var solidVertices = new List<Vector3>(); var triangles = new List<int>(); var edges = new HashSet<(int, int)>();
            foreach (var face in faces)
            {
                var start = solidVertices.Count; foreach (var index in face) solidVertices.Add(vertices[index]);
                for (var index = 1; index < 4; index++) { triangles.Add(start); triangles.Add(start + index); triangles.Add(start + index + 1); }
                for (var index = 0; index < 5; index++) { var left = face[index]; var right = face[(index + 1) % 5]; edges.Add(left < right ? (left, right) : (right, left)); }
            }
            var solid = new Mesh { name = "ReactSurvivalRockDodeca" }; solid.SetVertices(solidVertices); solid.SetTriangles(triangles, 0); solid.RecalculateNormals(); solid.RecalculateBounds();
            var edge = new Mesh { name = "ReactSurvivalRockDodecaEdges" }; edge.SetVertices(vertices); var edgeIndices = new List<int>(); foreach (var pair in edges) { edgeIndices.Add(pair.Item1); edgeIndices.Add(pair.Item2); } edge.SetIndices(edgeIndices, MeshTopology.Lines, 0); edge.RecalculateBounds();
            return (solid, edge);
        }

        private readonly struct PendingChunk
        {
            public PendingChunk(int chunkX, int chunkZ, int distance, float readyAt)
            {
                ChunkX = chunkX;
                ChunkZ = chunkZ;
                Distance = distance;
                ReadyAt = readyAt;
            }

            public int ChunkX { get; }
            public int ChunkZ { get; }
            public int Distance { get; }
            public float ReadyAt { get; }
        }

        private sealed class RockBatch
        {
            private readonly Matrix4x4[] _matrices = new Matrix4x4[64];

            public RockBatch(Mesh mesh, Material material)
            {
                Mesh = mesh;
                Material = material;
            }

            private Mesh Mesh { get; }
            private Material Material { get; }
            public int Count { get; set; }

            public void Add(Matrix4x4 matrix)
            {
                if (Count >= _matrices.Length) return;
                _matrices[Count++] = matrix;
            }

            public void Draw(int layer)
            {
                if (Count <= 0 || Mesh == null || Material == null) return;
                Graphics.DrawMeshInstanced(
                    Mesh,
                    0,
                    Material,
                    _matrices,
                    Count,
                    null,
                    ShadowCastingMode.Off,
                    false,
                    layer,
                    null,
                    LightProbeUsage.Off);
            }

            public void Dispose()
            {
                if (Material != null) UnityEngine.Object.Destroy(Material);
            }
        }
    }
}
