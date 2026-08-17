using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalHobbitHutRuntime : MonoBehaviour
    {
        private const string ProbeArgument = "--wof-hobbit-hut-probe";
        private readonly Dictionary<long, ChunkStage> _visibleStages = new();
        private readonly Dictionary<string, Material> _materials = new(StringComparer.Ordinal);
        private readonly List<Mesh> _ownedMeshes = new();
        private WofPlayerController _player;
        private Transform _contentRoot;
        private Mesh _boxMesh;
        private Mesh _cylinderMesh;
        private Mesh _planeMesh;
        private Mesh _dodecaDetailZeroMesh;
        private Mesh _dodecaDetailOneMesh;
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

        public int HutCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            if (FindFirstObjectByType<WofSurvivalHobbitHutRuntime>() != null) return;
            new GameObject("ReactSurvivalHobbitHutRuntime").AddComponent<WofSurvivalHobbitHutRuntime>();
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
            _boxMesh = Own(CreateBoxMesh());
            _cylinderMesh = Own(CreateCylinderMesh(6));
            _planeMesh = Own(CreatePlaneMesh());
            (_dodecaDetailZeroMesh, _dodecaDetailOneMesh) = CreateDodecaMeshes();
            Own(_dodecaDetailZeroMesh);
            Own(_dodecaDetailOneMesh);
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
            if (!WofSurvivalHobbitHutRules.ShouldShowRuntime(survival, _mobile, _grassInspectionView))
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
                    Time.unscaledTime + WofSurvivalHobbitHutRules.GetReadyDelaySeconds(
                        chunkX, chunkZ, distance)));
            }

            var existingKeys = new List<long>(_visibleStages.Keys);
            foreach (var key in existingKeys)
                if (!desired.Contains(key)) _visibleStages.Remove(key);

            if (_contentX != centerX || _contentZ != centerZ) ClearContent();
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_HOBBIT_HUT_WINDOW center={centerX}:{centerZ} staged={_visibleStages.Count}");
        }

        private void TryBuildCurrentChunk()
        {
            if (_contentRoot != null && _contentX == _centerX && _contentZ == _centerZ) return;
            if (!_visibleStages.TryGetValue(MakeCoordinateKey(_centerX, _centerZ), out var stage) ||
                stage.ReadyAt > Time.unscaledTime) return;

            var records = WofSurvivalHobbitHutRules.MakeChunk(_centerX, _centerZ);
            var content = new GameObject($"ReactHobbitHuts-{_centerX}-{_centerZ}");
            content.transform.SetParent(transform, false);
            _contentRoot = content.transform;
            _contentX = _centerX;
            _contentZ = _centerZ;
            HutCount = 0;
            foreach (var record in records)
            {
                BuildHut(_contentRoot, record);
                HutCount++;
            }
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_HOBBIT_HUT_READY chunk={_centerX}:{_centerZ} huts={HutCount}");
        }

        private void BuildHut(Transform parent, WofSurvivalHobbitHutRecord record)
        {
            var root = new GameObject($"ReactHobbitHut-{record.SourceIndex}-{record.Biome}").transform;
            root.SetParent(parent, false);
            root.position = record.Position;
            root.rotation = Quaternion.Euler(0f, record.YawRadians * Mathf.Rad2Deg, 0f);
            root.localScale = Vector3.one * record.Scale;

            var roofGreen = record.Biome == WofSurvivalBiome.Jungle ? "#1d5a2c" :
                record.Biome == WofSurvivalBiome.Mushroom ? "#8e4aa2" : "#3f7734";
            var roofDark = record.Biome == WofSurvivalBiome.Jungle ? "#123d20" :
                record.Biome == WofSurvivalBiome.Mushroom ? "#5d336f" : "#2d5d29";
            var earthColor = record.Biome == WofSurvivalBiome.Jungle ? "#3a2416" :
                record.Biome == WofSurvivalBiome.Mushroom ? "#4a3158" : "#4d311f";
            var plankColor = record.Biome == WofSurvivalBiome.Jungle ? "#5a341e" :
                record.Biome == WofSurvivalBiome.Mushroom ? "#70446f" : "#6a4125";

            AddMesh(root, "EarthShell", _dodecaDetailOneMesh, GetMaterial(earthColor),
                new Vector3(0f, 2.7f, 0.7f), Vector3.zero, new Vector3(10.8f, 4.6f, 8.2f));
            AddMesh(root, "LivingRoof", _dodecaDetailOneMesh, GetMaterial(roofGreen),
                new Vector3(0f, 4.35f, 0.1f), Vector3.zero, new Vector3(10.4f, 2.25f, 7.8f));
            AddMesh(root, "RoofCap", _dodecaDetailZeroMesh, GetMaterial(roofDark),
                new Vector3(0f, 5.25f, -0.2f), Vector3.zero, new Vector3(7.4f, 0.72f, 5.8f));
            AddMesh(root, "FrontWall", _boxMesh, GetMaterial("#2c1b13"),
                new Vector3(0f, 2.95f, -7.48f), Vector3.zero, new Vector3(10.2f, 5.9f, 0.52f));

            var plankXs = new[] { -4.25f, -2.55f, -0.85f, 0.85f, 2.55f, 4.25f };
            foreach (var x in plankXs)
                AddMesh(root, "FrontPlank", _boxMesh, GetMaterial(plankColor),
                    new Vector3(x, 3f, -7.82f), Vector3.zero, new Vector3(1.06f, 5.25f, 0.42f));
            AddMesh(root, "DoorFrame", _boxMesh, GetMaterial("#1c120d"),
                new Vector3(0f, 2.54f, -8.08f), Vector3.zero, new Vector3(3.32f, 4.42f, 0.54f));
            AddMesh(root, "DoorGlow", _boxMesh, GetMaterial("#ef6d1b", 0.68f),
                new Vector3(0f, 2.58f, -8.12f), Vector3.zero, new Vector3(2.35f, 3.22f, 0.58f));
            AddMesh(root, "FrontBeam", _cylinderMesh, GetMaterial("#5a321c"),
                new Vector3(0f, 4.96f, -8.18f), new Vector3(0f, 0f, 90f),
                new Vector3(0.34f, 8.6f, 0.34f));
            foreach (var x in new[] { -4.76f, 4.76f })
                AddMesh(root, "FrontPost", _cylinderMesh, GetMaterial("#5a321c"),
                    new Vector3(x, 2.98f, -8.16f), Vector3.zero, new Vector3(0.34f, 5.28f, 0.34f));
            AddMesh(root, "Porch", _planeMesh, GetMaterial("#5b3b22", 0.78f),
                new Vector3(-5.9f, 0.13f, -10.2f),
                new Vector3(-90f, 0f, record.Variant * 180f), new Vector3(7.8f, 9.5f, 1f));
            AddMesh(root, "Chimney", _boxMesh, GetMaterial("#47301f"),
                new Vector3(3.95f, 7.05f, -1.55f), Vector3.zero, new Vector3(1.45f, 4.2f, 1.45f));

            for (var smoke = 0; smoke < 3; smoke++)
                AddMesh(root, $"Smoke{smoke}", _dodecaDetailZeroMesh,
                    GetMaterial("#d5d0c2", 0.28f - smoke * 0.06f),
                    new Vector3(4.12f + smoke * 0.62f, 10f + smoke * 1.35f, -1.55f - smoke * 0.34f),
                    Vector3.zero,
                    new Vector3(1f + smoke * 0.38f, 0.72f + smoke * 0.18f, 1f + smoke * 0.32f) * 0.7f);

            var leafXs = new[] { -4.9f, -2.9f, 3f, 5.2f };
            for (var index = 0; index < leafXs.Length; index++)
                AddMesh(root, $"RoofLeaf{index}", _planeMesh,
                    GetMaterial(index % 2 == 0 ? "#4d9a3e" : "#6fb64a", 0.9f, true),
                    new Vector3(leafXs[index], 5.45f + index * 0.18f, -6.25f + index % 2 * 0.7f),
                    new Vector3(0.18f, index * 0.7f, -0.12f) * Mathf.Rad2Deg,
                    new Vector3(1.1f, 2.8f, 1f));

            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = WofSurvivalHobbitHutRules.ColliderCenter;
            collider.size = WofSurvivalHobbitHutRules.ColliderSize;
        }

        private Transform AddMesh(
            Transform parent,
            string meshName,
            Mesh mesh,
            Material material,
            Vector3 localPosition,
            Vector3 localEulerDegrees,
            Vector3 localScale)
        {
            var item = new GameObject(meshName);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localRotation = Quaternion.Euler(localEulerDegrees);
            item.transform.localScale = localScale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return item.transform;
        }

        private Material GetMaterial(string hex, float alpha = 1f, bool doubleSided = false)
        {
            var key = $"{hex}:{alpha:0.000}:{doubleSided}";
            if (_materials.TryGetValue(key, out var material)) return material;
            ColorUtility.TryParseHtmlString(hex, out var color);
            color.a = alpha;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader)
            {
                name = $"ReactHobbitHut-{key}",
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
            var chunkX = _probeKind == "jungle" ? -4 : 2;
            var chunkZ = _probeKind == "mushroom" ? -4 : _probeKind == "jungle" ? 0 : -1;
            var records = WofSurvivalHobbitHutRules.MakeChunk(chunkX, chunkZ);
            if (records.Length == 0) return;
            var record = records[0];
            var yawDegrees = record.YawRadians * Mathf.Rad2Deg;
            var forward = Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward;
            var viewPosition = record.Position - forward * 28f + Vector3.up * 2.4f;
            var viewSurfaceY = WofSurvivalHobbitHutRules.GetRenderedTerrainHeightAtWorld(
                viewPosition.x,
                viewPosition.z);
            viewPosition.y = Mathf.Max(viewPosition.y, viewSurfaceY + 2.4f);
            var target = record.Position + Vector3.up * 2.4f;
            var toTarget = target - viewPosition;
            var horizontalDistance = new Vector2(toTarget.x, toTarget.z).magnitude;
            var probeYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            var probePitch = Mathf.Atan2(-toTarget.y, horizontalDistance) * Mathf.Rad2Deg;
            if (!_player.PrepareForAutomationStaticViewProbe(viewPosition, probeYaw, probePitch)) return;
            var camera = _player.GetComponentInChildren<Camera>(true);
            if (camera != null) camera.farClipPlane = 2500f;
            _probeViewPrepared = true;
        }

        private void TryReportProbeReady()
        {
            if (!_probe || !_probeViewPrepared || _probeReported || _contentRoot == null ||
                _contentX != _centerX || _contentZ != _centerZ || HutCount == 0) return;
            _probeReported = true;
            Debug.Log($"[WOF-AUTOMATION] HOBBIT_HUT_PROBE_READY kind={_probeKind} chunk={_centerX}:{_centerZ} huts={HutCount}");
        }

        private void ClearContent()
        {
            if (_contentRoot != null) Destroy(_contentRoot.gameObject);
            _contentRoot = null;
            _contentX = int.MinValue;
            _contentZ = int.MinValue;
            HutCount = 0;
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

        private static Mesh CreatePlaneMesh()
        {
            var mesh = new Mesh { name = "ReactHobbitHutPlane" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateBoxMesh()
        {
            var vertices = new List<Vector3>(24);
            var triangles = new List<int>(36);
            AddQuad(vertices, triangles, new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f));
            AddQuad(vertices, triangles, new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f));
            AddQuad(vertices, triangles, new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f));
            AddQuad(vertices, triangles, new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f));
            AddQuad(vertices, triangles, new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f));
            AddQuad(vertices, triangles, new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, -0.5f));
            var mesh = new Mesh { name = "ReactHobbitHutBox" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 lowerLeft,
            Vector3 upperLeft,
            Vector3 lowerRight,
            Vector3 upperRight)
        {
            var start = vertices.Count;
            vertices.Add(lowerLeft);
            vertices.Add(upperLeft);
            vertices.Add(lowerRight);
            vertices.Add(upperRight);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start + 3);
        }

        private static Mesh CreateCylinderMesh(int segments)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                var angle = index * Mathf.PI * 2f / segments;
                var nextAngle = next * Mathf.PI * 2f / segments;
                var bottom = new Vector3(Mathf.Cos(angle), -0.5f, Mathf.Sin(angle));
                var top = new Vector3(bottom.x, 0.5f, bottom.z);
                var nextBottom = new Vector3(Mathf.Cos(nextAngle), -0.5f, Mathf.Sin(nextAngle));
                var nextTop = new Vector3(nextBottom.x, 0.5f, nextBottom.z);
                var start = vertices.Count;
                vertices.Add(bottom); vertices.Add(top); vertices.Add(nextBottom); vertices.Add(nextTop);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start + 3);
                var cap = vertices.Count;
                vertices.Add(Vector3.down * 0.5f); vertices.Add(nextBottom); vertices.Add(bottom);
                vertices.Add(Vector3.up * 0.5f); vertices.Add(top); vertices.Add(nextTop);
                triangles.Add(cap); triangles.Add(cap + 1); triangles.Add(cap + 2);
                triangles.Add(cap + 3); triangles.Add(cap + 4); triangles.Add(cap + 5);
            }
            var mesh = new Mesh { name = "ReactHobbitHutCylinder6" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static (Mesh DetailZero, Mesh DetailOne) CreateDodecaMeshes()
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

            var faces = BuildDodecaFaces(vertices);
            var baseTriangles = new List<(Vector3 A, Vector3 B, Vector3 C)>();
            foreach (var face in faces)
            for (var index = 1; index < 4; index++)
                baseTriangles.Add((vertices[face[0]], vertices[face[index]], vertices[face[index + 1]]));

            var detailZero = CreateTriangleMesh("ReactHobbitHutDodecaDetail0", baseTriangles);
            var subdivided = new List<(Vector3 A, Vector3 B, Vector3 C)>(baseTriangles.Count * 4);
            foreach (var triangle in baseTriangles)
            {
                var ab = ((triangle.A + triangle.B) * 0.5f).normalized;
                var ac = ((triangle.A + triangle.C) * 0.5f).normalized;
                var bc = ((triangle.B + triangle.C) * 0.5f).normalized;
                subdivided.Add((triangle.A, ab, ac));
                subdivided.Add((ab, triangle.B, bc));
                subdivided.Add((ac, bc, triangle.C));
                subdivided.Add((ab, bc, ac));
            }
            var detailOne = CreateTriangleMesh("ReactHobbitHutDodecaDetail1", subdivided);
            return (detailZero, detailOne);
        }

        private static List<int[]> BuildDodecaFaces(IReadOnlyList<Vector3> vertices)
        {
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
                throw new InvalidOperationException($"Expected 12 hobbit-hut dodecahedron faces, generated {faces.Count}.");
            return faces;
        }

        private static Mesh CreateTriangleMesh(
            string meshName,
            IReadOnlyList<(Vector3 A, Vector3 B, Vector3 C)> sourceTriangles)
        {
            var vertices = new List<Vector3>(sourceTriangles.Count * 3);
            var triangles = new List<int>(sourceTriangles.Count * 3);
            foreach (var triangle in sourceTriangles)
            {
                var start = vertices.Count;
                vertices.Add(triangle.A);
                vertices.Add(triangle.B);
                vertices.Add(triangle.C);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }
            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static long MakeCoordinateKey(int x, int z) => ((long)x << 32) ^ (uint)z;

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
