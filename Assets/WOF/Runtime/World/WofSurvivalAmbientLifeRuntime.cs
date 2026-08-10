using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalAmbientLifeRuntime : MonoBehaviour
    {
        private const float MobileUpdateInterval = 1f / 24f;
        private const float CollectionPollInterval = 0.1f;
        private const int PetalCount = 7;
        private const int LeafCount = 3;
        private static WofSurvivalAmbientLifeRuntime _instance;

        private readonly Dictionary<string, double> _flowerCooldowns = new();
        private WofPlayerController _player;
        private int _centerX = int.MinValue;
        private int _centerZ = int.MinValue;
        private WofAmbientInsectRecord[] _butterflies = Array.Empty<WofAmbientInsectRecord>();
        private WofAmbientInsectRecord[] _bees = Array.Empty<WofAmbientInsectRecord>();
        private WofManaFlowerRecord[] _flowers = Array.Empty<WofManaFlowerRecord>();
        private Matrix4x4[] _butterflyLeft = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _butterflyRight = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _beeWings = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _stems = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _leaves = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _petals = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _centers = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _glows = Array.Empty<Matrix4x4>();
        private int _readyPetalCount;
        private int _readyCenterCount;
        private Mesh _quad;
        private Mesh _sphere;
        private Mesh _cylinder;
        private Mesh _disc;
        private Mesh _leaf;
        private Material _butterflyLeftMaterial;
        private Material _butterflyRightMaterial;
        private Material _beeMaterial;
        private Material _stemMaterial;
        private Material _leafMaterial;
        private Material _petalMaterial;
        private Material _centerMaterial;
        private Material _glowMaterial;
        private float _nextResolveAt;
        private float _nextVisualUpdateAt;
        private float _nextCollectionPollAt;
        private bool _manaFlowerProbe;
        private bool _manaFlowerProbePositioned;
        private bool _manaFlowerProbeMovedToCollect;
        private bool _manaFlowerProbeFinished;
        private float _manaFlowerProbeCollectAt;
        private WofManaFlowerRecord _manaFlowerProbeTarget;

        public int ButterflyCount => _butterflies.Length;
        public int BeeCount => _bees.Length;
        public int ManaFlowerCount => _flowers.Length;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            if (FindFirstObjectByType<WofSurvivalAmbientLifeRuntime>() != null) return;
            new GameObject("ReactSurvivalAmbientLifeRuntime").AddComponent<WofSurvivalAmbientLifeRuntime>();
        }

        public static void MarkFlowerCollected(int chunkX, int chunkZ, int flowerIndex, double until)
        {
            if (_instance == null) return;
            _instance._flowerCooldowns[$"mana-flower-{chunkX}:{chunkZ}:{flowerIndex}"] = until;
            _instance._nextVisualUpdateAt = 0f;
        }

        private void Awake()
        {
            _instance = this;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-mana-flower-probe", StringComparison.OrdinalIgnoreCase))
                {
                    _manaFlowerProbe = true;
                    break;
                }
            }
            _quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            _sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            _cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            _disc = CreateDiscMesh(12);
            _leaf = CreateConeMesh(5);
            _butterflyLeftMaterial = MakeUnlitMaterial("WOF_AmbientButterflyLeft", new Color32(254, 240, 138, 209));
            _butterflyRightMaterial = MakeUnlitMaterial("WOF_AmbientButterflyRight", new Color32(186, 230, 253, 209));
            _beeMaterial = MakeUnlitMaterial("WOF_AmbientBeeWing", new Color32(224, 242, 254, 133));
            _stemMaterial = MakeUnlitMaterial("WOF_ManaFlowerStem", new Color32(82, 193, 93, 255), false);
            _leafMaterial = MakeUnlitMaterial("WOF_ManaFlowerLeaf", new Color32(114, 221, 111, 255), false);
            _petalMaterial = MakeUnlitMaterial("WOF_ManaFlowerPetal", new Color32(244, 114, 182, 238));
            _centerMaterial = MakeUnlitMaterial("WOF_ManaFlowerCenter", new Color32(254, 240, 138, 250));
            _glowMaterial = MakeUnlitMaterial("WOF_ManaFlowerGlow", new Color32(240, 171, 252, 46), true);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Destroy(_disc);
            Destroy(_leaf);
            Destroy(_butterflyLeftMaterial);
            Destroy(_butterflyRightMaterial);
            Destroy(_beeMaterial);
            Destroy(_stemMaterial);
            Destroy(_leafMaterial);
            Destroy(_petalMaterial);
            Destroy(_centerMaterial);
            Destroy(_glowMaterial);
        }

        private void Update()
        {
            if (WofBootstrap.Instance == null || !WofBootstrap.Instance.IsSurvivalSession) return;
            ResolvePlayer();
            if (_player == null) return;
            var nextX = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.x);
            var nextZ = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.z);
            if (nextX != _centerX || nextZ != _centerZ) Rebuild(nextX, nextZ);
            if (WofSurvivalTerrainMath.IsLilyRealmCenter(_centerX, _centerZ)) return;
            UpdateManaFlowerProbe();

            var camera = Camera.main;
            if (camera == null) return;
            if (_nextVisualUpdateAt <= Time.unscaledTime)
            {
                UpdateDynamicMatrices(Time.time, camera);
                _nextVisualUpdateAt = Time.unscaledTime +
                                      (WofPerformanceModeRuntime.IsMobilePerformanceMode ? MobileUpdateInterval : 0f);
            }
            DrawBatches();
            if (Time.unscaledTime >= _nextCollectionPollAt)
            {
                _nextCollectionPollAt = Time.unscaledTime + CollectionPollInterval;
                TryCollectNearbyFlower();
            }
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

        private void Rebuild(int chunkX, int chunkZ)
        {
            _centerX = chunkX;
            _centerZ = chunkZ;
            if (WofSurvivalTerrainMath.IsLilyRealmCenter(chunkX, chunkZ))
            {
                _butterflies = Array.Empty<WofAmbientInsectRecord>();
                _bees = Array.Empty<WofAmbientInsectRecord>();
                _flowers = Array.Empty<WofManaFlowerRecord>();
            }
            else
            {
                var mobile = WofPerformanceModeRuntime.IsMobilePerformanceMode;
                _butterflies = WofSurvivalAmbientMath.MakeAmbientInsects(
                    chunkX, chunkZ, mobile, WofAmbientInsectKind.Butterfly);
                _bees = WofSurvivalAmbientMath.MakeAmbientInsects(
                    chunkX, chunkZ, mobile, WofAmbientInsectKind.Bee);
                _flowers = WofSurvivalAmbientMath.GetNearbyManaFlowers(chunkX, chunkZ);
            }
            _butterflyLeft = new Matrix4x4[_butterflies.Length];
            _butterflyRight = new Matrix4x4[_butterflies.Length];
            _beeWings = new Matrix4x4[_bees.Length];
            _stems = new Matrix4x4[_flowers.Length];
            _leaves = new Matrix4x4[_flowers.Length * LeafCount];
            _petals = new Matrix4x4[_flowers.Length * PetalCount];
            _centers = new Matrix4x4[_flowers.Length];
            _glows = new Matrix4x4[_flowers.Length];
            BuildStaticFlowerMatrices();
            _nextVisualUpdateAt = 0f;
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_AMBIENT_READY center={chunkX}:{chunkZ} butterflies={ButterflyCount} bees={BeeCount} manaFlowers={ManaFlowerCount}");
        }

        private void BuildStaticFlowerMatrices()
        {
            for (var index = 0; index < _flowers.Length; index++)
            {
                var flower = _flowers[index];
                _stems[index] = Matrix4x4.TRS(
                    flower.Position + Vector3.up * (flower.StemHeight * 0.5f),
                    Quaternion.identity,
                    new Vector3(0.14f, flower.StemHeight * 0.5f, 0.14f));
                for (var leafIndex = 0; leafIndex < LeafCount; leafIndex++)
                {
                    var angle = leafIndex * Mathf.PI * 0.72f;
                    var position = flower.Position + new Vector3(
                        Mathf.Sin(angle) * 0.18f,
                        flower.StemHeight * (0.34f + leafIndex * 0.13f),
                        Mathf.Cos(angle) * 0.18f);
                    _leaves[index * LeafCount + leafIndex] = Matrix4x4.TRS(
                        position,
                        Quaternion.Euler(0.9f * Mathf.Rad2Deg, angle * Mathf.Rad2Deg, 0.35f * Mathf.Rad2Deg),
                        Vector3.one * 0.72f);
                }
            }
        }

        private void UpdateDynamicMatrices(float time, Camera camera)
        {
            var cameraRight = camera.transform.right;
            var cameraUp = camera.transform.up;
            var cameraRotation = camera.transform.rotation;
            for (var index = 0; index < _butterflies.Length; index++)
            {
                var insect = _butterflies[index];
                var orbit = time * insect.Speed + insect.Phase;
                var position = insect.Position + new Vector3(
                    Mathf.Cos(orbit) * insect.OrbitRadius,
                    insect.Height + Mathf.Sin(time * 1.9f + insect.Wobble) * 0.55f,
                    Mathf.Sin(orbit * 0.83f) * insect.OrbitRadius * 0.72f);
                var flap = Mathf.Abs(Mathf.Sin(time * (9.2f + insect.Size) + insect.Phase));
                var offset = insect.Size * (0.34f + flap * 0.18f);
                var scale = new Vector3(insect.Size * 0.82f, insect.Size * 0.58f, 1f);
                _butterflyLeft[index] = Matrix4x4.TRS(
                    position - cameraRight * offset,
                    cameraRotation * Quaternion.AngleAxis((-0.34f - flap * 0.5f) * Mathf.Rad2Deg, Vector3.forward),
                    scale);
                _butterflyRight[index] = Matrix4x4.TRS(
                    position + cameraRight * offset,
                    cameraRotation * Quaternion.AngleAxis((0.34f + flap * 0.5f) * Mathf.Rad2Deg, Vector3.forward),
                    scale);
            }
            for (var index = 0; index < _bees.Length; index++)
            {
                var insect = _bees[index];
                var orbit = time * insect.Speed + insect.Phase;
                var position = insect.Position + new Vector3(
                    Mathf.Cos(orbit) * insect.OrbitRadius,
                    insect.Height + Mathf.Sin(time * 4.1f + insect.Wobble) * 0.28f,
                    Mathf.Sin(orbit * 1.17f) * insect.OrbitRadius * 0.62f);
                _beeWings[index] = Matrix4x4.TRS(
                    position + cameraUp * (insect.Size * 0.22f),
                    cameraRotation * Quaternion.AngleAxis(
                        Mathf.Sin(time * 24f + insect.Phase) * 0.18f * Mathf.Rad2Deg, Vector3.forward),
                    new Vector3(insect.Size * 0.72f, insect.Size * 0.34f, 1f));
            }

            var now = NetworkManager.Singleton?.ServerTime.Time ?? Time.unscaledTimeAsDouble;
            _readyPetalCount = 0;
            _readyCenterCount = 0;
            for (var index = 0; index < _flowers.Length; index++)
            {
                var flower = _flowers[index];
                if (_flowerCooldowns.TryGetValue(flower.Id, out var until) && until > now) continue;
                var phase = time * 2.9f + flower.Position.x * 0.01f;
                var head = flower.Position + Vector3.up *
                    (flower.StemHeight + 0.46f + Mathf.Sin(phase) * 0.06f);
                var pulse = 1f + Mathf.Sin(time * 2.4f + flower.Position.x * 0.01f) * 0.045f;
                var rotation = cameraRotation * Quaternion.AngleAxis(time * 0.28f * Mathf.Rad2Deg, Vector3.forward);
                var headScale = flower.HeadScale * pulse;
                for (var petalIndex = 0; petalIndex < PetalCount; petalIndex++)
                {
                    var angle = petalIndex / (float)PetalCount * Mathf.PI * 2f - Mathf.PI * 0.5f;
                    var center = head + cameraRight * (Mathf.Cos(angle) * headScale * 0.27f) +
                                 cameraUp * (Mathf.Sin(angle) * headScale * 0.25f);
                    _petals[_readyPetalCount++] = Matrix4x4.TRS(
                        center,
                        rotation * Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.forward),
                        new Vector3(headScale * 0.48f, headScale * 0.23f, 1f));
                }
                _centers[_readyCenterCount] = Matrix4x4.TRS(
                    head, rotation, Vector3.one * (headScale * 0.22f));
                _glows[_readyCenterCount] = Matrix4x4.TRS(
                    head, rotation, Vector3.one * (flower.HeadScale * 2.05f * pulse));
                _readyCenterCount++;
            }
        }

        private void TryCollectNearbyFlower()
        {
            if (_player == null || !_player.CanRechargeMana) return;
            var now = NetworkManager.Singleton?.ServerTime.Time ?? Time.unscaledTimeAsDouble;
            var position = _player.transform.position;
            for (var index = 0; index < _flowers.Length; index++)
            {
                var flower = _flowers[index];
                if (_flowerCooldowns.TryGetValue(flower.Id, out var until) && until > now) continue;
                var dx = position.x - flower.Position.x;
                var dz = position.z - flower.Position.z;
                if (dx * dx + dz * dz >= flower.Radius * flower.Radius) continue;
                if (_player.RequestManaFlowerCollection(flower.ChunkX, flower.ChunkZ, flower.Index)) return;
            }
        }

        private void UpdateManaFlowerProbe()
        {
            if (!_manaFlowerProbe || _manaFlowerProbeFinished || _flowers.Length == 0) return;
            if (!_manaFlowerProbePositioned)
            {
                _manaFlowerProbeTarget = FindClosestFlower(_player.transform.position);
                const float viewDistance = 6f;
                var viewPosition = _manaFlowerProbeTarget.Position + new Vector3(0f, 0.15f, -viewDistance);
                if (!_player.PrepareForAutomationStaticViewProbe(viewPosition, 0f, -4f)) return;
                _manaFlowerProbePositioned = true;
                _manaFlowerProbeCollectAt = Time.unscaledTime + 4f;
                Debug.Log($"[WOF-AUTOMATION] MANA_FLOWER_PROBE_READY id={_manaFlowerProbeTarget.Id} position={_manaFlowerProbeTarget.Position} butterflies={ButterflyCount} bees={BeeCount} manaFlowers={ManaFlowerCount}");
                return;
            }
            if (!_manaFlowerProbeMovedToCollect && Time.unscaledTime >= _manaFlowerProbeCollectAt)
            {
                var collectPosition = _manaFlowerProbeTarget.Position + Vector3.up * 0.15f;
                if (!_player.PrepareForAutomationStaticViewProbe(collectPosition, 0f, -4f)) return;
                _manaFlowerProbeMovedToCollect = true;
                Debug.Log($"[WOF-AUTOMATION] MANA_FLOWER_PROBE_ENTERED_RADIUS id={_manaFlowerProbeTarget.Id}");
                return;
            }
            if (!_manaFlowerProbeMovedToCollect || _player.LeftMana <= 0f && _player.RightMana <= 0f) return;
            _manaFlowerProbeFinished = true;
            Debug.Log($"[WOF-AUTOMATION] MANA_FLOWER_PROBE_PASS id={_manaFlowerProbeTarget.Id} leftMana={_player.LeftMana:F1} rightMana={_player.RightMana:F1}");
        }

        private WofManaFlowerRecord FindClosestFlower(Vector3 position)
        {
            var closest = _flowers[0];
            var closestDistance = (closest.Position - position).sqrMagnitude;
            for (var index = 1; index < _flowers.Length; index++)
            {
                var distance = (_flowers[index].Position - position).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closest = _flowers[index];
                closestDistance = distance;
            }
            return closest;
        }

        private void DrawBatches()
        {
            Draw(_quad, _butterflyLeftMaterial, _butterflyLeft, _butterflyLeft.Length);
            Draw(_quad, _butterflyRightMaterial, _butterflyRight, _butterflyRight.Length);
            Draw(_quad, _beeMaterial, _beeWings, _beeWings.Length);
            Draw(_cylinder, _stemMaterial, _stems, _stems.Length);
            Draw(_leaf, _leafMaterial, _leaves, _leaves.Length);
            Draw(_disc, _petalMaterial, _petals, _readyPetalCount);
            Draw(_sphere, _centerMaterial, _centers, _readyCenterCount);
            Draw(_disc, _glowMaterial, _glows, _readyCenterCount);
        }

        private void Draw(Mesh mesh, Material material, Matrix4x4[] matrices, int count)
        {
            if (!ShouldDrawInstances(SystemInfo.supportsInstancing, count) ||
                mesh == null || material == null || matrices == null) return;
            Graphics.DrawMeshInstanced(
                mesh, 0, material, matrices, count, null,
                ShadowCastingMode.Off, false, gameObject.layer, null, LightProbeUsage.Off);
        }

        internal static bool ShouldDrawInstances(bool supportsInstancing, int count)
        {
            return supportsInstancing && count > 0;
        }

        private static Material MakeUnlitMaterial(string name, Color color, bool additive = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = name, color = color, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (color.a < 0.999f || additive)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_ZWrite", 0f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            return material;
        }

        private static Mesh CreateDiscMesh(int segments)
        {
            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var triangles = new int[segments * 3];
            normals[0] = Vector3.back;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                vertices[index + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                normals[index + 1] = Vector3.back;
                uvs[index + 1] = new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f);
                triangles[index * 3] = 0;
                triangles[index * 3 + 1] = index + 1;
                triangles[index * 3 + 2] = (index + 1) % segments + 1;
            }
            var mesh = new Mesh { name = "WOF_RuntimeDisc12" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateConeMesh(int segments)
        {
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 6];
            vertices[0] = new Vector3(0f, 0.72f, 0f);
            vertices[1] = Vector3.zero;
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                vertices[index + 2] = new Vector3(Mathf.Cos(angle) * 0.26f, 0f, Mathf.Sin(angle) * 0.26f);
                var next = (index + 1) % segments + 2;
                var write = index * 6;
                triangles[write] = 0;
                triangles[write + 1] = index + 2;
                triangles[write + 2] = next;
                triangles[write + 3] = 1;
                triangles[write + 4] = next;
                triangles[write + 5] = index + 2;
            }
            var mesh = new Mesh { name = "WOF_RuntimeManaLeaf" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
