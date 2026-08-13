using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalAmbientBirdRuntime : MonoBehaviour
    {
        private readonly Dictionary<string, BirdBatch> _batches = new(StringComparer.Ordinal);
        private WofPlayerController _player;
        private WofAmbientBirdFlock _flock;
        private int _centerX = int.MinValue;
        private int _centerZ = int.MinValue;
        private float _nextResolveAt;
        private float _nextVisualUpdateAt;
        private float _ambientReadyAt;
        private bool _hasFlock;
        private bool _grassInspectionView;
        private bool _probe;
        private string _probeBiome = "jungle";
        private bool _probePositioned;
        private bool _probePassed;
        private int _probeBirdIndex;
        private float _probeStartedAt;
        private Vector3 _probeStartPosition;

        public int BirdCount => _hasFlock ? _flock.Birds.Length : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            if (FindFirstObjectByType<WofSurvivalAmbientBirdRuntime>() != null) return;
            new GameObject("ReactSurvivalAmbientBirdRuntime").AddComponent<WofSurvivalAmbientBirdRuntime>();
        }

        private void Awake()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-grass-view-probe", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("--wof-grass-view-probe=", StringComparison.OrdinalIgnoreCase))
                    _grassInspectionView = true;
                if (argument.Equals("--wof-ambient-bird-probe", StringComparison.OrdinalIgnoreCase))
                    _probe = true;
                const string prefix = "--wof-ambient-bird-probe=";
                if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                _probe = true;
                _probeBiome = argument.Substring(prefix.Length).Trim().ToLowerInvariant();
            }
        }

        private void OnDestroy()
        {
            foreach (var batch in _batches.Values) batch.Dispose();
            _batches.Clear();
        }

        private void Update()
        {
            var survival = WofBootstrap.Instance != null && WofBootstrap.Instance.IsSurvivalSession;
            if (!survival)
            {
                _hasFlock = false;
                return;
            }

            ResolvePlayer();
            if (_player == null) return;
            if (_probe && !_probePositioned)
            {
                PositionProbeInWilderness();
                if (!_probePositioned) return;
            }

            var nextX = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.x);
            var nextZ = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.z);
            if (nextX != _centerX || nextZ != _centerZ)
            {
                _ambientReadyAt = Time.unscaledTime + WofSurvivalAmbientBirdRules.GetAmbientReadyDelaySeconds(
                    nextX, nextZ, WofPerformanceModeRuntime.IsMobilePerformanceMode);
                Rebuild(nextX, nextZ, survival, false);
            }
            else if (!_hasFlock && Time.unscaledTime >= _ambientReadyAt &&
                     WofSurvivalAmbientBirdRules.ShouldShowBirds(
                         survival, true, _grassInspectionView, nextX, nextZ, 0))
            {
                Rebuild(nextX, nextZ, survival, true);
            }
            if (!_hasFlock) return;

            var elapsed = Time.timeAsDouble;
            if (_nextVisualUpdateAt <= Time.unscaledTime)
            {
                UpdateMatrices(elapsed);
                if (_probe) TrackProbeBird(elapsed);
                _nextVisualUpdateAt = Time.unscaledTime +
                                      (WofPerformanceModeRuntime.IsMobilePerformanceMode
                                          ? WofSurvivalAmbientBirdRules.MobileUpdateInterval
                                          : 0f);
            }
            DrawBatches();
            UpdateProbePass(elapsed);
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

        private void PositionProbeInWilderness()
        {
            var (chunkX, chunkZ) = GetProbeChunk(_probeBiome);
            var seedFlock = WofSurvivalAmbientBirdRules.MakeFlock(chunkX, chunkZ);
            var birdIndex = FindNearestBirdIndex(seedFlock);
            var elapsed = Time.timeAsDouble;
            var birdPosition = WofSurvivalAmbientBirdRules.GetBirdWorldPosition(
                seedFlock, seedFlock.Birds[birdIndex], elapsed);
            var chunkCenter = new Vector3(
                chunkX * WofSurvivalTerrainMath.BlockSize, 0f,
                chunkZ * WofSurvivalTerrainMath.BlockSize);
            var outward = birdPosition - chunkCenter;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
            var birdRadius = new Vector2(
                seedFlock.Birds[birdIndex].LocalPosition.x,
                seedFlock.Birds[birdIndex].LocalPosition.z).magnitude;
            var cameraRadius = Mathf.Min(220f, Mathf.Max(0f, birdRadius - 12f));
            var viewPosition = chunkCenter + outward * cameraRadius;
            viewPosition.y = birdPosition.y - 1.65f;
            var yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
            if (!_player.PrepareForAutomationStaticViewProbe(viewPosition, yaw, 0f)) return;
            _probePositioned = true;
            _probeBirdIndex = birdIndex;
            _probeStartedAt = Time.unscaledTime;
            _probeStartPosition = birdPosition;
            Debug.Log(
                $"[WOF-AUTOMATION] AMBIENT_BIRD_PROBE_POSITIONED biome={WofSurvivalTerrainMath.GetBiomeName(seedFlock.Biome)} " +
                $"chunk={chunkX}:{chunkZ} species={seedFlock.Birds[birdIndex].Species.Name} count={seedFlock.Birds.Length}");
        }

        private void Rebuild(int chunkX, int chunkZ, bool survival, bool ambientLifeReady)
        {
            _centerX = chunkX;
            _centerZ = chunkZ;
            _hasFlock = WofSurvivalAmbientBirdRules.ShouldShowBirds(
                survival, ambientLifeReady, _grassInspectionView, chunkX, chunkZ, 0);
            foreach (var batch in _batches.Values) batch.Count = 0;
            if (_hasFlock)
            {
                _flock = WofSurvivalAmbientBirdRules.MakeFlock(chunkX, chunkZ);
                for (var index = 0; index < _flock.Birds.Length; index++)
                {
                    var species = _flock.Birds[index].Species;
                    if (!_batches.ContainsKey(species.Name))
                        _batches.Add(species.Name, new BirdBatch(species));
                }
            }
            _nextVisualUpdateAt = 0f;
            Debug.Log(
                $"[WOF-AUTOMATION] SURVIVAL_AMBIENT_BIRDS_READY center={chunkX}:{chunkZ} " +
                $"biome={WofSurvivalTerrainMath.GetBiomeName(WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ))} " +
                $"birds={BirdCount} villageExcluded={!_hasFlock && WofSurvivalTerrainMath.IsAuthoredChunk(chunkX, chunkZ)}");
        }

        private void UpdateMatrices(double elapsed)
        {
            foreach (var batch in _batches.Values) batch.Count = 0;
            var groupRotation = Quaternion.AngleAxis(
                WofSurvivalAmbientBirdRules.GetFlockRotationRadians(_flock, elapsed) * Mathf.Rad2Deg,
                Vector3.up);
            var groupMatrix = Matrix4x4.TRS(
                new Vector3(
                    _flock.ChunkX * WofSurvivalTerrainMath.BlockSize,
                    WofSurvivalAmbientBirdRules.GetFlockWorldY(_flock, elapsed),
                    _flock.ChunkZ * WofSurvivalTerrainMath.BlockSize),
                groupRotation,
                Vector3.one);
            for (var index = 0; index < _flock.Birds.Length; index++)
            {
                var bird = _flock.Birds[index];
                var parent = groupMatrix * Matrix4x4.TRS(
                    bird.LocalPosition,
                    Quaternion.AngleAxis(bird.Tilt * Mathf.Rad2Deg, Vector3.up),
                    Vector3.one * bird.Scale);
                var batch = _batches[bird.Species.Name];
                batch.Add(
                    parent * Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(90f, Vector3.forward), Vector3.one),
                    parent * Matrix4x4.TRS(
                        new Vector3(-bird.Species.WingLength * 0.45f, 0f, 0f),
                        Quaternion.Euler(0f, 0.1f * Mathf.Rad2Deg,
                            (0.22f + Mathf.Sin(bird.WingPhase) * 0.08f) * Mathf.Rad2Deg), Vector3.one),
                    parent * Matrix4x4.TRS(
                        new Vector3(bird.Species.WingLength * 0.45f, 0f, 0f),
                        Quaternion.Euler(0f, -0.1f * Mathf.Rad2Deg,
                            (-0.22f - Mathf.Sin(bird.WingPhase) * 0.08f) * Mathf.Rad2Deg), Vector3.one),
                    parent * Matrix4x4.TRS(
                        new Vector3(0f, -0.02f, -0.92f),
                        Quaternion.AngleAxis(90f, Vector3.right), Vector3.one));
            }
        }

        private void TrackProbeBird(double elapsed)
        {
            if (!_probePositioned || _probeBirdIndex < 0 || _probeBirdIndex >= _flock.Birds.Length) return;
            var birdPosition = WofSurvivalAmbientBirdRules.GetBirdWorldPosition(
                _flock, _flock.Birds[_probeBirdIndex], elapsed);
            var chunkCenter = new Vector3(
                _flock.ChunkX * WofSurvivalTerrainMath.BlockSize, 0f,
                _flock.ChunkZ * WofSurvivalTerrainMath.BlockSize);
            var outward = birdPosition - chunkCenter;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
            var birdRadius = new Vector2(
                _flock.Birds[_probeBirdIndex].LocalPosition.x,
                _flock.Birds[_probeBirdIndex].LocalPosition.z).magnitude;
            var cameraRadius = Mathf.Min(220f, Mathf.Max(0f, birdRadius - 12f));
            var viewPosition = chunkCenter + outward * cameraRadius;
            viewPosition.y = birdPosition.y - 1.65f;
            var yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
            _player.PrepareForAutomationStaticViewProbe(viewPosition, yaw, 0f);
        }

        private void UpdateProbePass(double elapsed)
        {
            if (!_probe || !_probePositioned || _probePassed || Time.unscaledTime - _probeStartedAt < 3f) return;
            var bird = _flock.Birds[_probeBirdIndex];
            var current = WofSurvivalAmbientBirdRules.GetBirdWorldPosition(_flock, bird, elapsed);
            var moved = Vector3.Distance(_probeStartPosition, current);
            if (moved <= 0.5f) return;
            _probePassed = true;
            Debug.Log(
                $"[WOF-AUTOMATION] AMBIENT_BIRD_PROBE_PASS biome={WofSurvivalTerrainMath.GetBiomeName(_flock.Biome)} " +
                $"species={bird.Species.Name} birds={BirdCount} moved={moved:F2} mobileHz={(WofPerformanceModeRuntime.IsMobilePerformanceMode ? 24 : 0)}");
        }

        private void DrawBatches()
        {
            if (!SystemInfo.supportsInstancing) return;
            foreach (var batch in _batches.Values) batch.Draw(gameObject.layer);
        }

        private static int FindNearestBirdIndex(WofAmbientBirdFlock flock)
        {
            var bestIndex = 0;
            var bestRadius = float.PositiveInfinity;
            for (var index = 0; index < flock.Birds.Length; index++)
            {
                var position = flock.Birds[index].LocalPosition;
                var radius = position.x * position.x + position.z * position.z;
                if (radius >= bestRadius) continue;
                bestRadius = radius;
                bestIndex = index;
            }
            return bestIndex;
        }

        private static (int X, int Z) GetProbeChunk(string biome)
        {
            return biome switch
            {
                "plains" => (0, -1),
                "desert" => (1, 0),
                "swamp" => (-2, 0),
                "mushroom" => (1, -2),
                "tallgrass" => (-1, -2),
                _ => (-2, -2)
            };
        }

        private sealed class BirdBatch : IDisposable
        {
            private readonly Mesh _bodyMesh;
            private readonly Mesh _wingMesh;
            private readonly Mesh _tailMesh;
            private readonly Material _bodyMaterial;
            private readonly Material _wingMaterial;
            private readonly Material _tailMaterial;
            private readonly Matrix4x4[] _bodyMatrices = new Matrix4x4[16];
            private readonly Matrix4x4[] _leftWingMatrices = new Matrix4x4[16];
            private readonly Matrix4x4[] _rightWingMatrices = new Matrix4x4[16];
            private readonly Matrix4x4[] _tailMatrices = new Matrix4x4[16];

            public BirdBatch(WofAmbientBirdSpecies species)
            {
                _bodyMesh = CreateConeMesh(0.45f, species.BodyLength, 5, $"WOF_{species.Name}_Body");
                _wingMesh = CreateQuadMesh(species.WingLength, species.WingHeight, $"WOF_{species.Name}_Wing");
                _tailMesh = CreateConeMesh(
                    species.TailRadius, species.TailLength, 4, $"WOF_{species.Name}_Tail");
                _bodyMaterial = MakeUnlitMaterial($"WOF_{species.Name}_Body", species.BodyColor, false);
                var wingColor = species.WingColor;
                wingColor.a = 219;
                _wingMaterial = MakeUnlitMaterial($"WOF_{species.Name}_Wing", wingColor, true);
                _tailMaterial = MakeUnlitMaterial($"WOF_{species.Name}_Tail", species.AccentColor, false);
            }

            public int Count { get; set; }

            public void Add(Matrix4x4 body, Matrix4x4 leftWing, Matrix4x4 rightWing, Matrix4x4 tail)
            {
                _bodyMatrices[Count] = body;
                _leftWingMatrices[Count] = leftWing;
                _rightWingMatrices[Count] = rightWing;
                _tailMatrices[Count] = tail;
                Count++;
            }

            public void Draw(int layer)
            {
                if (Count <= 0) return;
                DrawPart(_bodyMesh, _bodyMaterial, _bodyMatrices, Count, layer);
                DrawPart(_wingMesh, _wingMaterial, _leftWingMatrices, Count, layer);
                DrawPart(_wingMesh, _wingMaterial, _rightWingMatrices, Count, layer);
                DrawPart(_tailMesh, _tailMaterial, _tailMatrices, Count, layer);
            }

            public void Dispose()
            {
                UnityEngine.Object.Destroy(_bodyMesh);
                UnityEngine.Object.Destroy(_wingMesh);
                UnityEngine.Object.Destroy(_tailMesh);
                UnityEngine.Object.Destroy(_bodyMaterial);
                UnityEngine.Object.Destroy(_wingMaterial);
                UnityEngine.Object.Destroy(_tailMaterial);
            }

            private static void DrawPart(
                Mesh mesh,
                Material material,
                Matrix4x4[] matrices,
                int count,
                int layer)
            {
                Graphics.DrawMeshInstanced(
                    mesh, 0, material, matrices, count, null,
                    ShadowCastingMode.Off, false, layer, null, LightProbeUsage.Off);
            }

            private static Material MakeUnlitMaterial(string name, Color color, bool transparent)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                var material = new Material(shader) { name = name, color = color, enableInstancing = true };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (!transparent) return material;
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                return material;
            }

            private static Mesh CreateQuadMesh(float width, float height, string name)
            {
                var halfWidth = width * 0.5f;
                var halfHeight = height * 0.5f;
                var mesh = new Mesh { name = name };
                mesh.vertices = new[]
                {
                    new Vector3(-halfWidth, -halfHeight, 0f),
                    new Vector3(halfWidth, -halfHeight, 0f),
                    new Vector3(halfWidth, halfHeight, 0f),
                    new Vector3(-halfWidth, halfHeight, 0f)
                };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            private static Mesh CreateConeMesh(float radius, float height, int segments, string name)
            {
                var vertices = new Vector3[segments + 2];
                var triangles = new int[segments * 6];
                vertices[0] = new Vector3(0f, height * 0.5f, 0f);
                vertices[1] = new Vector3(0f, -height * 0.5f, 0f);
                for (var index = 0; index < segments; index++)
                {
                    var angle = index / (float)segments * Mathf.PI * 2f;
                    vertices[index + 2] = new Vector3(
                        Mathf.Cos(angle) * radius, -height * 0.5f, Mathf.Sin(angle) * radius);
                    var next = (index + 1) % segments + 2;
                    var write = index * 6;
                    triangles[write] = 0;
                    triangles[write + 1] = index + 2;
                    triangles[write + 2] = next;
                    triangles[write + 3] = 1;
                    triangles[write + 4] = next;
                    triangles[write + 5] = index + 2;
                }
                var mesh = new Mesh { name = name };
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
