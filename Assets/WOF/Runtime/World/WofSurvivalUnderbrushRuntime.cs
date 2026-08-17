using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalUnderbrushRuntime : MonoBehaviour
    {
        private const string ProbeArgument = "--wof-underbrush-probe";
        private readonly Dictionary<string, WofSurvivalUnderbrushChunk> _recordCache = new(StringComparer.Ordinal);
        private readonly Dictionary<long, ActiveChunk> _activeChunks = new();
        private readonly List<PendingChunk> _pendingChunks = new();
        private readonly UnderbrushBatch[] _bushBatches = new UnderbrushBatch[18];
        private readonly UnderbrushBatch[] _fernBatches = new UnderbrushBatch[18];
        private WofPlayerController _player;
        private Mesh _bushMesh;
        private Mesh _fernMesh;
        private float _nextResolveAt;
        private int _centerX = int.MinValue;
        private int _centerZ = int.MinValue;
        private bool _mobile;
        private bool _grassInspectionView;
        private bool _probe;
        private bool _probeFern;
        private bool _probeViewPrepared;
        private bool _probeReported;

        public int BushClusterCount { get; private set; }
        public int BushLobeCount { get; private set; }
        public int FernCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            if (FindFirstObjectByType<WofSurvivalUnderbrushRuntime>() != null) return;
            new GameObject("ReactSurvivalUnderbrushRuntime").AddComponent<WofSurvivalUnderbrushRuntime>();
        }

        private void Awake()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals(ProbeArgument, StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith(ProbeArgument + "=", StringComparison.OrdinalIgnoreCase))
                {
                    _probe = true;
                    _probeFern = argument.EndsWith("=fern", StringComparison.OrdinalIgnoreCase);
                }
                if (argument.Equals("--wof-grass-view-probe", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("--wof-grass-view-probe=", StringComparison.OrdinalIgnoreCase))
                    _grassInspectionView = true;
            }
            _mobile = WofPerformanceModeRuntime.IsMobilePerformanceMode;
            _bushMesh = CreateDodecaMesh(0.5f, "ReactSurvivalUnderbrushLobe");
            _fernMesh = CreateQuadMesh("ReactSurvivalUnderbrushFern");
            for (var biomeIndex = 0; biomeIndex < 6; biomeIndex++)
            for (var colorIndex = 0; colorIndex < 3; colorIndex++)
            {
                var biome = (WofSurvivalBiome)biomeIndex;
                var batchIndex = biomeIndex * 3 + colorIndex;
                _bushBatches[batchIndex] = new UnderbrushBatch(
                    _bushMesh,
                    MakeBushMaterial(biome, colorIndex));
                _fernBatches[batchIndex] = new UnderbrushBatch(
                    _fernMesh,
                    MakeFernMaterial(biome, colorIndex));
            }
        }

        private void OnDestroy()
        {
            foreach (var batch in _bushBatches) batch?.Dispose();
            foreach (var batch in _fernBatches) batch?.Dispose();
            Destroy(_bushMesh);
            Destroy(_fernMesh);
        }

        private void Update()
        {
            var survival = WofBootstrap.Instance != null && WofBootstrap.Instance.IsSurvivalSession;
            if (!survival || _grassInspectionView)
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
            var chunk = WofSurvivalUnderbrushRules.MakeChunk(-1, -1, 0, _mobile);
            if (chunk.BushLobes.Length == 0 || chunk.Ferns.Length == 0) return;
            var viewPosition = _probeFern
                ? chunk.Ferns[0].Position + new Vector3(0f, chunk.Ferns[0].Scale.y * 0.26f, -7f)
                : chunk.BushLobes[0].Position + new Vector3(0f, chunk.BushLobes[0].Scale.y * 0.14f, -16f);
            if (!_player.PrepareForAutomationStaticViewProbe(viewPosition, 0f, -7f)) return;
            var camera = _player.GetComponentInChildren<Camera>(true);
            if (camera != null) camera.farClipPlane = 2500f;
            _probeViewPrepared = true;
        }

        private void RebuildWindow(int centerX, int centerZ)
        {
            _centerX = centerX;
            _centerZ = centerZ;
            var previousPending = new Dictionary<long, PendingChunk>();
            foreach (var pending in _pendingChunks)
                previousPending[MakeCoordinateKey(pending.ChunkX, pending.ChunkZ)] = pending;
            _pendingChunks.Clear();
            var desiredCoordinates = new HashSet<long>();
            var retained = 0;
            for (var dz = -WofSurvivalTerrainMath.NearRadius; dz <= WofSurvivalTerrainMath.NearRadius; dz++)
            for (var dx = -WofSurvivalTerrainMath.NearRadius; dx <= WofSurvivalTerrainMath.NearRadius; dx++)
            {
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dz));
                var chunkX = centerX + dx;
                var chunkZ = centerZ + dz;
                if (!WofSurvivalUnderbrushRules.ShouldGenerateChunk(
                        true, false, chunkX, chunkZ, distance)) continue;
                var coordinateKey = MakeCoordinateKey(chunkX, chunkZ);
                desiredCoordinates.Add(coordinateKey);
                if (_activeChunks.ContainsKey(coordinateKey))
                {
                    // The React tree-load stage does not reset when an existing
                    // chunk's near/mid distance changes. Recompute density at its
                    // new LOD immediately while preserving its ready state.
                    _activeChunks[coordinateKey] = new ActiveChunk(
                        chunkX, chunkZ, distance, GetOrCreateChunk(chunkX, chunkZ, distance));
                    retained++;
                    continue;
                }
                if (previousPending.TryGetValue(coordinateKey, out var retainedPending))
                {
                    // The source timer depends on chunk coordinates but not later
                    // distance changes, so retain its original ready time too.
                    _pendingChunks.Add(new PendingChunk(
                        chunkX, chunkZ, distance, retainedPending.ReadyAt));
                    continue;
                }
                _pendingChunks.Add(new PendingChunk(
                    chunkX,
                    chunkZ,
                    distance,
                    Time.unscaledTime + WofSurvivalUnderbrushRules.GetReadyDelaySeconds(
                        chunkX, chunkZ, distance, _mobile)));
            }
            var activeCoordinates = new List<long>(_activeChunks.Keys);
            foreach (var coordinateKey in activeCoordinates)
                if (!desiredCoordinates.Contains(coordinateKey)) _activeChunks.Remove(coordinateKey);
            RebuildActiveBatches();
            _pendingChunks.Sort((left, right) =>
            {
                var ready = left.ReadyAt.CompareTo(right.ReadyAt);
                if (ready != 0) return ready;
                var distance = left.Distance.CompareTo(right.Distance);
                if (distance != 0) return distance;
                var x = left.ChunkX.CompareTo(right.ChunkX);
                return x != 0 ? x : left.ChunkZ.CompareTo(right.ChunkZ);
            });
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_UNDERBRUSH_WINDOW center={centerX}:{centerZ} retained={retained} active={_activeChunks.Count} queued={_pendingChunks.Count} mobile={_mobile}");
        }

        private void ContinueStagedGeneration()
        {
            // React defers underbrush until tree-load stage two. Applying at most one
            // due chunk per frame prevents a center-window terrain sampling spike.
            if (_pendingChunks.Count == 0 || _pendingChunks[0].ReadyAt > Time.unscaledTime) return;
            var pending = _pendingChunks[0];
            _pendingChunks.RemoveAt(0);
            var chunk = GetOrCreateChunk(pending.ChunkX, pending.ChunkZ, pending.Distance);
            _activeChunks[MakeCoordinateKey(pending.ChunkX, pending.ChunkZ)] = new ActiveChunk(
                pending.ChunkX, pending.ChunkZ, pending.Distance, chunk);
            var biome = WofSurvivalTerrainMath.GetBiome(pending.ChunkX, pending.ChunkZ);
            AddChunkToBatches(chunk, biome);
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_UNDERBRUSH_CHUNK_READY chunk={pending.ChunkX}:{pending.ChunkZ} distance={pending.Distance} bushes={chunk.BushClusterCount} lobes={chunk.BushLobes.Length} ferns={chunk.Ferns.Length}");
        }

        private WofSurvivalUnderbrushChunk GetOrCreateChunk(int chunkX, int chunkZ, int distance)
        {
            var key = $"{chunkX}:{chunkZ}:{distance}:{_mobile}";
            if (_recordCache.TryGetValue(key, out var chunk)) return chunk;
            chunk = WofSurvivalUnderbrushRules.MakeChunk(chunkX, chunkZ, distance, _mobile);
            _recordCache.Add(key, chunk);
            return chunk;
        }

        private void RebuildActiveBatches()
        {
            foreach (var batch in _bushBatches) batch.Count = 0;
            foreach (var batch in _fernBatches) batch.Count = 0;
            BushClusterCount = 0;
            BushLobeCount = 0;
            FernCount = 0;
            foreach (var active in _activeChunks.Values)
                AddChunkToBatches(active.Chunk, WofSurvivalTerrainMath.GetBiome(active.ChunkX, active.ChunkZ));
        }

        private void AddChunkToBatches(WofSurvivalUnderbrushChunk chunk, WofSurvivalBiome biome)
        {
            var biomeOffset = (int)biome * 3;
            foreach (var lobe in chunk.BushLobes)
                _bushBatches[biomeOffset + lobe.ColorIndex].Add(lobe.Matrix);
            foreach (var fern in chunk.Ferns)
                _fernBatches[biomeOffset + fern.ColorIndex].Add(fern.Matrix);
            BushClusterCount += chunk.BushClusterCount;
            BushLobeCount += chunk.BushLobes.Length;
            FernCount += chunk.Ferns.Length;
        }

        private static long MakeCoordinateKey(int chunkX, int chunkZ)
        {
            return ((long)chunkX << 32) | (uint)chunkZ;
        }

        private void DrawBatches()
        {
            if (!SystemInfo.supportsInstancing) return;
            // React leaves underbrush visible to the live map rather than tagging
            // it with HIDE_FROM_MINIMAP like grass blades and decorative lobes.
            foreach (var batch in _bushBatches) batch.Draw(gameObject.layer);
            foreach (var batch in _fernBatches) batch.Draw(gameObject.layer);
        }

        private void TryReportProbeReady()
        {
            if (!_probeViewPrepared || _probeReported || _centerX != -1 || _centerZ != -1) return;
            if (_pendingChunks.Exists(chunk => chunk.ChunkX == -1 && chunk.ChunkZ == -1 && chunk.Distance == 0))
                return;
            var target = WofSurvivalUnderbrushRules.MakeChunk(-1, -1, 0, _mobile);
            if (target.BushLobes.Length == 0 || BushLobeCount < target.BushLobes.Length) return;
            _probeReported = true;
            var first = target.BushLobes[0];
            var variant = _probeFern ? "fern" : "bush";
            var position = _probeFern ? target.Ferns[0].Position : first.Position;
            var source = _probeFern ? target.Ferns[0].SourceIndex.ToString() : $"{first.SourceIndex}:{first.LobeIndex}";
            var palette = _probeFern ? target.Ferns[0].ColorIndex : first.ColorIndex;
            Debug.Log($"[WOF-AUTOMATION] UNDERBRUSH_PROBE_POSITIONED variant={variant} chunk=-1:-1 bushes={target.BushClusterCount} lobes={target.BushLobes.Length} ferns={target.Ferns.Length} position={position}");
            Debug.Log($"[WOF-AUTOMATION] UNDERBRUSH_PROBE_PASS variant={variant} source={source} palette={palette} mobile={_mobile}");
        }

        private void ClearRuntimeState()
        {
            _pendingChunks.Clear();
            _activeChunks.Clear();
            foreach (var batch in _bushBatches) if (batch != null) batch.Count = 0;
            foreach (var batch in _fernBatches) if (batch != null) batch.Count = 0;
            BushClusterCount = 0;
            BushLobeCount = 0;
            FernCount = 0;
            _centerX = int.MinValue;
            _centerZ = int.MinValue;
        }

        private static Material MakeBushMaterial(WofSurvivalBiome biome, int colorIndex)
        {
            var fill = WofSurvivalUnderbrushRules.GetBushColor(biome, colorIndex);
            var shader = Resources.Load<Shader>("Shaders/WofUnderbrushFaceted") ??
                         Shader.Find("WOF/Underbrush Faceted");
            if (shader == null)
                throw new InvalidOperationException("Required WOF/Underbrush Faceted shader was not imported.");
            var material = new Material(shader)
            {
                name = $"ReactUnderbrushBush-{biome}-{colorIndex}",
                color = fill,
                enableInstancing = true
            };
            var edge = WofSurvivalUnderbrushRules.GetBushEdgeColor(biome, colorIndex);
            material.SetColor("_BaseColor", fill);
            material.SetColor("_BushLineColor", edge);
            material.SetFloat("_BushLineWidth", 0.044f);
            material.SetFloat("_BushLineOpacity", 0.74f);
            return material;
        }

        private static Material MakeFernMaterial(WofSurvivalBiome biome, int colorIndex)
        {
            var material = MakeUnlitMaterial(
                $"ReactUnderbrushFern-{biome}-{colorIndex}",
                WofSurvivalUnderbrushRules.GetFernColor(biome, colorIndex),
                true);
            return material;
        }

        private static Material MakeUnlitMaterial(string name, Color color, bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = name, color = color, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.SetFloat("_Cull", (float)CullMode.Off);
            if (!transparent) return material;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static Mesh CreateQuadMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateDodecaMesh(float radius, string name)
        {
            var phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var inverse = 1f / phi;
            var vertices = new List<Vector3>(20);
            foreach (var x in new[] { -1f, 1f })
            foreach (var y in new[] { -1f, 1f })
            foreach (var z in new[] { -1f, 1f })
                vertices.Add(new Vector3(x, y, z).normalized * radius);
            foreach (var y in new[] { -inverse, inverse })
            foreach (var z in new[] { -phi, phi })
                vertices.Add(new Vector3(0f, y, z).normalized * radius);
            foreach (var x in new[] { -inverse, inverse })
            foreach (var y in new[] { -phi, phi })
                vertices.Add(new Vector3(x, y, 0f).normalized * radius);
            foreach (var x in new[] { -phi, phi })
            foreach (var z in new[] { -inverse, inverse })
                vertices.Add(new Vector3(x, 0f, z).normalized * radius);
            var faces = new List<int[]>();
            for (var a = 0; a < vertices.Count - 2; a++)
            for (var b = a + 1; b < vertices.Count - 1; b++)
            for (var c = b + 1; c < vertices.Count; c++)
            {
                var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (normal.sqrMagnitude < 0.000001f) continue;
                normal.Normalize();
                var positive = false;
                var negative = false;
                foreach (var point in vertices)
                {
                    var side = Vector3.Dot(normal, point - vertices[a]);
                    if (side > 0.0001f) positive = true;
                    if (side < -0.0001f) negative = true;
                }
                if (positive && negative) continue;
                if (positive) normal = -normal;
                var distance = Vector3.Dot(normal, vertices[a]);
                if (distance < 0f) { normal = -normal; distance = -distance; }
                var duplicate = false;
                foreach (var face in faces)
                {
                    var center = Vector3.zero;
                    foreach (var index in face) center += vertices[index];
                    center /= face.Length;
                    if (Mathf.Abs(Vector3.Dot(normal, center) - distance) >= 0.001f) continue;
                    duplicate = true;
                    break;
                }
                if (duplicate) continue;
                var indices = new List<int>();
                for (var index = 0; index < vertices.Count; index++)
                    if (Mathf.Abs(Vector3.Dot(normal, vertices[index]) - distance) < 0.001f)
                        indices.Add(index);
                if (indices.Count != 5) continue;
                var centerPoint = Vector3.zero;
                foreach (var index in indices) centerPoint += vertices[index];
                centerPoint /= indices.Count;
                var axis = (vertices[indices[0]] - centerPoint).normalized;
                var tangent = Vector3.Cross(normal, axis).normalized;
                indices.Sort((left, right) => Mathf.Atan2(
                    Vector3.Dot(vertices[left] - centerPoint, tangent),
                    Vector3.Dot(vertices[left] - centerPoint, axis)).CompareTo(Mathf.Atan2(
                    Vector3.Dot(vertices[right] - centerPoint, tangent),
                    Vector3.Dot(vertices[right] - centerPoint, axis))));
                if (Vector3.Dot(Vector3.Cross(
                        vertices[indices[1]] - vertices[indices[0]],
                        vertices[indices[2]] - vertices[indices[0]]), normal) < 0f)
                    indices.Reverse();
                faces.Add(indices.ToArray());
            }
            if (faces.Count != 12)
                throw new InvalidOperationException($"Expected 12 dodecahedron faces, generated {faces.Count}.");
            var solidVertices = new List<Vector3>();
            var barycentric = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var face in faces)
            {
                for (var index = 1; index < 4; index++)
                {
                    var start = solidVertices.Count;
                    solidVertices.Add(vertices[face[0]]);
                    solidVertices.Add(vertices[face[index]]);
                    solidVertices.Add(vertices[face[index + 1]]);
                    barycentric.Add(new Vector3(1f, 0f, 0f));
                    barycentric.Add(new Vector3(0f, 1f, 0f));
                    barycentric.Add(new Vector3(0f, 0f, 1f));
                    triangles.Add(start);
                    triangles.Add(start + 1);
                    triangles.Add(start + 2);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(solidVertices);
            mesh.SetUVs(1, barycentric);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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

        private readonly struct ActiveChunk
        {
            public ActiveChunk(int chunkX, int chunkZ, int distance, WofSurvivalUnderbrushChunk chunk)
            {
                ChunkX = chunkX;
                ChunkZ = chunkZ;
                Distance = distance;
                Chunk = chunk;
            }
            public int ChunkX { get; }
            public int ChunkZ { get; }
            public int Distance { get; }
            public WofSurvivalUnderbrushChunk Chunk { get; }
        }

        private sealed class UnderbrushBatch
        {
            private const int Capacity = 1023;
            private readonly Matrix4x4[] _matrices = new Matrix4x4[Capacity];

            public UnderbrushBatch(Mesh mesh, Material material)
            {
                Mesh = mesh;
                Material = material;
            }
            private Mesh Mesh { get; }
            private Material Material { get; }
            public int Count { get; set; }
            public void Add(Matrix4x4 matrix)
            {
                if (Count >= Capacity) return;
                _matrices[Count++] = matrix;
            }
            public void Draw(int layer)
            {
                if (Count == 0 || Mesh == null || Material == null) return;
                Graphics.DrawMeshInstanced(
                    Mesh, 0, Material, _matrices, Count, null,
                    ShadowCastingMode.Off, false, layer, null, LightProbeUsage.Off);
            }
            public void Dispose()
            {
                if (Material != null) UnityEngine.Object.Destroy(Material);
            }
        }
    }
}
