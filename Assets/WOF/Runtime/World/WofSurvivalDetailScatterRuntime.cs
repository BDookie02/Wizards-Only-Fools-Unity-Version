using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalDetailScatterRuntime : MonoBehaviour
    {
        private const string ProbeArgument = "--wof-detail-scatter-probe";
        private readonly Dictionary<long, ChunkStage> _visibleStages = new();
        private readonly Dictionary<string, Material> _materials = new(StringComparer.Ordinal);
        private readonly List<Mesh> _ownedMeshes = new();
        private WofPlayerController _player;
        private Transform _contentRoot;
        private Mesh _branchMesh;
        private Mesh _rodMesh;
        private Mesh _planeMesh;
        private Mesh _defaultTrunkMesh;
        private Mesh _jungleTrunkMesh;
        private Mesh _swampTrunkMesh;
        private Mesh _mushroomTrunkMesh;
        private Mesh _mushroomSmallTrunkMesh;
        private Mesh _dodecaMesh;
        private Mesh _dodecaWireMesh;
        private float _nextResolveAt;
        private int _centerX = int.MinValue;
        private int _centerZ = int.MinValue;
        private int _contentX = int.MinValue;
        private int _contentZ = int.MinValue;
        private bool _mobile;
        private bool _grassInspectionView;
        private bool _probe;
        private string _probeKind = "plains";
        private bool _probeViewPrepared;
        private bool _probeReported;

        public int TreeCount { get; private set; }
        public int TumbleweedCount { get; private set; }
        public int SuppressedSourceCactusCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            if (FindFirstObjectByType<WofSurvivalDetailScatterRuntime>() != null) return;
            new GameObject("ReactSurvivalDetailScatterRuntime").AddComponent<WofSurvivalDetailScatterRuntime>();
        }

        private void Awake()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals(ProbeArgument, StringComparison.OrdinalIgnoreCase))
                    _probe = true;
                else if (argument.StartsWith(ProbeArgument + "=", StringComparison.OrdinalIgnoreCase))
                {
                    _probe = true;
                    _probeKind = argument[(ProbeArgument.Length + 1)..].Trim().ToLowerInvariant();
                }
                if (argument.Equals("--wof-grass-view-probe", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("--wof-grass-view-probe=", StringComparison.OrdinalIgnoreCase))
                    _grassInspectionView = true;
            }

            _mobile = WofPerformanceModeRuntime.IsMobilePerformanceMode;
            _branchMesh = Own(CreateFrustumMesh(0.68f, 1f, 1f, 5, "ReactDetailScatterBranch"));
            _rodMesh = Own(CreateFrustumMesh(1f, 1f, 1f, 4, "ReactDetailScatterTumbleweedRod"));
            _planeMesh = Own(CreatePlaneMesh("ReactDetailScatterLeafPlane"));
            _defaultTrunkMesh = Own(CreateFrustumMesh(0.95f, 1.55f, 22.4f, 6, "ReactDetailScatterDefaultTrunk"));
            _jungleTrunkMesh = Own(CreateFrustumMesh(1.35f, 2.45f, 36f, 6, "ReactDetailScatterJungleTrunk"));
            _swampTrunkMesh = Own(CreateFrustumMesh(1.2f, 2.25f, 27f, 6, "ReactDetailScatterSwampTrunk"));
            _mushroomTrunkMesh = Own(CreateFrustumMesh(0.75f, 1.05f, 8f, 6, "ReactDetailScatterMushroomTrunk"));
            _mushroomSmallTrunkMesh = Own(CreateFrustumMesh(0.55f, 0.78f, 5.2f, 6, "ReactDetailScatterMushroomSmallTrunk"));
            (_dodecaMesh, _dodecaWireMesh) = CreateDodecaMeshes();
            Own(_dodecaMesh);
            Own(_dodecaWireMesh);
        }

        private void OnDestroy()
        {
            ClearContent();
            foreach (var material in _materials.Values) Destroy(material);
            foreach (var mesh in _ownedMeshes) Destroy(mesh);
        }

        private void Update()
        {
            var survival = WofBootstrap.Instance != null && WofBootstrap.Instance.IsSurvivalSession;
            if (!WofSurvivalDetailScatterRules.ShouldShowRuntime(survival, _mobile, _grassInspectionView))
            {
                ClearRuntimeState();
                return;
            }

            ResolvePlayer();
            if (_player == null) return;
            if (_probe && !_probeViewPrepared) PrepareProbeView();
            var centerX = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.x);
            var centerZ = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.z);
            if (centerX != _centerX || centerZ != _centerZ) RebuildStageWindow(centerX, centerZ);
            TryBuildCurrentChunk();
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

        private void RebuildStageWindow(int centerX, int centerZ)
        {
            _centerX = centerX;
            _centerZ = centerZ;
            var desired = new HashSet<long>();
            const float roundedRadiusSquared = 3.25f * 3.25f;
            for (var dz = -WofSurvivalTerrainMath.RenderRadius; dz <= WofSurvivalTerrainMath.RenderRadius; dz++)
            for (var dx = -WofSurvivalTerrainMath.RenderRadius; dx <= WofSurvivalTerrainMath.RenderRadius; dx++)
            {
                if (dx * dx + dz * dz > roundedRadiusSquared) continue;
                var chunkX = centerX + dx;
                var chunkZ = centerZ + dz;
                var key = MakeCoordinateKey(chunkX, chunkZ);
                desired.Add(key);
                if (_visibleStages.ContainsKey(key)) continue;
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dz));
                _visibleStages.Add(key, new ChunkStage(
                    chunkX,
                    chunkZ,
                    Time.unscaledTime + WofSurvivalDetailScatterRules.GetReadyDelaySeconds(
                        chunkX, chunkZ, distance)));
            }

            var existingKeys = new List<long>(_visibleStages.Keys);
            foreach (var key in existingKeys)
                if (!desired.Contains(key)) _visibleStages.Remove(key);

            if (_contentX != centerX || _contentZ != centerZ) ClearContent();
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_DETAIL_SCATTER_WINDOW center={centerX}:{centerZ} staged={_visibleStages.Count}");
        }

        private void TryBuildCurrentChunk()
        {
            if (_contentRoot != null && _contentX == _centerX && _contentZ == _centerZ) return;
            if (!_visibleStages.TryGetValue(MakeCoordinateKey(_centerX, _centerZ), out var stage) ||
                stage.ReadyAt > Time.unscaledTime) return;

            var records = WofSurvivalDetailScatterRules.MakeChunk(_centerX, _centerZ);
            var content = new GameObject($"ReactDetailScatter-{_centerX}-{_centerZ}");
            content.transform.SetParent(transform, false);
            _contentRoot = content.transform;
            _contentX = _centerX;
            _contentZ = _centerZ;
            TreeCount = 0;
            TumbleweedCount = 0;
            SuppressedSourceCactusCount = 0;
            foreach (var record in records)
            {
                switch (record.Kind)
                {
                    case WofSurvivalDetailScatterKind.Tree:
                        BuildTree(_contentRoot, record);
                        TreeCount++;
                        break;
                    case WofSurvivalDetailScatterKind.Tumbleweed:
                        BuildTumbleweed(_contentRoot, record);
                        TumbleweedCount++;
                        break;
                    case WofSurvivalDetailScatterKind.Cactus:
                        // The user explicitly replaced React's thin detail cactus with
                        // the thick WofSurvivalDesertCactusRuntime saguaro system.
                        SuppressedSourceCactusCount++;
                        break;
                }
            }
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_DETAIL_SCATTER_READY chunk={_centerX}:{_centerZ} trees={TreeCount} tumbleweeds={TumbleweedCount} sourceCactusSuppressed={SuppressedSourceCactusCount}");
        }

        private void BuildTree(Transform parent, WofSurvivalDetailScatterRecord record)
        {
            var root = new GameObject($"ReactDetailTree-{record.SourceIndex}-{record.Biome}").transform;
            root.SetParent(parent, false);
            root.position = record.Position;
            root.rotation = Quaternion.Euler(0f, record.Variant * 360f, 0f);
            var visualScale = WofSurvivalDetailScatterRules.GetTreeVisualScale(record.Biome, record.Scale);
            var footprintScale = WofSurvivalDetailScatterRules.GetTreeFootprintScale(record.Biome, visualScale);
            root.localScale = new Vector3(footprintScale, visualScale, footprintScale);

            switch (record.Biome)
            {
                case WofSurvivalBiome.Mushroom:
                    AddMesh(root, "MushroomTrunk", _mushroomTrunkMesh, GetMaterial("#e8d5bb", true),
                        new Vector3(0f, 3.6f, 0f), Vector3.zero, Vector3.one);
                    AddDodeca(root, "MushroomCanopy", new Vector3(0f, 8.3f, 0f), 3.1f, "#c865d6", "#271231");
                    AddMesh(root, "MushroomSmallTrunk", _mushroomSmallTrunkMesh, GetMaterial("#dfcab0", true),
                        new Vector3(2.7f, 4.4f, -1.8f), Vector3.zero, Vector3.one * 0.72f);
                    AddDodeca(root, "MushroomSmallCanopy", new Vector3(2.7f, 7.5f, -1.8f), 2.6f,
                        "#eb80f0", "#271231", Vector3.one * 0.72f);
                    break;
                case WofSurvivalBiome.Jungle:
                    BuildJungleTree(root, record.Variant);
                    break;
                case WofSurvivalBiome.Swamp:
                    BuildSwampTree(root, record.Variant);
                    break;
                default:
                    BuildDefaultTree(root, record.Biome);
                    break;
            }
        }

        private void BuildJungleTree(Transform root, float variant)
        {
            const string branchColor = "#3a2418";
            AddMesh(root, "JungleTrunk", _jungleTrunkMesh, GetMaterial(branchColor, false),
                new Vector3(0f, 16f, 0f), new Vector3(0.08f, 0f, 0.05f), Vector3.one);
            AddBranch(root, new Vector3(0f, 19f, 0f), new Vector3(10f, 27f, -4f), 0.52f, branchColor);
            AddBranch(root, new Vector3(0f, 23f, 0f), new Vector3(-12f, 32f, 3f), 0.48f, branchColor);
            AddBranch(root, new Vector3(0f, 26f, 0f), new Vector3(7f, 36f, 8f), 0.42f, branchColor);
            AddDodeca(root, "JungleCanopy0", new Vector3(0f, 36f, 0f), 7.2f,
                variant > 0.5f ? "#1f6b35" : "#23763b", "#244a1c");
            AddDodeca(root, "JungleCanopy1", new Vector3(6.8f, 32.5f, -4.2f), 5.4f,
                variant > 0.5f ? "#32914d" : "#2e8547", "#244a1c");
            AddDodeca(root, "JungleCanopy2", new Vector3(-7.5f, 35.5f, 3.2f), 5.8f, "#2c7b3f", "#244a1c");
            AddDodeca(root, "JungleCanopy3", new Vector3(2.8f, 42f, 5.6f), 5.2f, "#1d5f32", "#244a1c");
            AddHangingVine(root, 9.2f, 27.2f, -3.7f, 13.5f, variant * 5.1f);
            AddHangingVine(root, -10.6f, 32.2f, 3.1f, 16.5f, variant * 4.4f + 1.7f);
            AddHangingVine(root, 5.6f, 36f, 7.6f, 12.2f, variant * 3.8f + 2.4f);
        }

        private void BuildSwampTree(Transform root, float variant)
        {
            AddMesh(root, "SwampTrunk", _swampTrunkMesh, GetMaterial("#3a2a1d", false),
                new Vector3(0f, 12.5f, 0f), new Vector3(0.1f, 0f, -0.07f), Vector3.one);
            AddBranch(root, new Vector3(0f, 14f, 0f), new Vector3(8.4f, 21f, -3.2f), 0.46f, "#3a2a1d");
            AddBranch(root, new Vector3(0f, 17f, 0f), new Vector3(-7.8f, 23.5f, 4.6f), 0.4f, "#3a2a1d");
            AddBranch(root, new Vector3(-0.2f, 4.5f, 0f), new Vector3(-4.8f, 1.1f, -4.5f), 0.36f, "#2d2117");
            AddBranch(root, new Vector3(0.3f, 4.2f, 0f), new Vector3(5.2f, 1f, 3.6f), 0.34f, "#2d2117");
            AddDodeca(root, "SwampCanopy0", new Vector3(0f, 25f, 0f), 5.5f, "#56652b", "#171c0d");
            AddDodeca(root, "SwampCanopy1", new Vector3(5.5f, 22.5f, -2.4f), 4.1f, "#667536", "#171c0d");
            AddDodeca(root, "SwampCanopy2", new Vector3(-4.8f, 24.4f, 3.5f), 4.4f, "#4a5f28", "#171c0d");
            AddHangingVine(root, 7.2f, 21.3f, -2.6f, 12.2f, variant * 5.7f);
            AddHangingVine(root, -6.3f, 23.8f, 4.3f, 13.4f, variant * 4.2f + 1.2f);
        }

        private void BuildDefaultTree(Transform root, WofSurvivalBiome biome)
        {
            AddMesh(root, "DefaultTrunk", _defaultTrunkMesh, GetMaterial("#5b3a20", false),
                new Vector3(0f, 10.4f, 0f), Vector3.zero, Vector3.one);
            AddBranch(root, new Vector3(0f, 10f, 0f), new Vector3(6.4f, 17.5f, -2.4f), 0.38f, "#5b3a20");
            AddBranch(root, new Vector3(0f, 12.2f, 0f), new Vector3(-6.8f, 18.8f, 3.1f), 0.35f, "#5b3a20");
            AddBranch(root, new Vector3(0f, 15.5f, 0f), new Vector3(4.5f, 21.5f, 4.7f), 0.31f, "#5b3a20");
            var accent = biome == WofSurvivalBiome.Tallgrass ? "#64ad39" : "#5fa43a";
            AddDodeca(root, "DefaultCanopy0", new Vector3(0f, 22f, 0f), 5.2f, accent, "#244a1c");
            AddDodeca(root, "DefaultCanopy1", new Vector3(4.8f, 18.5f, -2.2f), 3.9f, "#6aa846", "#244a1c");
            AddDodeca(root, "DefaultCanopy2", new Vector3(-5.2f, 20.2f, 2.6f), 4.2f, "#5d9b3f", "#244a1c");
        }

        private void BuildTumbleweed(Transform parent, WofSurvivalDetailScatterRecord record)
        {
            var root = new GameObject($"ReactTumbleweed-{record.SourceIndex}").transform;
            root.SetParent(parent, false);
            root.position = WofSurvivalDetailScatterRules.GetTumbleweedPosition(record);
            SetThreeEuler(root, WofSurvivalDetailScatterRules.GetTumbleweedRotationRadians(record));
            root.localScale = Vector3.one * WofSurvivalDetailScatterRules.GetTumbleweedScale(record);
            AddMesh(root, "TumbleweedWire", _dodecaWireMesh, GetMaterial("#9b6a35", false),
                Vector3.zero, Vector3.zero, Vector3.one * 1.25f);
            AddMesh(root, "TumbleweedRod0", _rodMesh, GetMaterial("#6f4a22", false),
                Vector3.zero, new Vector3(0.6f, 0.3f, 0.15f), new Vector3(0.045f, 2.6f, 0.045f));
            AddMesh(root, "TumbleweedRod1", _rodMesh, GetMaterial("#7f5629", false),
                Vector3.zero, new Vector3(1.2f, -0.7f, 1.1f), new Vector3(0.04f, 2.35f, 0.04f));
            AddMesh(root, "TumbleweedRod2", _rodMesh, GetMaterial("#8a5d2c", false),
                Vector3.zero, new Vector3(-0.5f, 1.1f, 0.9f), new Vector3(0.035f, 2.2f, 0.035f));
        }

        private void AddBranch(Transform root, Vector3 start, Vector3 end, float radius, string color)
        {
            var direction = end - start;
            var length = Mathf.Max(0.1f, direction.magnitude);
            var branch = AddMesh(root, "Branch", _branchMesh, GetMaterial(color, true),
                (start + end) * 0.5f, Vector3.zero, new Vector3(radius, length, radius));
            branch.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        }

        private void AddHangingVine(Transform root, float x, float y, float z, float length, float sway)
        {
            var start = new Vector3(x, y, z);
            var end = new Vector3(x + Mathf.Sin(sway) * 1.25f, y - length, z + Mathf.Cos(sway) * 1.25f);
            AddBranch(root, start, end, 0.13f, "#1f4f20");
            AddLeaf(root,
                new Vector3(x + Mathf.Sin(sway) * 0.65f, y - length * 0.52f, z + Mathf.Cos(sway) * 0.65f),
                new Vector3(0.25f, sway, 0.65f), 1.2f, 2.6f, "#2f7b35");
            AddLeaf(root,
                new Vector3(x - Mathf.Sin(sway) * 0.55f, y - length * 0.78f, z - Mathf.Cos(sway) * 0.55f),
                new Vector3(-0.18f, sway + 0.9f, -0.5f), 1f, 2.1f, "#3d8f42");
        }

        private void AddLeaf(Transform root, Vector3 position, Vector3 rotation, float width, float height, string color)
        {
            var leafRoot = new GameObject("VineLeaf").transform;
            leafRoot.SetParent(root, false);
            leafRoot.localPosition = position;
            SetThreeEuler(leafRoot, rotation);
            AddMesh(leafRoot, "LeafEdge", _planeMesh, GetMaterial("#3a6330", false, true),
                new Vector3(0f, 0f, -0.01f), Vector3.zero, new Vector3(width * 1.14f, height * 1.1f, 1f));
            AddMesh(leafRoot, "LeafFill", _planeMesh, GetMaterial(color, false, true),
                new Vector3(0f, 0f, 0.01f), Vector3.zero, new Vector3(width, height, 1f));
        }

        private void AddDodeca(
            Transform root,
            string name,
            Vector3 position,
            float radius,
            string fill,
            string edge,
            Vector3? groupScale = null)
        {
            var dodecaRoot = new GameObject(name).transform;
            dodecaRoot.SetParent(root, false);
            dodecaRoot.localPosition = position;
            dodecaRoot.localScale = groupScale ?? Vector3.one;
            AddMesh(dodecaRoot, "Fill", _dodecaMesh, GetMaterial(fill, false),
                Vector3.zero, Vector3.zero, Vector3.one * radius);
            AddMesh(dodecaRoot, "Wire", _dodecaWireMesh, GetMaterial(edge, false, true, 0.48f),
                Vector3.zero, Vector3.zero, Vector3.one * (radius * 1.004f));
        }

        private Transform AddMesh(
            Transform parent,
            string name,
            Mesh mesh,
            Material material,
            Vector3 localPosition,
            Vector3 rotationRadians,
            Vector3 localScale)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            SetThreeEuler(item.transform, rotationRadians);
            item.transform.localScale = localScale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return item.transform;
        }

        private Material GetMaterial(string hex, bool lit, bool doubleSided = false, float alpha = 1f)
        {
            var key = $"{hex}:{lit}:{doubleSided}:{alpha:0.000}";
            if (_materials.TryGetValue(key, out var material)) return material;
            var color = ParseHex(hex);
            color.a = alpha;
            var shader = lit
                ? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")
                : Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader)
            {
                name = $"ReactDetailScatter-{key}",
                color = color,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (doubleSided) material.SetFloat("_Cull", (float)CullMode.Off);
            if (alpha < 0.999f)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_ZWrite", 0f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            _materials.Add(key, material);
            return material;
        }

        private void PrepareProbeView()
        {
            var chunkX = _probeKind == "tumbleweed" ? 4 : _probeKind == "jungle" ? -4 : _probeKind == "swamp" ? 7 : -1;
            var chunkZ = _probeKind == "tumbleweed" ? -3 : _probeKind == "jungle" ? 0 : _probeKind == "swamp" ? 4 : -1;
            var records = WofSurvivalDetailScatterRules.MakeChunk(chunkX, chunkZ);
            var desiredKind = _probeKind == "tumbleweed"
                ? WofSurvivalDetailScatterKind.Tumbleweed
                : WofSurvivalDetailScatterKind.Tree;
            WofSurvivalDetailScatterRecord? target = null;
            foreach (var record in records)
            {
                if (record.Kind != desiredKind) continue;
                target = record;
                if (desiredKind == WofSurvivalDetailScatterKind.Tumbleweed) break;
            }
            if (!target.HasValue) return;
            var recordTarget = target.Value;
            var viewPosition = desiredKind == WofSurvivalDetailScatterKind.Tumbleweed
                ? WofSurvivalDetailScatterRules.GetTumbleweedPosition(recordTarget) + new Vector3(0f, 1.2f, -18f)
                : recordTarget.Position + new Vector3(0f, 140f, -300f);
            if (!_player.PrepareForAutomationStaticViewProbe(viewPosition, 0f,
                    desiredKind == WofSurvivalDetailScatterKind.Tumbleweed ? -4f : -2f)) return;
            var camera = _player.GetComponentInChildren<Camera>(true);
            if (camera != null) camera.farClipPlane = 3200f;
            _probeViewPrepared = true;
        }

        private void TryReportProbeReady()
        {
            if (!_probe || !_probeViewPrepared || _probeReported || _contentRoot == null ||
                _contentX != _centerX || _contentZ != _centerZ) return;
            var hasTarget = _probeKind == "tumbleweed" ? TumbleweedCount > 0 : TreeCount > 0;
            if (!hasTarget) return;
            _probeReported = true;
            Debug.Log($"[WOF-AUTOMATION] DETAIL_SCATTER_PROBE_READY kind={_probeKind} chunk={_centerX}:{_centerZ} trees={TreeCount} tumbleweeds={TumbleweedCount} sourceCactusSuppressed={SuppressedSourceCactusCount}");
        }

        private void ClearContent()
        {
            if (_contentRoot != null) Destroy(_contentRoot.gameObject);
            _contentRoot = null;
            _contentX = int.MinValue;
            _contentZ = int.MinValue;
            TreeCount = 0;
            TumbleweedCount = 0;
            SuppressedSourceCactusCount = 0;
        }

        private void ClearRuntimeState()
        {
            ClearContent();
            _visibleStages.Clear();
            _centerX = int.MinValue;
            _centerZ = int.MinValue;
        }

        private Mesh Own(Mesh mesh)
        {
            _ownedMeshes.Add(mesh);
            return mesh;
        }

        private static void SetThreeEuler(Transform target, Vector3 rotationRadians)
        {
            target.localRotation = WofSurvivalUnderbrushRules.MakeThreeJsMatrix(
                Vector3.zero, rotationRadians, Vector3.one).rotation;
        }

        private static long MakeCoordinateKey(int x, int z) => ((long)x << 32) ^ (uint)z;

        private static Color ParseHex(string value)
        {
            if (ColorUtility.TryParseHtmlString(value, out var color)) return color;
            throw new ArgumentException($"Invalid detail-scatter color {value}.", nameof(value));
        }

        private static Mesh CreatePlaneMesh(string name)
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

        private static Mesh CreateFrustumMesh(
            float topRadius,
            float bottomRadius,
            float height,
            int segments,
            string name)
        {
            var vertices = new List<Vector3>(segments * 2 + 2);
            var triangles = new List<int>(segments * 12);
            var half = height * 0.5f;
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                var x = Mathf.Cos(angle);
                var z = Mathf.Sin(angle);
                vertices.Add(new Vector3(x * bottomRadius, -half, z * bottomRadius));
                vertices.Add(new Vector3(x * topRadius, half, z * topRadius));
            }
            var bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -half, 0f));
            var topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, half, 0f));
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                var bottom = index * 2;
                var top = bottom + 1;
                var nextBottom = next * 2;
                var nextTop = nextBottom + 1;
                triangles.Add(bottom); triangles.Add(top); triangles.Add(nextTop);
                triangles.Add(bottom); triangles.Add(nextTop); triangles.Add(nextBottom);
                triangles.Add(bottomCenter); triangles.Add(nextBottom); triangles.Add(bottom);
                triangles.Add(topCenter); triangles.Add(top); triangles.Add(nextTop);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static (Mesh Solid, Mesh Wire) CreateDodecaMeshes()
        {
            var phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var inverse = 1f / phi;
            var vertices = new List<Vector3>(20);
            foreach (var x in new[] { -1f, 1f })
            foreach (var y in new[] { -1f, 1f })
            foreach (var z in new[] { -1f, 1f }) vertices.Add(new Vector3(x, y, z).normalized);
            foreach (var y in new[] { -inverse, inverse })
            foreach (var z in new[] { -phi, phi }) vertices.Add(new Vector3(0f, y, z).normalized);
            foreach (var x in new[] { -inverse, inverse })
            foreach (var y in new[] { -phi, phi }) vertices.Add(new Vector3(x, y, 0f).normalized);
            foreach (var x in new[] { -phi, phi })
            foreach (var z in new[] { -inverse, inverse }) vertices.Add(new Vector3(x, 0f, z).normalized);

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
                    foreach (var faceIndex in face) center += vertices[faceIndex];
                    center /= face.Length;
                    if (Mathf.Abs(Vector3.Dot(normal, center) - distance) >= 0.001f) continue;
                    duplicate = true;
                    break;
                }
                if (duplicate) continue;
                var indices = new List<int>();
                for (var index = 0; index < vertices.Count; index++)
                    if (Mathf.Abs(Vector3.Dot(normal, vertices[index]) - distance) < 0.001f) indices.Add(index);
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
                if (Vector3.Dot(Vector3.Cross(vertices[indices[1]] - vertices[indices[0]],
                        vertices[indices[2]] - vertices[indices[0]]), normal) < 0f) indices.Reverse();
                faces.Add(indices.ToArray());
            }
            if (faces.Count != 12)
                throw new InvalidOperationException($"Expected 12 detail-scatter dodecahedron faces, generated {faces.Count}.");

            var solidVertices = new List<Vector3>();
            var triangles = new List<int>();
            var lineIndices = new List<int>();
            foreach (var face in faces)
            {
                var start = solidVertices.Count;
                foreach (var index in face) solidVertices.Add(vertices[index]);
                for (var index = 1; index < 4; index++)
                {
                    var a = start;
                    var b = start + index;
                    var c = start + index + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    lineIndices.Add(a); lineIndices.Add(b);
                    lineIndices.Add(b); lineIndices.Add(c);
                    lineIndices.Add(c); lineIndices.Add(a);
                }
            }
            var solid = new Mesh { name = "ReactDetailScatterDodeca" };
            solid.SetVertices(solidVertices);
            solid.SetTriangles(triangles, 0);
            solid.RecalculateNormals();
            solid.RecalculateBounds();
            var wire = new Mesh { name = "ReactDetailScatterDodecaWire" };
            wire.SetVertices(solidVertices);
            wire.SetIndices(lineIndices, MeshTopology.Lines, 0);
            wire.RecalculateBounds();
            return (solid, wire);
        }

        private readonly struct ChunkStage
        {
            public ChunkStage(int chunkX, int chunkZ, float readyAt)
            {
                ChunkX = chunkX;
                ChunkZ = chunkZ;
                ReadyAt = readyAt;
            }

            public int ChunkX { get; }
            public int ChunkZ { get; }
            public float ReadyAt { get; }
        }
    }
}
