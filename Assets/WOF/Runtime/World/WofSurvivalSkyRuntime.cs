using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    public readonly struct WofSurvivalSkyCycle
    {
        public WofSurvivalSkyCycle(float phase, float sunAngle, float sunHeight, float dayAmount, float nightAmount, float duskAmount)
        {
            Phase = phase;
            SunAngle = sunAngle;
            SunHeight = sunHeight;
            DayAmount = dayAmount;
            NightAmount = nightAmount;
            DuskAmount = duskAmount;
        }

        public float Phase { get; }
        public float SunAngle { get; }
        public float SunHeight { get; }
        public float DayAmount { get; }
        public float NightAmount { get; }
        public float DuskAmount { get; }
    }

    [DisallowMultipleComponent]
    public sealed class WofSurvivalSkyRuntime : MonoBehaviour
    {
        public const float CycleSeconds = 600f;
        public const float ForcedDaySeconds = CycleSeconds * 0.07f;
        public const float ForcedNightSeconds = CycleSeconds * 0.57f;
        private const float VisualScale = 0.38f;
        private const float SkyRadius = 512f * 2.85f * VisualScale;
        private const float StarRadius = 585f;

        private static readonly Color DaySky = Hex("#bcefff");
        private static readonly Color DuskSky = Hex("#ffc68f");
        private static readonly Color NightSky = Hex("#172342");
        private static readonly Color SunDay = Hex("#fff5a8");
        private static readonly Color SunDusk = Hex("#ff9e4d");
        private static readonly Color MoonDay = Hex("#cbd8ff");
        private static readonly Color MoonNight = Hex("#f1f6ff");
        private static readonly Color CloudDay = Color.white;
        private static readonly Color CloudNight = Hex("#7e8cb1");
        private static readonly Color HemisphereGround = Hex("#8a684b");
        private static readonly Color TerrainDayTint = Color.white;
        private static readonly Color TerrainNightTint = Hex("#3f4f45");
        private static readonly Color TerrainDuskTint = Hex("#c4ae72");
        private static readonly int TerrainTintProperty = Shader.PropertyToID("_WofSurvivalTerrainTint");

        private readonly List<SkyBillboard> _clouds = new();
        private readonly List<SkyBillboard> _moons = new();
        private Light _directionalLight;
        private SkyBillboard _sun;
        private Transform _starSphere;
        private Material _starMaterial;
        private Texture2D[] _moonTextures;
        private Camera _camera;
        private double? _forcedElapsed;
        private float _nextCameraResolveAt;

        public void Configure(Light light)
        {
            _directionalLight = light;
        }

        private void Awake()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-sky-probe=day", StringComparison.OrdinalIgnoreCase)) _forcedElapsed = ForcedDaySeconds;
                else if (argument.Equals("--wof-sky-probe=night", StringComparison.OrdinalIgnoreCase)) _forcedElapsed = ForcedNightSeconds;
            }

            BuildVisuals();
            Shader.SetGlobalColor(TerrainTintProperty, TerrainDayTint);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.ambientMode = AmbientMode.Trilight;
        }

        private void LateUpdate()
        {
            ResolveCamera();
            var elapsed = _forcedElapsed ?? ResolveSynchronizedElapsed();
            var cycle = Evaluate((float)elapsed);
            ApplyLighting(cycle);
            if (_camera != null) ApplyVisuals(cycle, (float)elapsed);
        }

        private void ResolveCamera()
        {
            if (_camera != null && _camera.isActiveAndEnabled) return;
            if (Time.unscaledTime < _nextCameraResolveAt) return;
            _nextCameraResolveAt = Time.unscaledTime + 0.25f;
            _camera = Camera.main;
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                foreach (var candidate in Camera.allCameras)
                {
                    if (!candidate.isActiveAndEnabled) continue;
                    _camera = candidate;
                    break;
                }
            }
            if (_camera != null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.farClipPlane = Mathf.Max(_camera.farClipPlane, 600f);
            }
        }

        private static double ResolveSynchronizedElapsed()
        {
            var manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening ? manager.ServerTime.Time : Time.unscaledTimeAsDouble;
        }

        private void ApplyLighting(WofSurvivalSkyCycle cycle)
        {
            var duskWarmth = cycle.DuskAmount * (cycle.SunHeight > -0.12f ? 0.42f : 0.2f);
            var sky = Color.Lerp(NightSky, DaySky, cycle.DayAmount);
            sky = Color.Lerp(sky, DuskSky, duskWarmth);
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 512f * (3.2f + cycle.DayAmount * 0.8f);
            RenderSettings.fogEndDistance = 512f * (10.5f + cycle.DayAmount * 4.2f);
            if (_camera != null) _camera.backgroundColor = sky;

            var mobile = WofPerformanceModeRuntime.IsMobilePerformanceMode;
            var ambient = mobile
                ? 0.86f + cycle.DayAmount * 0.58f + cycle.NightAmount * 0.1f
                : 0.34f + cycle.DayAmount * 0.44f + cycle.NightAmount * 0.08f;
            var hemisphere = mobile
                ? 0.78f + cycle.DayAmount * 0.52f
                : 0.34f + cycle.DayAmount * 0.28f;
            RenderSettings.ambientSkyColor = Color.white * (ambient + hemisphere);
            RenderSettings.ambientGroundColor = Color.white * ambient + HemisphereGround * hemisphere;
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientGroundColor, RenderSettings.ambientSkyColor, 0.5f);
            Shader.SetGlobalColor(TerrainTintProperty, EvaluateTerrainTint(cycle));

            if (_directionalLight == null) return;
            var sunVector = new Vector3(Mathf.Cos(cycle.SunAngle) * 0.82f, Mathf.Sin(cycle.SunAngle) * 0.96f, -0.38f).normalized;
            _directionalLight.transform.rotation = Quaternion.LookRotation(-sunVector, Vector3.up);
            _directionalLight.intensity = mobile
                ? 1.55f + cycle.DayAmount * 1.05f + cycle.DuskAmount * 0.34f
                : 0.56f + cycle.DayAmount * 1.46f + cycle.DuskAmount * 0.22f;
            _directionalLight.color = Color.Lerp(SunDay, SunDusk, cycle.DuskAmount * 0.55f);
        }

        private void ApplyVisuals(WofSurvivalSkyCycle cycle, float elapsed)
        {
            var cameraPosition = _camera.transform.position;
            var sunVector = new Vector3(Mathf.Cos(cycle.SunAngle) * 0.82f, Mathf.Sin(cycle.SunAngle) * 0.96f, -0.38f).normalized;
            _sun.Transform.position = cameraPosition + sunVector * SkyRadius;
            _sun.Transform.localScale = Vector3.one * ((280f + cycle.DuskAmount * 42f) * VisualScale);
            _sun.Material.color = WithAlpha(Color.Lerp(SunDay, SunDusk, cycle.DuskAmount * 0.68f),
                Mathf.Clamp01(0.02f + cycle.DayAmount * 0.92f + cycle.DuskAmount * 0.38f));
            FaceCamera(_sun.Transform);

            _starSphere.position = cameraPosition;
            _starMaterial.color = WithAlpha(Color.white, Mathf.Clamp01(cycle.NightAmount * 1.18f + cycle.DuskAmount * 0.2f));

            var moonSpecs = new[]
            {
                new Vector4(-0.2f, -0.1f, 0.02f, 94f),
                new Vector4(0.54f, 0.32f, 0.14f, 50f),
                new Vector4(-0.76f, -0.42f, -0.04f, 38f)
            };
            var moonOpacity = new[] { 1f, 0.76f, 0.64f };
            var moonPhaseOffset = new[] { 0f, 0.32f, 0.61f };
            for (var index = 0; index < _moons.Count; index++)
            {
                var spec = moonSpecs[index];
                var angle = cycle.SunAngle + Mathf.PI + spec.x;
                var vector = new Vector3(
                    Mathf.Cos(angle) * 0.76f,
                    Mathf.Max(-0.18f, Mathf.Sin(angle) * 0.92f + spec.z),
                    -0.34f + spec.y).normalized;
                var moon = _moons[index];
                moon.Transform.position = cameraPosition + vector * (SkyRadius * 0.92f);
                moon.Transform.localScale = Vector3.one * (spec.w * 1.85f * VisualScale);
                var phase = Mathf.FloorToInt(Mathf.Repeat(elapsed / (CycleSeconds * 2f) + moonPhaseOffset[index], 1f) * 8f) % 8;
                if (moon.Material.mainTexture != _moonTextures[phase]) moon.Material.mainTexture = _moonTextures[phase];
                var phaseDim = phase == 4 ? 0.42f : phase == 3 || phase == 5 ? 0.72f : 1f;
                var alpha = Mathf.Clamp01((cycle.NightAmount * 1.05f + cycle.DuskAmount * 0.18f) * moonOpacity[index] * phaseDim);
                moon.Material.color = WithAlpha(Color.Lerp(MoonDay, MoonNight, cycle.NightAmount), alpha);
                FaceCamera(moon.Transform);
            }

            var cloudTint = Color.Lerp(CloudNight, CloudDay, cycle.DayAmount);
            for (var index = 0; index < _clouds.Count; index++)
            {
                var hash = WofSurvivalSkyTextures.Hash01(index, _clouds.Count, 240f);
                var angle = Mathf.PI * 2f * index / _clouds.Count + hash * 0.7f;
                var radius = SkyRadius * (0.64f + WofSurvivalSkyTextures.Hash01(index, _clouds.Count, 260f) * 0.26f);
                var height = (130f + WofSurvivalSkyTextures.Hash01(index, _clouds.Count, 280f) * 170f) * VisualScale;
                var width = (120f + WofSurvivalSkyTextures.Hash01(index, _clouds.Count, 300f) * 140f) * VisualScale;
                var heightScale = (34f + WofSurvivalSkyTextures.Hash01(index, _clouds.Count, 320f) * 42f) * VisualScale;
                var opacity = 0.52f + WofSurvivalSkyTextures.Hash01(index, _clouds.Count, 340f) * 0.34f;
                var drift = (0.006f + WofSurvivalSkyTextures.Hash01(index, _clouds.Count, 360f) * 0.009f) * (index % 2 == 0 ? 1f : -1f);
                var driftAngle = angle + Time.unscaledTime * drift;
                var cloud = _clouds[index];
                cloud.Transform.position = new Vector3(
                    cameraPosition.x + Mathf.Cos(driftAngle) * radius,
                    cameraPosition.y * 0.08f + height + Mathf.Sin(Time.unscaledTime * 0.035f + index) * 8f * VisualScale,
                    cameraPosition.z + Mathf.Sin(driftAngle) * radius);
                cloud.Transform.localScale = new Vector3(width, heightScale, 1f);
                cloud.Material.color = WithAlpha(cloudTint,
                    Mathf.Clamp01((0.18f + cycle.DayAmount * 0.46f + cycle.DuskAmount * 0.08f - cycle.NightAmount * 0.08f) * opacity));
                FaceCamera(cloud.Transform);
            }
        }

        private void BuildVisuals()
        {
            var shader = Shader.Find("WOF/Sky Unlit");
            if (shader == null)
            {
                Debug.LogError("[WOF-AUTOMATION] SURVIVAL_SKY_FAILED reason=missing-shader");
                enabled = false;
                return;
            }

            var root = new GameObject("ReactSurvivalSkyVisuals").transform;
            root.SetParent(transform, false);
            _sun = MakeBillboard(root, "ReactSurvivalSun", shader, WofSurvivalSkyTextures.CreateSun());
            _moonTextures = WofSurvivalSkyTextures.CreateMoonPhases();
            for (var index = 0; index < 3; index++)
                _moons.Add(MakeBillboard(root, $"ReactSurvivalMoon{index}", shader, _moonTextures[0]));
            var cloudTexture = WofSurvivalSkyTextures.CreateCloud();
            var cloudCount = WofPerformanceModeRuntime.IsMobilePerformanceMode ? 8 : 13;
            for (var index = 0; index < cloudCount; index++)
                _clouds.Add(MakeBillboard(root, $"ReactSurvivalCloud{index}", shader, cloudTexture));

            var stars = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stars.name = "ReactSurvivalStars";
            stars.transform.SetParent(root, false);
            DisableAndDestroyCollider(stars);
            stars.transform.localScale = Vector3.one * (StarRadius * 2f);
            stars.transform.localRotation = Quaternion.Euler(0.16f * Mathf.Rad2Deg, 0.44f * Mathf.Rad2Deg, 0f);
            _starMaterial = new Material(shader)
            {
                name = "ReactSurvivalStarsMaterial",
                mainTexture = WofSurvivalSkyTextures.CreateStars(),
                color = Color.clear,
                renderQueue = 2890
            };
            _starMaterial.SetFloat("_Cull", 1f);
            stars.GetComponent<MeshRenderer>().sharedMaterial = _starMaterial;
            _starSphere = stars.transform;
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_SKY_READY cycleSeconds={CycleSeconds:F0} moons={_moons.Count} clouds={_clouds.Count} stars=520");
        }

        private static SkyBillboard MakeBillboard(Transform parent, string name, Shader shader, Texture texture)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            item.name = name;
            item.transform.SetParent(parent, false);
            DisableAndDestroyCollider(item);
            var material = new Material(shader)
            {
                name = name + "Material",
                mainTexture = texture,
                color = Color.clear,
                renderQueue = 2900
            };
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            return new SkyBillboard(item.transform, material);
        }

        private static void DisableAndDestroyCollider(GameObject item)
        {
            var collider = item.GetComponent<Collider>();
            if (collider == null) return;
            collider.enabled = false;
            Destroy(collider);
        }

        private void FaceCamera(Transform target)
        {
            target.rotation = Quaternion.LookRotation(_camera.transform.position - target.position, _camera.transform.up);
        }

        public static WofSurvivalSkyCycle Evaluate(float elapsedSeconds)
        {
            var phase = Mathf.Repeat(elapsedSeconds / CycleSeconds + 0.18f, 1f);
            var angle = phase * Mathf.PI * 2f;
            var height = Mathf.Sin(angle);
            return new WofSurvivalSkyCycle(
                phase,
                angle,
                height,
                Smoothstep(-0.12f, 0.34f, height),
                1f - Smoothstep(-0.36f, 0.08f, height),
                1f - Smoothstep(0.02f, 0.48f, Mathf.Abs(height)));
        }

        private static float Smoothstep(float edge0, float edge1, float value)
        {
            var t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        public static Color EvaluateTerrainTint(WofSurvivalSkyCycle cycle)
        {
            var tint = Color.Lerp(TerrainNightTint, TerrainDayTint, cycle.DayAmount);
            return Color.Lerp(tint, TerrainDuskTint, cycle.DuskAmount * 0.16f);
        }

        private void OnDestroy()
        {
            Shader.SetGlobalColor(TerrainTintProperty, TerrainDayTint);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.magenta;
        }

        private readonly struct SkyBillboard
        {
            public SkyBillboard(Transform transform, Material material)
            {
                Transform = transform;
                Material = material;
            }

            public Transform Transform { get; }
            public Material Material { get; }
        }
    }
}
