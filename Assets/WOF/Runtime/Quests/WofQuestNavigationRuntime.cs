using System;
using UnityEngine;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofQuestNavigationRuntime : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform beam;
        [SerializeField] private Transform ring;
        [SerializeField] private SpriteRenderer icon;
        [SerializeField] private Material beamMaterial;
        [SerializeField] private Material ringMaterial;
        [SerializeField] private Material diskMaterial;
        [SerializeField] private Material iconMaterial;

        private WofQuestNavigationTarget _target;
        private float _nextRefreshAt;
        private string _lastTargetId;

        public WofQuestNavigationTarget CurrentTarget => _target;

        public void ConfigureGeneratedView(
            Transform generatedVisualRoot,
            Transform generatedBeam,
            Transform generatedRing,
            SpriteRenderer generatedIcon,
            Material generatedBeamMaterial,
            Material generatedRingMaterial,
            Material generatedDiskMaterial,
            Material generatedIconMaterial)
        {
            visualRoot = generatedVisualRoot;
            beam = generatedBeam;
            ring = generatedRing;
            icon = generatedIcon;
            beamMaterial = generatedBeamMaterial;
            ringMaterial = generatedRingMaterial;
            diskMaterial = generatedDiskMaterial;
            iconMaterial = generatedIconMaterial;
        }

        private void Awake()
        {
            visualRoot?.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRefreshAt)
            {
                _nextRefreshAt = Time.unscaledTime + 0.25f;
                RefreshTarget();
            }
            AnimateTarget();
        }

        private void RefreshTarget()
        {
            var bootstrap = WofBootstrap.Instance;
            if (bootstrap == null || bootstrap.Mode == WofSessionMode.None || !bootstrap.IsSurvivalSession)
            {
                SetTarget(null);
                return;
            }
            SetTarget(WofDarrelQuestNavigationRules.Resolve(WofSurvivalProfileStore.Load()));
        }

        private void SetTarget(WofQuestNavigationTarget target)
        {
            _target = target;
            var visible = target != null;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(visible);
            }
            if (!visible)
            {
                _lastTargetId = null;
                return;
            }

            visualRoot.position = target.Position;
            var color = WofDarrelQuestNavigationRules.ResolveColor(target.Tone);
            SetColor(beamMaterial, color, 0.24f);
            SetColor(ringMaterial, color, 0.72f);
            SetColor(diskMaterial, color, 0.18f);
            SetColor(iconMaterial, color, 0.96f);
            if (!string.Equals(_lastTargetId, target.Id, StringComparison.Ordinal))
            {
                _lastTargetId = target.Id;
                Debug.Log($"[WOF-AUTOMATION] QUEST_NAVIGATION_TARGET id={target.Id} label=\"{target.Label}\" tone={target.Tone} position={target.Position}");
            }
        }

        private void AnimateTarget()
        {
            if (_target == null || visualRoot == null || !visualRoot.gameObject.activeSelf)
            {
                return;
            }

            var elapsed = Time.unscaledTime;
            var pulse = 1f + Mathf.Sin(
                elapsed * 2.8f + _target.Position.x * 0.01f + _target.Position.z * 0.01f) * 0.08f;
            visualRoot.localScale = Vector3.one * pulse;
            if (beam != null)
            {
                beam.localRotation = Quaternion.Euler(0f, elapsed * 0.45f * Mathf.Rad2Deg, 0f);
            }
            if (ring != null)
            {
                ring.localRotation = Quaternion.Euler(0f, elapsed * 0.9f * Mathf.Rad2Deg, 0f);
            }

            var camera = Camera.main;
            if (icon != null && camera != null)
            {
                var direction = icon.transform.position - camera.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    icon.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }
        }

        private static void SetColor(Material material, Color color, float alpha)
        {
            if (material == null)
            {
                return;
            }
            color.a = alpha;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }
    }
}
