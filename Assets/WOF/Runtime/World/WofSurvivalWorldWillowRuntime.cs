using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalWorldWillowRuntime : MonoBehaviour
    {
        private readonly List<WillowVisual> _visuals = new();
        private WofWorldWillowRecord[] _willows = Array.Empty<WofWorldWillowRecord>();
        private WofPlayerController _player;
        private Mesh _trunkMesh;
        private Mesh _branchMesh;
        private Mesh _dodecaMesh;
        private Mesh _dodecaEdgeMesh;
        private Mesh _leafMesh;
        private Mesh _particleMesh;
        private float _nextResolveAt;
        private int _centerX = int.MinValue;
        private int _centerZ = int.MinValue;
        private bool _probe;
        private int _probeIndex = 4;
        private bool _probePositioned;
        private bool _probePassed;
        private bool _grassInspectionView;
        private float _probeStartAt;
        private Vector3 _probeStartParticle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            if (FindFirstObjectByType<WofSurvivalWorldWillowRuntime>() != null) return;
            new GameObject("ReactSurvivalWorldWillowRuntime").AddComponent<WofSurvivalWorldWillowRuntime>();
        }

        private void Awake()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-grass-view-probe", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("--wof-grass-view-probe=", StringComparison.OrdinalIgnoreCase))
                    _grassInspectionView = true;
                const string prefix = "--wof-world-willow-probe=";
                if (argument.Equals("--wof-world-willow-probe", StringComparison.OrdinalIgnoreCase))
                    _probe = true;
                if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                _probe = true;
                if (int.TryParse(argument.Substring(prefix.Length), out var parsed))
                    _probeIndex = Mathf.Clamp(parsed, 0, WofSurvivalWorldWillowRules.WillowCount - 1);
            }

            _trunkMesh = CreateFrustumMesh(0.68f, 1f, 7, "ReactWorldWillowTrunk");
            _branchMesh = CreateFrustumMesh(0.68f, 1f, 5, "ReactWorldWillowBranch");
            (_dodecaMesh, _dodecaEdgeMesh) = CreateDodecaMeshes();
            _leafMesh = CreateQuadMesh("ReactWorldWillowLeaf");
            _particleMesh = CreateLowPolySphere("ReactWorldWillowParticle");
            _willows = WofSurvivalWorldWillowRules.MakeWillows();
            for (var index = 0; index < _willows.Length; index++)
                _visuals.Add(new WillowVisual(transform, _willows[index], _trunkMesh, _branchMesh,
                    _dodecaMesh, _dodecaEdgeMesh, _leafMesh, _particleMesh));
        }

        private void OnDestroy()
        {
            foreach (var visual in _visuals) visual.Dispose();
            Destroy(_trunkMesh);
            Destroy(_branchMesh);
            Destroy(_dodecaMesh);
            Destroy(_dodecaEdgeMesh);
            Destroy(_leafMesh);
            Destroy(_particleMesh);
        }

        private void Update()
        {
            var survival = WofBootstrap.Instance != null && WofBootstrap.Instance.IsSurvivalSession;
            if (!WofSurvivalWorldWillowRules.ShouldShowWillows(survival, _grassInspectionView))
            {
                SetAllInactive();
                return;
            }
            ResolvePlayer();
            if (_player == null) return;
            if (_probe && !_probePositioned) PositionProbe();
            var centerX = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.x);
            var centerZ = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.z);
            if (centerX != _centerX || centerZ != _centerZ)
            {
                _centerX = centerX;
                _centerZ = centerZ;
                ApplyVisibility();
            }
            foreach (var visual in _visuals) visual.UpdateParticles(Time.timeAsDouble);
            UpdateProbe();
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

        private void PositionProbe()
        {
            var willow = _willows[_probeIndex];
            var viewPosition = willow.Position + new Vector3(0f, willow.TrunkHeight * 0.22f, -willow.CanopyRadius * 4.1f);
            if (!_player.PrepareForAutomationStaticViewProbe(viewPosition, 0f, -24f)) return;
            var camera = _player.GetComponentInChildren<Camera>(true);
            if (camera != null) camera.farClipPlane = 2500f;
            _probePositioned = true;
            _probeStartAt = Time.unscaledTime + 1f;
            _probeStartParticle = WofSurvivalWorldWillowRules.GetParticleLocalPosition(
                willow, WofSurvivalWorldWillowRules.MakeParticles(willow,
                    WofPerformanceModeRuntime.IsMobilePerformanceMode)[0], Time.timeAsDouble);
            Debug.Log($"[WOF-AUTOMATION] WORLD_WILLOW_PROBE_POSITIONED index={_probeIndex} key={willow.Key} chunk={willow.ChunkX}:{willow.ChunkZ} biome={willow.Biome.ToString().ToLowerInvariant()} position={willow.Position}");
        }

        private void UpdateProbe()
        {
            if (!_probePositioned || _probePassed || Time.unscaledTime < _probeStartAt + 2f) return;
            var willow = _willows[_probeIndex];
            var particle = WofSurvivalWorldWillowRules.MakeParticles(
                willow, WofPerformanceModeRuntime.IsMobilePerformanceMode)[0];
            var current = WofSurvivalWorldWillowRules.GetParticleLocalPosition(willow, particle, Time.timeAsDouble);
            var movement = Vector3.Distance(_probeStartParticle, current);
            if (movement <= 0.1f) return;
            _probePassed = true;
            Debug.Log($"[WOF-AUTOMATION] WORLD_WILLOW_PROBE_PASS index={_probeIndex} movement={movement:F2} particles={(WofPerformanceModeRuntime.IsMobilePerformanceMode ? 36 : 72)} mobileHz={(WofPerformanceModeRuntime.IsMobilePerformanceMode ? 24 : 0)}");
        }

        private void ApplyVisibility()
        {
            for (var index = 0; index < _visuals.Count; index++)
            {
                var visible = WofSurvivalWorldWillowRules.IsVisible(
                    _willows[index], _centerX, _centerZ, WofSurvivalTerrainMath.RenderRadius);
                var particles = visible && WofSurvivalWorldWillowRules.ShouldShowParticles(
                    _willows[index], _centerX, _centerZ, WofSurvivalTerrainMath.RenderRadius);
                _visuals[index].SetVisible(visible, particles);
            }
            var visibleCount = 0;
            foreach (var visual in _visuals) if (visual.Visible) visibleCount++;
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_WORLD_WILLOWS_READY center={_centerX}:{_centerZ} visible={visibleCount} total={_visuals.Count}");
        }

        private void SetAllInactive()
        {
            foreach (var visual in _visuals) visual.SetVisible(false, false);
        }

        private sealed class WillowVisual
        {
            private readonly WofWorldWillowRecord _willow;
            private readonly GameObject _root;
            private readonly Material[] _materials;
            private readonly Material _particleMaterial;
            private readonly Mesh _particleMesh;
            private readonly Texture2D _barkTexture;
            private readonly WofWorldWillowParticle[] _particles;
            private readonly Matrix4x4[] _particleMatrices;
            private float _nextParticleUpdateAt;
            private bool _showParticles;

            public WillowVisual(
                Transform parent,
                WofWorldWillowRecord willow,
                Mesh trunkMesh,
                Mesh branchMesh,
                Mesh dodecaMesh,
                Mesh dodecaEdgeMesh,
                Mesh leafMesh,
                Mesh particleMesh)
            {
                _willow = willow;
                _particleMesh = particleMesh;
                _root = new GameObject(willow.Key);
                _root.transform.SetParent(parent, false);
                _root.transform.SetPositionAndRotation(willow.Position, Quaternion.Euler(0f, willow.Yaw * Mathf.Rad2Deg, 0f));
                var trunk = MakeMaterial($"{willow.Key}-trunk", WofSurvivalWorldWillowRules.GetTrunkColor(willow.Biome));
                _barkTexture = CreateBarkTexture();
                trunk.mainTexture = _barkTexture;
                trunk.mainTextureScale = new Vector2(1.8f, 2.6f);
                var branch = MakeMaterial($"{willow.Key}-branch", WofSurvivalWorldWillowRules.GetBranchColor(willow.Biome));
                var edgeColor = WofSurvivalWorldWillowRules.GetEdgeColor(willow.Biome);
                edgeColor.a = 122;
                var edge = MakeMaterial($"{willow.Key}-edge", edgeColor, true);
                var vine = MakeMaterial($"{willow.Key}-vine", WofSurvivalWorldWillowRules.VineColor);
                var leafEdge = MakeMaterial($"{willow.Key}-leaf-edge", Hex("#3a6330"));
                var leafA = MakeMaterial($"{willow.Key}-leaf-a", WofSurvivalWorldWillowRules.VineLeafColor);
                var leafB = MakeMaterial($"{willow.Key}-leaf-b", Hex("#3d8f42"));
                var canopy = new Material[3];
                var lobes = WofSurvivalWorldWillowRules.MakeLobes(willow);
                for (var index = 0; index < canopy.Length; index++)
                    canopy[index] = MakeMaterial($"{willow.Key}-canopy-{index}",
                        WofSurvivalWorldWillowRules.GetCanopyColor(willow.Biome, index));
                _particleMaterial = MakeMaterial($"{willow.Key}-particles", WofSurvivalWorldWillowRules.ParticleColor, true, true);
                _materials = new[] { trunk, branch, edge, vine, leafEdge, leafA, leafB,
                    canopy[0], canopy[1], canopy[2], _particleMaterial };

                AddPart("Trunk", trunkMesh,
                    new Vector3(0f, willow.TrunkHeight * 0.5f - willow.TrunkHeight * 0.035f, 0f),
                    Quaternion.Euler(0.04f * Mathf.Rad2Deg, 0f, -0.025f * Mathf.Rad2Deg),
                    new Vector3(willow.TrunkRadius, willow.TrunkHeight * 1.07f, willow.TrunkRadius), trunk);
                foreach (var item in WofSurvivalWorldWillowRules.MakeBranches(willow))
                    AddSegment("Branch", branchMesh, item.Start, item.End, item.Radius, branch);
                for (var index = 0; index < lobes.Length; index++)
                {
                    var lobe = lobes[index];
                    var material = canopy[index == 0 ? 0 : (index - 1) % 3];
                    AddPart($"Canopy-{index}", dodecaMesh, lobe.Position, Quaternion.identity,
                        lobe.Scale * lobe.Radius, material);
                    AddPart($"CanopyEdge-{index}", dodecaEdgeMesh, lobe.Position, Quaternion.identity,
                        lobe.Scale * lobe.Radius * 1.004f, edge);
                }
                var vines = WofSurvivalWorldWillowRules.MakeVines(willow);
                for (var index = 0; index < vines.Length; index++)
                {
                    var item = vines[index];
                    AddSegment($"Vine-{index}", branchMesh, item.Start, item.End, 0.13f, vine);
                    AddLeaf($"VineLeafA-{index}", item.LeafPosition,
                        Quaternion.Euler(0.25f * Mathf.Rad2Deg, item.Sway * Mathf.Rad2Deg, 0.65f * Mathf.Rad2Deg),
                        1.2f, 2.6f, leafEdge, leafA, leafMesh);
                    var second = new Vector3(
                        item.X - Mathf.Sin(item.Sway) * 0.55f,
                        item.Y - item.Length * 0.78f,
                        item.Z - Mathf.Cos(item.Sway) * 0.55f);
                    AddLeaf($"VineLeafB-{index}", second,
                        Quaternion.Euler(-0.18f * Mathf.Rad2Deg, (item.Sway + 0.9f) * Mathf.Rad2Deg, -0.5f * Mathf.Rad2Deg),
                        1f, 2.1f, leafEdge, leafB, leafMesh);
                }
                _particles = WofSurvivalWorldWillowRules.MakeParticles(
                    willow, WofPerformanceModeRuntime.IsMobilePerformanceMode);
                _particleMatrices = new Matrix4x4[_particles.Length];
                _root.SetActive(false);
            }

            public bool Visible => _root.activeSelf;

            public void SetVisible(bool visible, bool showParticles)
            {
                if (_root.activeSelf != visible) _root.SetActive(visible);
                _showParticles = showParticles;
            }

            public void UpdateParticles(double elapsed)
            {
                if (!Visible || !_showParticles) return;
                if (WofPerformanceModeRuntime.IsMobilePerformanceMode && Time.unscaledTime < _nextParticleUpdateAt)
                {
                    DrawParticles();
                    return;
                }
                _nextParticleUpdateAt = Time.unscaledTime +
                    (WofPerformanceModeRuntime.IsMobilePerformanceMode
                        ? WofSurvivalWorldWillowRules.MobileParticleUpdateInterval : 0f);
                for (var index = 0; index < _particles.Length; index++)
                {
                    var position = WofSurvivalWorldWillowRules.GetParticleLocalPosition(_willow, _particles[index], elapsed);
                    var scale = WofSurvivalWorldWillowRules.GetParticleScale(_particles[index], elapsed);
                    _particleMatrices[index] = _root.transform.localToWorldMatrix * Matrix4x4.TRS(
                        position, Quaternion.Euler(0f, _particles[index].Angle * Mathf.Rad2Deg, 0f), Vector3.one * scale);
                }
                DrawParticles();
            }

            public void Dispose()
            {
                foreach (var material in _materials) UnityEngine.Object.Destroy(material);
                UnityEngine.Object.Destroy(_barkTexture);
                UnityEngine.Object.Destroy(_root);
            }

            private void DrawParticles()
            {
                Graphics.DrawMeshInstanced(_particleMesh, 0, _particleMaterial, _particleMatrices,
                    _particleMatrices.Length, null, ShadowCastingMode.Off, false, 0, null, LightProbeUsage.Off);
            }

            private void AddLeaf(string name, Vector3 position, Quaternion rotation, float width, float height,
                Material edge, Material fill, Mesh mesh)
            {
                AddPart(name + "Edge", mesh, position + rotation * new Vector3(0f, 0f, -0.01f), rotation,
                    new Vector3(width * 1.14f, height * 1.1f, 1f), edge);
                AddPart(name, mesh, position + rotation * new Vector3(0f, 0f, 0.01f), rotation,
                    new Vector3(width, height, 1f), fill);
            }

            private void AddSegment(string name, Mesh mesh, Vector3 start, Vector3 end, float radius, Material material)
            {
                var direction = end - start;
                AddPart(name, mesh, (start + end) * 0.5f,
                    Quaternion.FromToRotation(Vector3.up, direction.normalized),
                    new Vector3(radius, direction.magnitude, radius), material);
            }

            private void AddPart(string name, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
            {
                var item = new GameObject(name);
                item.transform.SetParent(_root.transform, false);
                item.transform.localPosition = position;
                item.transform.localRotation = rotation;
                item.transform.localScale = scale;
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                item.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        private static Material MakeMaterial(string name, Color color, bool transparent = false, bool additive = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = name, color = color, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (!transparent) return material;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static Color32 Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? (Color32)color : default;
        }

        private static Mesh CreateFrustumMesh(float topRadius, float bottomRadius, int segments, string name)
        {
            var vertices = new Vector3[segments * 2 + 2];
            var uvs = new Vector2[segments * 2 + 2];
            var triangles = new int[segments * 12];
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                vertices[index] = new Vector3(Mathf.Cos(angle) * bottomRadius, -0.5f, Mathf.Sin(angle) * bottomRadius);
                vertices[index + segments] = new Vector3(Mathf.Cos(angle) * topRadius, 0.5f, Mathf.Sin(angle) * topRadius);
                uvs[index] = new Vector2(index / (float)segments, 0f);
                uvs[index + segments] = new Vector2(index / (float)segments, 1f);
                var next = (index + 1) % segments;
                var write = index * 12;
                triangles[write] = index; triangles[write + 1] = index + segments; triangles[write + 2] = next + segments;
                triangles[write + 3] = index; triangles[write + 4] = next + segments; triangles[write + 5] = next;
                triangles[write + 6] = segments * 2; triangles[write + 7] = next; triangles[write + 8] = index;
                triangles[write + 9] = segments * 2 + 1; triangles[write + 10] = index + segments; triangles[write + 11] = next + segments;
            }
            vertices[segments * 2] = new Vector3(0f, -0.5f, 0f);
            vertices[segments * 2 + 1] = new Vector3(0f, 0.5f, 0f);
            uvs[segments * 2] = new Vector2(0.5f, 0.5f);
            uvs[segments * 2 + 1] = new Vector2(0.5f, 0.5f);
            var mesh = new Mesh { name = name, vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh CreateQuadMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f),
                new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh CreateLowPolySphere(string name)
        {
            const int widthSegments = 5;
            const int heightSegments = 4;
            var vertices = new List<Vector3>((widthSegments + 1) * (heightSegments + 1));
            var triangles = new List<int>(widthSegments * heightSegments * 6);
            for (var y = 0; y <= heightSegments; y++)
            {
                var v = y / (float)heightSegments;
                var phi = v * Mathf.PI;
                for (var x = 0; x <= widthSegments; x++)
                {
                    var u = x / (float)widthSegments;
                    var theta = u * Mathf.PI * 2f;
                    vertices.Add(new Vector3(
                        -Mathf.Cos(theta) * Mathf.Sin(phi),
                        Mathf.Cos(phi),
                        Mathf.Sin(theta) * Mathf.Sin(phi)));
                }
            }
            for (var y = 0; y < heightSegments; y++)
            for (var x = 0; x < widthSegments; x++)
            {
                var a = y * (widthSegments + 1) + x;
                var b = a + widthSegments + 1;
                if (y != 0) { triangles.Add(a); triangles.Add(b); triangles.Add(a + 1); }
                if (y != heightSegments - 1) { triangles.Add(b); triangles.Add(b + 1); triangles.Add(a + 1); }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D CreateBarkTexture()
        {
            const int width = 64;
            const int height = 128;
            var pixels = new Color32[width * height];
            var background = Hex("#f3dfbd");
            for (var index = 0; index < pixels.Length; index++) pixels[index] = background;
            for (var x = 0; x < width; x += 8)
            {
                var shade = WofSurvivalTerrainMath.Hash01(x, 11, 18910);
                var overlay = shade > 0.68d ? new Color32(255, 255, 255, 41) :
                    shade > 0.34d ? new Color32(0, 0, 0, 20) : new Color32(94, 45, 18, 46);
                BlendRect(pixels, width, height, x, 0, 4 + (int)Math.Floor(shade * 5d), height, overlay);
            }
            for (var y = 0; y < height; y += 8)
            for (var x = 0; x < width; x += 8)
            {
                var chip = WofSurvivalTerrainMath.Hash01(x, y, 18920);
                if (chip > 0.78d) BlendRect(pixels, width, height, x + 1, y + 1, 4, 2, new Color32(255, 246, 211, 61));
                else if (chip < 0.18d) BlendRect(pixels, width, height, x + 2, y + 3, 3, 3, new Color32(66, 28, 12, 51));
            }
            for (var crack = 0; crack < 12; crack++)
            {
                var baseX = (int)Math.Floor(WofSurvivalTerrainMath.Hash01(crack, 3, 18930) * width);
                var crackWidth = WofSurvivalTerrainMath.Hash01(crack, 4, 18931) > 0.68d ? 3 : 2;
                var color = WofSurvivalTerrainMath.Hash01(crack, 5, 18932) > 0.44d
                    ? new Color32(35, 16, 8, 122) : new Color32(75, 33, 14, 82);
                for (var y = -8; y < height; y += 8)
                {
                    var wobble = (int)Math.Floor((WofSurvivalTerrainMath.Hash01(crack, y, 18933) - 0.5d) * 6d);
                    var run = 5 + (int)Math.Floor(WofSurvivalTerrainMath.Hash01(crack, y, 18934) * 8d);
                    BlendRect(pixels, width, height, (baseX + wobble + width) % width, y, crackWidth, run, color);
                }
            }
            for (var knot = 0; knot < 7; knot++)
            {
                var x = 7 + (int)Math.Floor(WofSurvivalTerrainMath.Hash01(knot, 13, 18940) * (width - 14));
                var y = 8 + (int)Math.Floor(WofSurvivalTerrainMath.Hash01(knot, 17, 18941) * (height - 16));
                var w = 6 + (int)Math.Floor(WofSurvivalTerrainMath.Hash01(knot, 19, 18942) * 6d);
                var h = 4 + (int)Math.Floor(WofSurvivalTerrainMath.Hash01(knot, 23, 18943) * 5d);
                BlendRect(pixels, width, height, x, y, w, h, new Color32(59, 28, 13, 107));
                BlendRect(pixels, width, height, x + 1, y + 1, Math.Max(2, w - 3), 1, new Color32(246, 214, 163, 46));
                BlendRect(pixels, width, height, x + (int)Math.Floor(w * 0.35d), y + (int)Math.Floor(h * 0.45d),
                    Math.Max(2, (int)Math.Floor(w * 0.34d)), 1, new Color32(22, 10, 5, 92));
            }
            for (var y = 0; y < height; y += 16)
                BlendRect(pixels, width, height, 0, y, width, 2, new Color32(255, 255, 255, 20));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ReactSurvivalSolidTreeBark",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void BlendRect(Color32[] pixels, int width, int height, int x, int y, int w, int h, Color32 overlay)
        {
            var alpha = overlay.a / 255f;
            for (var py = Math.Max(0, y); py < Math.Min(height, y + h); py++)
            for (var px = Math.Max(0, x); px < Math.Min(width, x + w); px++)
            {
                var index = py * width + px;
                var current = pixels[index];
                pixels[index] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Lerp(current.r, overlay.r, alpha)),
                    (byte)Mathf.RoundToInt(Mathf.Lerp(current.g, overlay.g, alpha)),
                    (byte)Mathf.RoundToInt(Mathf.Lerp(current.b, overlay.b, alpha)),
                    255);
            }
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
                if (normal.sqrMagnitude < 0.000001f) continue; normal.Normalize();
                var positive = false; var negative = false;
                foreach (var point in vertices) { var side = Vector3.Dot(normal, point - vertices[a]); if (side > 0.0001f) positive = true; if (side < -0.0001f) negative = true; }
                if (positive && negative) continue; if (positive) normal = -normal;
                var distance = Vector3.Dot(normal, vertices[a]); if (distance < 0f) { normal = -normal; distance = -distance; }
                var duplicate = false;
                foreach (var face in faces) { var center = Vector3.zero; foreach (var i in face) center += vertices[i]; center /= face.Length; if (Mathf.Abs(Vector3.Dot(normal, center) - distance) < 0.001f) { duplicate = true; break; } }
                if (duplicate) continue;
                var indices = new List<int>(); for (var i = 0; i < vertices.Count; i++) if (Mathf.Abs(Vector3.Dot(normal, vertices[i]) - distance) < 0.001f) indices.Add(i);
                if (indices.Count != 5) continue;
                var centerPoint = Vector3.zero; foreach (var i in indices) centerPoint += vertices[i]; centerPoint /= indices.Count;
                var axis = (vertices[indices[0]] - centerPoint).normalized; var tangent = Vector3.Cross(normal, axis).normalized;
                indices.Sort((left, right) => Mathf.Atan2(Vector3.Dot(vertices[left] - centerPoint, tangent), Vector3.Dot(vertices[left] - centerPoint, axis)).CompareTo(Mathf.Atan2(Vector3.Dot(vertices[right] - centerPoint, tangent), Vector3.Dot(vertices[right] - centerPoint, axis))));
                if (Vector3.Dot(Vector3.Cross(vertices[indices[1]] - vertices[indices[0]],
                        vertices[indices[2]] - vertices[indices[0]]), normal) < 0f) indices.Reverse();
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
            var solid = new Mesh { name = "ReactWorldWillowDodeca" }; solid.SetVertices(solidVertices); solid.SetTriangles(triangles, 0); solid.RecalculateNormals(); solid.RecalculateBounds();
            var edge = new Mesh { name = "ReactWorldWillowDodecaEdges" }; edge.SetVertices(vertices); var edgeIndices = new List<int>(); foreach (var pair in edges) { edgeIndices.Add(pair.Item1); edgeIndices.Add(pair.Item2); } edge.SetIndices(edgeIndices, MeshTopology.Lines, 0); edge.RecalculateBounds();
            return (solid, edge);
        }
    }
}
