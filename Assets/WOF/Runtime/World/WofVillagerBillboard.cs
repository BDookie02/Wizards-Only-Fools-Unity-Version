using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofVillagerBillboard : MonoBehaviour
    {
        private const int LocalFrameCacheLimit = 4;
        private const float IdleFrameSeconds = 0.21f;
        private const float StartledFrameSeconds = 0.085f;
        private const float AngryFrameSeconds = 0.21f;
        private const float BlinkDelayBaseSeconds = 2.4f;
        private const float BlinkDelayRandomSeconds = 5.2f;
        private const float BlinkDurationBaseSeconds = 0.095f;
        private const float BlinkDurationRandomSeconds = 0.07f;

        [SerializeField] private string villagerId;
        [SerializeField] private string reactDisplayName;
        [SerializeField] private string reactTownId;
        [SerializeField] private string archiveFile;
        [SerializeField] private float baseYaw;
        [SerializeField] private float lookUpdateDesktopSeconds;
        [SerializeField] private float lookUpdateMobileSeconds;
        [SerializeField] private WofVillagerHutRecord hut;
        [SerializeField] private Transform spriteTransform;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private readonly Dictionary<string, CachedFrame> _frames = new(StringComparer.Ordinal);
        private readonly LinkedList<string> _frameOrder = new();
        private WofVillagerFrameArchive _archive;
        private Coroutine _loadRoutine;
        private Vector3 _baseLocalPosition;
        private float _lookYaw;
        private float _nextLookUpdateAt;
        private float _nextFrameAt;
        private int _frame;
        private WofVillagerPhase _lastPhase;
        private WofSeededRandom _blinkRandom;
        private float _blinkStartAt;
        private float _blinkEndAt;
        private bool _blinking;
        private bool _worldVisible;
        private bool _playerInside;
        private float _reactionStartedAt = float.NegativeInfinity;
        private float _startledUntil = float.NegativeInfinity;
        private float _angryUntil = float.NegativeInfinity;
        private bool _loadFailed;
        private AudioSource _yelpSource;
        private AudioClip _activeYelpClip;

        public string VillagerId => villagerId;
        public string ReactDisplayName => reactDisplayName;
        public string ReactTownId => reactTownId;
        public WofVillagerHutRecord Hut => hut;
        public Vector3 VillagerPosition => _baseLocalPosition;
        public Vector3 InteractionCenter => transform.position + Vector3.up *
                                            (WofQuestTargetMath.TargetCenterHeight - WofVillagerMath.AvatarGroundLift);
        public bool IsDarrel => WofQuestDevStore.IsDarrelNpc(villagerId);

        private void Awake()
        {
            _baseLocalPosition = transform.localPosition;
            _lookYaw = baseYaw;
            _blinkRandom = new WofSeededRandom($"avatar-blink:villager:{villagerId}");
            _blinkStartAt = Time.unscaledTime + NextBlinkDelay();
            if (spriteTransform == null && transform.childCount > 0)
            {
                spriteTransform = transform.GetChild(0);
            }
            if (spriteRenderer == null && spriteTransform != null)
            {
                spriteRenderer = spriteTransform.GetComponent<SpriteRenderer>();
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            ReleaseFrames();
            if (_activeYelpClip != null)
            {
                Destroy(_activeYelpClip);
            }
        }

        public void Configure(
            string id,
            string frameArchive,
            Vector3 localPosition,
            float yawRadians,
            float desktopLookUpdateMs,
            float mobileLookUpdateMs,
            WofVillagerHutRecord hutRecord,
            Transform visual,
            SpriteRenderer renderer,
            string displayName = "",
            string townId = "base-village")
        {
            villagerId = id;
            reactDisplayName = displayName ?? string.Empty;
            reactTownId = string.IsNullOrWhiteSpace(townId) ? "base-village" : townId;
            archiveFile = frameArchive;
            transform.localPosition = localPosition;
            transform.localScale = Vector3.one * WofVillagerMath.AvatarScale;
            baseYaw = yawRadians;
            lookUpdateDesktopSeconds = desktopLookUpdateMs * 0.001f;
            lookUpdateMobileSeconds = mobileLookUpdateMs * 0.001f;
            hut = hutRecord;
            spriteTransform = visual;
            spriteRenderer = renderer;
        }

        public void SetWorldVisible(bool visible)
        {
            if (_worldVisible == visible)
            {
                return;
            }

            _worldVisible = visible;
            if (!visible)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = false;
                }
                ReleaseFrames();
                return;
            }

            if (_archive == null && _loadRoutine == null && !_loadFailed)
            {
                _loadRoutine = StartCoroutine(LoadArchive());
            }
            else if (_archive != null && spriteRenderer != null && spriteRenderer.sprite != null)
            {
                spriteRenderer.enabled = true;
            }
        }

        public void SetPlayerInside(bool inside, float now, float yelpVolume = 0f)
        {
            if (inside && !_playerInside && now - _reactionStartedAt > 0.9f)
            {
                TriggerReaction(now, WofVillagerMath.StartledInsideSeconds, WofVillagerMath.AngryInsideSeconds);
                PlayYelp(yelpVolume);
            }
            _playerInside = inside;
        }

        public void TriggerInteraction(float now)
        {
            TriggerReaction(now, WofVillagerMath.StartledInteractSeconds, WofVillagerMath.AngryInteractSeconds);
            PlayYelp(0.42f);
        }

        public bool IsReacting(float now)
        {
            return _playerInside || now < _angryUntil;
        }

        public void TickVisual(
            Camera camera,
            Vector3 playerPosition,
            IReadOnlyList<Vector3> facingTargets,
            float now,
            bool mobile)
        {
            if (!_worldVisible || camera == null)
            {
                return;
            }

            UpdateLook(facingTargets, now, mobile);
            UpdateBlink(now);
            var phase = WofVillagerMath.ResolvePhase(now, _startledUntil, _angryUntil, _playerInside);
            UpdateFrame(now, phase);
            var jumpOffset = WofVillagerMath.ResolveJumpOffset(now, _reactionStartedAt, phase);
            transform.localPosition = _baseLocalPosition + Vector3.up * jumpOffset;

            if (spriteTransform != null)
            {
                var toCamera = camera.transform.position - spriteTransform.position;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    spriteTransform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
                }
            }

            if (_archive == null)
            {
                return;
            }

            var direction = WofVillagerMath.ResolveDirection(_lookYaw, transform.position, camera.transform.position);
            var key = WofVillagerMath.ResolveFrameKey(phase, direction, _frame, _blinking);
            if (!TryShowFrame(key) && _blinking)
            {
                TryShowFrame(WofVillagerMath.ResolveFrameKey(phase, direction, _frame, false));
            }
        }

        private void TriggerReaction(float now, float startledSeconds, float angrySeconds)
        {
            _reactionStartedAt = now;
            _startledUntil = now + startledSeconds;
            _angryUntil = now + angrySeconds;
        }

        private void PlayYelp(float volume)
        {
            if (_yelpSource == null)
            {
                _yelpSource = gameObject.AddComponent<AudioSource>();
                _yelpSource.playOnAwake = false;
                _yelpSource.loop = false;
                _yelpSource.spatialBlend = 0f;
            }
            if (_activeYelpClip != null)
            {
                _yelpSource.Stop();
                Destroy(_activeYelpClip);
            }

            var safeVolume = WofVillagerYelp.ClampVolume(volume);
            _activeYelpClip = WofVillagerYelp.CreateClip(safeVolume, AudioSettings.outputSampleRate);
            _yelpSource.clip = _activeYelpClip;
            _yelpSource.PlayScheduled(AudioSettings.dspTime + WofVillagerYelp.StartDelaySeconds);
            Debug.Log($"[WOF-AUTOMATION] VILLAGER_YELP id={villagerId} volume={safeVolume:F3} duration={WofVillagerYelp.DurationSeconds:F2}");
        }

        private void UpdateLook(IReadOnlyList<Vector3> facingTargets, float now, bool mobile)
        {
            if (now < _nextLookUpdateAt)
            {
                return;
            }

            var interval = mobile ? lookUpdateMobileSeconds : lookUpdateDesktopSeconds;
            _nextLookUpdateAt = now + Mathf.Max(0.01f, interval);
            var targetYaw = baseYaw;
            WofVillagerMath.TryResolveNearestFacingYaw(_baseLocalPosition, facingTargets, baseYaw, out targetYaw);
            if (WofVillagerMath.AngleDistance(targetYaw, _lookYaw) >= WofVillagerMath.LookYawEpsilon)
            {
                _lookYaw = targetYaw;
            }
        }

        private void UpdateBlink(float now)
        {
            if (!_blinking && now >= _blinkStartAt)
            {
                _blinking = true;
                _blinkEndAt = now + BlinkDurationBaseSeconds + (float)_blinkRandom.NextDouble() * BlinkDurationRandomSeconds;
            }
            else if (_blinking && now >= _blinkEndAt)
            {
                _blinking = false;
                _blinkStartAt = now + NextBlinkDelay();
            }
        }

        private float NextBlinkDelay()
        {
            return BlinkDelayBaseSeconds + (float)_blinkRandom.NextDouble() * BlinkDelayRandomSeconds;
        }

        private void UpdateFrame(float now, WofVillagerPhase phase)
        {
            if (phase != _lastPhase)
            {
                _lastPhase = phase;
                _nextFrameAt = now;
            }
            if (now < _nextFrameAt)
            {
                return;
            }

            var delay = phase switch
            {
                WofVillagerPhase.Startled => StartledFrameSeconds,
                WofVillagerPhase.Angry => AngryFrameSeconds,
                _ => IdleFrameSeconds
            };
            _nextFrameAt = now + delay;
            _frame = (_frame + 1) % 4;
        }

        private IEnumerator LoadArchive()
        {
            var path = $"{Application.streamingAssetsPath.TrimEnd('/', '\\')}/WOF/Villagers/Base/{archiveFile}";
            if (!path.Contains("://", StringComparison.Ordinal))
            {
                path = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            }

            using var request = UnityWebRequest.Get(path);
            yield return request.SendWebRequest();
            _loadRoutine = null;
            if (request.result != UnityWebRequest.Result.Success)
            {
                _loadFailed = true;
                Debug.LogError($"[WOF] Villager archive load failed for {villagerId}: {request.error}");
                yield break;
            }

            if (!WofVillagerFrameArchive.TryParse(request.downloadHandler.data, out _archive, out var error))
            {
                _loadFailed = true;
                Debug.LogError($"[WOF] Villager archive parse failed for {villagerId}: {error}");
                yield break;
            }

            Debug.Log($"[WOF-AUTOMATION] VILLAGER_ARCHIVE_READY id={villagerId} entries={_archive.EntryCount}");
        }

        private bool TryShowFrame(string key)
        {
            if (!_frames.TryGetValue(key, out var frame))
            {
                if (!_archive.TryExtractPng(key, out var png))
                {
                    return false;
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                {
                    name = $"Villager {villagerId} {key}",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                if (!ImageConversion.LoadImage(texture, png, true))
                {
                    Destroy(texture);
                    return false;
                }

                var pixelsPerUnit = texture.height / WofVillagerMath.AvatarWorldHeight;
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit,
                    0u,
                    SpriteMeshType.FullRect);
                sprite.name = texture.name;
                frame = new CachedFrame(texture, sprite);
                _frames.Add(key, frame);
                _frameOrder.AddLast(key);
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = frame.Sprite;
                spriteRenderer.enabled = _worldVisible;
            }
            TouchFrame(key);
            TrimFrameCache(key);
            return true;
        }

        private void TouchFrame(string key)
        {
            var node = _frameOrder.Find(key);
            if (node == null || node == _frameOrder.Last)
            {
                return;
            }
            _frameOrder.Remove(node);
            _frameOrder.AddLast(node);
        }

        private void TrimFrameCache(string currentKey)
        {
            while (_frames.Count > LocalFrameCacheLimit)
            {
                var node = _frameOrder.First;
                while (node != null && string.Equals(node.Value, currentKey, StringComparison.Ordinal))
                {
                    node = node.Next;
                }
                if (node == null)
                {
                    return;
                }

                var key = node.Value;
                _frameOrder.Remove(node);
                if (_frames.Remove(key, out var frame))
                {
                    frame.Destroy();
                }
            }
        }

        private void ReleaseFrames()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = null;
            }
            foreach (var frame in _frames.Values)
            {
                frame.Destroy();
            }
            _frames.Clear();
            _frameOrder.Clear();
        }

        private readonly struct CachedFrame
        {
            public CachedFrame(Texture2D texture, Sprite sprite)
            {
                Texture = texture;
                Sprite = sprite;
            }

            public Texture2D Texture { get; }
            public Sprite Sprite { get; }

            public void Destroy()
            {
                if (Sprite != null)
                {
                    UnityEngine.Object.Destroy(Sprite);
                }
                if (Texture != null)
                {
                    UnityEngine.Object.Destroy(Texture);
                }
            }
        }
    }
}
