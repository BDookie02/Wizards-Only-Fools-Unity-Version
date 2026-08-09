using System;
using UnityEngine;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSwampToadRuntime : MonoBehaviour
    {
        private const float DayNightCycleSeconds = 600f;
        private const float CyclePhaseOffset = 0.18f;

        [SerializeField] private SpriteRenderer toadRenderer;
        [SerializeField] private SpriteRenderer sleepZRenderer;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] yawnFrames;
        [SerializeField] private Sprite sleepFrame;
        [SerializeField] private float idleFrameMilliseconds = 200f;
        [SerializeField] private float yawnFrameMilliseconds = 120f;
        [SerializeField] private float platformY = WofSwampVillageLayout.ReactPlatformY;

        private Camera _camera;
        private bool _sleeping;
        private bool _nightCue;
        private float _yawnStartedAt = float.NegativeInfinity;
        private float _yawnUntil = float.NegativeInfinity;

        public void Configure(
            SpriteRenderer generatedToadRenderer,
            SpriteRenderer generatedSleepZRenderer,
            Sprite[] generatedIdleFrames,
            Sprite[] generatedYawnFrames,
            Sprite generatedSleepFrame,
            float generatedIdleFrameMilliseconds,
            float generatedYawnFrameMilliseconds,
            float generatedPlatformY)
        {
            toadRenderer = generatedToadRenderer;
            sleepZRenderer = generatedSleepZRenderer;
            idleFrames = generatedIdleFrames;
            yawnFrames = generatedYawnFrames;
            sleepFrame = generatedSleepFrame;
            idleFrameMilliseconds = generatedIdleFrameMilliseconds;
            yawnFrameMilliseconds = generatedYawnFrameMilliseconds;
            platformY = generatedPlatformY;
        }

        private void Awake()
        {
            if (toadRenderer != null && idleFrames != null && idleFrames.Length > 0)
            {
                toadRenderer.sprite = idleFrames[0];
            }
            if (sleepZRenderer != null)
            {
                sleepZRenderer.enabled = false;
            }
        }

        private void LateUpdate()
        {
            var elapsedSeconds = Time.time;
            var nightAmount = GetNightAmount(elapsedSeconds);
            var nightCue = nightAmount > 0.42f;
            var sleeping = nightAmount > 0.68f;
            var yawnDuration = Mathf.Max(
                2.6f,
                (yawnFrames?.Length ?? 0) * yawnFrameMilliseconds * 2.1f / 1000f);

            if ((nightCue && !_nightCue) || (_sleeping && !sleeping))
            {
                _yawnStartedAt = elapsedSeconds;
                _yawnUntil = elapsedSeconds + yawnDuration;
            }
            _nightCue = nightCue;

            var yawning = elapsedSeconds < _yawnUntil;
            if (toadRenderer != null)
            {
                toadRenderer.sprite = ResolveFrame(elapsedSeconds, yawning, sleeping);
                var breathWave = (Mathf.Sin(elapsedSeconds * (sleeping ? 0.82f : 1.12f)) + 1f) * 0.5f;
                const float baseWidth = 56f;
                const float baseHeight = 36f;
                var breathWidth = sleeping ? 1.8f : 2.2f;
                var breathHeight = sleeping ? 0.7f : 1.35f;
                var squat = sleeping ? 0.84f : 1f;
                var currentHeight = baseHeight * squat + breathWave * breathHeight;
                var plantedBottomY = platformY + 0.12f;
                toadRenderer.transform.localPosition = new Vector3(0f, plantedBottomY + currentHeight * 0.5f, 0f);
                SetSpriteWorldSize(
                    toadRenderer,
                    baseWidth + breathWave * breathWidth,
                    currentHeight);
            }

            if (sleepZRenderer != null)
            {
                var sleepFade = yawning ? 0f : Mathf.Clamp01((nightAmount - 0.68f) / 0.22f);
                var drift = elapsedSeconds * 0.34f % 1f;
                var bob = Mathf.Sin(elapsedSeconds * 1.6f) * 0.38f;
                sleepZRenderer.enabled = sleepFade > 0.02f;
                sleepZRenderer.transform.localPosition = new Vector3(
                    17.5f + drift * 3.5f,
                    platformY + 38.5f + drift * 5.2f + bob,
                    0f);
                SetSpriteWorldSize(
                    sleepZRenderer,
                    12.5f + sleepFade * 2.5f,
                    7.5f + sleepFade * 1.5f);
                var color = sleepZRenderer.color;
                color.a = sleepFade * (0.62f + 0.28f * (1f - drift));
                sleepZRenderer.color = color;
            }

            _sleeping = sleeping;
            FaceCamera(toadRenderer);
            FaceCamera(sleepZRenderer);
        }

        private Sprite ResolveFrame(float elapsedSeconds, bool yawning, bool sleeping)
        {
            var fallback = idleFrames != null && idleFrames.Length > 0 ? idleFrames[0] : sleepFrame;
            if (yawning && yawnFrames != null && yawnFrames.Length > 0)
            {
                var yawnElapsedMilliseconds = Mathf.Max(0f, (elapsedSeconds - _yawnStartedAt) * 1000f);
                var frame = Mathf.Min(
                    yawnFrames.Length - 1,
                    Mathf.FloorToInt(yawnElapsedMilliseconds / (yawnFrameMilliseconds * 2.1f)));
                return yawnFrames[frame] ?? fallback;
            }
            if (sleeping && sleepFrame != null)
            {
                return sleepFrame;
            }
            if (idleFrames == null || idleFrames.Length == 0)
            {
                return fallback;
            }
            var idleFrame = Mathf.FloorToInt(elapsedSeconds * 1000f / idleFrameMilliseconds) % idleFrames.Length;
            return idleFrames[idleFrame] ?? fallback;
        }

        private void FaceCamera(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            if (_camera == null) return;
            var direction = renderer.transform.position - _camera.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                renderer.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private static void SetSpriteWorldSize(SpriteRenderer renderer, float width, float height)
        {
            var size = renderer.sprite != null ? renderer.sprite.bounds.size : Vector3.one;
            renderer.transform.localScale = new Vector3(
                width / Mathf.Max(0.0001f, size.x),
                height / Mathf.Max(0.0001f, size.y),
                1f);
        }

        private static float GetNightAmount(float elapsedSeconds)
        {
            var phase = elapsedSeconds / DayNightCycleSeconds + CyclePhaseOffset;
            phase -= Mathf.Floor(phase);
            var sunHeight = Mathf.Sin(phase * Mathf.PI * 2f);
            return 1f - SmoothStepRange(-0.36f, 0.08f, sunHeight);
        }

        private static float SmoothStepRange(float edge0, float edge1, float value)
        {
            var t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
