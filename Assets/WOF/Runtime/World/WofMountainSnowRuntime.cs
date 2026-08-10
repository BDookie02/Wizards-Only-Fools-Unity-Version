using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofMountainSnowRuntime : MonoBehaviour
    {
        public const int DesktopFlakeCount = 240;
        public const int MobileFlakeCount = 120;
        public const float ActiveRadius = 900f;

        private const float FieldRadius = 44f;
        private const float FieldHeight = 72f;
        private const float MobileUpdateInterval = 1f / 24f;
        private Matrix4x4[] _matrices = Array.Empty<Matrix4x4>();
        private Mesh _quad;
        private Material _material;
        private float _nextUpdateAt;
        private bool _motionProbe;
        private bool _motionProbeStarted;
        private bool _motionProbeFinished;
        private float _motionProbeStartedAt;
        private Vector3 _motionProbeStart;

        public int ActiveFlakeCount => _matrices.Length;

        private void Awake()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (!argument.Equals("--wof-mountain-snow-probe", StringComparison.OrdinalIgnoreCase)) continue;
                _motionProbe = true;
                break;
            }
            _quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _material = new Material(shader)
            {
                name = "WOF_MountainSnowfall",
                color = new Color32(238, 248, 255, 220),
                enableInstancing = true,
                renderQueue = (int)RenderQueue.Transparent
            };
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", _material.color);
            _material.SetFloat("_Surface", 1f);
            _material.SetFloat("_ZWrite", 0f);
            _material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _matrices = new Matrix4x4[WofPerformanceModeRuntime.IsMobilePerformanceMode
                ? MobileFlakeCount
                : DesktopFlakeCount];
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        private void Update()
        {
            var camera = Camera.main;
            if (camera == null || _quad == null || _material == null) return;
            var local = camera.transform.position - transform.position;
            var horizontalDistance = new Vector2(local.x, local.z).magnitude;
            if (horizontalDistance > ActiveRadius ||
                horizontalDistance < 65f && local.y < WofMountainVillageLayout.ReactSummitY - 15f) return;
            if (!WofPerformanceModeRuntime.IsMobilePerformanceMode || Time.unscaledTime >= _nextUpdateAt)
            {
                UpdateMatrices(camera, Time.time);
                _nextUpdateAt = Time.unscaledTime + MobileUpdateInterval;
                UpdateMotionProbe();
            }
            Graphics.DrawMeshInstanced(
                _quad, 0, _material, _matrices, _matrices.Length, null,
                ShadowCastingMode.Off, false, gameObject.layer, null, LightProbeUsage.Off);
        }

        private void UpdateMatrices(Camera camera, float time)
        {
            var cameraPosition = camera.transform.position;
            var cameraRotation = camera.transform.rotation;
            for (var index = 0; index < _matrices.Length; index++)
            {
                var seedX = Hash01(index, 17f);
                var seedY = Hash01(index, 43f);
                var seedZ = Hash01(index, 79f);
                var speed = Mathf.Lerp(5.8f, 10.8f, Hash01(index, 113f));
                var fall = Mathf.Repeat(time * speed + seedY * FieldHeight, FieldHeight);
                var wind = Mathf.Sin(time * 0.72f + seedX * 12.4f) * 2.8f;
                var position = new Vector3(
                    cameraPosition.x + (seedX * 2f - 1f) * FieldRadius + wind,
                    cameraPosition.y + FieldHeight * 0.58f - fall,
                    cameraPosition.z + (seedZ * 2f - 1f) * FieldRadius);
                var size = Mathf.Lerp(0.12f, 0.34f, Hash01(index, 151f));
                var stretch = Mathf.Lerp(1.4f, 2.7f, Hash01(index, 181f));
                _matrices[index] = Matrix4x4.TRS(
                    position,
                    cameraRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(time + seedX * 8f) * 24f),
                    new Vector3(size, size * stretch, 1f));
            }
        }

        private void UpdateMotionProbe()
        {
            if (!_motionProbe || _motionProbeFinished || _matrices.Length == 0) return;
            var current = new Vector3(_matrices[0].m03, _matrices[0].m13, _matrices[0].m23);
            if (!_motionProbeStarted)
            {
                _motionProbeStarted = true;
                _motionProbeStartedAt = Time.unscaledTime;
                _motionProbeStart = current;
                Debug.Log($"[WOF-AUTOMATION] MOUNTAIN_SNOW_RENDER_READY flakes={ActiveFlakeCount}");
                return;
            }
            if (Time.unscaledTime - _motionProbeStartedAt < 1.5f) return;
            var delta = Vector3.Distance(_motionProbeStart, current);
            _motionProbeFinished = true;
            Debug.Log($"[WOF-AUTOMATION] MOUNTAIN_SNOW_MOTION_{(delta > 0.5f ? "PASS" : "FAIL")} delta={delta:F2} flakes={ActiveFlakeCount}");
        }

        private static float Hash01(int index, float salt)
        {
            var value = Mathf.Sin(index * 127.1f + salt * 311.7f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }
    }
}
