using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofMountainAccessPathRuntime : MonoBehaviour
    {
        [SerializeField] private Vector3[] localPoints = Array.Empty<Vector3>();
        [SerializeField] private MeshCollider pathCollider;

        private bool _probeRequested;
        private bool _probeFinished;
        private Mesh _runtimeDeckMesh;
        private Mesh _runtimeTopMesh;
        private Mesh _runtimeColliderMesh;

        public int PointCount => localPoints?.Length ?? 0;
        public Vector3 StartLocalPoint => PointCount == 0 ? Vector3.zero : localPoints[0];
        public Vector3 EndLocalPoint => PointCount == 0 ? Vector3.zero : localPoints[PointCount - 1];

        internal bool TryCopyWorldPoints(out Vector3[] worldPoints)
        {
            if (PointCount < 2)
            {
                worldPoints = Array.Empty<Vector3>();
                return false;
            }

            worldPoints = new Vector3[PointCount];
            for (var index = 0; index < PointCount; index++)
                worldPoints[index] = transform.TransformPoint(localPoints[index]);
            return true;
        }

        public void Configure(Vector3[] exactLocalPoints, MeshCollider exactPathCollider)
        {
            localPoints = exactLocalPoints ?? Array.Empty<Vector3>();
            pathCollider = exactPathCollider;
        }

        private void Awake()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (!argument.Equals("--wof-mountain-access-path-probe", StringComparison.OrdinalIgnoreCase)) continue;
                _probeRequested = true;
                break;
            }
        }

        private void Start()
        {
            RebuildNaturalFaceTrail();
        }

        private void OnDestroy()
        {
            if (_runtimeDeckMesh != null) Destroy(_runtimeDeckMesh);
            if (_runtimeTopMesh != null) Destroy(_runtimeTopMesh);
            if (_runtimeColliderMesh != null) Destroy(_runtimeColliderMesh);
        }

        private void Update()
        {
            if (!_probeRequested || _probeFinished || Time.unscaledTime < 1f) return;
            _probeFinished = true;
            if (!TryValidate(out var maximumGrade, out var maximumGap, out var raycastMisses))
            {
                Debug.LogError($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_PATH_CONTINUITY_FAIL points={PointCount} maxGrade={maximumGrade:F3} maxGap={maximumGap:F2} raycastMisses={raycastMisses}");
                return;
            }
            Debug.Log($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_PATH_CONTINUITY_PASS points={PointCount} maxGrade={maximumGrade:F3} maxGap={maximumGap:F2} raycastMisses=0 start={StartLocalPoint} end={EndLocalPoint}");
        }

        internal bool TryValidate(out float maximumGrade, out float maximumGap, out int raycastMisses)
        {
            maximumGrade = 0f;
            maximumGap = 0f;
            raycastMisses = 0;
            if (PointCount < 8 || pathCollider == null || pathCollider.sharedMesh == null) return false;
            for (var index = 0; index < PointCount; index++)
            {
                var point = localPoints[index];
                var world = transform.TransformPoint(point);
                var ray = new Ray(world + Vector3.up * 4f, Vector3.down);
                if (!pathCollider.Raycast(ray, out _, 8f)) raycastMisses++;
                if (index == 0) continue;
                var previous = localPoints[index - 1];
                var horizontal = Vector2.Distance(
                    new Vector2(previous.x, previous.z),
                    new Vector2(point.x, point.z));
                maximumGap = Mathf.Max(maximumGap, horizontal);
                if (horizontal > 0.001f)
                    maximumGrade = Mathf.Max(maximumGrade, Mathf.Abs(point.y - previous.y) / horizontal);
            }
            return raycastMisses == 0 && maximumGrade <= WofMountainAccessPathLayout.MaximumGrade + 0.015f &&
                   maximumGap <= WofMountainAccessPathLayout.MaximumSegmentLength + 0.05f &&
                   StartLocalPoint.z > 300f &&
                   new Vector2(EndLocalPoint.x, EndLocalPoint.z).magnitude < 100f;
        }

        private void RebuildNaturalFaceTrail()
        {
            var mountainRoot = transform.parent;
            if (mountainRoot == null || pathCollider == null)
            {
                Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_ACCESS_PATH_REBUILD_FAIL stage=hierarchy");
                return;
            }

            Physics.SyncTransforms();
            var horizontal = WofMountainAccessPathLayout.BuildHorizontalPoints();
            var surfaceHeights = new float[horizontal.Length];
            var terrainCollider = FindExactMountainTerrainCollider(mountainRoot);
            if (terrainCollider == null || terrainCollider.sharedMesh == null)
            {
                Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_ACCESS_PATH_REBUILD_FAIL stage=terrain-collider");
                return;
            }
            var points = BuildSurfaceConformingPoints(
                mountainRoot,
                terrainCollider,
                horizontal,
                surfaceHeights,
                out var terrainSampleMisses);

            var deckFilter = transform.Find("ContinuousSwitchbackDeck")?.GetComponent<MeshFilter>();
            var topFilter = transform.Find("DirtStoneWalkingSurface")?.GetComponent<MeshFilter>();
            if (deckFilter == null || topFilter == null)
            {
                Debug.LogError("[WOF-AUTOMATION] MOUNTAIN_ACCESS_PATH_REBUILD_FAIL stage=visuals");
                return;
            }

            _runtimeDeckMesh = CreateDeckMesh(points, WofMountainAccessPathLayout.Width);
            _runtimeDeckMesh.name = "MountainNaturalFaceTrailDeckRuntime";
            _runtimeTopMesh = CreateTopMesh(points, WofMountainAccessPathLayout.Width * 0.94f);
            _runtimeTopMesh.name = "MountainNaturalFaceTrailTopRuntime";
            _runtimeColliderMesh = CreateDeckMesh(points, WofMountainAccessPathLayout.Width * 0.96f);
            _runtimeColliderMesh.name = "MountainNaturalFaceTrailColliderRuntime";
            deckFilter.sharedMesh = _runtimeDeckMesh;
            topFilter.sharedMesh = _runtimeTopMesh;
            pathCollider.enabled = false;
            pathCollider.sharedMesh = null;
            pathCollider.sharedMesh = _runtimeColliderMesh;
            pathCollider.enabled = true;
            localPoints = points;

            DisableBridgeFurniture();
            var maximumLift = 0f;
            for (var index = 0; index < points.Length; index++)
                maximumLift = Mathf.Max(maximumLift, points[index].y - surfaceHeights[index]);
            Debug.Log($"[WOF-AUTOMATION] MOUNTAIN_ACCESS_PATH_REBUILT style=natural-face-switchback points={points.Length} width={WofMountainAccessPathLayout.Width:F1} maxSurfaceLift={maximumLift:F2} terrainSampleMisses={terrainSampleMisses}");
        }

        private void DisableBridgeFurniture()
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;
                if (!child.name.StartsWith("Rail_", StringComparison.Ordinal) &&
                    !child.name.StartsWith("RailPost_", StringComparison.Ordinal) &&
                    !child.name.StartsWith("BridgeSupport_", StringComparison.Ordinal)) continue;
                child.gameObject.SetActive(false);
            }
        }

        private static Vector3[] BuildSurfaceConformingPoints(
            Transform mountainRoot,
            MeshCollider terrainCollider,
            IReadOnlyList<Vector2> horizontal,
            float[] surfaceHeights,
            out int terrainSampleMisses)
        {
            terrainSampleMisses = 0;
            var points = new Vector3[horizontal.Count];
            var terrainVertices = terrainCollider.sharedMesh.vertices;
            var terrainTriangles = terrainCollider.sharedMesh.triangles;
            for (var index = 0; index < horizontal.Count; index++)
            {
                var local = horizontal[index];
                if (!TrySampleTerrainSurfaceHeight(
                        mountainRoot,
                        terrainCollider.transform,
                        terrainVertices,
                        terrainTriangles,
                        local.x,
                        local.y,
                        out var surfaceHeight))
                {
                    terrainSampleMisses++;
                    var worldX = mountainRoot.position.x + local.x;
                    var worldZ = mountainRoot.position.z + local.y;
                    var chunkX = WofSurvivalTerrainMath.GetChunkCoordinate(worldX);
                    var chunkZ = WofSurvivalTerrainMath.GetChunkCoordinate(worldZ);
                    surfaceHeight = (float)WofSurvivalTerrainMath.GetTerrainHeight(
                        chunkX,
                        chunkZ,
                        worldX - chunkX * WofSurvivalTerrainMath.BlockSize,
                        worldZ - chunkZ * WofSurvivalTerrainMath.BlockSize);
                }
                surfaceHeights[index] = surfaceHeight;
                points[index] = new Vector3(local.x, surfaceHeight + WofMountainAccessPathLayout.DeckClearance, local.y);
            }

            points[points.Length - 1].y = Mathf.Max(points[points.Length - 1].y,
                WofMountainVillageLayout.ReactSummitY + 1.35f);
            for (var pass = 0; pass < 3; pass++)
            {
                for (var index = 1; index < points.Length; index++)
                {
                    var horizontalDistance = Vector2.Distance(
                        new Vector2(points[index - 1].x, points[index - 1].z),
                        new Vector2(points[index].x, points[index].z));
                    var minimum = points[index - 1].y -
                                  horizontalDistance * WofMountainAccessPathLayout.MaximumGrade;
                    points[index].y = Mathf.Max(points[index].y, minimum);
                }
                for (var index = points.Length - 2; index >= 0; index--)
                {
                    var horizontalDistance = Vector2.Distance(
                        new Vector2(points[index].x, points[index].z),
                        new Vector2(points[index + 1].x, points[index + 1].z));
                    var minimum = points[index + 1].y -
                                  horizontalDistance * WofMountainAccessPathLayout.MaximumGrade;
                    points[index].y = Mathf.Max(points[index].y, minimum);
                }
            }
            return points;
        }

        private static MeshCollider FindExactMountainTerrainCollider(Transform mountainRoot)
        {
            foreach (var candidate in mountainRoot.GetComponentsInChildren<MeshCollider>(true))
            {
                if (string.Equals(candidate.gameObject.name, "ExactMountainTerrainCollider", StringComparison.Ordinal))
                    return candidate;
            }
            return null;
        }

        public static bool TrySampleTerrainSurfaceHeight(
            Transform mountainRoot,
            Transform terrainTransform,
            IReadOnlyList<Vector3> terrainVertices,
            IReadOnlyList<int> terrainTriangles,
            float localX,
            float localZ,
            out float surfaceHeight)
        {
            surfaceHeight = 0f;
            if (mountainRoot == null || terrainTransform == null || terrainVertices == null || terrainTriangles == null)
                return false;

            var mountainLocal = new Vector3(localX, 0f, localZ);
            var colliderLocal = terrainTransform.InverseTransformPoint(mountainRoot.TransformPoint(mountainLocal));
            var found = false;
            var highestColliderLocalY = float.NegativeInfinity;
            for (var index = 0; index < terrainTriangles.Count; index += 3)
            {
                var a = terrainVertices[terrainTriangles[index]];
                var b = terrainVertices[terrainTriangles[index + 1]];
                var c = terrainVertices[terrainTriangles[index + 2]];
                var denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) <= 0.000001f) continue;
                var u = ((b.z - c.z) * (colliderLocal.x - c.x) +
                         (c.x - b.x) * (colliderLocal.z - c.z)) / denominator;
                var v = ((c.z - a.z) * (colliderLocal.x - c.x) +
                         (a.x - c.x) * (colliderLocal.z - c.z)) / denominator;
                var w = 1f - u - v;
                const float edgeTolerance = 0.0001f;
                if (u < -edgeTolerance || v < -edgeTolerance || w < -edgeTolerance) continue;
                highestColliderLocalY = Mathf.Max(highestColliderLocalY, u * a.y + v * b.y + w * c.y);
                found = true;
            }

            if (!found) return false;
            var colliderSurface = new Vector3(colliderLocal.x, highestColliderLocalY, colliderLocal.z);
            surfaceHeight = mountainRoot.InverseTransformPoint(
                terrainTransform.TransformPoint(colliderSurface)).y;
            return true;
        }

        private static Mesh CreateTopMesh(IReadOnlyList<Vector3> points, float width)
        {
            var vertices = new Vector3[points.Count * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(points.Count - 1) * 6];
            for (var index = 0; index < points.Count; index++)
            {
                var right = GetRight(points, index);
                vertices[index * 2] = points[index] - right * (width * 0.5f) + Vector3.up * 0.49f;
                vertices[index * 2 + 1] = points[index] + right * (width * 0.5f) + Vector3.up * 0.49f;
                uvs[index * 2] = new Vector2(0f, index * 0.35f);
                uvs[index * 2 + 1] = new Vector2(1f, index * 0.35f);
                if (index >= points.Count - 1) continue;
                var write = index * 6;
                var current = index * 2;
                triangles[write] = current;
                triangles[write + 1] = current + 2;
                triangles[write + 2] = current + 1;
                triangles[write + 3] = current + 1;
                triangles[write + 4] = current + 2;
                triangles[write + 5] = current + 3;
            }
            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateDeckMesh(IReadOnlyList<Vector3> points, float width)
        {
            const float topOffset = 0.44f;
            const float bottomOffset = -0.52f;
            var vertices = new Vector3[points.Count * 4];
            var triangles = new List<int>((points.Count - 1) * 24 + 12);
            for (var index = 0; index < points.Count; index++)
            {
                var right = GetRight(points, index);
                var leftPosition = points[index] - right * (width * 0.5f);
                var rightPosition = points[index] + right * (width * 0.5f);
                var write = index * 4;
                vertices[write] = leftPosition + Vector3.up * topOffset;
                vertices[write + 1] = rightPosition + Vector3.up * topOffset;
                vertices[write + 2] = leftPosition + Vector3.up * bottomOffset;
                vertices[write + 3] = rightPosition + Vector3.up * bottomOffset;
                if (index >= points.Count - 1) continue;
                var next = write + 4;
                AddQuad(triangles, write, next, write + 1, next + 1);
                AddQuad(triangles, write + 2, write + 3, next + 2, next + 3);
                AddQuad(triangles, write, write + 2, next, next + 2);
                AddQuad(triangles, write + 1, next + 1, write + 3, next + 3);
            }
            AddQuad(triangles, 0, 1, 2, 3);
            var last = (points.Count - 1) * 4;
            AddQuad(triangles, last, last + 2, last + 1, last + 3);
            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(c);
            triangles.Add(b);
            triangles.Add(d);
        }

        private static Vector3 GetRight(IReadOnlyList<Vector3> points, int index)
        {
            var previous = points[Mathf.Max(0, index - 1)];
            var next = points[Mathf.Min(points.Count - 1, index + 1)];
            var tangent = next - previous;
            tangent.y = 0f;
            tangent.Normalize();
            return new Vector3(tangent.z, 0f, -tangent.x);
        }
    }
}
