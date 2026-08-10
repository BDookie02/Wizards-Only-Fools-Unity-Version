using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofLilyCoilAmbientEffectsRuntime : MonoBehaviour
    {
        private const float VisibilityRadius = 1900f;
        private const float MobileUpdateInterval = 1f / 24f;

        [SerializeField] private WofLilyCoilFlowerRecord[] flowers = Array.Empty<WofLilyCoilFlowerRecord>();
        [SerializeField] private WofLilyCoilFlowerRecord[] smallFlowers = Array.Empty<WofLilyCoilFlowerRecord>();
        [SerializeField] private WofLilyCoilBloomParticleRecord[] bloomParticles = Array.Empty<WofLilyCoilBloomParticleRecord>();
        [SerializeField] private WofLilyCoilFlyingLightRecord[] fireflies = Array.Empty<WofLilyCoilFlyingLightRecord>();
        [SerializeField] private WofLilyCoilFlyingLightRecord[] butterflies = Array.Empty<WofLilyCoilFlyingLightRecord>();
        [SerializeField] private Material particleMaterial;
        [SerializeField] private Material particleGlowMaterial;
        [SerializeField] private Material fireflyMaterial;
        [SerializeField] private Material fireflyGlowMaterial;
        [SerializeField] private Material butterflyLeftMaterial;
        [SerializeField] private Material butterflyRightMaterial;
        [SerializeField] private Material butterflyBodyMaterial;

        private LilyAnchor[] _flowerAnchors = Array.Empty<LilyAnchor>();
        private LilyAnchor[] _smallFlowerAnchors = Array.Empty<LilyAnchor>();
        private Matrix4x4[] _particleMatrices = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _particleGlowMatrices = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _fireflyMatrices = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _fireflyGlowMatrices = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _butterflyLeftMatrices = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _butterflyRightMatrices = Array.Empty<Matrix4x4>();
        private Matrix4x4[] _butterflyBodyMatrices = Array.Empty<Matrix4x4>();
        private Mesh _sphereMesh;
        private Mesh _quadMesh;
        private float _nextUpdateAt;
        private bool _hasMatrices;
        private bool _motionProbe;
        private bool _motionProbeStarted;
        private bool _motionProbeFinished;
        private float _motionProbeStartedAt;
        private Vector3 _motionProbeBloomStart;
        private Vector3 _motionProbeFireflyStart;
        private Vector3 _motionProbeButterflyStart;

        public int BloomParticleCount => bloomParticles?.Length ?? 0;
        public int FireflyCount => fireflies?.Length ?? 0;
        public int ButterflyCount => butterflies?.Length ?? 0;

        public void Configure(
            WofLilyCoilFlowerRecord[] exactFlowers,
            WofLilyCoilFlowerRecord[] exactSmallFlowers,
            WofLilyCoilBloomParticleRecord[] exactBloomParticles,
            WofLilyCoilFlyingLightRecord[] exactFireflies,
            WofLilyCoilFlyingLightRecord[] exactButterflies,
            Material exactParticleMaterial,
            Material exactParticleGlowMaterial,
            Material exactFireflyMaterial,
            Material exactFireflyGlowMaterial,
            Material exactButterflyLeftMaterial,
            Material exactButterflyRightMaterial,
            Material exactButterflyBodyMaterial)
        {
            flowers = exactFlowers ?? Array.Empty<WofLilyCoilFlowerRecord>();
            smallFlowers = exactSmallFlowers ?? Array.Empty<WofLilyCoilFlowerRecord>();
            bloomParticles = exactBloomParticles ?? Array.Empty<WofLilyCoilBloomParticleRecord>();
            fireflies = exactFireflies ?? Array.Empty<WofLilyCoilFlyingLightRecord>();
            butterflies = exactButterflies ?? Array.Empty<WofLilyCoilFlyingLightRecord>();
            particleMaterial = exactParticleMaterial;
            particleGlowMaterial = exactParticleGlowMaterial;
            fireflyMaterial = exactFireflyMaterial;
            fireflyGlowMaterial = exactFireflyGlowMaterial;
            butterflyLeftMaterial = exactButterflyLeftMaterial;
            butterflyRightMaterial = exactButterflyRightMaterial;
            butterflyBodyMaterial = exactButterflyBodyMaterial;
        }

        private void Awake()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-lily-ambient-motion-probe", StringComparison.OrdinalIgnoreCase))
                {
                    _motionProbe = true;
                    break;
                }
            }
            _sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            _quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            _flowerAnchors = MakeAnchors(flowers, 1.8f, 0.44f);
            _smallFlowerAnchors = MakeAnchors(smallFlowers, 1.6f, 0.56f);
            _particleMatrices = new Matrix4x4[BloomParticleCount];
            _particleGlowMatrices = new Matrix4x4[BloomParticleCount];
            _fireflyMatrices = new Matrix4x4[FireflyCount];
            _fireflyGlowMatrices = new Matrix4x4[FireflyCount];
            _butterflyLeftMatrices = new Matrix4x4[ButterflyCount];
            _butterflyRightMatrices = new Matrix4x4[ButterflyCount];
            _butterflyBodyMatrices = new Matrix4x4[ButterflyCount];
            DisableBakedEffectRenderer("SmallBloomParticles_750");
            DisableBakedEffectRenderer("SmallBloomParticleGlow_750");
            DisableBakedEffectRenderer("Fireflies_160");
            DisableBakedEffectRenderer("FireflyGlow_160");
            DisableBakedEffectRenderer("ButterflyLeftWings_10");
            DisableBakedEffectRenderer("ButterflyRightWings_10");
            DisableBakedEffectRenderer("ButterflyBodies_10");
        }

        private void Update()
        {
            var camera = Camera.main;
            if (camera == null || _sphereMesh == null || _quadMesh == null) return;
            var delta = camera.transform.position - transform.position;
            if (delta.sqrMagnitude > VisibilityRadius * VisibilityRadius) return;

            if (!_hasMatrices || !WofPerformanceModeRuntime.IsMobilePerformanceMode ||
                Time.unscaledTime >= _nextUpdateAt)
            {
                UpdateMatrices(Time.time, camera);
                _nextUpdateAt = Time.unscaledTime + MobileUpdateInterval;
                _hasMatrices = true;
                UpdateMotionProbe();
            }
            DrawBatches();
        }

        private void UpdateMotionProbe()
        {
            if (!_motionProbe || _motionProbeFinished || _particleMatrices.Length == 0 ||
                _fireflyMatrices.Length == 0 || _butterflyBodyMatrices.Length == 0) return;
            var bloom = GetPosition(_particleMatrices[0]);
            var firefly = GetPosition(_fireflyMatrices[0]);
            var butterfly = GetPosition(_butterflyBodyMatrices[0]);
            if (!_motionProbeStarted)
            {
                _motionProbeStarted = true;
                _motionProbeStartedAt = Time.unscaledTime;
                _motionProbeBloomStart = bloom;
                _motionProbeFireflyStart = firefly;
                _motionProbeButterflyStart = butterfly;
                Debug.Log($"[WOF-AUTOMATION] LILY_AMBIENT_RENDER_READY blooms={BloomParticleCount} fireflies={FireflyCount} butterflies={ButterflyCount}");
                return;
            }
            if (Time.unscaledTime - _motionProbeStartedAt < 1.5f) return;
            var bloomDelta = Vector3.Distance(_motionProbeBloomStart, bloom);
            var fireflyDelta = Vector3.Distance(_motionProbeFireflyStart, firefly);
            var butterflyDelta = Vector3.Distance(_motionProbeButterflyStart, butterfly);
            _motionProbeFinished = true;
            var passed = bloomDelta > 0.02f && fireflyDelta > 0.02f && butterflyDelta > 0.02f;
            Debug.Log($"[WOF-AUTOMATION] LILY_AMBIENT_MOTION_{(passed ? "PASS" : "FAIL")} bloomDelta={bloomDelta:F3} fireflyDelta={fireflyDelta:F3} butterflyDelta={butterflyDelta:F3}");
        }

        private void UpdateMatrices(float time, Camera camera)
        {
            var localToWorld = transform.localToWorldMatrix;
            if (_smallFlowerAnchors.Length > 0)
            {
                for (var index = 0; index < bloomParticles.Length; index++)
                {
                    var particle = bloomParticles[index];
                    var anchor = _smallFlowerAnchors[PositiveModulo(particle.flowerIndex, _smallFlowerAnchors.Length)];
                    var orbit = time * particle.speed + particle.phase;
                    var localPosition = anchor.Glow + anchor.Width * (Mathf.Cos(orbit) * particle.radius) +
                                        anchor.Normal * (Mathf.Sin(orbit * 0.86f) * particle.radius * 0.72f) +
                                        anchor.Growth * (particle.height + Mathf.Sin(time * 1.7f + particle.phase) * 0.72f);
                    var sparkle = 0.76f + Mathf.Sin(time * 4.6f + particle.phase) * 0.24f;
                    _particleMatrices[index] = localToWorld * Matrix4x4.TRS(
                        localPosition, Quaternion.identity, Vector3.one * (particle.size * sparkle));
                    _particleGlowMatrices[index] = localToWorld * Matrix4x4.TRS(
                        localPosition, Quaternion.identity, Vector3.one * (particle.size * (2f + sparkle * 1.45f)));
                }
            }

            if (_flowerAnchors.Length > 1)
            {
                for (var index = 0; index < fireflies.Length; index++)
                {
                    var fly = fireflies[index];
                    var route = time * fly.speed + fly.phase;
                    var step = Mathf.FloorToInt(route);
                    var amount = Mathf.SmoothStep(0f, 1f, route - step);
                    var from = _flowerAnchors[PositiveModulo(fly.anchor + step * fly.hop, _flowerAnchors.Length)];
                    var to = _flowerAnchors[PositiveModulo(fly.anchor + (step + 1) * fly.hop, _flowerAnchors.Length)];
                    var localPosition = Vector3.Lerp(from.Glow, to.Glow, amount) +
                                        from.Growth * (Mathf.Sin(amount * Mathf.PI) * fly.arc) +
                                        from.Width * (Mathf.Sin(time * 2.7f + fly.phase) * fly.wander) +
                                        from.Normal * (Mathf.Cos(time * 2.1f + fly.phase * 1.7f) * fly.wander * 0.55f);
                    var pulse = 0.72f + Mathf.Sin(time * 5.8f + fly.phase) * 0.22f;
                    _fireflyMatrices[index] = localToWorld * Matrix4x4.TRS(
                        localPosition, Quaternion.identity, Vector3.one * (fly.size * pulse));
                    var blink = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 1.35f + fly.phase * 2.1f)), 8f);
                    var blinkSize = blink > 0.035f ? fly.size * (3.2f + blink * 9.5f) : 0.001f;
                    _fireflyGlowMatrices[index] = localToWorld * Matrix4x4.TRS(
                        localPosition, Quaternion.identity, Vector3.one * blinkSize);
                }

                var cameraRotation = camera.transform.rotation;
                var cameraRight = camera.transform.right;
                for (var index = 0; index < butterflies.Length; index++)
                {
                    var butterfly = butterflies[index];
                    var route = time * butterfly.speed + butterfly.phase;
                    var step = Mathf.FloorToInt(route);
                    var amount = Mathf.SmoothStep(0f, 1f, route - step);
                    var from = _flowerAnchors[PositiveModulo(
                        butterfly.anchor + step * butterfly.hop, _flowerAnchors.Length)];
                    var to = _flowerAnchors[PositiveModulo(
                        butterfly.anchor + (step + 1) * butterfly.hop, _flowerAnchors.Length)];
                    var localPosition = Vector3.Lerp(from.Glow, to.Glow, amount) +
                                        from.Growth * (Mathf.Sin(amount * Mathf.PI) * butterfly.arc) +
                                        from.Width * (Mathf.Sin(time * 1.3f + butterfly.phase) * butterfly.wander) +
                                        from.Normal * (Mathf.Cos(time * 1.7f + butterfly.phase) * butterfly.wander * 0.75f);
                    var worldPosition = transform.TransformPoint(localPosition);
                    var flap = Mathf.Sin(time * (7.5f + index * 0.17f) + butterfly.phase);
                    var spread = butterfly.size * (0.82f + Mathf.Abs(flap) * 0.32f);
                    var leftRotation = cameraRotation * Quaternion.AngleAxis(
                        (-0.35f - Mathf.Abs(flap) * 0.52f) * Mathf.Rad2Deg, Vector3.forward);
                    var rightRotation = cameraRotation * Quaternion.AngleAxis(
                        (0.35f + Mathf.Abs(flap) * 0.52f) * Mathf.Rad2Deg, Vector3.forward);
                    var wingScale = new Vector3(butterfly.size * 1.25f, butterfly.size * 1.85f, 1f);
                    _butterflyLeftMatrices[index] = Matrix4x4.TRS(
                        worldPosition - cameraRight * (spread * 1.15f), leftRotation, wingScale);
                    _butterflyRightMatrices[index] = Matrix4x4.TRS(
                        worldPosition + cameraRight * (spread * 1.15f), rightRotation, wingScale);
                    _butterflyBodyMatrices[index] = Matrix4x4.TRS(
                        worldPosition, cameraRotation,
                        new Vector3(butterfly.size * 0.2f, butterfly.size * 0.72f, butterfly.size * 0.2f));
                }
            }
        }

        private void DrawBatches()
        {
            Draw(_sphereMesh, particleMaterial, _particleMatrices);
            Draw(_sphereMesh, particleGlowMaterial, _particleGlowMatrices);
            Draw(_sphereMesh, fireflyMaterial, _fireflyMatrices);
            Draw(_sphereMesh, fireflyGlowMaterial, _fireflyGlowMatrices);
            Draw(_quadMesh, butterflyLeftMaterial, _butterflyLeftMatrices);
            Draw(_quadMesh, butterflyRightMaterial, _butterflyRightMatrices);
            Draw(_sphereMesh, butterflyBodyMaterial, _butterflyBodyMatrices);
        }

        private void Draw(Mesh mesh, Material material, Matrix4x4[] matrices)
        {
            if (mesh == null || material == null || matrices == null || matrices.Length == 0) return;
            Graphics.DrawMeshInstanced(
                mesh, 0, material, matrices, matrices.Length, null,
                ShadowCastingMode.Off, false, gameObject.layer, null, LightProbeUsage.Off);
        }

        private void DisableBakedEffectRenderer(string childName)
        {
            var child = transform.Find(childName);
            var renderer = child == null ? null : child.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
        }

        private static LilyAnchor[] MakeAnchors(
            WofLilyCoilFlowerRecord[] records,
            float radiusInset,
            float glowHeightFactor)
        {
            if (records == null || records.Length == 0) return Array.Empty<LilyAnchor>();
            var result = new LilyAnchor[records.Length];
            for (var index = 0; index < records.Length; index++)
            {
                var flower = records[index];
                var frame = WofLilyCoilLayout.GetFrame(flower.t);
                var radial = WofLilyCoilLayout.GetRadial(frame, flower.angle);
                var growth = -radial;
                var around = WofLilyCoilLayout.GetAroundSurface(frame, flower.angle);
                var width = (frame.Tangent * Mathf.Cos(flower.yaw) + around * Mathf.Sin(flower.yaw)).normalized;
                var normal = Vector3.Cross(width, growth).normalized;
                var basePosition = frame.Center - WofLilyCoilLayout.WorldOrigin +
                                   radial * (WofLilyCoilLayout.TubeRadius - radiusInset);
                result[index] = new LilyAnchor(
                    basePosition,
                    growth,
                    width,
                    normal,
                    basePosition + growth * (flower.stemHeight + flower.bloomHeight * glowHeightFactor));
            }
            return result;
        }

        private static int PositiveModulo(int value, int length)
        {
            var result = value % length;
            return result < 0 ? result + length : result;
        }

        private static Vector3 GetPosition(Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        private readonly struct LilyAnchor
        {
            public LilyAnchor(Vector3 @base, Vector3 growth, Vector3 width, Vector3 normal, Vector3 glow)
            {
                Base = @base;
                Growth = growth;
                Width = width;
                Normal = normal;
                Glow = glow;
            }

            public Vector3 Base { get; }
            public Vector3 Growth { get; }
            public Vector3 Width { get; }
            public Vector3 Normal { get; }
            public Vector3 Glow { get; }
        }
    }
}
