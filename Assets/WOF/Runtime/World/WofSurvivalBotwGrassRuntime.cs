using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalBotwGrassRuntime : MonoBehaviour
    {
        public const float Radius = 224f;
        public const float EdgeFade = 34f;
        public const float CenterStep = 96f;
        public const float RecenterDistance = 64f;
        public const int BladeCount = 56000;
        public const int FlowerCount = 760;
        public const int CandidateCount = 71680;
        public const int RenderLayer = 30;
        private const int InstancesPerBatch = 1023;
        private const int CandidatesPerFrameDesktop = 2200;
        private const int CandidatesPerFrameMobile = 1600;
        private const float GoldenAngle = 2.399963229728653f;
        private const string OpenWorldTerrainName = "ReactSurvivalOpenWorldBaseRegion";
        private static readonly RaycastHit[] TerrainHits = new RaycastHit[16];
        private static readonly Color GrassDark = new Color32(0x3c, 0x8b, 0x2d, 0xff);
        private static readonly Color GrassLight = new Color32(0x62, 0xbd, 0x37, 0xff);
        private static readonly Color GrassHighlight = new Color32(0xb9, 0xef, 0x5b, 0xff);
        private static readonly Color MeadowGreen = new Color32(0x83, 0xc7, 0x50, 0xff);
        private static readonly Color HillsideDark = new Color32(0x66, 0x7f, 0x43, 0xff);
        private static readonly Color HillsideLight = new Color32(0x89, 0x92, 0x5a, 0xff);
        private static readonly Color GrassMeshRoot = new Color32(0x85, 0xd2, 0x4a, 0xff);
        private static readonly Color GrassMeshTip = new Color32(0xf0, 0xff, 0x90, 0xff);
        private static readonly Color FlowerStem = new Color32(0x61, 0xb6, 0x40, 0xff);
        private static readonly Color[] MeadowFlowerPalette =
        {
            new Color32(0xf8, 0xfa, 0xfc, 0xff), new Color32(0xfd, 0xe0, 0x47, 0xff),
            new Color32(0xf9, 0xa8, 0xd4, 0xff), new Color32(0x93, 0xc5, 0xfd, 0xff),
            new Color32(0x7d, 0xd3, 0xfc, 0xff), new Color32(0xff, 0xf1, 0x76, 0xff)
        };
        private static readonly Color[] SwampFlowerPalette =
        {
            new Color32(0xd9, 0xf9, 0x9d, 0xff), new Color32(0xbb, 0xf7, 0xd0, 0xff),
            new Color32(0x99, 0xf6, 0xe4, 0xff), new Color32(0xdd, 0xd6, 0xfe, 0xff),
            new Color32(0xfe, 0xf3, 0xc7, 0xff), new Color32(0xba, 0xe6, 0xfd, 0xff)
        };

        [SerializeField] private Texture2D bladeTexture;

        private readonly List<InstanceBatch> _activeGrass = new();
        private readonly List<InstanceBatch> _buildingGrass = new();
        private readonly List<InstanceBatch> _activeFlowers = new();
        private readonly List<InstanceBatch> _buildingFlowers = new();
        private Mesh _grassMesh;
        private Mesh _flowerMesh;
        private Material _grassMaterial;
        private Material _flowerMaterial;
        private Transform _viewer;
        private Vector3 _center;
        private int _candidate;
        private int _acceptedGrass;
        private int _acceptedFlowers;
        private bool _building;
        private float _nextViewerResolveAt;
        private float _buildStartedAt;

        public void Configure(Texture2D exactReactBladeTexture)
        {
            bladeTexture = exactReactBladeTexture;
        }

        private void Awake()
        {
            if (!SystemInfo.supportsInstancing)
            {
                Debug.Log("[WOF-AUTOMATION] BOTW_GRASS_RENDERING_SKIPPED reason=instancing-unavailable");
                enabled = false;
                return;
            }

            var grassShader = Shader.Find("WOF/BOTW Grass");
            var foliageShader = Shader.Find("WOF/Instanced Foliage");
            if (grassShader == null || foliageShader == null || bladeTexture == null)
            {
                Debug.LogError($"[WOF-AUTOMATION] BOTW_GRASS_FAILED shader={grassShader != null} foliageShader={foliageShader != null} texture={bladeTexture != null}");
                enabled = false;
                return;
            }

            _grassMesh = CreateGrassClusterMesh();
            _flowerMesh = CreateFlowerMesh();
            _grassMaterial = new Material(grassShader)
            {
                name = "ReactBotwGrassRuntimeMaterial",
                mainTexture = bladeTexture,
                enableInstancing = true,
                renderQueue = 3000
            };
            _grassMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 0.98f));
            _grassMaterial.SetFloat("_Radius", Radius + EdgeFade * 0.72f);
            _grassMaterial.SetFloat("_FadeWidth", EdgeFade);
            _flowerMaterial = new Material(foliageShader)
            {
                name = "ReactBotwWildflowerRuntimeMaterial",
                enableInstancing = true
            };
            Debug.Log($"[WOF-AUTOMATION] BOTW_GRASS_RUNTIME_READY radius={Radius:F0} blades={BladeCount} flowers={FlowerCount}");
        }

        private void OnDestroy()
        {
            if (_grassMesh != null) Destroy(_grassMesh);
            if (_flowerMesh != null) Destroy(_flowerMesh);
            if (_grassMaterial != null) Destroy(_grassMaterial);
            if (_flowerMaterial != null) Destroy(_flowerMaterial);
        }

        private void Update()
        {
            ResolveViewer();
            if (_viewer == null) return;

            if (!_building && (_activeGrass.Count == 0 || HorizontalDistance(_viewer.position, _center) >= RecenterDistance))
            {
                StartBuild(QuantizeCenter(_viewer.position));
            }

            if (_building) ContinueBuild();
            _grassMaterial.SetVector("_ViewerXZ", new Vector4(_viewer.position.x, _viewer.position.z, 0f, 0f));
            _grassMaterial.SetFloat("_WindTime", Time.time);
            DrawBatches(_activeGrass.Count > 0 ? _activeGrass : _buildingGrass, _grassMesh, _grassMaterial);
            DrawBatches(_activeFlowers.Count > 0 ? _activeFlowers : _buildingFlowers, _flowerMesh, _flowerMaterial);
        }

        private void ResolveViewer()
        {
            if (_viewer != null) return;
            if (Time.unscaledTime < _nextViewerResolveAt) return;
            _nextViewerResolveAt = Time.unscaledTime + 0.25f;
            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (playerObject != null) _viewer = playerObject.transform;
        }

        private void StartBuild(Vector3 center)
        {
            _center = center;
            _candidate = 0;
            _acceptedGrass = 0;
            _acceptedFlowers = 0;
            _buildingGrass.Clear();
            _buildingFlowers.Clear();
            _building = true;
            _buildStartedAt = Time.realtimeSinceStartup;
            Debug.Log($"[WOF-AUTOMATION] BOTW_GRASS_BUILD_STARTED center={center.x:F0},{center.z:F0}");
        }

        private void ContinueBuild()
        {
            var frameLimit = WofPerformanceModeRuntime.IsMobilePerformanceMode
                ? CandidatesPerFrameMobile
                : CandidatesPerFrameDesktop;
            var end = Mathf.Min(CandidateCount, _candidate + frameLimit);
            for (; _candidate < end && _acceptedGrass < BladeCount; _candidate++)
            {
                if (TryMakeGrass(_candidate, out var matrix, out var color))
                {
                    AddInstance(_buildingGrass, matrix, color);
                    _acceptedGrass++;
                }

                if (_acceptedFlowers < FlowerCount && _candidate % 7 == 0 && TryMakeFlower(_candidate, out matrix, out color))
                {
                    AddInstance(_buildingFlowers, matrix, color);
                    _acceptedFlowers++;
                }
            }

            if (_candidate < CandidateCount && _acceptedGrass < BladeCount) return;
            SwapBatches(_activeGrass, _buildingGrass);
            SwapBatches(_activeFlowers, _buildingFlowers);
            _building = false;
            Debug.Log($"[WOF-AUTOMATION] BOTW_GRASS_BUILD_COMPLETE center={_center.x:F0},{_center.z:F0} blades={_acceptedGrass} flowers={_acceptedFlowers} ms={(Time.realtimeSinceStartup - _buildStartedAt) * 1000f:F0}");
        }

        private bool TryMakeGrass(int candidate, out Matrix4x4 matrix, out Vector4 color)
        {
            var radialCandidate = candidate * 8191 % CandidateCount;
            var radial = Mathf.Sqrt((radialCandidate + 0.5f) / CandidateCount);
            var seedX = Mathf.FloorToInt(_center.x * 0.25f);
            var seedZ = Mathf.FloorToInt(_center.z * 0.25f);
            var angleOffset = Hash01(seedX, seedZ, 1850) * Mathf.PI * 2f;
            var angle = angleOffset + candidate * GoldenAngle;
            var jitter = (Hash01(seedX + candidate * 17, seedZ - candidate * 11, 1900) - 0.5f) * 0.56f;
            var distance = Radius * radial + jitter;
            var worldX = _center.x + Mathf.Cos(angle) * distance;
            var worldZ = _center.z + Mathf.Sin(angle) * distance;
            if (IsBaseVillageBlocked(worldX, worldZ) || IsStrictDesert(worldX, worldZ) ||
                !TrySampleTerrain(worldX, worldZ, 0.68f, out var hit))
            {
                matrix = default;
                color = default;
                return false;
            }

            var meadow = RestoredMeadowMask(worldX, worldZ);
            var slopeCompression = Mathf.Lerp(1f, 0.96f, Smoothstep(0.34f, 0.78f, 1f - hit.normal.y));
            var baseHeight = (1.1f + Hash01(seedX + candidate * 31, seedZ, 2500) * 0.34f + meadow * 0.12f) *
                             slopeCompression * Mathf.Lerp(1f, 1.16f, meadow);
            var baseWidth = (1.42f + Hash01(seedX, seedZ + candidate * 37, 2700) * 0.54f) * Mathf.Lerp(1f, 1.42f, meadow);
            var hillside = Smoothstep(18f, 58f, hit.point.y) * Smoothstep(0.01f, 0.22f, 1f - hit.normal.y);
            var height = baseHeight * Mathf.Lerp(1f, 0.94f, hillside);
            var width = baseWidth * Mathf.Lerp(1f, 1.12f, hillside);
            var yaw = Hash01(seedX - candidate * 41, seedZ + candidate * 43, 2900) * 360f;
            var rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.AngleAxis(yaw, Vector3.up);
            matrix = Matrix4x4.TRS(hit.point + hit.normal * 0.018f, rotation, new Vector3(width, height, width));
            color = ResolveGrassColor(worldX, worldZ, hit.point.y, candidate, meadow, hillside);
            return true;
        }

        private bool TryMakeFlower(int candidate, out Matrix4x4 matrix, out Vector4 color)
        {
            var seedX = Mathf.FloorToInt(_center.x * 0.25f);
            var seedZ = Mathf.FloorToInt(_center.z * 0.25f);
            var flowerCandidate = candidate / 7;
            var count = Mathf.RoundToInt(FlowerCount * 6.4f);
            var radial = Mathf.Sqrt((flowerCandidate + 0.5f) / count);
            var angle = Hash01(seedX, seedZ, 4850) * Mathf.PI * 2f + flowerCandidate * GoldenAngle;
            var radius = Radius * 0.84f;
            var worldX = _center.x + Mathf.Cos(angle) * (radius * radial + (Hash01(seedX + flowerCandidate * 13, seedZ - flowerCandidate * 19, 4900) - 0.5f) * 2.2f);
            var worldZ = _center.z + Mathf.Sin(angle) * (radius * radial + (Hash01(seedX - flowerCandidate * 17, seedZ + flowerCandidate * 23, 5360) - 0.5f) * 2.2f);
            var patchWave = Mathf.Sin(worldX * 0.041f + seedX * 0.013f) + Mathf.Cos(worldZ * 0.049f - seedZ * 0.019f) * 0.52f;
            if (Hash01(seedX - flowerCandidate * 23, seedZ + flowerCandidate * 17, 4920) > Mathf.Lerp(0.62f, 1f, Smoothstep(0.18f, 0.92f, patchWave)) ||
                IsBaseVillageBlocked(worldX, worldZ) || IsStrictDesert(worldX, worldZ) ||
                !TrySampleTerrain(worldX, worldZ, 0.84f, out var hit))
            {
                matrix = default;
                color = default;
                return false;
            }

            var variant = Hash01(seedX + flowerCandidate * 29, seedZ - flowerCandidate * 31, 4940);
            var stemHeight = Mathf.Lerp(0.58f, 0.96f, variant);
            var bloomSize = 0.72f + Hash01(seedX, seedZ + flowerCandidate * 43, 5020) * 0.22f;
            var rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) *
                           Quaternion.AngleAxis(Hash01(seedX + flowerCandidate * 59, seedZ - flowerCandidate * 61, 5180) * 360f, Vector3.up);
            matrix = Matrix4x4.TRS(hit.point + hit.normal * 0.018f, rotation, new Vector3(bloomSize, stemHeight, bloomSize));
            color = ResolveFlowerColor(worldX, worldZ, variant);
            return true;
        }

        private static bool TrySampleTerrain(float x, float z, float minimumNormalY, out RaycastHit hit)
        {
            var count = Physics.RaycastNonAlloc(new Vector3(x, 900f, z), Vector3.down, TerrainHits, 1400f, ~0,
                QueryTriggerInteraction.Ignore);
            hit = default;
            var found = false;
            for (var index = 0; index < count; index++)
            {
                var candidate = TerrainHits[index];
                if (candidate.collider == null || candidate.normal.y < minimumNormalY) continue;
                var name = candidate.collider.gameObject.name;
                if (!string.Equals(name, OpenWorldTerrainName, StringComparison.Ordinal) &&
                    name.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (found && candidate.point.y <= hit.point.y) continue;
                hit = candidate;
                found = true;
            }

            return found;
        }

        private static bool IsBaseVillageBlocked(float x, float z)
        {
            var absX = Mathf.Abs(x);
            var absZ = Mathf.Abs(z);
            var max = Mathf.Max(absX, absZ);
            if (max <= 260f) return true;
            if (max > 328f) return false;
            var radiusSquared = x * x + z * z;
            return absX < 50f || absZ < 50f || radiusSquared < 72f * 72f ||
                   radiusSquared > 42f * 42f && radiusSquared < 58f * 58f ||
                   radiusSquared > 125f * 125f && radiusSquared < 145f * 145f;
        }

        private static bool IsStrictDesert(float x, float z)
        {
            return x >= 1792f && x < 2304f && z >= -2304f && z < -1792f;
        }

        private static float RestoredMeadowMask(float x, float z)
        {
            var distance = Mathf.Sqrt(x * x + z * z);
            return 1f - Smoothstep(260f, 620f, distance);
        }

        private static Vector4 ResolveGrassColor(float x, float z, float height, int candidate, float meadow, float hillside)
        {
            var seed = Hash01(Mathf.FloorToInt(x * 0.44f), Mathf.FloorToInt(z * 0.44f), 8200 + candidate % 1000);
            var baseColor = Color.Lerp(GrassDark, GrassLight, 0.56f + seed * 0.28f);
            baseColor = Color.Lerp(baseColor, GrassHighlight, Smoothstep(0.62f, 1f, seed) * 0.34f);
            baseColor = Color.Lerp(baseColor, MeadowGreen, 0.18f + meadow * 0.18f);
            var hillsideColor = Color.Lerp(HillsideDark, HillsideLight, Smoothstep(32f, 90f, height));
            baseColor = Color.Lerp(baseColor, hillsideColor, hillside * 0.74f);
            baseColor *= 0.98f + Hash01(Mathf.FloorToInt(x * 0.18f), Mathf.FloorToInt(z * 0.18f), 8300) * 0.1f;
            return new Vector4(Mathf.Clamp01(baseColor.r), Mathf.Clamp01(baseColor.g), Mathf.Clamp01(baseColor.b), 1f);
        }

        private static Vector4 ResolveFlowerColor(float x, float z, float variant)
        {
            var swamp = z >= -1792f && z < -1280f && x >= -256f && x < 256f;
            var palette = swamp ? SwampFlowerPalette : MeadowFlowerPalette;
            var paletteIndex = Mathf.Clamp(Mathf.FloorToInt(variant * palette.Length), 0, palette.Length - 1);
            var selected = palette[paletteIndex];
            return new Vector4(selected.r, selected.g, selected.b, 1f);
        }

        private static void AddInstance(List<InstanceBatch> batches, Matrix4x4 matrix, Vector4 color)
        {
            if (batches.Count == 0 || batches[^1].Count >= InstancesPerBatch) batches.Add(new InstanceBatch());
            batches[^1].Add(matrix, color);
        }

        private static void SwapBatches(List<InstanceBatch> active, List<InstanceBatch> building)
        {
            active.Clear();
            active.AddRange(building);
            building.Clear();
        }

        private static void DrawBatches(List<InstanceBatch> batches, Mesh mesh, Material material)
        {
            foreach (var batch in batches)
            {
                if (batch.Count <= 0) continue;
                batch.Prepare();
                Graphics.DrawMeshInstanced(mesh, 0, material, batch.Matrices, batch.Count, batch.Properties,
                    ShadowCastingMode.Off, false, RenderLayer, null, LightProbeUsage.Off);
            }
        }

        private static Vector3 QuantizeCenter(Vector3 position)
        {
            return new Vector3(
                Mathf.Round(position.x / CenterStep) * CenterStep,
                position.y,
                Mathf.Round(position.z / CenterStep) * CenterStep);
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            var x = left.x - right.x;
            var z = left.z - right.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        public static float Hash01(float x, float z, float salt = 0f)
        {
            var value = Mathf.Sin(x * 127.1f + z * 311.7f + salt * 74.7f) * 43758.5453123f;
            return value - Mathf.Floor(value);
        }

        private static float Smoothstep(float edge0, float edge1, float value)
        {
            var t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static Mesh CreateGrassClusterMesh()
        {
            const int cards = 4;
            var vertices = new Vector3[cards * 4];
            var colors = new Color[cards * 4];
            var uvs = new Vector2[cards * 4];
            var triangles = new int[cards * 6];
            var root = GrassMeshRoot;
            var tip = GrassMeshTip;
            for (var card = 0; card < cards; card++)
            {
                var angle = card / (float)cards * Mathf.PI;
                var side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var width = card % 2 == 0 ? 0.72f : 0.58f;
                var vertex = card * 4;
                vertices[vertex] = -side * width;
                vertices[vertex + 1] = side * width;
                vertices[vertex + 2] = -side * width * 0.78f + Vector3.up;
                vertices[vertex + 3] = side * width * 0.78f + Vector3.up;
                colors[vertex] = colors[vertex + 1] = root;
                colors[vertex + 2] = colors[vertex + 3] = tip;
                uvs[vertex] = new Vector2(0f, 0f);
                uvs[vertex + 1] = new Vector2(1f, 0f);
                uvs[vertex + 2] = new Vector2(0f, 1f);
                uvs[vertex + 3] = new Vector2(1f, 1f);
                var triangle = card * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }
            var mesh = new Mesh { name = "ReactBotwGrassCluster", vertices = vertices, colors = colors, uv = uvs, triangles = triangles };
            mesh.RecalculateBounds();
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(4f, 3f, 4f));
            return mesh;
        }

        private static Mesh CreateFlowerMesh()
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var stem = FlowerStem;
            for (var card = 0; card < 2; card++)
            {
                var angle = card * Mathf.PI * 0.5f;
                var side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.045f;
                var baseIndex = vertices.Count;
                vertices.Add(-side); vertices.Add(side); vertices.Add(-side + Vector3.up); vertices.Add(side + Vector3.up);
                colors.Add(stem); colors.Add(stem); colors.Add(stem); colors.Add(stem);
                triangles.Add(baseIndex); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 3);
            }
            const int petals = 6;
            for (var petal = 0; petal < petals; petal++)
            {
                var angle = petal / (float)petals * Mathf.PI * 2f;
                var next = (petal + 1f) / petals * Mathf.PI * 2f;
                var middle = (angle + next) * 0.5f;
                var baseIndex = vertices.Count;
                vertices.Add(new Vector3(0f, 1f, 0f));
                vertices.Add(new Vector3(Mathf.Cos(angle) * 0.16f, 1f, Mathf.Sin(angle) * 0.16f));
                vertices.Add(new Vector3(Mathf.Cos(middle) * 0.48f, 1.02f, Mathf.Sin(middle) * 0.48f));
                vertices.Add(new Vector3(Mathf.Cos(next) * 0.16f, 1f, Mathf.Sin(next) * 0.16f));
                colors.Add(Color.white); colors.Add(Color.white); colors.Add(Color.white); colors.Add(Color.white);
                triangles.Add(baseIndex); triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 3);
            }
            var mesh = new Mesh { name = "ReactBotwWildflower", vertices = vertices.ToArray(), colors = colors.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateBounds();
            return mesh;
        }

        private sealed class InstanceBatch
        {
            public readonly Matrix4x4[] Matrices = new Matrix4x4[InstancesPerBatch];
            private readonly Vector4[] _colors = new Vector4[InstancesPerBatch];
            public readonly MaterialPropertyBlock Properties = new();
            private bool _dirty;
            public int Count { get; private set; }

            public void Add(Matrix4x4 matrix, Vector4 color)
            {
                Matrices[Count] = matrix;
                _colors[Count] = color;
                Count++;
                _dirty = true;
            }

            public void Prepare()
            {
                if (!_dirty) return;
                Properties.SetVectorArray("_InstanceColor", _colors);
                _dirty = false;
            }
        }
    }
}
