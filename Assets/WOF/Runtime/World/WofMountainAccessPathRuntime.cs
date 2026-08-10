using System;
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

        public int PointCount => localPoints?.Length ?? 0;
        public Vector3 StartLocalPoint => PointCount == 0 ? Vector3.zero : localPoints[0];
        public Vector3 EndLocalPoint => PointCount == 0 ? Vector3.zero : localPoints[PointCount - 1];

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
                   StartLocalPoint.z > 620f && new Vector2(EndLocalPoint.x, EndLocalPoint.z).magnitude < 100f;
        }
    }
}
