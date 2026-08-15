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

    public readonly struct WofSkyPresentationLayout
    {
        public WofSkyPresentationLayout(
            float horizonRadius,
            float horizonHeight,
            float horizonY,
            int horizonSegments,
            bool followsCamera,
            bool fogEnabled,
            bool survivalSpritesVisible,
            bool classicAtmosphereVisible)
        {
            HorizonRadius = horizonRadius;
            HorizonHeight = horizonHeight;
            HorizonY = horizonY;
            HorizonSegments = horizonSegments;
            FollowsCamera = followsCamera;
            FogEnabled = fogEnabled;
            SurvivalSpritesVisible = survivalSpritesVisible;
            ClassicAtmosphereVisible = classicAtmosphereVisible;
        }

        public float HorizonRadius { get; }
        public float HorizonHeight { get; }
        public float HorizonY { get; }
        public int HorizonSegments { get; }
        public bool FollowsCamera { get; }
        public bool FogEnabled { get; }
        public bool SurvivalSpritesVisible { get; }
        public bool ClassicAtmosphereVisible { get; }
    }

    [DisallowMultipleComponent]
    public sealed class WofSurvivalSkyRuntime : MonoBehaviour
    {
        public const float CycleSeconds = 600f;
        public const float ForcedDaySeconds = CycleSeconds * 0.07f;
        public const float ForcedNightSeconds = CycleSeconds * 0.57f;
        public const float HorizonRadius = 512f * 5.5f;
        public const float HorizonHeight = 2200f;
        public const float HorizonY = 330f;
        public const int HorizonSegments = 96;
        public const float ClassicHorizonRadius = 400f;
        public const float ClassicHorizonHeight = 250f;
        public const float ClassicHorizonY = 40f;
        public const int ClassicHorizonSegments = 64;
        private const float VisualScale = 0.38f;
        private const float SkyRadius = 512f * 2.85f * VisualScale;
        private const float StarRadius = 585f;

        private static readonly Color DaySky = Hex("#bcefff");
        private static readonly Color DuskSky = Hex("#ffc68f");
        private static readonly Color NightSky = Hex("#172342");
        private static readonly Color AstralSky = Hex("#4a1d78");
        private static readonly Color SunDay = Hex("#fff5a8");
        private static readonly Color SunDusk = Hex("#ff9e4d");
        private static readonly Color MoonDay = Hex("#cbd8ff");
        private static readonly Color MoonNight = Hex("#f1f6ff");
        private static readonly Color CloudDay = Color.white;
        private static readonly Color CloudNight = Hex("#7e8cb1");
        private static readonly Color CloudAstral = Hex("#e9d5ff");
        private static readonly Color AstralVeilTint = Hex("#c084fc");
        private static readonly Color AstralBlinkTint = Hex("#16001f");
        private static readonly Color HemisphereGround = Hex("#8a684b");
        private static readonly Color TerrainDayTint = Color.white;
        private static readonly Color TerrainNightTint = Hex("#3f4f45");
        private static readonly Color TerrainDuskTint = Hex("#c4ae72");
        private static readonly Color HorizonDayTint = Color.white;
        private static readonly Color HorizonDuskTint = Hex("#ffd09a");
        private static readonly Color HorizonNightTint = Hex("#4b547c");
        private static readonly int TerrainTintProperty = Shader.PropertyToID("_WofSurvivalTerrainTint");

        private readonly List<SkyBillboard> _clouds = new();
        private readonly List<SkyBillboard> _moons = new();
        private Light _directionalLight;
        private SkyBillboard _sun;
        private SkyBillboard _astralVeil;
        private SkyBillboard _astralBlink;
        private Transform _starSphere;
        private Material _starMaterial;
        private Transform _horizonCylinder;
        private MeshFilter _horizonMeshFilter;
        private Material _horizonMaterial;
        private Mesh _horizonMesh;
        private Mesh _classicHorizonMesh;
        private Texture2D _horizonTexture;
        private Transform _classicAtmosphere;
        private Material _classicAtmosphereMaterial;
        private Texture2D[] _moonTextures;
        private Camera _camera;
        private WofPlayerController _localPlayer;
        private double? _forcedElapsed;
        private float _nextCameraResolveAt;
        private float _nextPlayerResolveAt;
        private float _astralStartedAt;
        private bool _astralWasActive;
        private bool _astralPresentationLogged;
        private bool? _lastSurvivalPresentation;

        public float? ForcedElapsedSeconds => _forcedElapsed.HasValue ? (float)_forcedElapsed.Value : null;

        public void Configure(Light light)
        {
            _directionalLight = light;
        }

        public void SetTimeOverrideSeconds(float? seconds)
        {
            _forcedElapsed = seconds.HasValue ? Mathf.Max(0f, seconds.Value) : (double?)null;
            Debug.Log(seconds.HasValue
                ? $"[WOF-AUTOMATION] SURVIVAL_SKY_OVERRIDE seconds={_forcedElapsed.Value:F1}"
                : "[WOF-AUTOMATION] SURVIVAL_SKY_OVERRIDE cleared=true");
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
            var survivalPresentation = WofBootstrap.Instance == null || WofBootstrap.Instance.IsSurvivalSession;
            ApplyPresentationMode(survivalPresentation);
            if (!survivalPresentation)
            {
                var classicAstral = ResolveAstralPresentation();
                ApplyClassicLighting();
                if (_camera != null) ApplyClassicVisuals(classicAstral);
                return;
            }

            var elapsed = _forcedElapsed ?? ResolveSynchronizedElapsed();
            var cycle = Evaluate((float)elapsed);
            var astral = ResolveAstralPresentation();
            ApplyLighting(cycle, astral.SkyStrength);
            if (_camera != null) ApplyVisuals(cycle, (float)elapsed, astral);
        }

        public static WofSkyPresentationLayout ResolvePresentationLayout(
            bool survivalSession,
            bool mobilePerformanceMode)
        {
            return survivalSession
                ? new WofSkyPresentationLayout(
                    HorizonRadius,
                    HorizonHeight,
                    HorizonY,
                    HorizonSegments,
                    true,
                    true,
                    true,
                    false)
                : new WofSkyPresentationLayout(
                    ClassicHorizonRadius,
                    ClassicHorizonHeight,
                    ClassicHorizonY,
                    ClassicHorizonSegments,
                    false,
                    false,
                    false,
                    !mobilePerformanceMode);
        }

        private void ApplyPresentationMode(bool survivalPresentation)
        {
            if (_lastSurvivalPresentation == survivalPresentation) return;
            _lastSurvivalPresentation = survivalPresentation;
            var layout = ResolvePresentationLayout(
                survivalPresentation,
                WofPerformanceModeRuntime.IsMobilePerformanceMode);
            if (_horizonMeshFilter != null)
                _horizonMeshFilter.sharedMesh = survivalPresentation ? _horizonMesh : _classicHorizonMesh;
            RenderSettings.fog = layout.FogEnabled;
            if (_classicAtmosphere != null)
                _classicAtmosphere.gameObject.SetActive(layout.ClassicAtmosphereVisible);
            SetSurvivalSpriteVisibility(layout.SurvivalSpritesVisible);
            Debug.Log(
                $"[WOF-AUTOMATION] SKY_PRESENTATION mode={(survivalPresentation ? "survival" : "classic")} " +
                $"horizon={layout.HorizonRadius:F0}x{layout.HorizonHeight:F0}@{layout.HorizonY:F0} " +
                $"segments={layout.HorizonSegments} followCamera={layout.FollowsCamera} fog={layout.FogEnabled}");
        }

        private void SetSurvivalSpriteVisibility(bool visible)
        {
            _sun.Transform.gameObject.SetActive(visible);
            if (_starSphere != null) _starSphere.gameObject.SetActive(visible);
            foreach (var moon in _moons) moon.Transform.gameObject.SetActive(visible);
            foreach (var cloud in _clouds) cloud.Transform.gameObject.SetActive(visible);
        }

        private void ApplyClassicLighting()
        {
            var mobile = WofPerformanceModeRuntime.IsMobilePerformanceMode;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            if (mobile)
            {
                RenderSettings.ambientSkyColor = WofGameWorldLightingLayout.GetMobileAmbientSkyColor();
                RenderSettings.ambientGroundColor = WofGameWorldLightingLayout.GetMobileAmbientGroundColor();
                RenderSettings.ambientEquatorColor = WofGameWorldLightingLayout.GetMobileAmbientEquatorColor();
            }
            else
            {
                var ambient = WofGameWorldLightingLayout.GetClassicAmbientColor();
                RenderSettings.ambientSkyColor = ambient;
                RenderSettings.ambientGroundColor = ambient;
                RenderSettings.ambientEquatorColor = ambient;
            }
            Shader.SetGlobalColor(TerrainTintProperty, TerrainDayTint);
            if (_camera != null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = mobile ? Hex("#9bdcff") : Hex("#000000");
            }
            if (_directionalLight == null) return;
            _directionalLight.transform.rotation = WofGameWorldLightingLayout.GetDirectionalLightRotation();
            _directionalLight.intensity = mobile
                ? WofGameWorldLightingLayout.UnityMobileDirectionalIntensity
                : WofGameWorldLightingLayout.ClassicDirectionalIntensity;
            _directionalLight.color = Color.white;
        }

        private void ApplyClassicVisuals(WofAstralPresentationFrame astral)
        {
            _horizonCylinder.position = new Vector3(0f, ClassicHorizonY, 0f);
            _horizonMaterial.color = HorizonDayTint;
            if (_classicAtmosphere != null) _classicAtmosphere.position = Vector3.zero;
            ApplyAstralVeil(astral);
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
                _camera.farClipPlane = Mathf.Max(_camera.farClipPlane, HorizonRadius + 512f);
            }
        }

        private static double ResolveSynchronizedElapsed()
        {
            var manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening ? manager.ServerTime.Time : Time.unscaledTimeAsDouble;
        }

        private WofAstralPresentationFrame ResolveAstralPresentation()
        {
            if ((_localPlayer == null || !_localPlayer.IsSpawned || !_localPlayer.IsOwner) &&
                Time.unscaledTime >= _nextPlayerResolveAt)
            {
                _nextPlayerResolveAt = Time.unscaledTime + 0.25f;
                _localPlayer = null;
                foreach (var candidate in FindObjectsByType<WofPlayerController>(
                             FindObjectsInactive.Exclude,
                             FindObjectsSortMode.None))
                {
                    if (!candidate.IsSpawned || !candidate.IsOwner) continue;
                    _localPlayer = candidate;
                    break;
                }
            }

            var active = _localPlayer != null && _localPlayer.IsSpawned &&
                         _localPlayer.IsOwner && _localPlayer.IsMeditating;
            if (active && !_astralWasActive)
            {
                _astralStartedAt = Time.unscaledTime;
                _astralPresentationLogged = false;
                Debug.Log("[WOF-AUTOMATION] ASTRAL_SKY active=true");
            }
            else if (!active && _astralWasActive)
            {
                _astralPresentationLogged = false;
                Debug.Log("[WOF-AUTOMATION] ASTRAL_SKY active=false");
            }
            _astralWasActive = active;
            return WofAstralMeditationRules.EvaluatePresentation(
                active,
                Time.unscaledTime - _astralStartedAt,
                Time.unscaledTime);
        }

        private void ApplyLighting(WofSurvivalSkyCycle cycle, float astralStrength)
        {
            var duskWarmth = cycle.DuskAmount * (cycle.SunHeight > -0.12f ? 0.42f : 0.2f);
            var sky = Color.Lerp(NightSky, DaySky, cycle.DayAmount);
            sky = Color.Lerp(sky, DuskSky, duskWarmth);
            sky = Color.Lerp(sky, AstralSky, astralStrength * 0.72f);
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 512f * (3.2f + cycle.DayAmount * 0.8f - astralStrength * 0.7f);
            RenderSettings.fogEndDistance = 512f * (10.5f + cycle.DayAmount * 4.2f - astralStrength * 2.2f);
            if (_camera != null) _camera.backgroundColor = sky;

            // Mobile uses the same visual exposure as quality mode. The stronger
            // raw React mobile lights compensated for a different renderer and DPR;
            // in Unity those values were additive and clipped the base village.
            var ambient = 0.34f + cycle.DayAmount * 0.44f + cycle.NightAmount * 0.08f;
            ambient += astralStrength * 0.18f;
            var hemisphere = 0.34f + cycle.DayAmount * 0.28f;
            RenderSettings.ambientSkyColor = Color.white * (ambient + hemisphere);
            RenderSettings.ambientGroundColor = Color.white * ambient + HemisphereGround * hemisphere;
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientGroundColor, RenderSettings.ambientSkyColor, 0.5f);
            Shader.SetGlobalColor(TerrainTintProperty, EvaluateTerrainTint(cycle));

            if (_directionalLight == null) return;
            var sunVector = new Vector3(Mathf.Cos(cycle.SunAngle) * 0.82f, Mathf.Sin(cycle.SunAngle) * 0.96f, -0.38f).normalized;
            _directionalLight.transform.rotation = Quaternion.LookRotation(-sunVector, Vector3.up);
            _directionalLight.intensity = 0.56f + cycle.DayAmount * 1.46f + cycle.DuskAmount * 0.22f;
            _directionalLight.color = Color.Lerp(SunDay, SunDusk, cycle.DuskAmount * 0.55f);
            _directionalLight.color = Color.Lerp(_directionalLight.color, AstralSky, astralStrength * 0.35f);
        }

        private void ApplyVisuals(
            WofSurvivalSkyCycle cycle,
            float elapsed,
            WofAstralPresentationFrame astral)
        {
            var cameraPosition = _camera.transform.position;
            _horizonCylinder.position = new Vector3(cameraPosition.x, HorizonY, cameraPosition.z);
            _horizonMaterial.color = EvaluateHorizonTint(cycle);
            var sunVector = new Vector3(Mathf.Cos(cycle.SunAngle) * 0.82f, Mathf.Sin(cycle.SunAngle) * 0.96f, -0.38f).normalized;
            _sun.Transform.position = cameraPosition + sunVector * SkyRadius;
            _sun.Transform.localScale = Vector3.one * ((280f + cycle.DuskAmount * 42f) * VisualScale);
            _sun.Material.color = WithAlpha(Color.Lerp(SunDay, SunDusk, cycle.DuskAmount * 0.68f),
                Mathf.Clamp01(0.02f + cycle.DayAmount * 0.92f + cycle.DuskAmount * 0.38f));
            FaceCamera(_sun.Transform);

            _starSphere.position = cameraPosition;
            _starMaterial.color = WithAlpha(
                Color.white,
                Mathf.Clamp01((cycle.NightAmount * 1.18f + cycle.DuskAmount * 0.2f) *
                              (1f - astral.SkyStrength * 0.18f)));

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
            cloudTint = Color.Lerp(cloudTint, CloudAstral, astral.SkyStrength * 0.55f);
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

            ApplyAstralVeil(astral);
        }

        private void ApplyAstralVeil(WofAstralPresentationFrame astral)
        {
            _astralVeil.Transform.gameObject.SetActive(astral.Active && astral.VeilAlpha > 0.001f);
            _astralBlink.Transform.gameObject.SetActive(astral.Active && astral.BlinkAlpha > 0.001f);
            if (!astral.Active) return;

            var forward = _camera.transform.forward;
            var overlayPosition = _camera.transform.position + forward * WofAstralMeditationRules.VeilDistance;
            var vertical = 2f * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f) *
                           WofAstralMeditationRules.VeilDistance;
            var horizontal = vertical * _camera.aspect;

            _astralVeil.Transform.position = overlayPosition;
            _astralVeil.Transform.localScale = new Vector3(horizontal * 1.38f, vertical * 1.42f, 1f);
            FaceCamera(_astralVeil.Transform);
            _astralVeil.Transform.Rotate(
                0f,
                0f,
                astral.VeilRotationRadians * Mathf.Rad2Deg,
                Space.Self);
            _astralVeil.Material.color = WithAlpha(AstralVeilTint, astral.VeilAlpha);

            _astralBlink.Transform.position = overlayPosition + forward * 0.01f;
            _astralBlink.Transform.localScale = new Vector3(horizontal * 1.5f, vertical * 1.5f, 1f);
            FaceCamera(_astralBlink.Transform);
            _astralBlink.Material.color = WithAlpha(AstralBlinkTint, astral.BlinkAlpha);

            if (!_astralPresentationLogged && astral.SkyStrength >= 0.999f &&
                astral.VeilStrength >= 0.999f && astral.BlinkStrength <= 0.001f)
            {
                _astralPresentationLogged = true;
                Debug.Log(
                    $"[WOF-AUTOMATION] ASTRAL_SKY_PRESENTATION sky={astral.SkyStrength:F3} " +
                    $"veil={astral.VeilStrength:F3} blink={astral.BlinkStrength:F3} " +
                    $"veilAlpha={astral.VeilAlpha:F3} fogStart={RenderSettings.fogStartDistance:F1} " +
                    $"fogEnd={RenderSettings.fogEndDistance:F1}");
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
            var horizon = new GameObject("ReactHorizonCylinder");
            horizon.transform.SetParent(root, false);
            horizon.transform.localPosition = new Vector3(0f, HorizonY, 0f);
            _horizonMesh = CreateHorizonCylinderMesh(HorizonRadius, HorizonHeight, HorizonSegments);
            _classicHorizonMesh = CreateHorizonCylinderMesh(
                ClassicHorizonRadius,
                ClassicHorizonHeight,
                ClassicHorizonSegments);
            _horizonTexture = WofSurvivalSkyTextures.CreateHorizonHills();
            _horizonMeshFilter = horizon.AddComponent<MeshFilter>();
            _horizonMeshFilter.sharedMesh = _horizonMesh;
            _horizonMaterial = new Material(shader)
            {
                name = "ReactHorizonCylinderMaterial",
                mainTexture = _horizonTexture,
                color = HorizonDayTint,
                renderQueue = 2880
            };
            _horizonMaterial.SetFloat("_Cull", 0f);
            _horizonMaterial.SetFloat("_UseFog", 1f);
            horizon.AddComponent<MeshRenderer>().sharedMaterial = _horizonMaterial;
            _horizonCylinder = horizon.transform;

            var classicAtmosphere = GameObject.CreatePrimitive(PrimitiveType.Cube);
            classicAtmosphere.name = "ReactClassicSkyEnvironment";
            classicAtmosphere.transform.SetParent(root, false);
            DisableAndDestroyCollider(classicAtmosphere);
            classicAtmosphere.transform.localScale = Vector3.one * 1000f;
            _classicAtmosphereMaterial = new Material(shader)
            {
                name = "ReactClassicSkyEnvironmentMaterial",
                mainTexture = Texture2D.whiteTexture,
                color = Color.white,
                renderQueue = 2870
            };
            _classicAtmosphereMaterial.SetFloat("_Cull", 1f);
            _classicAtmosphereMaterial.SetFloat("_UseClassicAtmosphere", 1f);
            _classicAtmosphereMaterial.SetFloat("_ClassicTurbidity", 0.3f);
            _classicAtmosphereMaterial.SetFloat("_ClassicRayleigh", 0.5f);
            _classicAtmosphereMaterial.SetFloat("_ClassicMieCoefficient", 0.005f);
            _classicAtmosphereMaterial.SetFloat("_ClassicMieDirectionalG", 0.8f);
            _classicAtmosphereMaterial.SetVector("_ClassicSunPosition", new Vector4(50f, 20f, 50f, 0f));
            classicAtmosphere.GetComponent<MeshRenderer>().sharedMaterial = _classicAtmosphereMaterial;
            _classicAtmosphere = classicAtmosphere.transform;
            _classicAtmosphere.gameObject.SetActive(false);
            _sun = MakeBillboard(root, "ReactSurvivalSun", shader, WofSurvivalSkyTextures.CreateSun());
            _astralVeil = MakeBillboard(
                root,
                "ReactAstralRealmVeil",
                shader,
                WofSurvivalSkyTextures.CreateAstralVeil());
            _astralVeil.Material.renderQueue = 3998;
            _astralVeil.Transform.gameObject.SetActive(false);
            _astralBlink = MakeBillboard(root, "ReactAstralRealmBlink", shader, Texture2D.whiteTexture);
            _astralBlink.Material.renderQueue = 3999;
            _astralBlink.Transform.gameObject.SetActive(false);
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
            Debug.Log($"[WOF-AUTOMATION] SURVIVAL_SKY_READY cycleSeconds={CycleSeconds:F0} moons={_moons.Count} clouds={_clouds.Count} stars=520 horizonSegments={HorizonSegments}");
        }

        internal static Mesh CreateHorizonCylinderMesh(float radius, float height, int segments)
        {
            if (radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            if (height <= 0f) throw new ArgumentOutOfRangeException(nameof(height));
            if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments));
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            var halfHeight = height * 0.5f;
            for (var index = 0; index <= segments; index++)
            {
                var amount = index / (float)segments;
                var angle = amount * Mathf.PI * 2f;
                var x = Mathf.Sin(angle) * radius;
                var z = Mathf.Cos(angle) * radius;
                var vertex = index * 2;
                vertices[vertex] = new Vector3(x, -halfHeight, z);
                vertices[vertex + 1] = new Vector3(x, halfHeight, z);
                uvs[vertex] = new Vector2(amount, 0f);
                uvs[vertex + 1] = new Vector2(amount, 1f);
                if (index == segments) continue;
                var triangle = index * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }

            var mesh = new Mesh { name = "ReactHorizonCylinderMesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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

        public static Color EvaluateHorizonTint(WofSurvivalSkyCycle cycle)
        {
            var tint = Color.Lerp(HorizonNightTint, HorizonDayTint, cycle.DayAmount);
            return Color.Lerp(tint, HorizonDuskTint, cycle.DuskAmount * 0.32f);
        }

        private void OnDestroy()
        {
            Shader.SetGlobalColor(TerrainTintProperty, TerrainDayTint);
            if (_horizonMesh != null) Destroy(_horizonMesh);
            if (_classicHorizonMesh != null) Destroy(_classicHorizonMesh);
            if (_horizonMaterial != null) Destroy(_horizonMaterial);
            if (_classicAtmosphereMaterial != null) Destroy(_classicAtmosphereMaterial);
            if (_horizonTexture != null) Destroy(_horizonTexture);
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
