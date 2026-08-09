using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofStaticAvatarBillboard : MonoBehaviour
    {
        [SerializeField] private string avatarId;
        [SerializeField] private string archiveFile;
        [SerializeField] private float baseYaw;
        [SerializeField] private int fixedDirection = -1;
        [SerializeField] private Transform spriteTransform;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private readonly Dictionary<string, CachedFrame> _frames = new(StringComparer.Ordinal);
        private WofVillagerFrameArchive _archive;
        private Coroutine _loadRoutine;
        private WofSeededRandom _blinkRandom;
        private float _blinkStartAt;
        private float _blinkEndAt;
        private bool _blinking;
        private string _shownKey;

        public string AvatarId => avatarId;
        public string ArchiveFile => archiveFile;

        public void Configure(
            string id,
            string frameArchive,
            Vector3 localPosition,
            float yawRadians,
            int forcedDirection,
            Transform visual,
            SpriteRenderer renderer)
        {
            avatarId = id;
            archiveFile = frameArchive;
            transform.localPosition = localPosition;
            transform.localScale = Vector3.one * WofVillagerMath.AvatarScale;
            baseYaw = yawRadians;
            fixedDirection = forcedDirection;
            spriteTransform = visual;
            spriteRenderer = renderer;
        }

        private void Awake()
        {
            _blinkRandom = new WofSeededRandom($"avatar-blink:chapel:{avatarId}");
            _blinkStartAt = Time.unscaledTime + NextBlinkDelay();
            if (spriteTransform == null && transform.childCount > 0) spriteTransform = transform.GetChild(0);
            if (spriteRenderer == null && spriteTransform != null)
                spriteRenderer = spriteTransform.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.enabled = false;
        }

        private void OnEnable()
        {
            if (_archive == null && _loadRoutine == null && !string.IsNullOrWhiteSpace(archiveFile))
                _loadRoutine = StartCoroutine(LoadArchive());
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null || spriteTransform == null) return;
            var toCamera = camera.transform.position - spriteTransform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
                spriteTransform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            if (_archive == null) return;

            UpdateBlink();
            var direction = fixedDirection >= 0
                ? fixedDirection
                : WofVillagerMath.ResolveDirection(baseYaw, transform.position, camera.transform.position);
            var key = WofVillagerMath.ResolveFrameKey(WofVillagerPhase.Idle, direction, 0, _blinking);
            if (string.Equals(key, _shownKey, StringComparison.Ordinal)) return;
            if (!TryShowFrame(key) && _blinking)
                TryShowFrame(WofVillagerMath.ResolveFrameKey(WofVillagerPhase.Idle, direction, 0, false));
        }

        private void OnDestroy()
        {
            foreach (var frame in _frames.Values) frame.Destroy();
            _frames.Clear();
        }

        private void UpdateBlink()
        {
            var now = Time.unscaledTime;
            if (!_blinking && now >= _blinkStartAt)
            {
                _blinking = true;
                _blinkEndAt = now + 0.095f + (float)_blinkRandom.NextDouble() * 0.07f;
            }
            else if (_blinking && now >= _blinkEndAt)
            {
                _blinking = false;
                _blinkStartAt = now + NextBlinkDelay();
            }
        }

        private float NextBlinkDelay()
        {
            return 2.4f + (float)_blinkRandom.NextDouble() * 5.2f;
        }

        private IEnumerator LoadArchive()
        {
            var path = $"{Application.streamingAssetsPath.TrimEnd('/', '\\')}/WOF/Villagers/Base/{archiveFile}";
            if (!path.Contains("://", StringComparison.Ordinal))
                path = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            using var request = UnityWebRequest.Get(path);
            yield return request.SendWebRequest();
            _loadRoutine = null;
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[WOF] Chapel avatar archive load failed for {avatarId}: {request.error}");
                yield break;
            }
            if (!WofVillagerFrameArchive.TryParse(request.downloadHandler.data, out _archive, out var error))
            {
                Debug.LogError($"[WOF] Chapel avatar archive parse failed for {avatarId}: {error}");
                yield break;
            }
            Debug.Log($"[WOF-AUTOMATION] CHAPEL_AVATAR_ARCHIVE_READY id={avatarId} entries={_archive.EntryCount}");
        }

        private bool TryShowFrame(string key)
        {
            if (!_frames.TryGetValue(key, out var frame))
            {
                if (!_archive.TryExtractPng(key, out var png)) return false;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                {
                    name = $"Chapel Avatar {avatarId} {key}",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                if (!ImageConversion.LoadImage(texture, png, true))
                {
                    Destroy(texture);
                    return false;
                }
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    texture.height / WofVillagerMath.AvatarWorldHeight,
                    0u,
                    SpriteMeshType.FullRect);
                frame = new CachedFrame(texture, sprite);
                _frames.Add(key, frame);
            }
            spriteRenderer.sprite = frame.Sprite;
            spriteRenderer.enabled = true;
            _shownKey = key;
            return true;
        }

        private readonly struct CachedFrame
        {
            public CachedFrame(Texture2D texture, Sprite sprite)
            {
                Texture = texture;
                Sprite = sprite;
            }

            private Texture2D Texture { get; }
            public Sprite Sprite { get; }

            public void Destroy()
            {
                if (Sprite != null) UnityEngine.Object.Destroy(Sprite);
                if (Texture != null) UnityEngine.Object.Destroy(Texture);
            }
        }
    }
}
