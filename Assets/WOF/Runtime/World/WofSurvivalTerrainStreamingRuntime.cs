using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalTerrainStreamingRuntime : MonoBehaviour
    {
        public const int MaxConcurrentChunkBuilds = 1;
        internal const float ProbeWarmupSeconds = 3f;
        private const double StreamRounding = 0.45d;
        private const string ProbePrefix = "--wof-survival-streaming-probe=";
        private const int InstancesPerTreeBatch = 1023;

        [SerializeField] private Material terrainMaterial;
        [SerializeField] private WofSurvivalFoliageRuntime foliageRuntime;
        [SerializeField] private Material waterMaterial;

        private static readonly ChunkOffset[] OrderedOffsets = MakeOrderedOffsets();
        private readonly Dictionary<string, RuntimeChunk> _activeChunks = new();
        private readonly Dictionary<string, ChunkSpec> _targetChunks = new();
        private readonly List<ChunkSpec> _buildQueue = new();
        private readonly List<string> _removalKeys = new();
        private Task<ChunkBuildPayload> _buildTask;
        private ChunkSpec _buildingSpec;
        private Transform _viewer;
        private WofPlayerController _localPlayer;
        private float _nextViewerResolveAt;
        private bool _hasCenter;
        private int _centerX;
        private int _centerZ;
        private int _readyCenterX = int.MinValue;
        private int _readyCenterZ = int.MinValue;
        private bool _probeRequested;
        private bool _probePositioned;
        private int _probeChunkX;
        private int _probeChunkZ;
        private bool _measureWindowFrames;
        private int _windowFrameCount;
        private float _windowFrameTotalMilliseconds;
        private float _windowMaxFrameMilliseconds;
        private double _windowMaxWorkerMilliseconds;
        private double _windowMaxApplyMilliseconds;
        private double _windowMaxStreamingUpdateMilliseconds;
        private Mesh[] _treeMeshes;
        private Material _treeMaterial;
        private bool _probeWarmupComplete;

        public void Configure(
            Material exactTerrainMaterial,
            WofSurvivalFoliageRuntime exactFoliageRuntime,
            Material exactWaterMaterial)
        {
            terrainMaterial = exactTerrainMaterial;
            foliageRuntime = exactFoliageRuntime;
            waterMaterial = exactWaterMaterial;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            InstallIfNeeded();
        }

        internal static WofSurvivalTerrainStreamingRuntime InstallIfNeeded()
        {
            var existing = FindFirstObjectByType<WofSurvivalTerrainStreamingRuntime>();
            if (existing != null) return existing;
            var baseTerrain = GameObject.Find("ReactSurvivalOpenWorldBaseRegion");
            var material = baseTerrain?.GetComponent<MeshRenderer>()?.sharedMaterial;
            if (baseTerrain == null || material == null)
            {
                Debug.LogError("[WOF-AUTOMATION] SURVIVAL_STREAMING_FAILED reason=missing-base-terrain");
                return null;
            }

            var runtimeObject = new GameObject("ReactSurvivalTerrainStreamingRuntime");
            runtimeObject.SetActive(false);
            runtimeObject.transform.SetParent(baseTerrain.transform.parent, false);
            var runtime = runtimeObject.AddComponent<WofSurvivalTerrainStreamingRuntime>();
            var foliage = FindFirstObjectByType<WofSurvivalFoliageRuntime>();
            var fallbackWaterMaterial = Resources.Load<Material>("SurvivalStreamWater");
            if (fallbackWaterMaterial == null)
            {
                var waterShader = Shader.Find("WOF/Survival Stream Water");
                fallbackWaterMaterial = waterShader == null ? null : new Material(waterShader)
                {
                    name = "SurvivalStreamWater_RuntimeFallback"
                };
            }
            runtime.Configure(material, foliage, fallbackWaterMaterial);
            runtimeObject.SetActive(true);
            return runtime;
        }

        private void Awake()
        {
            ParseProbeArguments();
            if (foliageRuntime == null) foliageRuntime = FindFirstObjectByType<WofSurvivalFoliageRuntime>();
            if (waterMaterial == null) waterMaterial = Resources.Load<Material>("SurvivalStreamWater");
            if (terrainMaterial == null || foliageRuntime == null || waterMaterial == null ||
                !foliageRuntime.TryGetStreamingAssets(out _treeMeshes, out _treeMaterial))
            {
                Debug.LogError(
                    $"[WOF-AUTOMATION] SURVIVAL_STREAMING_FAILED terrain={terrainMaterial != null} " +
                    $"foliage={foliageRuntime != null} water={waterMaterial != null} " +
                    $"treeMeshes={_treeMeshes?.Length ?? 0} treeMaterial={_treeMaterial != null}");
                enabled = false;
                return;
            }
            _treeMaterial.enableInstancing = true;
            if (WofDesertVillageFoundationRuntime.InstallIfNeeded(transform.parent, terrainMaterial) == null)
            {
                Debug.LogError("[WOF-AUTOMATION] SURVIVAL_STREAMING_FAILED reason=desert-foundation");
                enabled = false;
                return;
            }
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_STREAMING_RUNTIME_READY radius={WofSurvivalTerrainMath.RenderRadius} collisionRadius={WofSurvivalTerrainMath.CollisionRadius} offsets={OrderedOffsets.Length}");
        }

        private void OnDestroy()
        {
            foreach (var pair in _activeChunks) DestroyRuntimeChunk(pair.Value);
            _activeChunks.Clear();
        }

        private void Update()
        {
            var streamingUpdateTimer = Stopwatch.StartNew();
            if (_measureWindowFrames)
            {
                var frameMilliseconds = Time.unscaledDeltaTime * 1000f;
                _windowFrameCount++;
                _windowFrameTotalMilliseconds += frameMilliseconds;
                _windowMaxFrameMilliseconds = Mathf.Max(_windowMaxFrameMilliseconds, frameMilliseconds);
            }
            ResolveViewer();
            if (_viewer == null) return;

            if (!_hasCenter)
            {
                _centerX = WofSurvivalTerrainMath.GetChunkCoordinate(_viewer.position.x);
                _centerZ = WofSurvivalTerrainMath.GetChunkCoordinate(_viewer.position.z);
                _hasCenter = true;
                ReconcileWindow(buildCenterImmediately: true);
            }
            else
            {
                if (_probeRequested && !_probeWarmupComplete && Time.unscaledTime >= ProbeWarmupSeconds)
                {
                    _probeWarmupComplete = true;
                    Debug.Log($"[WOF-AUTOMATION] SURVIVAL_STREAMING_PROBE_WARMUP_COMPLETE seconds={ProbeWarmupSeconds:F1}");
                }
                // A streaming probe begins before the player can safely be placed on the
                // requested chunk. Keep that requested window authoritative until its
                // center terrain exists; otherwise the still-origin player immediately
                // recenters the worker back to 0:0 and cancels the actual stress test.
                var nextX = _probeRequested && _probeWarmupComplete && !_probePositioned
                    ? _probeChunkX
                    : WofSurvivalTerrainMath.RecenterCoordinate(_centerX, _viewer.position.x);
                var nextZ = _probeRequested && _probeWarmupComplete && !_probePositioned
                    ? _probeChunkZ
                    : WofSurvivalTerrainMath.RecenterCoordinate(_centerZ, _viewer.position.z);
                if (nextX != _centerX || nextZ != _centerZ)
                {
                    _centerX = nextX;
                    _centerZ = nextZ;
                    ReconcileWindow(buildCenterImmediately: true);
                }
            }

            ContinueBuildQueue();
            TryPositionProbe();
            DrawStreamingTrees();
            streamingUpdateTimer.Stop();
            if (_measureWindowFrames)
                _windowMaxStreamingUpdateMilliseconds = Math.Max(
                    _windowMaxStreamingUpdateMilliseconds,
                    streamingUpdateTimer.Elapsed.TotalMilliseconds);
            ReportReadyWindow();
        }

        private void DrawStreamingTrees()
        {
            if (!CanDrawStreamingTrees(SystemInfo.supportsInstancing, _activeChunks.Count) ||
                _viewer == null || _treeMaterial == null) return;
            const float visibleRadius = 820f;
            var radiusSquared = visibleRadius * visibleRadius;
            var viewerPosition = _viewer.position;
            foreach (var chunk in _activeChunks.Values)
            foreach (var batch in chunk.TreeBatches)
            {
                var dx = batch.Center.x - viewerPosition.x;
                var dz = batch.Center.z - viewerPosition.z;
                if (dx * dx + dz * dz > radiusSquared) continue;
                Graphics.DrawMeshInstanced(
                    batch.Mesh,
                    0,
                    _treeMaterial,
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

        internal static bool CanDrawStreamingTrees(bool supportsInstancing, int activeChunkCount)
        {
            return supportsInstancing && activeChunkCount > 0;
        }

        private void ResolveViewer()
        {
            if (_viewer != null && _localPlayer != null && _localPlayer.IsSpawned && _localPlayer.IsOwner) return;
            if (Time.unscaledTime < _nextViewerResolveAt) return;
            _nextViewerResolveAt = Time.unscaledTime + 0.25f;
            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (playerObject == null) return;
            var player = playerObject.GetComponent<WofPlayerController>();
            if (player == null || !player.IsSpawned || !player.IsOwner) return;
            _viewer = playerObject.transform;
            _localPlayer = player;
        }

        private void ReconcileWindow(bool buildCenterImmediately)
        {
            _targetChunks.Clear();
            _buildQueue.Clear();
            _readyCenterX = int.MinValue;
            _readyCenterZ = int.MinValue;
            _measureWindowFrames = true;
            _windowFrameCount = 0;
            _windowFrameTotalMilliseconds = 0f;
            _windowMaxFrameMilliseconds = 0f;
            _windowMaxWorkerMilliseconds = 0d;
            _windowMaxApplyMilliseconds = 0d;
            _windowMaxStreamingUpdateMilliseconds = 0d;

            if (WofSurvivalTerrainMath.IsLilyRealmCenter(_centerX, _centerZ))
            {
                ClearActiveChunks();
                Debug.Log($"[WOF-AUTOMATION] SURVIVAL_STREAMING_SUPPRESSED realm=lily-coil center={_centerX}:{_centerZ}");
                return;
            }

            foreach (var offset in OrderedOffsets)
            {
                var cx = _centerX + offset.X;
                var cz = _centerZ + offset.Z;
                if (WofSurvivalTerrainMath.IsInsideBakedAtlas(cx, cz) || WofSurvivalTerrainMath.IsAuthoredChunk(cx, cz))
                    continue;
                var spec = new ChunkSpec(cx, cz, offset.Distance,
                    WofSurvivalTerrainMath.GetRenderSegments(offset.Distance),
                    WofSurvivalTerrainMath.GetCollisionSegments(offset.Distance), 0);
                _targetChunks[spec.Key] = spec;
            }

            var targetKeys = new List<string>(_targetChunks.Keys);
            foreach (var key in targetKeys)
            {
                var spec = _targetChunks[key];
                var edgeMask = GetSkirtEdgeMask(spec);
                _targetChunks[key] = spec.WithEdgeMask(edgeMask);
            }

            _removalKeys.Clear();
            foreach (var pair in _activeChunks)
            {
                // Keep an old LOD alive until its asynchronous replacement is ready.
                // This prevents holes and falling during a recenter.
                if (!_targetChunks.ContainsKey(pair.Key))
                    _removalKeys.Add(pair.Key);
            }
            foreach (var key in _removalKeys)
            {
                DestroyRuntimeChunk(_activeChunks[key]);
                _activeChunks.Remove(key);
            }

            foreach (var offset in OrderedOffsets)
            {
                var key = ChunkSpec.MakeKey(_centerX + offset.X, _centerZ + offset.Z);
                if (_targetChunks.TryGetValue(key, out var target) && !_activeChunks.ContainsKey(key))
                    _buildQueue.Add(target);
            }

            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_STREAMING_RECENTERED center={_centerX}:{_centerZ} active={_activeChunks.Count} queued={_buildQueue.Count} worker={MaxConcurrentChunkBuilds}");
        }

        private byte GetSkirtEdgeMask(ChunkSpec spec)
        {
            byte result = 0;
            if (ShouldRenderSkirt(spec, spec.X, spec.Z - 1)) result |= 1;
            if (ShouldRenderSkirt(spec, spec.X + 1, spec.Z)) result |= 2;
            if (ShouldRenderSkirt(spec, spec.X, spec.Z + 1)) result |= 4;
            if (ShouldRenderSkirt(spec, spec.X - 1, spec.Z)) result |= 8;
            return result;
        }

        private bool ShouldRenderSkirt(ChunkSpec spec, int neighborX, int neighborZ)
        {
            var key = ChunkSpec.MakeKey(neighborX, neighborZ);
            return !_targetChunks.TryGetValue(key, out var neighbor) || neighbor.RenderSegments != spec.RenderSegments;
        }

        private void ContinueBuildQueue()
        {
            if (_buildTask != null)
            {
                if (!_buildTask.IsCompleted) return;
                if (_buildTask.IsFaulted)
                {
                    Debug.LogError($"[WOF-AUTOMATION] SURVIVAL_STREAMING_CHUNK_FAILED chunk={_buildingSpec.Key} error={_buildTask.Exception?.GetBaseException().Message}");
                }
                else if (_buildTask.IsCompletedSuccessfully)
                {
                    ActivateBuild(_buildTask.Result);
                }
                _buildTask = null;
            }

            while (_buildTask == null && _buildQueue.Count > 0)
            {
                var spec = _buildQueue[0];
                _buildQueue.RemoveAt(0);
                if (_activeChunks.TryGetValue(spec.Key, out var active) && active.Spec.Equals(spec)) continue;
                _buildingSpec = spec;
                _buildTask = Task.Run(() => GenerateChunkBuildPayload(spec));
            }
        }

        private void ActivateBuild(ChunkBuildPayload payload)
        {
            var spec = payload.Spec;
            if (!_targetChunks.TryGetValue(spec.Key, out var target) || !target.Equals(spec)) return;

            var applyTimer = Stopwatch.StartNew();
            var root = new GameObject($"ReactSurvivalTerrainChunk_{spec.X}_{spec.Z}");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(spec.X * WofSurvivalTerrainMath.BlockSize, 0f,
                spec.Z * WofSurvivalTerrainMath.BlockSize);

            var renderMesh = CreateMesh(payload.Render);
            root.AddComponent<MeshFilter>().sharedMesh = renderMesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = terrainMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Mesh collisionMesh = null;
            if (spec.CollisionSegments > 0)
            {
                collisionMesh = spec.CollisionSegments == spec.RenderSegments
                    ? renderMesh
                    : CreateMesh(payload.Collision);
                root.AddComponent<MeshCollider>().sharedMesh = collisionMesh;
            }

            Mesh skirtMesh = null;
            GameObject skirtObject = null;
            if (spec.EdgeMask != 0)
            {
                skirtMesh = CreateMesh(payload.Skirt);
                skirtObject = new GameObject($"ReactSurvivalTerrainSkirt_{spec.X}_{spec.Z}");
                skirtObject.transform.SetParent(root.transform, false);
                skirtObject.AddComponent<MeshFilter>().sharedMesh = skirtMesh;
                var skirtRenderer = skirtObject.AddComponent<MeshRenderer>();
                skirtRenderer.sharedMaterial = terrainMaterial;
                skirtRenderer.shadowCastingMode = ShadowCastingMode.Off;
                skirtRenderer.receiveShadows = false;
            }

            Mesh waterMesh = null;
            GameObject waterObject = null;
            if (payload.Decorations.Water != null)
            {
                var water = payload.Decorations.Water;
                waterMesh = CreateMesh(new MeshBuildData(
                    $"ReactSurvivalRuntimeWater_{spec.X}_{spec.Z}_{spec.Distance}",
                    water.Vertices,
                    water.Colors,
                    null,
                    water.Indices,
                    MakeUpNormals(water.Vertices.Length)));
                waterObject = new GameObject($"ReactSurvivalWater_{spec.X}_{spec.Z}");
                waterObject.transform.SetParent(root.transform, false);
                waterObject.AddComponent<MeshFilter>().sharedMesh = waterMesh;
                var waterRenderer = waterObject.AddComponent<MeshRenderer>();
                waterRenderer.sharedMaterial = waterMaterial;
                waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
                waterRenderer.receiveShadows = false;
            }

            var treeBatches = BuildTreeBatches(payload.Decorations.Trees, _treeMeshes);

            if (_activeChunks.TryGetValue(spec.Key, out var previous))
            {
                _activeChunks.Remove(spec.Key);
                DestroyRuntimeChunk(previous);
            }
            _activeChunks.Add(spec.Key, new RuntimeChunk(
                spec,
                root,
                renderMesh,
                collisionMesh,
                skirtMesh,
                skirtObject,
                waterMesh,
                waterObject,
                treeBatches));
            root.SetActive(true);
            applyTimer.Stop();
            _windowMaxWorkerMilliseconds = Math.Max(_windowMaxWorkerMilliseconds, payload.WorkerMilliseconds);
            _windowMaxApplyMilliseconds = Math.Max(
                _windowMaxApplyMilliseconds,
                applyTimer.Elapsed.TotalMilliseconds);
            Debug.Log(
                $"[WOF-AUTOMATION] SURVIVAL_STREAMING_CHUNK_READY chunk={spec.Key} distance={spec.Distance} " +
                $"trees={payload.Decorations.Trees.Length} waterVertices={payload.Decorations.Water?.Vertices.Length ?? 0} " +
                $"workerMs={payload.WorkerMilliseconds:F2} applyMs={applyTimer.Elapsed.TotalMilliseconds:F2}");
        }

        private static StreamingTreeBatch[] BuildTreeBatches(
            WofSurvivalStreamTreePlacement[] placements,
            Mesh[] meshes)
        {
            if (placements == null || placements.Length == 0) return Array.Empty<StreamingTreeBatch>();
            var batches = new List<StreamingTreeBatch>();
            var currentByMesh = new Dictionary<int, StreamingTreeBatch>();
            foreach (var placement in placements)
            {
                if (placement.MeshIndex < 0 || placement.MeshIndex >= meshes.Length ||
                    meshes[placement.MeshIndex] == null)
                    continue;
                if (!currentByMesh.TryGetValue(placement.MeshIndex, out var batch) ||
                    batch.Count >= InstancesPerTreeBatch)
                {
                    batch = new StreamingTreeBatch(meshes[placement.MeshIndex], placement.Position);
                    currentByMesh[placement.MeshIndex] = batch;
                    batches.Add(batch);
                }
                batch.Add(Matrix4x4.TRS(
                    placement.Position,
                    Quaternion.Euler(
                        placement.RotationRadians.x * Mathf.Rad2Deg,
                        placement.RotationRadians.y * Mathf.Rad2Deg,
                        placement.RotationRadians.z * Mathf.Rad2Deg),
                    placement.Scale));
            }
            return batches.ToArray();
        }

        private static Vector3[] MakeUpNormals(int count)
        {
            var result = new Vector3[count];
            for (var index = 0; index < result.Length; index++) result[index] = Vector3.up;
            return result;
        }

        internal static Mesh BuildTerrainMeshForTests(int cx, int cz, int distance, bool collision)
        {
            var segments = collision
                ? WofSurvivalTerrainMath.GetCollisionSegments(distance)
                : WofSurvivalTerrainMath.GetRenderSegments(distance);
            if (segments <= 0) return null;
            return BuildTerrainMesh(cx, cz, segments, !collision);
        }

        private static Mesh BuildTerrainMesh(int cx, int cz, int segments, bool renderSurface)
        {
            return CreateMesh(GenerateTerrainMeshData(cx, cz, segments, renderSurface));
        }

        private static MeshBuildData GenerateTerrainMeshData(int cx, int cz, int segments, bool renderSurface)
        {
            var gridSize = segments + 1;
            var vertices = new Vector3[gridSize * gridSize];
            var colors = renderSurface ? new Color[vertices.Length] : null;
            var uvs = renderSurface ? new Vector2[vertices.Length] : null;
            var indices = new int[segments * segments * 6];
            var step = WofSurvivalTerrainMath.BlockSize / (double)segments;
            var half = WofSurvivalTerrainMath.BlockSize * 0.5d;
            var cursor = 0;
            for (var zIndex = 0; zIndex <= segments; zIndex++)
            {
                var localZ = -half + zIndex * step;
                for (var xIndex = 0; xIndex <= segments; xIndex++)
                {
                    var localX = -half + xIndex * step;
                    var height = WofSurvivalTerrainMath.GetTerrainHeight(cx, cz, localX, localZ);
                    var renderX = renderSurface && localX <= -half ? localX - WofSurvivalTerrainMath.EdgeOverlap :
                        renderSurface && localX >= half ? localX + WofSurvivalTerrainMath.EdgeOverlap : localX;
                    var renderZ = renderSurface && localZ <= -half ? localZ - WofSurvivalTerrainMath.EdgeOverlap :
                        renderSurface && localZ >= half ? localZ + WofSurvivalTerrainMath.EdgeOverlap : localZ;
                    vertices[cursor] = new Vector3((float)renderX, (float)height, (float)renderZ);
                    if (renderSurface)
                    {
                        var worldX = cx * (double)WofSurvivalTerrainMath.BlockSize + localX;
                        var worldZ = cz * (double)WofSurvivalTerrainMath.BlockSize + localZ;
                        colors[cursor] = WofSurvivalTerrainMath.GetRenderedTerrainColor(worldX, worldZ, height);
                        uvs[cursor] = new Vector2((float)(worldX / WofSurvivalTerrainMath.DetailUvWorldSize),
                            (float)(worldZ / WofSurvivalTerrainMath.DetailUvWorldSize));
                    }
                    cursor++;
                }
            }
            cursor = 0;
            for (var zIndex = 0; zIndex < segments; zIndex++)
            for (var xIndex = 0; xIndex < segments; xIndex++)
            {
                var a = zIndex * gridSize + xIndex;
                var b = a + 1;
                var c = a + gridSize;
                var d = c + 1;
                indices[cursor++] = a;
                indices[cursor++] = c;
                indices[cursor++] = b;
                indices[cursor++] = b;
                indices[cursor++] = c;
                indices[cursor++] = d;
            }
            return new MeshBuildData(
                $"ReactSurvivalRuntimeTerrain_{cx}_{cz}_{segments}_{(renderSurface ? "render" : "collision")}",
                vertices,
                colors,
                uvs,
                indices,
                CalculateNormals(vertices, indices));
        }

        private static Mesh BuildSkirtMesh(ChunkSpec spec)
        {
            return CreateMesh(GenerateSkirtMeshData(spec));
        }

        private static MeshBuildData GenerateSkirtMeshData(ChunkSpec spec)
        {
            var edgeCount = CountBits(spec.EdgeMask);
            var segments = spec.RenderSegments;
            var vertices = new Vector3[edgeCount * (segments + 1) * 2];
            var colors = new Color[vertices.Length];
            var indices = new int[edgeCount * segments * 6];
            var vertexCursor = 0;
            var indexCursor = 0;
            var half = WofSurvivalTerrainMath.BlockSize * 0.5d;
            var step = WofSurvivalTerrainMath.BlockSize / (double)segments;

            void AddEdge(Func<int, Vector2> getPoint)
            {
                var start = vertexCursor;
                for (var index = 0; index <= segments; index++)
                {
                    var point = getPoint(index);
                    var height = WofSurvivalTerrainMath.GetTerrainHeight(spec.X, spec.Z, point.x, point.y);
                    var color = WofSurvivalTerrainMath.GetRenderedTerrainColor(
                        spec.X * (double)WofSurvivalTerrainMath.BlockSize + point.x,
                        spec.Z * (double)WofSurvivalTerrainMath.BlockSize + point.y, height);
                    var top = height - WofSurvivalTerrainMath.SkirtTopInset;
                    vertices[vertexCursor] = new Vector3(point.x, (float)top, point.y);
                    colors[vertexCursor++] = color;
                    vertices[vertexCursor] = new Vector3(point.x,
                        (float)(top - WofSurvivalTerrainMath.SkirtDepth), point.y);
                    colors[vertexCursor++] = new Color(color.r * 0.98f, color.g * 0.98f, color.b * 0.98f, 1f);
                }
                for (var index = 0; index < segments; index++)
                {
                    var topA = start + index * 2;
                    var bottomA = topA + 1;
                    var topB = topA + 2;
                    var bottomB = topA + 3;
                    indices[indexCursor++] = topA;
                    indices[indexCursor++] = topB;
                    indices[indexCursor++] = bottomA;
                    indices[indexCursor++] = topB;
                    indices[indexCursor++] = bottomB;
                    indices[indexCursor++] = bottomA;
                }
            }

            if ((spec.EdgeMask & 1) != 0) AddEdge(index => new Vector2((float)(-half + index * step), (float)-half));
            if ((spec.EdgeMask & 2) != 0) AddEdge(index => new Vector2((float)half, (float)(-half + index * step)));
            if ((spec.EdgeMask & 4) != 0) AddEdge(index => new Vector2((float)(half - index * step), (float)half));
            if ((spec.EdgeMask & 8) != 0) AddEdge(index => new Vector2((float)-half, (float)(half - index * step)));

            return new MeshBuildData(
                $"ReactSurvivalRuntimeTerrainSkirt_{spec.X}_{spec.Z}_{segments}_{spec.EdgeMask}",
                vertices,
                colors,
                null,
                indices,
                null);
        }

        private static ChunkBuildPayload GenerateChunkBuildPayload(ChunkSpec spec)
        {
            var timer = Stopwatch.StartNew();
            var render = GenerateTerrainMeshData(spec.X, spec.Z, spec.RenderSegments, true);
            MeshBuildData collision = null;
            if (spec.CollisionSegments > 0 && spec.CollisionSegments != spec.RenderSegments)
                collision = GenerateTerrainMeshData(spec.X, spec.Z, spec.CollisionSegments, false);
            var skirt = spec.EdgeMask == 0 ? null : GenerateSkirtMeshData(spec);
            var decorations = WofSurvivalStreamDecorationMath.Generate(spec.X, spec.Z, spec.Distance);
            timer.Stop();
            return new ChunkBuildPayload(
                spec,
                render,
                collision,
                skirt,
                decorations,
                timer.Elapsed.TotalMilliseconds);
        }

        private static Mesh CreateMesh(MeshBuildData data)
        {
            if (data == null) return null;
            var mesh = new Mesh
            {
                name = data.Name,
                indexFormat = data.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.vertices = data.Vertices;
            mesh.triangles = data.Indices;
            if (data.Colors != null) mesh.colors = data.Colors;
            if (data.Uvs != null) mesh.uv = data.Uvs;
            if (data.Normals != null) mesh.normals = data.Normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3[] CalculateNormals(Vector3[] vertices, int[] indices)
        {
            var normals = new Vector3[vertices.Length];
            for (var index = 0; index < indices.Length; index += 3)
            {
                var first = indices[index];
                var second = indices[index + 1];
                var third = indices[index + 2];
                var normal = Vector3.Cross(vertices[second] - vertices[first], vertices[third] - vertices[first]);
                normals[first] += normal;
                normals[second] += normal;
                normals[third] += normal;
            }
            for (var index = 0; index < normals.Length; index++)
            {
                var lengthSquared = normals[index].sqrMagnitude;
                normals[index] = lengthSquared > 0.00000001f
                    ? normals[index] / (float)Math.Sqrt(lengthSquared)
                    : Vector3.up;
            }
            return normals;
        }

        private void TryPositionProbe()
        {
            if (!_probeRequested || !_probeWarmupComplete || _probePositioned ||
                _localPlayer == null || !_localPlayer.IsSpawned ||
                !_localPlayer.IsOwner) return;
            var centerKey = ChunkSpec.MakeKey(_probeChunkX, _probeChunkZ);
            var bakedCenterReady = WofSurvivalTerrainMath.IsInsideBakedAtlas(_probeChunkX, _probeChunkZ) &&
                                   !WofSurvivalTerrainMath.IsAuthoredChunk(_probeChunkX, _probeChunkZ);
            if (!_activeChunks.ContainsKey(centerKey) && !bakedCenterReady) return;
            const double localX = 0d;
            const double localZ = 96d;
            var height = WofSurvivalTerrainMath.GetTerrainHeight(_probeChunkX, _probeChunkZ, localX, localZ);
            var position = new Vector3(_probeChunkX * WofSurvivalTerrainMath.BlockSize + (float)localX,
                (float)height + 3.2f,
                _probeChunkZ * WofSurvivalTerrainMath.BlockSize + (float)localZ);
            if (!_localPlayer.PrepareForAutomationStaticViewProbe(position, 0f, -8f)) return;
            _probePositioned = true;
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_STREAMING_PROBE_POSITIONED chunk={_probeChunkX}:{_probeChunkZ} position={position.x:F2},{position.y:F2},{position.z:F2}");
        }

        private void ReportReadyWindow()
        {
            if (_buildQueue.Count != 0 || _buildTask != null ||
                _readyCenterX == _centerX && _readyCenterZ == _centerZ) return;
            _readyCenterX = _centerX;
            _readyCenterZ = _centerZ;
            var colliders = 0;
            var vertices = 0;
            var trees = 0;
            var waterVertices = 0;
            foreach (var chunk in _activeChunks.Values)
            {
                if (chunk.Spec.CollisionSegments > 0) colliders++;
                vertices += chunk.RenderMesh.vertexCount;
                foreach (var batch in chunk.TreeBatches) trees += batch.Count;
                waterVertices += chunk.WaterMesh?.vertexCount ?? 0;
            }
            var averageFrameMilliseconds = _windowFrameCount > 0
                ? _windowFrameTotalMilliseconds / _windowFrameCount
                : 0f;
            _measureWindowFrames = false;
            Debug.Log(
                $"[WOF-AUTOMATION] SURVIVAL_STREAM_WINDOW_READY center={_centerX}:{_centerZ} " +
                $"dynamicChunks={_activeChunks.Count} colliders={colliders} vertices={vertices} " +
                $"trees={trees} waterVertices={waterVertices} frames={_windowFrameCount} " +
                $"avgFrameMs={averageFrameMilliseconds:F2} maxFrameMs={_windowMaxFrameMilliseconds:F2} " +
                $"maxWorkerMs={_windowMaxWorkerMilliseconds:F2} maxApplyMs={_windowMaxApplyMilliseconds:F2} " +
                $"maxStreamingUpdateMs={_windowMaxStreamingUpdateMilliseconds:F2}");
        }

        private void ParseProbeArguments()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith(ProbePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                var value = argument.Substring(ProbePrefix.Length);
                var split = value.Split(',');
                if (split.Length != 2 || !int.TryParse(split[0], out _probeChunkX) ||
                    !int.TryParse(split[1], out _probeChunkZ))
                {
                    Debug.LogError($"[WOF-AUTOMATION] SURVIVAL_STREAMING_PROBE_INVALID value={value}");
                    continue;
                }
                _probeRequested = true;
            }
        }

        private void ClearActiveChunks()
        {
            foreach (var pair in _activeChunks) DestroyRuntimeChunk(pair.Value);
            _activeChunks.Clear();
            _buildQueue.Clear();
            _targetChunks.Clear();
        }

        private static void DestroyRuntimeChunk(RuntimeChunk chunk)
        {
            if (chunk.Root != null) Destroy(chunk.Root);
            if (chunk.RenderMesh != null) Destroy(chunk.RenderMesh);
            if (chunk.CollisionMesh != null && chunk.CollisionMesh != chunk.RenderMesh) Destroy(chunk.CollisionMesh);
            if (chunk.SkirtMesh != null) Destroy(chunk.SkirtMesh);
            if (chunk.WaterMesh != null) Destroy(chunk.WaterMesh);
        }

        private static int CountBits(byte value)
        {
            var count = 0;
            for (; value != 0; value >>= 1) count += value & 1;
            return count;
        }

        private static ChunkOffset[] MakeOrderedOffsets()
        {
            var list = new List<ChunkOffset>();
            var roundedRadius = WofSurvivalTerrainMath.RenderRadius + StreamRounding;
            var radiusSq = roundedRadius * roundedRadius;
            for (var dz = -WofSurvivalTerrainMath.RenderRadius; dz <= WofSurvivalTerrainMath.RenderRadius; dz++)
            for (var dx = -WofSurvivalTerrainMath.RenderRadius; dx <= WofSurvivalTerrainMath.RenderRadius; dx++)
            {
                if (dx * dx + dz * dz > radiusSq) continue;
                var candidate = new ChunkOffset(dx, dz, Math.Max(Math.Abs(dx), Math.Abs(dz)));
                var insert = list.Count;
                while (insert > 0 && Compare(candidate, list[insert - 1]) < 0) insert--;
                list.Insert(insert, candidate);
            }
            return list.ToArray();
        }

        private static int Compare(ChunkOffset a, ChunkOffset b)
        {
            var distance = a.Distance.CompareTo(b.Distance);
            if (distance != 0) return distance;
            var x = a.X.CompareTo(b.X);
            return x != 0 ? x : a.Z.CompareTo(b.Z);
        }

        internal static (int x, int z, int distance)[] GetOrderedOffsetsForTests()
        {
            var result = new (int x, int z, int distance)[OrderedOffsets.Length];
            for (var index = 0; index < result.Length; index++)
                result[index] = (OrderedOffsets[index].X, OrderedOffsets[index].Z, OrderedOffsets[index].Distance);
            return result;
        }

        private sealed class MeshBuildData
        {
            public MeshBuildData(string name, Vector3[] vertices, Color[] colors, Vector2[] uvs, int[] indices,
                Vector3[] normals)
            {
                Name = name;
                Vertices = vertices;
                Colors = colors;
                Uvs = uvs;
                Indices = indices;
                Normals = normals;
            }

            public string Name { get; }
            public Vector3[] Vertices { get; }
            public Color[] Colors { get; }
            public Vector2[] Uvs { get; }
            public int[] Indices { get; }
            public Vector3[] Normals { get; }
        }

        private sealed class ChunkBuildPayload
        {
            public ChunkBuildPayload(ChunkSpec spec, MeshBuildData render, MeshBuildData collision,
                MeshBuildData skirt, WofSurvivalStreamDecorationData decorations, double workerMilliseconds)
            {
                Spec = spec;
                Render = render;
                Collision = collision;
                Skirt = skirt;
                Decorations = decorations;
                WorkerMilliseconds = workerMilliseconds;
            }

            public ChunkSpec Spec { get; }
            public MeshBuildData Render { get; }
            public MeshBuildData Collision { get; }
            public MeshBuildData Skirt { get; }
            public WofSurvivalStreamDecorationData Decorations { get; }
            public double WorkerMilliseconds { get; }
        }

        private readonly struct ChunkOffset
        {
            public ChunkOffset(int x, int z, int distance) { X = x; Z = z; Distance = distance; }
            public int X { get; }
            public int Z { get; }
            public int Distance { get; }
        }

        private readonly struct ChunkSpec : IEquatable<ChunkSpec>
        {
            public ChunkSpec(int x, int z, int distance, int renderSegments, int collisionSegments, byte edgeMask)
            {
                X = x; Z = z; Distance = distance; RenderSegments = renderSegments;
                CollisionSegments = collisionSegments; EdgeMask = edgeMask;
            }
            public int X { get; }
            public int Z { get; }
            public int Distance { get; }
            public int RenderSegments { get; }
            public int CollisionSegments { get; }
            public byte EdgeMask { get; }
            public string Key => MakeKey(X, Z);
            public static string MakeKey(int x, int z) => $"{x}:{z}";
            public ChunkSpec WithEdgeMask(byte edgeMask) => new(X, Z, Distance, RenderSegments, CollisionSegments, edgeMask);
            public bool Equals(ChunkSpec other) => X == other.X && Z == other.Z && Distance == other.Distance &&
                RenderSegments == other.RenderSegments && CollisionSegments == other.CollisionSegments && EdgeMask == other.EdgeMask;
            public override bool Equals(object obj) => obj is ChunkSpec other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(X, Z, Distance, RenderSegments, CollisionSegments, EdgeMask);
        }

        private sealed class RuntimeChunk
        {
            public RuntimeChunk(ChunkSpec spec, GameObject root, Mesh renderMesh, Mesh collisionMesh,
                Mesh skirtMesh, GameObject skirtObject, Mesh waterMesh, GameObject waterObject,
                StreamingTreeBatch[] treeBatches)
            {
                Spec = spec; Root = root; RenderMesh = renderMesh; CollisionMesh = collisionMesh;
                SkirtMesh = skirtMesh; SkirtObject = skirtObject;
                WaterMesh = waterMesh; WaterObject = waterObject; TreeBatches = treeBatches;
            }
            public ChunkSpec Spec { get; }
            public GameObject Root { get; }
            public Mesh RenderMesh { get; }
            public Mesh CollisionMesh { get; }
            public Mesh SkirtMesh { get; }
            public GameObject SkirtObject { get; }
            public Mesh WaterMesh { get; }
            public GameObject WaterObject { get; }
            public StreamingTreeBatch[] TreeBatches { get; }
        }

        private sealed class StreamingTreeBatch
        {
            private readonly Vector4[] _instanceColors = new Vector4[InstancesPerTreeBatch];

            public StreamingTreeBatch(Mesh mesh, Vector3 center)
            {
                Mesh = mesh;
                Center = center;
                for (var index = 0; index < _instanceColors.Length; index++)
                    _instanceColors[index] = Vector4.one;
                Properties.SetVectorArray("_InstanceColor", _instanceColors);
            }

            public Mesh Mesh { get; }
            public Vector3 Center { get; }
            public Matrix4x4[] Matrices { get; } = new Matrix4x4[InstancesPerTreeBatch];
            public MaterialPropertyBlock Properties { get; } = new();
            public int Count { get; private set; }

            public void Add(Matrix4x4 matrix)
            {
                if (Count >= Matrices.Length) return;
                Matrices[Count++] = matrix;
            }
        }
    }
}
