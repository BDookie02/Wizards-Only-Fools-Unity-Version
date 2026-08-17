using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalDesertLandmarkRuntime : MonoBehaviour
    {
        private const string ProbeArgument = "--wof-desert-landmark-probe";
        private readonly Dictionary<long, ChunkStage> _visibleStages = new();
        private readonly Dictionary<long, ChunkContent> _chunkContent = new();
        private readonly List<PendingChunk> _pendingChunks = new();
        private readonly Dictionary<string, Material> _materials = new(StringComparer.Ordinal);
        private readonly List<Mesh> _ownedMeshes = new();
        private WofPlayerController _player;
        private Mesh _boxMesh;
        private Mesh _planeMesh;
        private Mesh _coneFourMesh;
        private Mesh _coneSixMesh;
        private Mesh _egyptianColumnMesh;
        private Material _adobeSource;
        private Material _villagerMaterial;
        private float _nextResolveAt;
        private int _centerX = int.MinValue;
        private int _centerZ = int.MinValue;
        private bool _mobile;
        private bool _probe;
        private string _probeKind = "pyramid";
        private bool _probeViewPrepared;
        private bool _probeReported;

        public int LandmarkCount { get; private set; }
        public int PyramidCount { get; private set; }
        public int ObeliskCount { get; private set; }
        public int VillagerCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            if (FindFirstObjectByType<WofSurvivalDesertLandmarkRuntime>() != null) return;
            new GameObject("ReactSurvivalDesertLandmarkRuntime")
                .AddComponent<WofSurvivalDesertLandmarkRuntime>();
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
            }

            _mobile = WofPerformanceModeRuntime.IsMobilePerformanceMode;
            _boxMesh = Own(CreateBoxMesh());
            _planeMesh = Own(CreatePlaneMesh());
            _coneFourMesh = Own(CreateConeMesh(4, "ReactDesertLandmarkCone4"));
            _coneSixMesh = Own(CreateConeMesh(6, "ReactDesertLandmarkCone6"));
            _egyptianColumnMesh = Own(CreateTaperedCylinderMesh(6, 5f / 6f));
            ResolveSourceMaterials();
        }

        private void OnDestroy()
        {
            ClearRuntimeState();
            foreach (var material in _materials.Values) Destroy(material);
            foreach (var mesh in _ownedMeshes) Destroy(mesh);
        }

        private void Update()
        {
            var survival = WofBootstrap.Instance != null && WofBootstrap.Instance.IsSurvivalSession;
            if (!WofSurvivalDesertLandmarkRules.ShouldShowRuntime(survival))
            {
                ClearRuntimeState();
                return;
            }

            ResolvePlayer();
            if (_player == null) return;
            if (_adobeSource == null || _villagerMaterial == null) ResolveSourceMaterials();
            if (_probe && !_probeViewPrepared) PrepareProbeView();
            var centerX = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.x);
            var centerZ = WofSurvivalTerrainMath.GetChunkCoordinate(_player.transform.position.z);
            if (centerX != _centerX || centerZ != _centerZ) RebuildWindow(centerX, centerZ);
            ContinueStagedGeneration();
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

        private void RebuildWindow(int centerX, int centerZ)
        {
            _centerX = centerX;
            _centerZ = centerZ;
            const float roundedRadiusSquared = 3.25f * 3.25f;
            var desiredStages = new HashSet<long>();
            for (var dz = -WofSurvivalTerrainMath.RenderRadius; dz <= WofSurvivalTerrainMath.RenderRadius; dz++)
            for (var dx = -WofSurvivalTerrainMath.RenderRadius; dx <= WofSurvivalTerrainMath.RenderRadius; dx++)
            {
                if (dx * dx + dz * dz > roundedRadiusSquared) continue;
                var chunkX = centerX + dx;
                var chunkZ = centerZ + dz;
                var key = MakeCoordinateKey(chunkX, chunkZ);
                desiredStages.Add(key);
                if (_visibleStages.ContainsKey(key)) continue;
                var initialDistance = Math.Max(Math.Abs(dx), Math.Abs(dz));
                _visibleStages.Add(key, new ChunkStage(
                    chunkX,
                    chunkZ,
                    Time.unscaledTime + WofSurvivalDesertLandmarkRules.GetReadyDelaySeconds(
                        chunkX, chunkZ, initialDistance, _mobile)));
            }

            var stageKeys = new List<long>(_visibleStages.Keys);
            foreach (var key in stageKeys)
                if (!desiredStages.Contains(key)) _visibleStages.Remove(key);

            _pendingChunks.Clear();
            var desiredContent = new HashSet<long>();
            for (var dz = -WofSurvivalTerrainMath.NearRadius; dz <= WofSurvivalTerrainMath.NearRadius; dz++)
            for (var dx = -WofSurvivalTerrainMath.NearRadius; dx <= WofSurvivalTerrainMath.NearRadius; dx++)
            {
                var chunkX = centerX + dx;
                var chunkZ = centerZ + dz;
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dz));
                var key = MakeCoordinateKey(chunkX, chunkZ);
                desiredContent.Add(key);
                if (_chunkContent.TryGetValue(key, out var existing) && existing.Distance == distance) continue;
                if (existing != null) RemoveChunkContent(key);
                if (!WofSurvivalDesertLandmarkRules.ShouldGenerateChunk(true, chunkX, chunkZ, distance)) continue;
                var readyAt = _visibleStages.TryGetValue(key, out var stage)
                    ? stage.ReadyAt
                    : Time.unscaledTime + WofSurvivalDesertLandmarkRules.GetReadyDelaySeconds(
                        chunkX, chunkZ, distance, _mobile);
                _pendingChunks.Add(new PendingChunk(chunkX, chunkZ, distance, readyAt));
            }

            var contentKeys = new List<long>(_chunkContent.Keys);
            foreach (var key in contentKeys)
                if (!desiredContent.Contains(key)) RemoveChunkContent(key);

            _pendingChunks.Sort((left, right) =>
            {
                var ready = left.ReadyAt.CompareTo(right.ReadyAt);
                if (ready != 0) return ready;
                var distance = left.Distance.CompareTo(right.Distance);
                if (distance != 0) return distance;
                var x = left.ChunkX.CompareTo(right.ChunkX);
                return x != 0 ? x : left.ChunkZ.CompareTo(right.ChunkZ);
            });
            RecountContent();
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_DESERT_LANDMARK_WINDOW center={centerX}:{centerZ} staged={_visibleStages.Count} queued={_pendingChunks.Count}");
        }

        private void ContinueStagedGeneration()
        {
            if (_pendingChunks.Count == 0 || _pendingChunks[0].ReadyAt > Time.unscaledTime) return;
            var pending = _pendingChunks[0];
            _pendingChunks.RemoveAt(0);
            BuildChunk(pending.ChunkX, pending.ChunkZ, pending.Distance);
        }

        private void BuildChunk(int chunkX, int chunkZ, int distance)
        {
            var key = MakeCoordinateKey(chunkX, chunkZ);
            RemoveChunkContent(key);
            var records = WofSurvivalDesertLandmarkRules.MakeChunk(chunkX, chunkZ, distance);
            var root = new GameObject($"ReactDesertLandmarks-{chunkX}-{chunkZ}-lod{distance}").transform;
            root.SetParent(transform, false);
            var pyramids = 0;
            var obelisks = 0;
            var villagers = new List<WofVillagerBillboard>();
            var villagerIndex = 0;
            foreach (var record in records)
            {
                if (record.Kind == WofSurvivalDesertLandmarkKind.Pyramid)
                {
                    BuildPyramid(root, record, distance, villagers, ref villagerIndex);
                    pyramids++;
                }
                else
                {
                    BuildObelisk(root, record);
                    obelisks++;
                }
            }
            if (villagers.Count > 0)
                root.gameObject.AddComponent<WofVillagerManager>().Configure(villagers.ToArray());

            _chunkContent[key] = new ChunkContent(
                root,
                distance,
                records.Length,
                pyramids,
                obelisks,
                villagers.Count);
            RecountContent();
            if (villagers.Count > 0) WofQuestDialogRuntime.InvalidateVillagerManagerCaches();
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_DESERT_LANDMARK_CHUNK_READY chunk={chunkX}:{chunkZ} distance={distance} landmarks={records.Length} pyramids={pyramids} obelisks={obelisks} villagers={villagers.Count}");
        }

        private void BuildPyramid(
            Transform parent,
            WofSurvivalDesertLandmarkRecord landmark,
            int distance,
            List<WofVillagerBillboard> villagers,
            ref int villagerIndex)
        {
            var metrics = WofSurvivalDesertLandmarkRules.GetPyramidMetrics(landmark);
            var footprint = WofSurvivalDesertLandmarkRules.GetUnityPyramidFootprintStats(landmark, distance);
            var root = new GameObject($"{landmark.Key}-pyramid").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(landmark.Position.x, 0f, landmark.Position.z);
            root.rotation = Quaternion.Euler(0f, metrics.PyramidYawRadians * Mathf.Rad2Deg, 0f);

            var foundationSize = metrics.BaseSize * 1.1f;
            var lowerBuryY = Mathf.Min(footprint.Minimum, landmark.Position.y - 0.65f) - 1.8f;
            var topY = landmark.Position.y + 0.08f;
            var foundationHeight = Mathf.Max(0.85f, topY - lowerBuryY);
            AddMesh(root, "TerrainFoundation", _boxMesh, GetMaterial("#b98243", true),
                new Vector3(0f, lowerBuryY + foundationHeight * 0.5f, 0f), Vector3.zero,
                new Vector3(foundationSize, foundationHeight, foundationSize));
            AddMesh(root, "FoundationTop", _planeMesh, GetMaterial("#d3a35b", true),
                new Vector3(0f, topY + 0.035f, 0f), new Vector3(-90f, 0f, 0f),
                new Vector3(foundationSize * 1.08f, foundationSize * 1.08f, 1f));
            AddMesh(root, "EntranceRamp", _boxMesh, GetMaterial("#d7b06a"),
                new Vector3(0f, landmark.Position.y + 0.22f * landmark.Scale, -metrics.BaseSize * 0.64f),
                new Vector3(-0.42f * Mathf.Rad2Deg, 0f, 0f),
                new Vector3(metrics.DoorWidth * 1.75f, 0.55f * landmark.Scale, metrics.BaseSize * 0.32f));

            for (var index = 0; index < metrics.StepCount; index++)
            {
                var amount = index / (float)metrics.StepCount;
                var size = metrics.BaseSize * (1f - amount * 0.72f);
                var y = landmark.Position.y + metrics.StepHeight * index + metrics.StepHeight * 0.5f;
                var wall = Mathf.Min(metrics.WallThickness, size * 0.22f);
                var frontZ = -size * 0.5f + wall * 0.5f;
                var backZ = size * 0.5f - wall * 0.5f;
                var sideX = size * 0.5f - wall * 0.5f;
                var sideDepth = Mathf.Max(1f, size - wall * 2f);
                var color = index % 2 == 0 ? "#d2a45c" : "#c8954f";
                var material = GetMaterial(color, true);
                AddMesh(root, $"Step{index}-Back", _boxMesh, material,
                    new Vector3(0f, y, backZ), Vector3.zero, new Vector3(size, metrics.StepHeight, wall));
                AddMesh(root, $"Step{index}-Left", _boxMesh, material,
                    new Vector3(-sideX, y, 0f), Vector3.zero, new Vector3(wall, metrics.StepHeight, sideDepth));
                AddMesh(root, $"Step{index}-Right", _boxMesh, material,
                    new Vector3(sideX, y, 0f), Vector3.zero, new Vector3(wall, metrics.StepHeight, sideDepth));
                // React's double-precision expression is equivalent to index < 3 for
                // positive scale. Spell that contract explicitly so float rounding
                // cannot open an unintended fourth doorway tier in Unity.
                if (index < 3)
                {
                    var opening = Mathf.Min(
                        metrics.DoorWidth + index * 0.6f * landmark.Scale,
                        size * 0.48f);
                    var frontSideWidth = Mathf.Max(0.8f, (size - opening) * 0.5f);
                    AddMesh(root, $"Step{index}-FrontLeft", _boxMesh, material,
                        new Vector3(-opening * 0.5f - frontSideWidth * 0.5f, y, frontZ),
                        Vector3.zero, new Vector3(frontSideWidth, metrics.StepHeight, wall));
                    AddMesh(root, $"Step{index}-FrontRight", _boxMesh, material,
                        new Vector3(opening * 0.5f + frontSideWidth * 0.5f, y, frontZ),
                        Vector3.zero, new Vector3(frontSideWidth, metrics.StepHeight, wall));
                }
                else
                {
                    AddMesh(root, $"Step{index}-Front", _boxMesh, material,
                        new Vector3(0f, y, frontZ), Vector3.zero,
                        new Vector3(size, metrics.StepHeight, wall));
                }
            }

            BuildPyramidInterior(root, landmark, metrics);
            AddMesh(root, "PyramidCap", _coneFourMesh, GetMaterial("#f1d08a"),
                new Vector3(0f, landmark.Position.y + metrics.StepHeight * metrics.StepCount + 1.3f * landmark.Scale, 0f),
                Vector3.zero,
                new Vector3(metrics.BaseSize * 0.32f, 3.2f * landmark.Scale, metrics.BaseSize * 0.32f));
            AddMesh(root, "DarkDoor", _boxMesh, GetMaterial("#170d09", false, 0.72f),
                new Vector3(0f, landmark.Position.y + metrics.DoorHeight * 0.52f, -metrics.BaseSize * 0.5f - 0.06f),
                Vector3.zero,
                new Vector3(metrics.DoorWidth, metrics.DoorHeight, 0.24f * landmark.Scale));
            AddMesh(root, "GoldLintel", _boxMesh, GetMaterial("#facc15"),
                new Vector3(0f, landmark.Position.y + metrics.DoorHeight + 0.28f * landmark.Scale, -metrics.BaseSize * 0.5f - 0.09f),
                Vector3.zero,
                new Vector3(metrics.DoorWidth * 1.4f, 0.55f * landmark.Scale, 0.28f * landmark.Scale));

            if (distance != 0) return;
            AddPyramidColliders(root, landmark, metrics, lowerBuryY, foundationHeight);
            AddPyramidVillagers(parent, landmark, metrics, villagers, ref villagerIndex);
        }

        private void BuildPyramidInterior(
            Transform root,
            WofSurvivalDesertLandmarkRecord landmark,
            WofSurvivalDesertPyramidMetrics metrics)
        {
            var floorWidth = metrics.BaseSize * 0.52f;
            var floorDepth = metrics.BaseSize * 0.62f;
            var floorZ = -metrics.BaseSize * 0.05f;
            var floorY = landmark.Position.y + 0.05f;
            AddMesh(root, "InteriorFloor", _planeMesh, GetMaterial("#7c5230"),
                new Vector3(0f, floorY, floorZ), new Vector3(-90f, 0f, 0f),
                new Vector3(floorWidth, floorDepth, 1f));
            AddMesh(root, "Altar", _boxMesh, GetMaterial("#a86f38"),
                new Vector3(0f, landmark.Position.y + 0.46f * landmark.Scale, floorZ + floorDepth * 0.25f),
                Vector3.zero,
                new Vector3(metrics.BaseSize * 0.28f, 0.9f * landmark.Scale, metrics.BaseSize * 0.12f));
            AddMesh(root, "AltarGold", _boxMesh, GetMaterial("#facc15"),
                new Vector3(0f, landmark.Position.y + 1.12f * landmark.Scale, floorZ + floorDepth * 0.25f),
                Vector3.zero,
                new Vector3(metrics.BaseSize * 0.22f, 0.5f * landmark.Scale, metrics.BaseSize * 0.08f));

            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * floorWidth * 0.34f;
                var z = -metrics.BaseSize * 0.18f;
                AddMesh(root, side < 0 ? "InteriorColumnLeft" : "InteriorColumnRight",
                    _egyptianColumnMesh, GetMaterial("#d6a25b"),
                    new Vector3(x, landmark.Position.y + 2.2f * landmark.Scale, z),
                    Vector3.zero,
                    new Vector3(1.2f * landmark.Scale, 4.4f * landmark.Scale, 1.2f * landmark.Scale));
                AddMesh(root, side < 0 ? "InteriorColumnCapLeft" : "InteriorColumnCapRight",
                    _boxMesh, GetMaterial("#facc15"),
                    new Vector3(x, landmark.Position.y + 4.75f * landmark.Scale, z),
                    Vector3.zero,
                    new Vector3(1.9f, 0.45f, 1.9f) * landmark.Scale);
                AddMesh(root, side < 0 ? "InteriorFlameLeft" : "InteriorFlameRight",
                    _coneSixMesh, GetMaterial("#fb923c"),
                    new Vector3(x, landmark.Position.y + 5.35f * landmark.Scale, z),
                    Vector3.zero,
                    new Vector3(1.4f * landmark.Scale, 1.35f * landmark.Scale, 1.4f * landmark.Scale));
            }

            for (var glyph = -1; glyph <= 1; glyph++)
                AddMesh(root, $"Glyph{glyph + 1}", _boxMesh,
                    GetMaterial(glyph == 0 ? "#facc15" : "#0ea5e9"),
                    new Vector3(glyph * metrics.BaseSize * 0.09f,
                        landmark.Position.y + 3.25f * landmark.Scale,
                        metrics.BaseSize * 0.3f),
                    new Vector3(0f, 180f, 0f),
                    new Vector3(0.28f * landmark.Scale, 2f * landmark.Scale, 0.08f * landmark.Scale));
        }

        private static void AddPyramidColliders(
            Transform root,
            WofSurvivalDesertLandmarkRecord landmark,
            WofSurvivalDesertPyramidMetrics metrics,
            float foundationLowerY,
            float foundationHeight)
        {
            AddBoxCollider(root, "FoundationCollider",
                new Vector3(0f, foundationLowerY + foundationHeight * 0.5f, 0f),
                Vector3.zero,
                new Vector3(metrics.BaseSize * 1.1f, foundationHeight, metrics.BaseSize * 1.1f));
            AddBoxCollider(root, "RampCollider",
                new Vector3(0f, landmark.Position.y + 0.22f * landmark.Scale, -metrics.BaseSize * 0.64f),
                new Vector3(-0.42f * Mathf.Rad2Deg, 0f, 0f),
                new Vector3(metrics.DoorWidth * 1.8f, 0.56f * landmark.Scale, metrics.BaseSize * 0.32f));
            var sideWallX = metrics.BaseSize * 0.5f - metrics.WallThickness * 0.5f;
            var sideWallDepth = metrics.BaseSize * 0.86f;
            var wallY = landmark.Position.y + metrics.Height * 0.5f;
            var frontZ = -metrics.BaseSize * 0.5f + metrics.WallThickness * 0.5f;
            var backZ = metrics.BaseSize * 0.5f - metrics.WallThickness * 0.5f;
            var frontSideWidth = Mathf.Max(1f, (metrics.BaseSize - metrics.DoorWidth) * 0.5f);
            var lintelHeight = Mathf.Max(0.8f, metrics.Height - metrics.DoorHeight);
            AddBoxCollider(root, "LeftWallCollider", new Vector3(-sideWallX, wallY, 0f), Vector3.zero,
                new Vector3(metrics.WallThickness, metrics.Height, sideWallDepth));
            AddBoxCollider(root, "RightWallCollider", new Vector3(sideWallX, wallY, 0f), Vector3.zero,
                new Vector3(metrics.WallThickness, metrics.Height, sideWallDepth));
            AddBoxCollider(root, "BackWallCollider", new Vector3(0f, wallY, backZ), Vector3.zero,
                new Vector3(metrics.BaseSize, metrics.Height, metrics.WallThickness));
            AddBoxCollider(root, "FrontLeftCollider",
                new Vector3(-metrics.DoorWidth * 0.5f - frontSideWidth * 0.5f, wallY, frontZ), Vector3.zero,
                new Vector3(frontSideWidth, metrics.Height, metrics.WallThickness));
            AddBoxCollider(root, "FrontRightCollider",
                new Vector3(metrics.DoorWidth * 0.5f + frontSideWidth * 0.5f, wallY, frontZ), Vector3.zero,
                new Vector3(frontSideWidth, metrics.Height, metrics.WallThickness));
            AddBoxCollider(root, "LintelCollider",
                new Vector3(0f, landmark.Position.y + metrics.DoorHeight + lintelHeight * 0.5f, frontZ),
                Vector3.zero,
                new Vector3(metrics.DoorWidth, lintelHeight, metrics.WallThickness));
        }

        private void AddPyramidVillagers(
            Transform parent,
            WofSurvivalDesertLandmarkRecord landmark,
            WofSurvivalDesertPyramidMetrics metrics,
            List<WofVillagerBillboard> villagers,
            ref int villagerIndex)
        {
            var count = metrics.BaseSize > 43f ? 3 : 2;
            var spacing = metrics.BaseSize * (metrics.BaseSize > 43f ? 0.16f : 0.14f);
            var rotation = metrics.PyramidYawRadians + Mathf.PI;
            var doorDirection = new Vector3(Mathf.Sin(rotation), 0f, Mathf.Cos(rotation));
            var sideDirection = new Vector3(Mathf.Cos(rotation), 0f, -Mathf.Sin(rotation));
            for (var index = 0; index < count; index++)
            {
                var sideOffset = count == 3 ? (index - 1) * spacing : index == 0 ? -spacing : spacing;
                var backOffset = -metrics.BaseSize * (0.13f + index * 0.035f);
                var position = landmark.Position + doorDirection * backOffset + sideDirection * sideOffset;
                position.y = landmark.Position.y + 0.95f + WofVillagerMath.AvatarGroundLift;
                var id = $"{landmark.Key}-egyptian-villager-{index}";
                var hut = new WofVillagerHutRecord
                {
                    x = landmark.Position.x,
                    y = landmark.Position.y,
                    z = landmark.Position.z,
                    hutType = 20 + index,
                    isMushroom = false,
                    rotation = rotation,
                    interiorWidth = metrics.BaseSize * 0.62f,
                    interiorDepth = metrics.BaseSize * 0.66f,
                    interiorHeight = metrics.Height + 2f
                };

                var villagerObject = new GameObject($"PyramidVillager-{index}");
                villagerObject.transform.SetParent(parent, false);
                var visual = new GameObject("AvatarBillboard");
                visual.transform.SetParent(villagerObject.transform, false);
                visual.transform.localPosition = new Vector3(0f, WofVillagerMath.AvatarWorldCenterY, 0f);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = _villagerMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.enabled = false;
                var billboard = villagerObject.AddComponent<WofVillagerBillboard>();
                var desktopLook = 140f + HashValue(id, 0x51a7) * 90f;
                var mobileLook = 220f + HashValue(id, 0x51a7) * 90f;
                var archiveIndex = Mathf.Min(54, Mathf.FloorToInt(HashValue(id, 0x4d31) * 55f));
                billboard.Configure(
                    id,
                    $"desert-{archiveIndex:00}.wofavatar",
                    position,
                    rotation,
                    desktopLook,
                    mobileLook,
                    hut,
                    visual.transform,
                    renderer,
                    $"Dune Villager {villagerIndex + 1}",
                    $"survival-pyramid-villagers-{landmark.ChunkX}:{landmark.ChunkZ}");
                villagers.Add(billboard);
                villagerIndex++;
            }
        }

        private void BuildObelisk(Transform parent, WofSurvivalDesertLandmarkRecord landmark)
        {
            var root = new GameObject($"{landmark.Key}-obelisk").transform;
            root.SetParent(parent, false);
            root.position = landmark.Position;
            root.rotation = Quaternion.Euler(0f, landmark.YawRadians * Mathf.Rad2Deg, 0f);
            var height = 28f * landmark.Scale;
            AddMesh(root, "ObeliskBase", _boxMesh, GetMaterial("#c99b58", true),
                new Vector3(0f, 1.1f * landmark.Scale, 0f), Vector3.zero,
                new Vector3(9f, 2.2f, 9f) * landmark.Scale);
            AddMesh(root, "ObeliskShaft", _boxMesh, GetMaterial("#d7ad66", true),
                new Vector3(0f, height * 0.5f + 2.2f * landmark.Scale, 0f), Vector3.zero,
                new Vector3(4.8f * landmark.Scale, height, 4.8f * landmark.Scale));
            AddMesh(root, "ObeliskTop", _coneFourMesh, GetMaterial("#eecb80"),
                new Vector3(0f, height + 5.2f * landmark.Scale, 0f), Vector3.zero,
                new Vector3(7f * landmark.Scale, 5.5f * landmark.Scale, 7f * landmark.Scale));
            foreach (var side in new[] { -1f, 1f })
                AddMesh(root, side < 0 ? "SidePillarLeft" : "SidePillarRight", _boxMesh,
                    GetMaterial("#bd8c4d", true),
                    new Vector3(side * 8.5f * landmark.Scale, 3.8f * landmark.Scale, 2.5f * landmark.Scale),
                    Vector3.zero,
                    new Vector3(2.2f, 7.6f, 2.2f) * landmark.Scale);
        }

        private Transform AddMesh(
            Transform parent,
            string itemName,
            Mesh mesh,
            Material material,
            Vector3 localPosition,
            Vector3 localEulerDegrees,
            Vector3 localScale)
        {
            var item = new GameObject(itemName);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localRotation = Quaternion.Euler(localEulerDegrees);
            item.transform.localScale = localScale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return item.transform;
        }

        private static void AddBoxCollider(
            Transform parent,
            string itemName,
            Vector3 localPosition,
            Vector3 localEulerDegrees,
            Vector3 size)
        {
            var item = new GameObject(itemName);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localRotation = Quaternion.Euler(localEulerDegrees);
            item.AddComponent<BoxCollider>().size = size;
        }

        private Material GetMaterial(string hex, bool adobeTexture = false, float alpha = 1f)
        {
            var key = $"{hex}:{adobeTexture}:{alpha:0.000}";
            if (_materials.TryGetValue(key, out var existing)) return existing;
            ColorUtility.TryParseHtmlString(hex, out var color);
            color.a = alpha;
            Material material;
            if (adobeTexture && _adobeSource != null)
            {
                material = new Material(_adobeSource);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                material = new Material(shader);
            }
            material.name = $"ReactDesertLandmark-{key}";
            material.color = color;
            material.enableInstancing = true;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
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

        private void ResolveSourceMaterials()
        {
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (_adobeSource == null && string.Equals(material.name, "DesertAdobe", StringComparison.Ordinal))
                    _adobeSource = material;
            }
            if (_villagerMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
                             Shader.Find("Sprites/Default") ??
                             Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    _villagerMaterial = new Material(shader) { name = "ReactPyramidVillagerSprite" };
                    _materials.Add("villager-sprite", _villagerMaterial);
                }
            }
        }

        private void PrepareProbeView()
        {
            const int chunkX = 3;
            const int chunkZ = -3;
            var records = WofSurvivalDesertLandmarkRules.MakeChunk(chunkX, chunkZ, 0);
            WofSurvivalDesertLandmarkRecord? selected = null;
            foreach (var record in records)
            {
                var wanted = _probeKind == "obelisk"
                    ? record.Kind == WofSurvivalDesertLandmarkKind.Obelisk
                    : record.Kind == WofSurvivalDesertLandmarkKind.Pyramid;
                if (wanted)
                {
                    selected = record;
                    break;
                }
            }
            if (!selected.HasValue) return;
            var landmark = selected.Value;
            var yaw = landmark.Kind == WofSurvivalDesertLandmarkKind.Pyramid
                ? WofSurvivalDesertLandmarkRules.GetPyramidMetrics(landmark).PyramidYawRadians
                : landmark.YawRadians;
            var distance = landmark.Kind == WofSurvivalDesertLandmarkKind.Pyramid ? 62f : 46f;
            var height = landmark.Kind == WofSurvivalDesertLandmarkKind.Pyramid
                ? WofSurvivalDesertLandmarkRules.GetPyramidMetrics(landmark).Height
                : 36f * landmark.Scale;
            var viewPosition = landmark.Position +
                               Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f) * Vector3.back * distance;
            var surface = WofSurvivalDesertLandmarkRules.GetRenderedTerrainHeightAtWorld(
                viewPosition.x,
                viewPosition.z);
            viewPosition.y = Mathf.Max(landmark.Position.y + height * 0.22f, surface + 2.4f);
            var target = landmark.Position + Vector3.up * height * 0.45f;
            var direction = target - viewPosition;
            var horizontal = new Vector2(direction.x, direction.z).magnitude;
            var probeYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var probePitch = Mathf.Atan2(-direction.y, horizontal) * Mathf.Rad2Deg;
            if (!_player.PrepareForAutomationStaticViewProbe(viewPosition, probeYaw, probePitch)) return;
            var camera = _player.GetComponentInChildren<Camera>(true);
            if (camera != null) camera.farClipPlane = 2500f;
            _probeViewPrepared = true;
        }

        private void TryReportProbeReady()
        {
            if (!_probe || !_probeViewPrepared || _probeReported || _centerX != 3 || _centerZ != -3) return;
            var key = MakeCoordinateKey(3, -3);
            if (!_chunkContent.TryGetValue(key, out var content) || content.Distance != 0) return;
            var ready = _probeKind == "obelisk" ? content.Obelisks > 0 : content.Pyramids > 0;
            if (!ready) return;
            _probeReported = true;
            Debug.Log($"[WOF-AUTOMATION] DESERT_LANDMARK_PROBE_READY kind={_probeKind} chunk=3:-3 landmarks={content.Landmarks} pyramids={content.Pyramids} obelisks={content.Obelisks} villagers={content.Villagers}");
        }

        private void RemoveChunkContent(long key)
        {
            if (!_chunkContent.Remove(key, out var content)) return;
            if (content.Root != null) Destroy(content.Root.gameObject);
            if (content.Villagers > 0) WofQuestDialogRuntime.InvalidateVillagerManagerCaches();
        }

        private void RecountContent()
        {
            LandmarkCount = 0;
            PyramidCount = 0;
            ObeliskCount = 0;
            VillagerCount = 0;
            foreach (var content in _chunkContent.Values)
            {
                LandmarkCount += content.Landmarks;
                PyramidCount += content.Pyramids;
                ObeliskCount += content.Obelisks;
                VillagerCount += content.Villagers;
            }
        }

        private void ClearRuntimeState()
        {
            var keys = new List<long>(_chunkContent.Keys);
            foreach (var key in keys) RemoveChunkContent(key);
            _pendingChunks.Clear();
            _visibleStages.Clear();
            _centerX = int.MinValue;
            _centerZ = int.MinValue;
            RecountContent();
        }

        private Mesh Own(Mesh mesh)
        {
            _ownedMeshes.Add(mesh);
            return mesh;
        }

        private static float HashValue(string seed, int salt)
        {
            unchecked
            {
                var hash = 2166136261u ^ (uint)salt;
                foreach (var character in seed)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return hash / 4294967295f;
            }
        }

        private static long MakeCoordinateKey(int x, int z) => ((long)x << 32) ^ (uint)z;

        private static Mesh CreatePlaneMesh()
        {
            var mesh = new Mesh { name = "ReactDesertLandmarkPlane" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateBoxMesh()
        {
            var mesh = new Mesh { name = "ReactDesertLandmarkBox" };
            var vertices = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var triangles = new List<int>(36);
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                uvs.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            Face(new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f));
            Face(new(0.5f, -0.5f, 0.5f), new(-0.5f, -0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, 0.5f));
            Face(new(-0.5f, -0.5f, 0.5f), new(-0.5f, -0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, 0.5f));
            Face(new(0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, -0.5f));
            Face(new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f));
            Face(new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f));
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateConeMesh(int sides, string meshName)
        {
            var mesh = new Mesh { name = meshName };
            var vertices = new List<Vector3> { new(0f, 0.5f, 0f), new(0f, -0.5f, 0f) };
            for (var index = 0; index < sides; index++)
            {
                var angle = index * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Sin(angle) * 0.5f, -0.5f, Mathf.Cos(angle) * 0.5f));
            }
            var triangles = new List<int>();
            for (var index = 0; index < sides; index++)
            {
                var current = 2 + index;
                var next = 2 + (index + 1) % sides;
                triangles.AddRange(new[] { 0, next, current, 1, current, next });
            }
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateTaperedCylinderMesh(int sides, float topRadiusRatio)
        {
            var mesh = new Mesh { name = "ReactDesertLandmarkCylinder6" };
            var vertices = new List<Vector3> { new(0f, -0.5f, 0f), new(0f, 0.5f, 0f) };
            for (var index = 0; index < sides; index++)
            {
                var angle = index * Mathf.PI * 2f / sides;
                var sine = Mathf.Sin(angle) * 0.5f;
                var cosine = Mathf.Cos(angle) * 0.5f;
                vertices.Add(new Vector3(sine, -0.5f, cosine));
                vertices.Add(new Vector3(sine * topRadiusRatio, 0.5f, cosine * topRadiusRatio));
            }
            var triangles = new List<int>();
            for (var index = 0; index < sides; index++)
            {
                var next = (index + 1) % sides;
                var bottom = 2 + index * 2;
                var top = bottom + 1;
                var nextBottom = 2 + next * 2;
                var nextTop = nextBottom + 1;
                triangles.AddRange(new[] { 0, nextBottom, bottom, 1, top, nextTop,
                    bottom, nextBottom, nextTop, bottom, nextTop, top });
            }
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private sealed class ChunkContent
        {
            public ChunkContent(Transform root, int distance, int landmarks, int pyramids, int obelisks, int villagers)
            {
                Root = root;
                Distance = distance;
                Landmarks = landmarks;
                Pyramids = pyramids;
                Obelisks = obelisks;
                Villagers = villagers;
            }

            public Transform Root { get; }
            public int Distance { get; }
            public int Landmarks { get; }
            public int Pyramids { get; }
            public int Obelisks { get; }
            public int Villagers { get; }
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
    }
}
