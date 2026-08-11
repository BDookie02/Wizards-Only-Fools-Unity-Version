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
        public const float FlowerStemMinimum = 1.38f;
        public const float FlowerStemMaximum = 1.68f;
        public const float FlowerBloomMinimum = 0.64f;
        public const float FlowerBloomMaximum = 0.86f;
        public const float BladeAlphaCutoff = 0.14f;
        public const int GrassClusterCardCount = 4;
        public const float SlopeUprightBlend = 0.82f;
        public const int BuildBudgetCheckInterval = 4;
        public const float TerrainGrassDetailStrength = 0.14f;
        public const float TerrainGrassDetailScale = 0.2f;
        private const int InstancesPerBatch = 1023;
        public const int MaxCandidatesPerFrameDesktop = 384;
        public const int MaxCandidatesPerFrameMobile = 256;
        public const double DesktopBuildBudgetMilliseconds = 3.5;
        public const double MobileBuildBudgetMilliseconds = 2.0;
        private const string OpenWorldTerrainName = "ReactSurvivalOpenWorldBaseRegion";
        private static readonly RaycastHit[] TerrainHits = new RaycastHit[16];
        private static readonly Color GrassDark = new Color32(0x3c, 0x8b, 0x2d, 0xff);
        private static readonly Color GrassLight = new Color32(0x62, 0xbd, 0x37, 0xff);
        private static readonly Color GrassHighlight = new Color32(0xb9, 0xef, 0x5b, 0xff);
        private static readonly Color MeadowGreen = new Color32(0x83, 0xc7, 0x50, 0xff);
        private static readonly Color HillsideDark = new Color32(0x66, 0x7f, 0x43, 0xff);
        private static readonly Color HillsideLight = new Color32(0x89, 0x92, 0x5a, 0xff);
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
        private readonly Dictionary<Mesh, TerrainMeshSurfaceData> _terrainSurfaceData = new();
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
        private bool _hasCompletedBuild;
        private float _nextViewerResolveAt;
        private float _buildStartedAt;
        private double _maxBuildSliceMilliseconds;

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
                renderQueue = (int)RenderQueue.AlphaTest
            };
            _grassMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 0.98f));
            _grassMaterial.SetFloat("_Radius", Radius + EdgeFade * 0.72f);
            _grassMaterial.SetFloat("_FadeWidth", EdgeFade);
            _grassMaterial.SetFloat("_Cutoff", BladeAlphaCutoff);
            _grassMaterial.SetFloat("_SlopeUprightBlend", SlopeUprightBlend);
            ConfigureTerrainGrassDetail();
            _flowerMaterial = new Material(foliageShader)
            {
                name = "ReactBotwWildflowerRuntimeMaterial",
                enableInstancing = true
            };
            Debug.Log($"[WOF-AUTOMATION] BOTW_GRASS_RUNTIME_READY radius={Radius:F0} blades={BladeCount} flowers={FlowerCount}");
        }

        private static void ConfigureTerrainGrassDetail()
        {
            var terrain = GameObject.Find(OpenWorldTerrainName);
            var material = terrain?.GetComponent<MeshRenderer>()?.sharedMaterial;
            if (material == null || !material.HasProperty("_SurvivalGrassDetailStrength") ||
                !material.HasProperty("_SurvivalGrassDetailScale"))
                return;

            material.SetFloat("_SurvivalGrassDetailStrength", TerrainGrassDetailStrength);
            material.SetFloat("_SurvivalGrassDetailScale", TerrainGrassDetailScale);
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

            if (ShouldStartBuild(
                    _building,
                    _hasCompletedBuild,
                    HorizontalDistance(_viewer.position, _center)))
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
            _maxBuildSliceMilliseconds = 0d;
            Debug.Log($"[WOF-AUTOMATION] BOTW_GRASS_BUILD_STARTED center={center.x:F0},{center.z:F0}");
        }

        private void ContinueBuild()
        {
            var mobile = WofPerformanceModeRuntime.IsMobilePerformanceMode;
            var frameLimit = mobile ? MaxCandidatesPerFrameMobile : MaxCandidatesPerFrameDesktop;
            var budgetMilliseconds = mobile ? MobileBuildBudgetMilliseconds : DesktopBuildBudgetMilliseconds;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var processed = 0;
            while (_candidate < CandidateCount && _acceptedGrass < BladeCount && processed < frameLimit)
            {
                var candidate = _candidate++;
                if (TryMakeGrass(candidate, out var matrix, out var color))
                {
                    AddInstance(_buildingGrass, matrix, color);
                    _acceptedGrass++;
                }

                if (_acceptedFlowers < FlowerCount && candidate % 7 == 0 && TryMakeFlower(candidate, out matrix, out color))
                {
                    AddInstance(_buildingFlowers, matrix, color);
                    _acceptedFlowers++;
                }

                processed++;
                if (processed % BuildBudgetCheckInterval == 0 &&
                    timer.Elapsed.TotalMilliseconds >= budgetMilliseconds) break;
            }
            timer.Stop();
            _maxBuildSliceMilliseconds = Math.Max(_maxBuildSliceMilliseconds, timer.Elapsed.TotalMilliseconds);

            if (_candidate < CandidateCount && _acceptedGrass < BladeCount) return;
            SwapBatches(_activeGrass, _buildingGrass);
            SwapBatches(_activeFlowers, _buildingFlowers);
            _building = false;
            _hasCompletedBuild = true;
            Debug.Log($"[WOF-AUTOMATION] BOTW_GRASS_BUILD_COMPLETE center={_center.x:F0},{_center.z:F0} blades={_acceptedGrass} flowers={_acceptedFlowers} ms={(Time.realtimeSinceStartup - _buildStartedAt) * 1000f:F0} maxSliceMs={_maxBuildSliceMilliseconds:F2}");
        }

        private bool TryMakeGrass(int candidate, out Matrix4x4 matrix, out Vector4 color)
        {
            var seedX = Mathf.FloorToInt(_center.x * 0.25f);
            var seedZ = Mathf.FloorToInt(_center.z * 0.25f);
            var distribution = GetReactGrassDistributionPoint(candidate, CandidateCount, seedX, seedZ);
            var worldX = _center.x + distribution.x * Radius;
            var worldZ = _center.z + distribution.y * Radius;
            var biomeCoverage = (float)WofSurvivalTerrainMath.GetBiomeGrassCoverageAtWorld(worldX, worldZ);
            if (IsAuthoredSurfaceBlocked(worldX, worldZ) ||
                Hash01(seedX + candidate * 19, seedZ - candidate * 23, 4187) > biomeCoverage ||
                !TrySampleTerrain(worldX, worldZ, 0.68f, out var hit, out var surfaceNormal))
            {
                matrix = default;
                color = default;
                return false;
            }

            var meadow = RestoredMeadowMask(worldX, worldZ);
            var slopeCompression = Mathf.Lerp(1f, 0.96f, Smoothstep(0.34f, 0.78f, 1f - surfaceNormal.y));
            var baseHeight =
                (1.1f + Hash01(seedX + candidate * 31, seedZ, 2500) * 0.34f + meadow * 0.12f) *
                slopeCompression * Mathf.Lerp(1f, 1.16f, meadow);
            var baseWidth =
                (1.42f + Hash01(seedX, seedZ + candidate * 37, 2700) * 0.54f) *
                Mathf.Lerp(1f, 1.42f, meadow);
            var hillside = Smoothstep(18f, 58f, hit.point.y) * Smoothstep(0.01f, 0.22f, 1f - surfaceNormal.y);
            var slopeSurfaceTuck = Smoothstep(0.04f, 0.24f, 1f - surfaceNormal.y);
            var midDistanceFill = meadow * Smoothstep(28f, Radius * 0.82f, distribution.magnitude * Radius);
            var height = baseHeight *
                         Mathf.Lerp(1f, 1.28f, midDistanceFill) *
                         Mathf.Lerp(1f, 0.96f, slopeSurfaceTuck) *
                         Mathf.Lerp(1f, 0.94f, hillside);
            var width = baseWidth *
                        Mathf.Lerp(1f, 1.42f, midDistanceFill) *
                        Mathf.Lerp(1f, 1.04f, hillside);
            var yaw = Hash01(seedX - candidate * 41, seedZ + candidate * 43, 2900) * 360f;
            var rotation = GetSurfaceAlignedRotation(surfaceNormal, yaw);
            matrix = Matrix4x4.TRS(hit.point + surfaceNormal * 0.018f, rotation, new Vector3(width, height, width));
            color = ResolveGrassColor(worldX, worldZ, hit.point.y, candidate, meadow, hillside);
            return true;
        }

        private bool TryMakeFlower(int candidate, out Matrix4x4 matrix, out Vector4 color)
        {
            var seedX = Mathf.FloorToInt(_center.x * 0.25f);
            var seedZ = Mathf.FloorToInt(_center.z * 0.25f);
            var flowerCandidate = candidate / 7;
            var radius = Radius * 0.84f;
            var distribution = GetIrregularDistributionPoint(flowerCandidate, seedX + 311, seedZ - 197);
            var worldX = _center.x + distribution.x * radius;
            var worldZ = _center.z + distribution.y * radius;
            var patchWave = Mathf.Sin(worldX * 0.041f + seedX * 0.013f) + Mathf.Cos(worldZ * 0.049f - seedZ * 0.019f) * 0.52f;
            if (distribution.sqrMagnitude > 1f ||
                Hash01(seedX - flowerCandidate * 23, seedZ + flowerCandidate * 17, 4920) > Mathf.Lerp(0.62f, 1f, Smoothstep(0.18f, 0.92f, patchWave)) ||
                IsAuthoredSurfaceBlocked(worldX, worldZ) ||
                Hash01(seedX - flowerCandidate * 11, seedZ + flowerCandidate * 13, 4911) >
                WofSurvivalTerrainMath.GetBiomeGrassCoverageAtWorld(worldX, worldZ) ||
                !TrySampleTerrain(worldX, worldZ, 0.84f, out var hit, out var surfaceNormal))
            {
                matrix = default;
                color = default;
                return false;
            }

            var variant = Hash01(seedX + flowerCandidate * 29, seedZ - flowerCandidate * 31, 4940);
            // Keep the stem rooted on the sampled slope while placing the bloom
            // just above the local grass canopy so flowers survive overhead views.
            var canopy = ValueNoise2D(worldX * 0.017f, worldZ * 0.017f, 6100);
            var stemHeight = Mathf.Lerp(FlowerStemMinimum, FlowerStemMaximum, variant) + canopy * 0.08f;
            var bloomSize = Mathf.Lerp(
                FlowerBloomMinimum,
                FlowerBloomMaximum,
                Hash01(seedX, seedZ + flowerCandidate * 43, 5020));
            var rotation = GetSurfaceAlignedRotation(
                surfaceNormal,
                Hash01(seedX + flowerCandidate * 59, seedZ - flowerCandidate * 61, 5180) * 360f);
            matrix = Matrix4x4.TRS(hit.point + surfaceNormal * 0.018f, rotation, new Vector3(bloomSize, stemHeight, bloomSize));
            color = ResolveFlowerColor(worldX, worldZ, variant);
            return true;
        }

        private bool TrySampleTerrain(
            float x,
            float z,
            float minimumNormalY,
            out RaycastHit hit,
            out Vector3 surfaceNormal)
        {
            var count = Physics.RaycastNonAlloc(new Vector3(x, 900f, z), Vector3.down, TerrainHits, 1400f, ~0,
                QueryTriggerInteraction.Ignore);
            hit = default;
            surfaceNormal = Vector3.up;
            var found = false;
            for (var index = 0; index < count; index++)
            {
                var candidate = TerrainHits[index];
                if (candidate.collider == null) continue;
                var name = candidate.collider.gameObject.name;
                if (!string.Equals(name, OpenWorldTerrainName, StringComparison.Ordinal) &&
                    name.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var candidateNormal = GetSmoothedSurfaceNormal(candidate);
                if (candidateNormal.y < minimumNormalY) continue;
                if (found && candidate.point.y <= hit.point.y) continue;
                hit = candidate;
                surfaceNormal = candidateNormal;
                found = true;
            }

            return found;
        }

        internal static Quaternion GetSurfaceAlignedRotation(Vector3 surfaceNormal, float yawDegrees)
        {
            var normalized = surfaceNormal.sqrMagnitude > 0.000001f ? surfaceNormal.normalized : Vector3.up;
            return Quaternion.FromToRotation(Vector3.up, normalized) *
                   Quaternion.AngleAxis(yawDegrees, Vector3.up);
        }

        internal static Vector3 InterpolateSurfaceNormal(
            Vector3 first,
            Vector3 second,
            Vector3 third,
            Vector3 barycentric)
        {
            var normal = first * barycentric.x + second * barycentric.y + third * barycentric.z;
            return normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
        }

        private Vector3 GetSmoothedSurfaceNormal(RaycastHit hit)
        {
            if (hit.collider is not MeshCollider meshCollider || hit.triangleIndex < 0 || meshCollider.sharedMesh == null)
                return hit.normal.normalized;

            var mesh = meshCollider.sharedMesh;
            if (!_terrainSurfaceData.TryGetValue(mesh, out var surfaceData))
            {
                surfaceData = new TerrainMeshSurfaceData(mesh.triangles, mesh.normals);
                _terrainSurfaceData.Add(mesh, surfaceData);
            }

            var triangleOffset = hit.triangleIndex * 3;
            if (triangleOffset < 0 || triangleOffset + 2 >= surfaceData.Triangles.Length ||
                surfaceData.Normals.Length == 0)
                return hit.normal.normalized;

            var firstIndex = surfaceData.Triangles[triangleOffset];
            var secondIndex = surfaceData.Triangles[triangleOffset + 1];
            var thirdIndex = surfaceData.Triangles[triangleOffset + 2];
            if (firstIndex >= surfaceData.Normals.Length || secondIndex >= surfaceData.Normals.Length ||
                thirdIndex >= surfaceData.Normals.Length)
                return hit.normal.normalized;

            var localNormal = InterpolateSurfaceNormal(
                surfaceData.Normals[firstIndex],
                surfaceData.Normals[secondIndex],
                surfaceData.Normals[thirdIndex],
                hit.barycentricCoordinate);
            var worldNormal = meshCollider.transform.TransformDirection(localNormal).normalized;
            return worldNormal.y < 0f ? -worldNormal : worldNormal;
        }

        internal static bool IsAuthoredSurfaceBlocked(float x, float z)
        {
            var mountainX = x - WofMountainVillageLayout.WorldOrigin.x;
            var mountainZ = z - WofMountainVillageLayout.WorldOrigin.z;
            var mountainRadius = WofMountainVillageLayout.PerimeterShoulderRadius - 8f;
            if (mountainX * mountainX + mountainZ * mountainZ <= mountainRadius * mountainRadius)
                return true;

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

        internal static bool ShouldStartBuild(bool building, bool hasCompletedBuild, float distanceFromCenter)
        {
            return !building && (!hasCompletedBuild || distanceFromCenter >= RecenterDistance);
        }

        public static float Hash01(float x, float z, float salt = 0f)
        {
            var value = Mathf.Sin(x * 127.1f + z * 311.7f + salt * 74.7f) * 43758.5453123f;
            return value - Mathf.Floor(value);
        }

        internal static Vector2 GetIrregularDistributionPoint(int candidate, int seedX, int seedZ)
        {
            var radial = Mathf.Sqrt(Hash01(candidate * 17 + seedX, candidate * 47 - seedZ, 1911));
            var angle = Hash01(candidate * 71 - seedX, candidate * 29 + seedZ, 1979) * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle) * radial, Mathf.Sin(angle) * radial);
        }

        internal static Vector2 GetReactGrassDistributionPoint(
            int candidate,
            int candidateCount,
            int seedX,
            int seedZ)
        {
            candidateCount = Mathf.Max(1, candidateCount);
            var radialCandidate = (candidate * 8191) % candidateCount;
            if (radialCandidate < 0) radialCandidate += candidateCount;
            var radial = Mathf.Sqrt((radialCandidate + 0.5f) / candidateCount);
            var angleOffset = Hash01(seedX, seedZ, 1850) * Mathf.PI * 2f;
            var angle = angleOffset + candidate * 2.39996323f;
            var jitter = (Hash01(seedX + candidate * 17, seedZ - candidate * 11, 1900) - 0.5f) * 0.56f;
            var normalizedDistance = (Radius * radial + jitter) / Radius;
            return new Vector2(Mathf.Cos(angle) * normalizedDistance, Mathf.Sin(angle) * normalizedDistance);
        }

        private static float ValueNoise2D(float x, float z, float salt)
        {
            var x0 = Mathf.FloorToInt(x);
            var z0 = Mathf.FloorToInt(z);
            var tx = Smoothstep(0f, 1f, x - x0);
            var tz = Smoothstep(0f, 1f, z - z0);
            var a = Mathf.Lerp(Hash01(x0, z0, salt), Hash01(x0 + 1, z0, salt), tx);
            var b = Mathf.Lerp(Hash01(x0, z0 + 1, salt), Hash01(x0 + 1, z0 + 1, salt), tx);
            return Mathf.Lerp(a, b, tz);
        }

        private static float Smoothstep(float edge0, float edge1, float value)
        {
            var t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        internal static Mesh CreateGrassClusterMesh()
        {
            var vertices = new List<Vector3>(GrassClusterCardCount * 4);
            var colors = new List<Color>(vertices.Capacity);
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(GrassClusterCardCount * 6);
            var root = new Color32(0x85, 0xd2, 0x4a, 0xff);
            var tip = new Color32(0xf0, 0xff, 0x90, 0xff);
            for (var card = 0; card < GrassClusterCardCount; card++)
            {
                var angle = card / (float)GrassClusterCardCount * Mathf.PI;
                var side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var width = card % 2 == 0 ? 0.72f : 0.58f;
                var vertex = vertices.Count;
                vertices.Add(-side * width);
                vertices.Add(side * width);
                vertices.Add(-side * (width * 0.78f) + Vector3.up);
                vertices.Add(side * (width * 0.78f) + Vector3.up);
                colors.Add(root); colors.Add(root); colors.Add(tip); colors.Add(tip);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f));
                triangles.Add(vertex); triangles.Add(vertex + 2); triangles.Add(vertex + 1);
                triangles.Add(vertex + 1); triangles.Add(vertex + 2); triangles.Add(vertex + 3);
            }

            var mesh = new Mesh
            {
                name = "ReactBotwGrassCluster",
                vertices = vertices.ToArray(),
                colors = colors.ToArray(),
                uv = uvs.ToArray(),
                triangles = triangles.ToArray()
            };
            mesh.RecalculateBounds();
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

        private sealed class TerrainMeshSurfaceData
        {
            public readonly int[] Triangles;
            public readonly Vector3[] Normals;

            public TerrainMeshSurfaceData(int[] triangles, Vector3[] normals)
            {
                Triangles = triangles ?? Array.Empty<int>();
                Normals = normals ?? Array.Empty<Vector3>();
            }
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
