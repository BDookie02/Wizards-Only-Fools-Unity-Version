using System;
using System.Collections.Generic;
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

        private readonly List<BeaconView> _views = new();
        private readonly HashSet<string> _lastTargetIds = new(StringComparer.Ordinal);
        private IReadOnlyList<WofQuestNavigationTarget> _targets = Array.Empty<WofQuestNavigationTarget>();
        private WofQuestNavigationTarget _target;
        private float _nextRefreshAt;

        public WofQuestNavigationTarget CurrentTarget => _target;
        public IReadOnlyList<WofQuestNavigationTarget> CurrentTargets => _targets;

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
            if (visualRoot != null)
            {
                _views.Add(CreateView(visualRoot, beam, ring, icon));
                visualRoot.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            for (var index = 0; index < _views.Count; index++)
            {
                _views[index].DestroyMaterials();
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRefreshAt)
            {
                _nextRefreshAt = Time.unscaledTime + 0.25f;
                RefreshTargets();
            }
            AnimateTargets();
        }

        private void RefreshTargets()
        {
            var bootstrap = WofBootstrap.Instance;
            if (bootstrap == null || bootstrap.Mode == WofSessionMode.None || !bootstrap.IsSurvivalSession)
            {
                SetTargets(Array.Empty<WofQuestNavigationTarget>());
                return;
            }

            SetTargets(WofQuestNavigationRules.ResolveAll(
                WofSurvivalProfileStore.Load(),
                WofQuestDevStore.LoadPrograms()));
        }

        private void SetTargets(IReadOnlyList<WofQuestNavigationTarget> targets)
        {
            _targets = targets ?? Array.Empty<WofQuestNavigationTarget>();
            _target = _targets.Count > 0 ? _targets[0] : null;
            EnsureViewCount(_targets.Count);

            var currentIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < _views.Count; index++)
            {
                var visible = index < _targets.Count;
                var view = _views[index];
                view.Root.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var target = _targets[index];
                currentIds.Add(target.Id);
                view.Root.position = target.Position;
                var color = WofDarrelQuestNavigationRules.ResolveColor(target.Tone);
                SetColor(view.BeamMaterial, color, 0.24f);
                SetColor(view.RingMaterial, color, 0.72f);
                SetColor(view.DiskMaterial, color, 0.18f);
                SetColor(view.IconMaterial, color, 0.96f);
                if (!_lastTargetIds.Contains(target.Id))
                {
                    Debug.Log($"[WOF-AUTOMATION] QUEST_NAVIGATION_TARGET id={target.Id} label=\"{target.Label}\" tone={target.Tone} position={target.Position}");
                }
            }

            _lastTargetIds.Clear();
            foreach (var id in currentIds)
            {
                _lastTargetIds.Add(id);
            }
        }

        private void EnsureViewCount(int requiredCount)
        {
            if (visualRoot == null)
            {
                return;
            }

            while (_views.Count < requiredCount)
            {
                var cloneObject = Instantiate(visualRoot.gameObject, visualRoot.parent);
                cloneObject.name = $"QuestNavigationBeacon_{_views.Count + 1}";
                var cloneRoot = cloneObject.transform;
                _views.Add(CreateView(
                    cloneRoot,
                    cloneRoot.Find("Beam"),
                    cloneRoot.Find("Ring"),
                    cloneRoot.Find("Icon")?.GetComponent<SpriteRenderer>()));
            }
        }

        private BeaconView CreateView(
            Transform root,
            Transform viewBeam,
            Transform viewRing,
            SpriteRenderer viewIcon)
        {
            var viewDisk = root.Find("Disk");
            var localBeamMaterial = CloneMaterial(beamMaterial, $"Beam_{_views.Count + 1}");
            var localRingMaterial = CloneMaterial(ringMaterial, $"Ring_{_views.Count + 1}");
            var localDiskMaterial = CloneMaterial(diskMaterial, $"Disk_{_views.Count + 1}");
            var localIconMaterial = CloneMaterial(iconMaterial, $"Icon_{_views.Count + 1}");
            AssignMaterial(viewBeam, localBeamMaterial);
            AssignMaterial(viewRing, localRingMaterial);
            AssignMaterial(viewDisk, localDiskMaterial);
            if (viewIcon != null)
            {
                viewIcon.sharedMaterial = localIconMaterial;
            }

            return new BeaconView(
                root,
                viewBeam,
                viewRing,
                viewIcon,
                localBeamMaterial,
                localRingMaterial,
                localDiskMaterial,
                localIconMaterial);
        }

        private void AnimateTargets()
        {
            if (_targets.Count == 0)
            {
                return;
            }

            var elapsed = Time.unscaledTime;
            var camera = Camera.main;
            for (var index = 0; index < _targets.Count && index < _views.Count; index++)
            {
                var target = _targets[index];
                var view = _views[index];
                if (!view.Root.gameObject.activeSelf)
                {
                    continue;
                }

                var pulse = 1f + Mathf.Sin(
                    elapsed * 2.8f + target.Position.x * 0.01f + target.Position.z * 0.01f) * 0.08f;
                view.Root.localScale = Vector3.one * pulse;
                if (view.Beam != null)
                {
                    view.Beam.localRotation = Quaternion.Euler(0f, elapsed * 0.45f * Mathf.Rad2Deg, 0f);
                }
                if (view.Ring != null)
                {
                    view.Ring.localRotation = Quaternion.Euler(0f, elapsed * 0.9f * Mathf.Rad2Deg, 0f);
                }
                if (view.Icon != null && camera != null)
                {
                    var direction = view.Icon.transform.position - camera.transform.position;
                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        view.Icon.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    }
                }
            }
        }

        private static void AssignMaterial(Transform target, Material material)
        {
            var renderer = target?.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material CloneMaterial(Material source, string suffix)
        {
            if (source == null)
            {
                return null;
            }

            return new Material(source) { name = $"{source.name}_{suffix}" };
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

        private sealed class BeaconView
        {
            public BeaconView(
                Transform root,
                Transform beam,
                Transform ring,
                SpriteRenderer icon,
                Material beamMaterial,
                Material ringMaterial,
                Material diskMaterial,
                Material iconMaterial)
            {
                Root = root;
                Beam = beam;
                Ring = ring;
                Icon = icon;
                BeamMaterial = beamMaterial;
                RingMaterial = ringMaterial;
                DiskMaterial = diskMaterial;
                IconMaterial = iconMaterial;
            }

            public Transform Root { get; }
            public Transform Beam { get; }
            public Transform Ring { get; }
            public SpriteRenderer Icon { get; }
            public Material BeamMaterial { get; }
            public Material RingMaterial { get; }
            public Material DiskMaterial { get; }
            public Material IconMaterial { get; }

            public void DestroyMaterials()
            {
                DestroyMaterial(BeamMaterial);
                DestroyMaterial(RingMaterial);
                DestroyMaterial(DiskMaterial);
                DestroyMaterial(IconMaterial);
            }

            private static void DestroyMaterial(Material material)
            {
                if (material != null)
                {
                    UnityEngine.Object.Destroy(material);
                }
            }
        }
    }
}
