using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace WOF
{
    public sealed class WofWaterRippleRuntime : MonoBehaviour
    {
        private sealed class ActiveRipple
        {
            public GameObject GameObject;
            public Material Material;
            public float SpawnedAt;
            public float LastMobileUpdateAt = float.NegativeInfinity;
        }

        [SerializeField] private Mesh desktopRingMesh;
        [SerializeField] private Mesh mobileRingMesh;
        [SerializeField] private Material rippleMaterial;

        private readonly List<ActiveRipple> _activeRipples = new(3);
        private bool _mobilePerformanceMode;
        private float _lastRippleAt;

        public void Configure(Mesh desktopMesh, Mesh mobileMesh, Material material)
        {
            desktopRingMesh = desktopMesh;
            mobileRingMesh = mobileMesh;
            rippleMaterial = material;
        }

        private void Awake()
        {
            _mobilePerformanceMode = WofPerformanceModeRuntime.IsMobilePerformanceMode;
        }

        private void Update()
        {
            var now = Time.time;
            UpdateActiveRipples(now);

            var networkManager = NetworkManager.Singleton;
            var playerObject = networkManager == null ? null : networkManager.LocalClient?.PlayerObject;
            var player = playerObject == null ? null : playerObject.GetComponent<WofPlayerController>();
            if (player == null || !player.IsMoving || !player.IsGrounded ||
                !WofWaterRippleLayout.IsBaseVillageWaterRippleSpot(player.transform.position))
            {
                return;
            }

            var interval = _mobilePerformanceMode
                ? WofWaterRippleLayout.MobileSpawnIntervalSeconds
                : WofWaterRippleLayout.DesktopSpawnIntervalSeconds;
            if (now - _lastRippleAt <= interval)
            {
                return;
            }

            _lastRippleAt = now;
            SpawnRipple(player.transform.position.x, player.transform.position.z, now);
        }

        private void SpawnRipple(float x, float z, float now)
        {
            var mesh = _mobilePerformanceMode ? mobileRingMesh : desktopRingMesh;
            if (mesh == null || rippleMaterial == null)
            {
                return;
            }

            var rippleObject = new GameObject("WaterRipple", typeof(MeshFilter), typeof(MeshRenderer));
            rippleObject.transform.SetParent(transform, true);
            rippleObject.transform.SetPositionAndRotation(
                new Vector3(x, WofWaterRippleLayout.BaseVillageRippleY, z),
                Quaternion.Euler(-90f, 0f, 0f));
            rippleObject.transform.localScale = Vector3.one * 0.5f;
            rippleObject.GetComponent<MeshFilter>().sharedMesh = mesh;

            var material = new Material(rippleMaterial) { name = "Water Ripple Instance" };
            SetOpacity(material, 1f);
            var renderer = rippleObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = -1;
            _activeRipples.Add(new ActiveRipple
            {
                GameObject = rippleObject,
                Material = material,
                SpawnedAt = now
            });
        }

        private void UpdateActiveRipples(float now)
        {
            for (var index = _activeRipples.Count - 1; index >= 0; index--)
            {
                var ripple = _activeRipples[index];
                var age = now - ripple.SpawnedAt;
                if (age >= WofWaterRippleLayout.RippleLifetimeSeconds)
                {
                    Destroy(ripple.Material);
                    Destroy(ripple.GameObject);
                    _activeRipples.RemoveAt(index);
                    continue;
                }

                if (_mobilePerformanceMode && now - ripple.LastMobileUpdateAt < 1f / 30f)
                {
                    continue;
                }

                ripple.LastMobileUpdateAt = now;
                var scale = WofWaterRippleLayout.ResolveScale(age);
                ripple.GameObject.transform.localScale = Vector3.one * scale;
                SetOpacity(ripple.Material, WofWaterRippleLayout.ResolveOpacity(age));
            }
        }

        private static void SetOpacity(Material material, float opacity)
        {
            var color = Color.white;
            color.a = opacity;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
    }
}
