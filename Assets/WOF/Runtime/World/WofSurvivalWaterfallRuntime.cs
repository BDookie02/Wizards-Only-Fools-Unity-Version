using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalWaterfallRuntime : MonoBehaviour
    {
        private const string ProbeArgument = "--wof-waterfall-probe";
        private readonly List<Mesh> _ownedMeshes = new();
        private WofPlayerController _player;
        private Transform _contentRoot;
        private Mesh _planeMesh;
        private Mesh _circleMesh;
        private Material _fallMaterial;
        private Material _highlightMaterial;
        private Material _poolMaterial;
        private float _nextResolveAt;
        private int _centerX = int.MinValue;
        private int _centerZ = int.MinValue;
        private bool _probe;
        private bool _probeViewPrepared;
        private bool _probeReported;

        public int WaterfallCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            if (FindFirstObjectByType<WofSurvivalWaterfallRuntime>() != null) return;
            new GameObject("ReactSurvivalWaterfallRuntime")
                .AddComponent<WofSurvivalWaterfallRuntime>();
        }

        private void Awake()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
                if (argument.Equals(ProbeArgument, StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith(ProbeArgument + "=", StringComparison.OrdinalIgnoreCase))
                    _probe = true;

            _planeMesh = Own(CreatePlaneMesh());
            _circleMesh = Own(CreateCircleMesh(18));
            _fallMaterial = CreateTransparentMaterial(
                "ReactSurvivalWaterfallMain",
                new Color(0x8d / 255f, 0xe8 / 255f, 1f, 0.42f),
                true);
            _highlightMaterial = CreateTransparentMaterial(
                "ReactSurvivalWaterfallHighlight",
                new Color(0xe8 / 255f, 0xfd / 255f, 1f, 0.22f),
                true);
            _poolMaterial = CreateTransparentMaterial(
                "ReactSurvivalWaterfallPool",
                new Color(0x5f / 255f, 0xc0 / 255f, 0xd5 / 255f, 0.62f),
                false);
        }

        private void OnDestroy()
        {
            ClearContent();
            foreach (var mesh in _ownedMeshes) Destroy(mesh);
            Destroy(_fallMaterial);
            Destroy(_highlightMaterial);
            Destroy(_poolMaterial);
        }

        private void Update()
        {
            var survival = WofBootstrap.Instance != null && WofBootstrap.Instance.IsSurvivalSession;
            if (!WofSurvivalWaterfallRules.ShouldShowRuntime(survival))
            {
                ClearContent();
                _centerX = int.MinValue;
                _centerZ = int.MinValue;
                return;
            }

            ResolvePlayer();
            if (_player == null) return;
            if (_probe && !_probeViewPrepared) PrepareProbeView();
            var centerX = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.x);
            var centerZ = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.z);
            if (centerX != _centerX || centerZ != _centerZ) RebuildCurrentChunk(centerX, centerZ);
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

        private void RebuildCurrentChunk(int centerX, int centerZ)
        {
            ClearContent();
            _centerX = centerX;
            _centerZ = centerZ;
            var records = WofSurvivalWaterfallRules.MakeChunk(centerX, centerZ, 0);
            if (records.Length == 0)
            {
                Debug.Log($"[WOF-AUTOMATION] SURVIVAL_WATERFALL_WINDOW center={centerX}:{centerZ} waterfalls=0");
                return;
            }

            _contentRoot = new GameObject($"ReactSurvivalWaterfalls-{centerX}-{centerZ}").transform;
            _contentRoot.SetParent(transform, false);
            foreach (var record in records) BuildWaterfall(_contentRoot, record);
            WaterfallCount = records.Length;
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_WATERFALL_WINDOW center={centerX}:{centerZ} waterfalls={WaterfallCount}");
        }

        private void BuildWaterfall(Transform parent, WofSurvivalWaterfallRecord record)
        {
            var root = new GameObject($"survival-waterfall-{record.Key}").transform;
            root.SetParent(parent, false);
            AddMesh(
                root,
                "WaterfallMain",
                _planeMesh,
                _fallMaterial,
                record.Position,
                new Vector3(0f, record.YawRadians * Mathf.Rad2Deg, 0f),
                new Vector3(record.Width, record.Height, 1f),
                -1);
            AddMesh(
                root,
                "WaterfallHighlight",
                _planeMesh,
                _highlightMaterial,
                record.Position + Vector3.up * (record.Height * 0.08f),
                new Vector3(0f, record.YawRadians * Mathf.Rad2Deg, 0f),
                new Vector3(record.Width * 0.34f, record.Height * 0.96f, 1f),
                0);
            AddMesh(
                root,
                "WaterfallPool",
                _circleMesh,
                _poolMaterial,
                record.PoolPosition,
                new Vector3(-90f, 0f, 0f),
                new Vector3(record.PoolScale * 1.35f, record.PoolScale, 1f),
                -2);
        }

        private static void AddMesh(
            Transform parent,
            string itemName,
            Mesh mesh,
            Material material,
            Vector3 position,
            Vector3 eulerDegrees,
            Vector3 scale,
            int sortingOrder)
        {
            var item = new GameObject(itemName);
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.transform.rotation = Quaternion.Euler(eulerDegrees);
            item.transform.localScale = scale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void PrepareProbeView()
        {
            const int chunkX = -3;
            const int chunkZ = -2;
            var records = WofSurvivalWaterfallRules.MakeChunk(chunkX, chunkZ, 0);
            if (records.Length == 0) return;
            var waterfall = records[0];
            var dropDirection = new Vector3(
                Mathf.Cos(waterfall.YawRadians),
                0f,
                Mathf.Sin(waterfall.YawRadians));
            var viewPosition = waterfall.PoolPosition + dropDirection * 24f;
            viewPosition.y = waterfall.PoolPosition.y + waterfall.Height * 0.68f;
            var target = new Vector3(
                waterfall.Position.x,
                waterfall.PoolPosition.y + waterfall.Height * 0.42f,
                waterfall.Position.z);
            var direction = target - viewPosition;
            var horizontal = new Vector2(direction.x, direction.z).magnitude;
            var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var pitch = Mathf.Atan2(-direction.y, horizontal) * Mathf.Rad2Deg;
            if (!_player.PrepareForAutomationStaticViewProbe(viewPosition, yaw, pitch)) return;
            var camera = _player.GetComponentInChildren<Camera>(true);
            if (camera != null) camera.farClipPlane = 2500f;
            _probeViewPrepared = true;
        }

        private void TryReportProbeReady()
        {
            if (!_probe || !_probeViewPrepared || _probeReported || _centerX != -3 || _centerZ != -2 ||
                WaterfallCount == 0)
                return;
            _probeReported = true;
            Debug.Log($"[WOF-AUTOMATION] WATERFALL_PROBE_READY chunk=-3:-2 waterfalls={WaterfallCount}");
        }

        private void ClearContent()
        {
            if (_contentRoot != null) Destroy(_contentRoot.gameObject);
            _contentRoot = null;
            WaterfallCount = 0;
        }

        private Mesh Own(Mesh mesh)
        {
            _ownedMeshes.Add(mesh);
            return mesh;
        }

        private static Material CreateTransparentMaterial(string materialName, Color color, bool doubleSided)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                renderQueue = (int)RenderQueue.Transparent,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (doubleSided && material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }

        private static Mesh CreatePlaneMesh()
        {
            var mesh = new Mesh { name = "ReactSurvivalWaterfallPlane" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateCircleMesh(int segments)
        {
            var mesh = new Mesh { name = $"ReactSurvivalWaterfallCircle{segments}" };
            var vertices = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                var x = Mathf.Cos(angle);
                var y = Mathf.Sin(angle);
                vertices[index + 1] = new Vector3(x, y, 0f);
                uvs[index + 1] = new Vector2(x * 0.5f + 0.5f, y * 0.5f + 0.5f);
                triangles[index * 3] = 0;
                triangles[index * 3 + 1] = index + 1;
                triangles[index * 3 + 2] = (index + 1) % segments + 1;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
