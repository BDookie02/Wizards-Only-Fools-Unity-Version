using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [Serializable]
    public struct WofSurvivalFoliagePlacement
    {
        public int meshIndex;
        public float x;
        public float y;
        public float z;
        public float pitch;
        public float yaw;
        public float roll;
        public float scaleX;
        public float scaleY;
        public float scaleZ;
    }

    [DisallowMultipleComponent]
    public sealed class WofSurvivalFoliageRuntime : MonoBehaviour
    {
        public const int ExactReactDenseTreeCount = 2526;
        public const int ExactReactMeshCount = 24;
        public const float VisibleRadius = 820f;
        private const int InstancesPerBatch = 1023;
        private const float SpatialCellSize = 512f;

        [SerializeField] private Mesh[] meshes;
        [SerializeField] private Material foliageMaterial;
        [SerializeField] private WofSurvivalFoliagePlacement[] placements;

        private readonly List<FoliageBatch> _batches = new();
        private Transform _viewer;
        private float _nextViewerResolveAt;

        public void Configure(
            Mesh[] exactReactMeshes,
            Material exactReactMaterial,
            WofSurvivalFoliagePlacement[] exactReactPlacements)
        {
            meshes = exactReactMeshes;
            foliageMaterial = exactReactMaterial;
            placements = exactReactPlacements;
        }

        internal bool TryGetStreamingAssets(out Mesh[] exactReactMeshes, out Material exactReactMaterial)
        {
            exactReactMeshes = meshes;
            exactReactMaterial = foliageMaterial;
            return exactReactMeshes != null && exactReactMeshes.Length == ExactReactMeshCount &&
                   exactReactMaterial != null;
        }

        private void Awake()
        {
            if (!SystemInfo.supportsInstancing || foliageMaterial == null || meshes == null ||
                meshes.Length != ExactReactMeshCount || placements == null ||
                placements.Length != ExactReactDenseTreeCount)
            {
                Debug.LogError(
                    $"[WOF-AUTOMATION] SURVIVAL_FOLIAGE_FAILED instancing={SystemInfo.supportsInstancing} " +
                    $"material={foliageMaterial != null} meshes={meshes?.Length ?? 0} placements={placements?.Length ?? 0}");
                enabled = false;
                return;
            }

            foliageMaterial.enableInstancing = true;
            foliageMaterial.SetColor("_Color", Color.white);
            var batchesByKey = new Dictionary<FoliageBatchKey, FoliageBatch>();
            foreach (var placement in placements)
            {
                if (placement.meshIndex < 0 || placement.meshIndex >= meshes.Length || meshes[placement.meshIndex] == null)
                    continue;
                var cellX = Mathf.FloorToInt(placement.x / SpatialCellSize);
                var cellZ = Mathf.FloorToInt(placement.z / SpatialCellSize);
                var key = new FoliageBatchKey(placement.meshIndex, cellX, cellZ);
                if (!batchesByKey.TryGetValue(key, out var batch) || batch.Count >= InstancesPerBatch)
                {
                    batch = new FoliageBatch(
                        meshes[placement.meshIndex],
                        new Vector3((cellX + 0.5f) * SpatialCellSize, 0f, (cellZ + 0.5f) * SpatialCellSize));
                    batchesByKey[key] = batch;
                    _batches.Add(batch);
                }
                batch.Add(Matrix4x4.TRS(
                    new Vector3(placement.x, placement.y, placement.z),
                    Quaternion.Euler(
                        placement.pitch * Mathf.Rad2Deg,
                        placement.yaw * Mathf.Rad2Deg,
                        placement.roll * Mathf.Rad2Deg),
                    new Vector3(placement.scaleX, placement.scaleY, placement.scaleZ)));
            }
            Debug.Log(
                $"[WOF-AUTOMATION] SURVIVAL_FOLIAGE_READY trees={placements.Length} meshes={meshes.Length} batches={_batches.Count}");
        }

        private void Update()
        {
            ResolveViewer();
            if (_viewer == null) return;
            var viewer = _viewer.position;
            var radiusSquared = VisibleRadius * VisibleRadius;
            foreach (var batch in _batches)
            {
                var dx = batch.Center.x - viewer.x;
                var dz = batch.Center.z - viewer.z;
                if (dx * dx + dz * dz > radiusSquared) continue;
                Graphics.DrawMeshInstanced(
                    batch.Mesh,
                    0,
                    foliageMaterial,
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

        private void ResolveViewer()
        {
            if (_viewer != null || Time.unscaledTime < _nextViewerResolveAt) return;
            _nextViewerResolveAt = Time.unscaledTime + 0.25f;
            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (playerObject != null) _viewer = playerObject.transform;
        }

        private readonly struct FoliageBatchKey : IEquatable<FoliageBatchKey>
        {
            private readonly int _mesh;
            private readonly int _x;
            private readonly int _z;

            public FoliageBatchKey(int mesh, int x, int z)
            {
                _mesh = mesh;
                _x = x;
                _z = z;
            }

            public bool Equals(FoliageBatchKey other) => _mesh == other._mesh && _x == other._x && _z == other._z;
            public override bool Equals(object obj) => obj is FoliageBatchKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_mesh, _x, _z);
        }

        private sealed class FoliageBatch
        {
            private readonly Vector4[] _instanceColors = new Vector4[InstancesPerBatch];

            public FoliageBatch(Mesh mesh, Vector3 center)
            {
                Mesh = mesh;
                Center = center;
                for (var index = 0; index < _instanceColors.Length; index++)
                {
                    _instanceColors[index] = Vector4.one;
                }
                Properties.SetVectorArray("_InstanceColor", _instanceColors);
            }

            public Mesh Mesh { get; }
            public Vector3 Center { get; }
            public Matrix4x4[] Matrices { get; } = new Matrix4x4[InstancesPerBatch];
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
