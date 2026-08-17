using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofManaSourceRuntime : MonoBehaviour
    {
        private const float CollectionPollSeconds = 0.1f;
        private const float ResolvePlayerSeconds = 0.25f;
        private const float MobileVisualSeconds = 1f / 24f;
        private static WofManaSourceRuntime _instance;

        private readonly HashSet<int> _collectedRunes = new();
        private readonly List<ManaPulse> _pulses = new(6);
        private WofPlayerController _player;
        private Mesh _octahedron;
        private Mesh _torus;
        private Mesh _disc;
        private Material _runeMaterial;
        private Material _infiniteMaterial;
        private Material _baseRingMaterial;
        private Material _wellRingMaterial;
        private Material _wellDiscMaterial;
        private Material[] _pulseMaterials;
        private Matrix4x4[] _runeMatrices = Array.Empty<Matrix4x4>();
        private int[] _activeRunes = Array.Empty<int>();
        private int _runeMatrixCount;
        private long _runeCycle = long.MinValue;
        private float _nextResolveAt;
        private float _nextCollectionAt;
        private float _nextVisualAt;
        private float _nextRequestAt;
        private bool _showBaseSources;
        private bool _showDesertWell;
        private Light _baseLight;
        private Light _wellLight;
        private bool _probeRequested;
        private string _probeKind;
        private bool _probePositioned;
        private bool _probeMovedToCollect;
        private bool _probeFinished;
        private float _probeCollectAt;
        private float _probeTimeoutAt;
        private WofManaSourceRecord _probeSource;

        public int VisibleRuneCount => _runeMatrixCount;
        public bool BaseSourceVisible => _showBaseSources;
        public bool DesertWellVisible => _showDesertWell;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            if (FindFirstObjectByType<WofManaSourceRuntime>() != null) return;
            new GameObject("ReactManaSourceRuntime").AddComponent<WofManaSourceRuntime>();
        }

        public static void ConfirmCollection(WofManaSourceKind kind, int sourceIndex, long cycle)
        {
            if (_instance == null) return;
            if (kind == WofManaSourceKind.HutRune && cycle == _instance._runeCycle)
            {
                _instance._collectedRunes.Add(sourceIndex);
                _instance._nextVisualAt = 0f;
            }
        }

        public static void SpawnPickupPulse(WofPlayerController player)
        {
            if (_instance == null || player == null) return;
            if (_instance._pulses.Count >= 6) _instance._pulses.RemoveAt(0);
            _instance._pulses.Add(new ManaPulse(player.transform, Time.unscaledTime));
        }

        private void Awake()
        {
            _instance = this;
            _octahedron = CreateOctahedronMesh();
            _torus = CreateTorusMesh(1f, 0.045f, WofPerformanceModeRuntime.IsMobilePerformanceMode ? 14 : 28, 6);
            _disc = CreateDiscMesh(WofPerformanceModeRuntime.IsMobilePerformanceMode ? 12 : 20);
            _runeMaterial = MakeUnlitMaterial("WOF_HutManaRune", new Color32(148, 0, 211, 255), true);
            _infiniteMaterial = MakeUnlitMaterial("WOF_InfiniteManaRune", new Color32(255, 79, 216, 217), true);
            _baseRingMaterial = MakeUnlitMaterial("WOF_BaseManaRing", new Color32(255, 214, 251, 166), true);
            _wellRingMaterial = MakeUnlitMaterial("WOF_DesertManaRing", new Color32(192, 132, 252, 122), true);
            _wellDiscMaterial = MakeUnlitMaterial("WOF_DesertManaDisc", new Color32(168, 85, 247, 56), true);
            _pulseMaterials = new[]
            {
                MakeUnlitMaterial("WOF_ManaPulse0", new Color32(240, 171, 252, 158), true),
                MakeUnlitMaterial("WOF_ManaPulse1", new Color32(168, 85, 247, 133), true),
                MakeUnlitMaterial("WOF_ManaPulse2", new Color32(168, 85, 247, 107), true)
            };
            _baseLight = CreateLight("BaseManaSourceLight", new Color32(255, 79, 216, 255), 3f, 7f);
            _wellLight = CreateLight("DesertManaWellLight", new Color32(255, 79, 216, 255), 2f, 10f);

            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith("--wof-mana-source-probe=", StringComparison.OrdinalIgnoreCase)) continue;
                _probeRequested = true;
                _probeKind = argument.Substring("--wof-mana-source-probe=".Length).Trim().ToLowerInvariant();
                break;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Destroy(_octahedron);
            Destroy(_torus);
            Destroy(_disc);
            Destroy(_runeMaterial);
            Destroy(_infiniteMaterial);
            Destroy(_baseRingMaterial);
            Destroy(_wellRingMaterial);
            Destroy(_wellDiscMaterial);
            if (_pulseMaterials != null)
                foreach (var material in _pulseMaterials) Destroy(material);
        }

        private void Update()
        {
            ResolvePlayer();
            if (_player == null || WofBootstrap.Instance == null) return;

            var now = NetworkManager.Singleton?.ServerTime.Time ?? Time.unscaledTimeAsDouble;
            var survival = WofBootstrap.Instance.IsSurvivalSession;
            _showBaseSources = WofManaSourceRules.ShouldShowBaseSources(survival, _player.transform.position);
            _showDesertWell = WofManaSourceRules.ShouldShowDesertWell(survival, _player.transform.position);
            var cycle = WofManaSourceRules.GetRuneCycle(now);
            if (cycle != _runeCycle)
            {
                _runeCycle = cycle;
                _activeRunes = WofManaSourceRules.BuildActiveRuneIndices(cycle);
                _collectedRunes.Clear();
                _nextVisualAt = 0f;
            }

            UpdateProbe();
            if (Time.unscaledTime >= _nextVisualAt)
            {
                UpdateVisuals();
                _nextVisualAt = Time.unscaledTime +
                                (WofPerformanceModeRuntime.IsMobilePerformanceMode ? MobileVisualSeconds : 0f);
            }
            DrawVisuals();
            DrawPulses();

            if (Time.unscaledTime >= _nextCollectionAt)
            {
                _nextCollectionAt = Time.unscaledTime + CollectionPollSeconds;
                TryCollectNearbySource();
            }
        }

        private void ResolvePlayer()
        {
            if (_player != null && _player.IsSpawned && _player.IsOwner) return;
            if (Time.unscaledTime < _nextResolveAt) return;
            _nextResolveAt = Time.unscaledTime + ResolvePlayerSeconds;
            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var candidate = playerObject == null ? null : playerObject.GetComponent<WofPlayerController>();
            _player = candidate != null && candidate.IsSpawned && candidate.IsOwner ? candidate : null;
        }

        private void UpdateVisuals()
        {
            if (!_showBaseSources)
            {
                _runeMatrixCount = 0;
            }
            else
            {
                if (_runeMatrices.Length < _activeRunes.Length) _runeMatrices = new Matrix4x4[_activeRunes.Length];
                var elapsed = Time.time;
                _runeMatrixCount = 0;
                foreach (var sourceIndex in _activeRunes)
                {
                    if (_collectedRunes.Contains(sourceIndex) ||
                        !WofManaSourceRules.TryGetHutRune(sourceIndex, out var source)) continue;
                    var rotation = Quaternion.Euler(elapsed * Mathf.Rad2Deg * 1.5f, elapsed * Mathf.Rad2Deg * 2f, 0f);
                    var position = source.Position + Vector3.up *
                        (WofManaSourceRules.HutRuneVisualLift + Mathf.Sin(elapsed * 3f) * 0.2f);
                    _runeMatrices[_runeMatrixCount++] = Matrix4x4.TRS(position, rotation, Vector3.one * 0.4f);
                }
            }

            var baseSource = WofManaSourceRules.BaseSource;
            var baseY = 0.72f + Mathf.Sin(Time.time * 2.4f) * 0.12f;
            _baseLight.transform.position = baseSource.Position + Vector3.up * baseY;
            _baseLight.enabled = _showBaseSources && !WofPerformanceModeRuntime.IsMobilePerformanceMode;
            var well = WofManaSourceRules.DesertWell;
            var wellY = 0.45f + Mathf.Sin(Time.time * 2.4f) * 0.12f;
            _wellLight.transform.position = well.Position + Vector3.up * wellY;
            _wellLight.enabled = _showDesertWell && !WofPerformanceModeRuntime.IsMobilePerformanceMode;
        }

        private void DrawVisuals()
        {
            if (_showBaseSources && _runeMatrixCount > 0 && SystemInfo.supportsInstancing)
                DrawInstanced(_octahedron, _runeMaterial, _runeMatrices, _runeMatrixCount);

            var elapsed = Time.time;
            if (_showBaseSources)
            {
                var source = WofManaSourceRules.BaseSource;
                var y = 0.72f + Mathf.Sin(elapsed * 2.4f) * 0.12f;
                var scale = 1f + Mathf.Sin(elapsed * 4f) * 0.05f;
                Graphics.DrawMesh(_octahedron,
                    Matrix4x4.TRS(source.Position + Vector3.up * y,
                        Quaternion.Euler(0f, -elapsed * Mathf.Rad2Deg * 1.4f, 0f), Vector3.one * 0.4f * scale),
                    _infiniteMaterial, gameObject.layer);
                Graphics.DrawMesh(_torus,
                    Matrix4x4.TRS(source.Position + Vector3.up * y, Quaternion.identity,
                        new Vector3(0.78f, 0.78f, 0.78f) * scale), _baseRingMaterial, gameObject.layer);
            }

            if (_showDesertWell)
            {
                var source = WofManaSourceRules.DesertWell;
                var y = 0.45f + Mathf.Sin(elapsed * 2.4f) * 0.12f;
                var scale = 1.8f + Mathf.Sin(elapsed * 4f) * 0.05f;
                var position = source.Position + Vector3.up * y;
                Graphics.DrawMesh(_torus,
                    Matrix4x4.TRS(position, Quaternion.Euler(0f, -elapsed * Mathf.Rad2Deg * 0.72f, 0f),
                        Vector3.one * 1.85f * scale / 1.8f), _wellRingMaterial, gameObject.layer);
                Graphics.DrawMesh(_disc,
                    Matrix4x4.TRS(position - Vector3.up * 0.015f, Quaternion.identity,
                        Vector3.one * 1.4f * scale / 1.8f), _wellDiscMaterial, gameObject.layer);
            }
        }

        private void DrawPulses()
        {
            for (var pulseIndex = _pulses.Count - 1; pulseIndex >= 0; pulseIndex--)
            {
                var pulse = _pulses[pulseIndex];
                if (pulse.Target == null)
                {
                    _pulses.RemoveAt(pulseIndex);
                    continue;
                }
                var progress = (Time.unscaledTime - pulse.StartedAt) / WofManaSourceRules.PickupPulseSeconds;
                if (progress >= 1f)
                {
                    _pulses.RemoveAt(pulseIndex);
                    continue;
                }
                for (var ring = 0; ring < 3; ring++)
                {
                    var ringProgress = Mathf.Clamp01(progress * 1.18f - ring * 0.1f);
                    var position = pulse.Target.position + Vector3.up * (2.25f - ringProgress * 2.15f);
                    var scale = (0.62f + ring * 0.08f) *
                                (1f + Mathf.Sin(ringProgress * Mathf.PI) * 0.38f + ring * 0.06f);
                    var opacity = Mathf.Max(0f, 0.62f * (1f - ringProgress) * (1f - ring * 0.16f));
                    SetMaterialAlpha(_pulseMaterials[ring], opacity);
                    Graphics.DrawMesh(_torus, Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * scale),
                        _pulseMaterials[ring], gameObject.layer);
                }
            }
        }

        private void TryCollectNearbySource()
        {
            if (!_player.CanRechargeMana || Time.unscaledTime < _nextRequestAt) return;
            var position = _player.transform.position;
            if (_showBaseSources &&
                WofManaSourceRules.IsWithinHorizontalRadius(position, WofManaSourceRules.BaseSource))
            {
                Request(WofManaSourceKind.BaseInfinite, -1);
                return;
            }
            if (_showDesertWell &&
                WofManaSourceRules.IsWithinHorizontalRadius(position, WofManaSourceRules.DesertWell))
            {
                Request(WofManaSourceKind.DesertWell, -1);
                return;
            }
            if (!_showBaseSources) return;
            foreach (var sourceIndex in _activeRunes)
            {
                if (_collectedRunes.Contains(sourceIndex) ||
                    !WofManaSourceRules.TryGetHutRune(sourceIndex, out var source) ||
                    !WofManaSourceRules.IsWithinHorizontalRadius(position, source)) continue;
                Request(WofManaSourceKind.HutRune, sourceIndex);
                return;
            }
        }

        private void Request(WofManaSourceKind kind, int sourceIndex)
        {
            _nextRequestAt = Time.unscaledTime +
                             (kind == WofManaSourceKind.HutRune ? 0.25f : (float)WofManaSourceRules.InfiniteSourceDebounceSeconds);
            _player.RequestManaSourceCollection(kind, sourceIndex, _runeCycle);
        }

        private void UpdateProbe()
        {
            if (!_probeRequested || _probeFinished) return;
            if (!_probePositioned)
            {
                switch (_probeKind)
                {
                    case "base":
                        _probeSource = WofManaSourceRules.BaseSource;
                        break;
                    case "well":
                        _probeSource = WofManaSourceRules.DesertWell;
                        break;
                    case "rune":
                        if (_activeRunes.Length == 0 ||
                            !WofManaSourceRules.TryGetHutRune(_activeRunes[0], out _probeSource)) return;
                        break;
                    default:
                        Debug.LogError($"[WOF-AUTOMATION] MANA_SOURCE_PROBE_FAILED reason=unknown-kind-{_probeKind}");
                        _probeFinished = true;
                        return;
                }
                var viewDistance = Mathf.Max(6f, _probeSource.Radius + 4f);
                var position = _probeSource.Position + Vector3.up * 0.15f + Vector3.back * viewDistance;
                if (!_player.PrepareForAutomationStaticViewProbe(position, 0f, -4f)) return;
                _probePositioned = true;
                _probeCollectAt = Time.unscaledTime + 3f;
                _probeTimeoutAt = Time.unscaledTime + 12f;
                Debug.Log($"[WOF-AUTOMATION] MANA_SOURCE_PROBE_READY kind={_probeKind} id={_probeSource.Id} position={_probeSource.Position} cycle={_runeCycle}");
                return;
            }
            if (!_probeMovedToCollect && Time.unscaledTime >= _probeCollectAt)
            {
                if (!_player.PrepareForAutomationStaticViewProbe(
                        _probeSource.Position + Vector3.up * 0.15f, 0f, -4f)) return;
                _probeMovedToCollect = true;
                Debug.Log($"[WOF-AUTOMATION] MANA_SOURCE_PROBE_ENTERED_RADIUS kind={_probeKind} id={_probeSource.Id}");
                return;
            }
            if (!_probeMovedToCollect) return;
            if (_player.LeftMana > 0f || _player.RightMana > 0f)
            {
                _probeFinished = true;
                Debug.Log($"[WOF-AUTOMATION] MANA_SOURCE_PROBE_PASS kind={_probeKind} id={_probeSource.Id} leftMana={_player.LeftMana:F1} rightMana={_player.RightMana:F1}");
            }
            else if (Time.unscaledTime >= _probeTimeoutAt)
            {
                _probeFinished = true;
                Debug.LogError($"[WOF-AUTOMATION] MANA_SOURCE_PROBE_FAILED reason=collection-timeout kind={_probeKind} id={_probeSource.Id}");
            }
        }

        private Light CreateLight(string lightName, Color color, float intensity, float range)
        {
            var lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(transform, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        private static void DrawInstanced(Mesh mesh, Material material, Matrix4x4[] matrices, int count)
        {
            const int batch = 1023;
            for (var offset = 0; offset < count; offset += batch)
            {
                var length = Mathf.Min(batch, count - offset);
                if (offset == 0)
                {
                    Graphics.DrawMeshInstanced(mesh, 0, material, matrices, length, null,
                        ShadowCastingMode.On, false, 0, null, LightProbeUsage.Off);
                    continue;
                }
                var slice = new Matrix4x4[length];
                Array.Copy(matrices, offset, slice, 0, length);
                Graphics.DrawMeshInstanced(mesh, 0, material, slice, length, null,
                    ShadowCastingMode.On, false, 0, null, LightProbeUsage.Off);
            }
        }

        private static Material MakeUnlitMaterial(string materialName, Color color, bool additive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = materialName, color = color, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static void SetMaterialAlpha(Material material, float alpha)
        {
            var color = material.color;
            color.a = alpha;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }

        private static Mesh CreateOctahedronMesh()
        {
            var vertices = new[]
            {
                Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back
            };
            var triangles = new[]
            {
                0, 4, 2, 0, 3, 4, 0, 5, 3, 0, 2, 5,
                1, 2, 4, 1, 4, 3, 1, 3, 5, 1, 5, 2
            };
            var mesh = new Mesh { name = "WOF_ManaOctahedron" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateDiscMesh(int segments)
        {
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                vertices[index + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                triangles[index * 3] = 0;
                triangles[index * 3 + 1] = index + 1;
                triangles[index * 3 + 2] = (index + 1) % segments + 1;
            }
            var mesh = new Mesh { name = "WOF_ManaDisc" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateTorusMesh(float radius, float tube, int radialSegments, int tubeSegments)
        {
            var vertices = new Vector3[radialSegments * tubeSegments];
            var triangles = new int[radialSegments * tubeSegments * 6];
            for (var radial = 0; radial < radialSegments; radial++)
            for (var side = 0; side < tubeSegments; side++)
            {
                var angle = radial / (float)radialSegments * Mathf.PI * 2f;
                var sideAngle = side / (float)tubeSegments * Mathf.PI * 2f;
                var ringRadius = radius + Mathf.Cos(sideAngle) * tube;
                vertices[radial * tubeSegments + side] = new Vector3(
                    Mathf.Cos(angle) * ringRadius, Mathf.Sin(sideAngle) * tube, Mathf.Sin(angle) * ringRadius);
                var nextRadial = (radial + 1) % radialSegments;
                var nextSide = (side + 1) % tubeSegments;
                var cursor = (radial * tubeSegments + side) * 6;
                var a = radial * tubeSegments + side;
                var b = nextRadial * tubeSegments + side;
                var c = radial * tubeSegments + nextSide;
                var d = nextRadial * tubeSegments + nextSide;
                triangles[cursor] = a;
                triangles[cursor + 1] = b;
                triangles[cursor + 2] = c;
                triangles[cursor + 3] = c;
                triangles[cursor + 4] = b;
                triangles[cursor + 5] = d;
            }
            var mesh = new Mesh { name = "WOF_ManaTorus" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private readonly struct ManaPulse
        {
            public ManaPulse(Transform target, float startedAt)
            {
                Target = target;
                StartedAt = startedAt;
            }

            public Transform Target { get; }
            public float StartedAt { get; }
        }
    }
}
