using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WOF
{
    [Serializable]
    public struct WofDarrelFallingPetalSeed
    {
        public float x;
        public float z;
        public float phase;
        public float speed;
        public float sway;
        public float drift;
        public float scale;
        public float spin;
    }

    [Serializable]
    public struct WofDarrelWaterfallSpraySeed
    {
        public float baseY;
        public float baseScale;
    }

    [DisallowMultipleComponent]
    public sealed class WofDarrelGroveRuntime : MonoBehaviour
    {
        private static readonly Color SleepColor = new(0.796f, 0.835f, 0.882f, 0.78f);
        private static readonly Color WakeColor = new(0.878f, 0.949f, 0.996f, 0.88f);
        private static readonly Color PeaceColor = new(0.875f, 0.969f, 1f, 0.94f);

        [SerializeField] private SpriteRenderer dragonRenderer;
        [SerializeField] private Light dragonLight;
        [SerializeField] private Sprite[] sleepFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] wakeFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] idleFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] attackFrames = Array.Empty<Sprite>();
        [SerializeField] private Transform[] fallingPetalTransforms = Array.Empty<Transform>();
        [SerializeField] private WofDarrelFallingPetalSeed[] fallingPetalSeeds = Array.Empty<WofDarrelFallingPetalSeed>();
        [SerializeField] private Material fallWaterMaterial;
        [SerializeField] private Material foamMaterial;
        [SerializeField] private Material[] poolWaterMaterials = Array.Empty<Material>();
        [SerializeField] private Material[] runnelWaterMaterials = Array.Empty<Material>();
        [SerializeField] private Transform[] waterfallRunnelTransforms = Array.Empty<Transform>();
        [SerializeField] private Transform[] waterfallSprayTransforms = Array.Empty<Transform>();
        [SerializeField] private WofDarrelWaterfallSpraySeed[] waterfallSpraySeeds = Array.Empty<WofDarrelWaterfallSpraySeed>();
        [SerializeField] private Font promptFont;

        private WofDarrelDragonMode _dragonMode = WofDarrelDragonMode.Sleep;
        private float _dragonModeStartedAt;
        private Vector3 _dragonBaseLocalPosition;
        private Vector2 _dragonSpriteSize = Vector2.one;
        private WofPlayerController _localPlayer;
        private WofQuestDialogRuntime _questDialog;
        private RectTransform _screenPrompt;
        private Text _screenPromptText;
        private Canvas _worldPromptCanvas;
        private Text _worldPromptText;
        private float _nextProfileRefreshAt;
        private float _nextReturnAt;
        private bool _hasPlayerEnteredHouse;
        private bool _hasWoken;
        private bool _hasFought;
        private bool _hasPeacefulDragon;
        private bool _returnProbeRequested;
        private bool _returnProbeStarted;

        public static WofDarrelGroveRuntime Instance { get; private set; }
        public bool IsPromptVisible => _screenPrompt != null && _screenPrompt.gameObject.activeSelf;
        public WofDarrelDragonMode DragonMode => _dragonMode;

        public void ConfigureGeneratedView(
            SpriteRenderer generatedDragonRenderer,
            Light generatedDragonLight,
            Sprite[] generatedSleepFrames,
            Sprite[] generatedWakeFrames,
            Sprite[] generatedIdleFrames,
            Sprite[] generatedAttackFrames,
            Transform[] generatedFallingPetalTransforms,
            WofDarrelFallingPetalSeed[] generatedFallingPetalSeeds,
            Material generatedFallWaterMaterial,
            Material generatedFoamMaterial,
            Material[] generatedPoolWaterMaterials,
            Material[] generatedRunnelWaterMaterials,
            Transform[] generatedWaterfallRunnelTransforms,
            Transform[] generatedWaterfallSprayTransforms,
            WofDarrelWaterfallSpraySeed[] generatedWaterfallSpraySeeds,
            Font generatedPromptFont)
        {
            dragonRenderer = generatedDragonRenderer;
            dragonLight = generatedDragonLight;
            sleepFrames = generatedSleepFrames ?? Array.Empty<Sprite>();
            wakeFrames = generatedWakeFrames ?? Array.Empty<Sprite>();
            idleFrames = generatedIdleFrames ?? Array.Empty<Sprite>();
            attackFrames = generatedAttackFrames ?? Array.Empty<Sprite>();
            fallingPetalTransforms = generatedFallingPetalTransforms ?? Array.Empty<Transform>();
            fallingPetalSeeds = generatedFallingPetalSeeds ?? Array.Empty<WofDarrelFallingPetalSeed>();
            fallWaterMaterial = generatedFallWaterMaterial;
            foamMaterial = generatedFoamMaterial;
            poolWaterMaterials = generatedPoolWaterMaterials ?? Array.Empty<Material>();
            runnelWaterMaterials = generatedRunnelWaterMaterials ?? Array.Empty<Material>();
            waterfallRunnelTransforms = generatedWaterfallRunnelTransforms ?? Array.Empty<Transform>();
            waterfallSprayTransforms = generatedWaterfallSprayTransforms ?? Array.Empty<Transform>();
            waterfallSpraySeeds = generatedWaterfallSpraySeeds ?? Array.Empty<WofDarrelWaterfallSpraySeed>();
            promptFont = generatedPromptFont;
        }

        private void Awake()
        {
            Instance = this;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-darrel-grove-return-probe", StringComparison.OrdinalIgnoreCase))
                {
                    _returnProbeRequested = true;
                    break;
                }
            }
            if (dragonRenderer != null)
            {
                _dragonBaseLocalPosition = dragonRenderer.transform.localPosition;
                if (dragonRenderer.sprite != null)
                {
                    _dragonSpriteSize = dragonRenderer.sprite.bounds.size;
                }
            }
            _dragonModeStartedAt = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            ResolveLocalPlayer();
            if (_returnProbeRequested && !_returnProbeStarted && _localPlayer != null)
            {
                _returnProbeStarted = true;
                StartCoroutine(RunReturnGateProbe());
            }
            RefreshQuestState();
            UpdateDragonVisuals();
            UpdateWaterfallVisuals();
            UpdateFallingPetals();
            UpdateInteractionPrompt();
            PollReturnGate();
        }

        public static bool TryInteractWithDragon(WofPlayerController player)
        {
            return Instance != null && Instance.TryInteract(player);
        }

        public static bool IsDragonInteractionReady(WofPlayerController player)
        {
            return Instance != null && Instance.CanInteract(player);
        }

        private bool TryInteract(WofPlayerController player)
        {
            if (!CanInteract(player))
            {
                return false;
            }
            _hasPlayerEnteredHouse = true;
            _questDialog ??= FindFirstObjectByType<WofQuestDialogRuntime>();
            return _questDialog != null && _questDialog.OpenSpiritDragonDialog();
        }

        private bool CanInteract(WofPlayerController player)
        {
            return player != null && player.IsOwner && !player.IsDead &&
                   !WofInputRouter.GameplaySuppressed &&
                   WofDarrelGroveLayout.CanInteractWithDragon(player.transform.position);
        }

        private void ResolveLocalPlayer()
        {
            if (_localPlayer != null && _localPlayer.IsSpawned && _localPlayer.IsOwner)
            {
                return;
            }

            _localPlayer = null;
            var players = FindObjectsByType<WofPlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var index = 0; index < players.Length; index++)
            {
                if (players[index] != null && players[index].IsOwner)
                {
                    _localPlayer = players[index];
                    break;
                }
            }
        }

        private void RefreshQuestState()
        {
            if (Time.unscaledTime < _nextProfileRefreshAt)
            {
                return;
            }
            _nextProfileRefreshAt = Time.unscaledTime + 0.18f;
            var profile = WofSurvivalProfileStore.Load();
            if (profile == null)
            {
                _hasWoken = _hasPlayerEnteredHouse;
                _hasFought = false;
                _hasPeacefulDragon = false;
                return;
            }

            _hasPeacefulDragon = IsTrue(WofSpellQuestRules.GetFlag(profile, WofDarrelProgressionRules.DragonPeacefulFlag)) ||
                                  Contains(profile.questUnlockedSpells, WofSpellQuestRules.DarrelRewardSpell);
            _hasFought = IsTrue(WofSpellQuestRules.GetFlag(profile, WofDarrelProgressionRules.DragonFoughtFlag)) &&
                         !_hasPeacefulDragon;
            _hasWoken = IsTrue(WofSpellQuestRules.GetFlag(profile, WofDarrelProgressionRules.DragonWokenFlag)) ||
                        _hasPeacefulDragon || _hasFought || _hasPlayerEnteredHouse;

            if (_localPlayer != null && WofDarrelGroveLayout.IsInsideDragonHouse(_localPlayer.transform.position))
            {
                _hasPlayerEnteredHouse = true;
                _hasWoken = true;
            }
        }

        private void UpdateDragonVisuals()
        {
            if (dragonRenderer == null)
            {
                return;
            }

            var nextMode = WofDarrelGroveLayout.ResolveNextDragonMode(_dragonMode, _hasFought, _hasWoken);
            if (nextMode != _dragonMode)
            {
                _dragonMode = nextMode;
                _dragonModeStartedAt = Time.unscaledTime;
            }

            var frame = WofDarrelGroveLayout.ResolveDragonFrame(_dragonMode, _dragonModeStartedAt, Time.unscaledTime);
            if (frame.Mode != _dragonMode)
            {
                _dragonMode = frame.Mode;
                _dragonModeStartedAt = frame.ModeStartedAt;
            }
            var frames = ResolveFrames(_dragonMode);
            if (frames.Length > 0)
            {
                dragonRenderer.sprite = frames[Mathf.Clamp(frame.FrameIndex, 0, frames.Length - 1)];
                _dragonSpriteSize = dragonRenderer.sprite.bounds.size;
            }

            var visuals = WofDarrelGroveLayout.ResolveDragonVisuals(
                _dragonMode,
                _dragonModeStartedAt,
                Time.unscaledTime);
            dragonRenderer.transform.localPosition = _dragonBaseLocalPosition + Vector3.up * visuals.LocalY;
            dragonRenderer.transform.localScale = new Vector3(
                visuals.Width / Mathf.Max(0.001f, _dragonSpriteSize.x),
                visuals.Height / Mathf.Max(0.001f, _dragonSpriteSize.y),
                1f);
            dragonRenderer.color = _hasPeacefulDragon ? PeaceColor : _hasWoken ? WakeColor : SleepColor;

            var camera = Camera.main;
            if (camera != null)
            {
                var direction = dragonRenderer.transform.position - camera.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    dragonRenderer.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }

            if (dragonLight != null)
            {
                dragonLight.color = _hasFought ? ParseHtmlColor("#7dd3fc") : ParseHtmlColor("#bfdbfe");
                dragonLight.intensity = _hasFought ? 4.4f : _hasWoken ? 2.4f : 1.2f;
                dragonLight.range = _hasFought ? 58f : 44f;
            }
        }

        private Sprite[] ResolveFrames(WofDarrelDragonMode mode)
        {
            return mode switch
            {
                WofDarrelDragonMode.Sleep => sleepFrames,
                WofDarrelDragonMode.Wake => wakeFrames,
                WofDarrelDragonMode.Attack => attackFrames,
                _ => idleFrames
            };
        }

        private void UpdateFallingPetals()
        {
            var count = Mathf.Min(fallingPetalTransforms.Length, fallingPetalSeeds.Length);
            var topY = WofDarrelGroveLayout.HutBaseY + 92f;
            const float spanY = 104f;
            for (var index = 0; index < count; index++)
            {
                var petal = fallingPetalTransforms[index];
                if (petal == null)
                {
                    continue;
                }
                var seed = fallingPetalSeeds[index];
                var fall = Mathf.Repeat(seed.phase + Time.unscaledTime * seed.speed, 1f);
                var flutter = Time.unscaledTime * (0.8f + seed.speed * 12f) + seed.phase * Mathf.PI * 2f;
                petal.localPosition = new Vector3(
                    seed.x + Mathf.Sin(flutter + seed.drift) * seed.sway,
                    topY - fall * spanY,
                    seed.z + Mathf.Cos(flutter * 0.72f + seed.drift) * seed.sway * 0.72f);
                petal.localRotation = Quaternion.Euler(
                    Mathf.Sin(flutter * 0.83f) * 0.55f * Mathf.Rad2Deg,
                    flutter * seed.spin * Mathf.Rad2Deg,
                    Mathf.Cos(flutter) * 0.7f * Mathf.Rad2Deg);
                var renderer = petal.GetComponent<SpriteRenderer>();
                var spriteSize = renderer != null && renderer.sprite != null
                    ? renderer.sprite.bounds.size
                    : Vector3.one;
                petal.localScale = new Vector3(
                    seed.scale / Mathf.Max(0.001f, spriteSize.x),
                    seed.scale * 0.56f / Mathf.Max(0.001f, spriteSize.y),
                    1f);
            }
        }

        private void UpdateWaterfallVisuals()
        {
            var elapsed = Time.unscaledTime;
            var visuals = WofDarrelGroveLayout.ResolveWaterfallVisuals(elapsed);
            SetTextureOffset(fallWaterMaterial, visuals.FallTextureOffset);
            SetMaterialAlpha(fallWaterMaterial, visuals.FallOpacity);
            SetMaterialAlpha(foamMaterial, visuals.FoamOpacity);

            for (var index = 0; index < poolWaterMaterials.Length; index++)
            {
                SetTextureOffset(poolWaterMaterials[index], visuals.PoolTextureOffset);
            }
            if (poolWaterMaterials.Length > 0)
            {
                SetMaterialAlpha(poolWaterMaterials[0], visuals.PoolOpacity);
            }
            for (var index = 0; index < runnelWaterMaterials.Length; index++)
            {
                SetTextureOffset(runnelWaterMaterials[index], visuals.RunnelTextureOffset);
            }
            for (var index = 0; index < waterfallRunnelTransforms.Length; index++)
            {
                var runnel = waterfallRunnelTransforms[index];
                if (runnel != null)
                {
                    var position = runnel.localPosition;
                    position.x = WofDarrelGroveLayout.ResolveWaterfallRunnelLocalX(index, elapsed);
                    runnel.localPosition = position;
                }
            }

            var sprayCount = Mathf.Min(waterfallSprayTransforms.Length, waterfallSpraySeeds.Length);
            for (var index = 0; index < sprayCount; index++)
            {
                var spray = waterfallSprayTransforms[index];
                if (spray == null)
                {
                    continue;
                }
                var seed = waterfallSpraySeeds[index];
                var sprayVisuals = WofDarrelGroveLayout.ResolveWaterfallSprayVisuals(
                    index,
                    seed.baseY,
                    seed.baseScale,
                    elapsed);
                var position = spray.localPosition;
                position.y = sprayVisuals.LocalY;
                spray.localPosition = position;
                spray.localScale = sprayVisuals.LocalScale;
            }
        }

        private static void SetTextureOffset(Material material, Vector2 offset)
        {
            if (material == null)
            {
                return;
            }
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTextureOffset("_BaseMap", offset);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTextureOffset("_MainTex", offset);
            }
        }

        private static void SetMaterialAlpha(Material material, float alpha)
        {
            if (material == null)
            {
                return;
            }
            if (material.HasProperty("_BaseColor"))
            {
                var color = material.GetColor("_BaseColor");
                color.a = alpha;
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                var color = material.GetColor("_Color");
                color.a = alpha;
                material.SetColor("_Color", color);
            }
        }

        private void UpdateInteractionPrompt()
        {
            var visible = CanInteract(_localPlayer);
            EnsurePrompts();
            if (_screenPrompt != null)
            {
                _screenPrompt.gameObject.SetActive(visible);
            }
            if (_worldPromptCanvas != null)
            {
                _worldPromptCanvas.gameObject.SetActive(visible);
            }
            if (!visible)
            {
                return;
            }

            var controller = WofInputRouter.IsControllerGameplayActive(Gamepad.current);
            var binding = controller ? "X / LT / RT" : "F / LMB / RMB";
            if (_screenPromptText != null)
            {
                _screenPromptText.text = $"!   PRESS {binding}";
            }
            if (_worldPromptText != null)
            {
                _worldPromptText.text = $"!\nPRESS {binding}\nSPEAK";
            }
            var camera = Camera.main;
            if (_worldPromptCanvas != null && camera != null)
            {
                var direction = _worldPromptCanvas.transform.position - camera.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    _worldPromptCanvas.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }
        }

        private void EnsurePrompts()
        {
            if (_screenPrompt == null)
            {
                var overlay = GameObject.Find("WOF_UI");
                if (overlay != null)
                {
                    var root = new GameObject("DarrelDragonScreenPrompt", typeof(RectTransform), typeof(Image));
                    root.transform.SetParent(overlay.transform, false);
                    _screenPrompt = root.GetComponent<RectTransform>();
                    _screenPrompt.anchorMin = new Vector2(0.5f, 0.86f);
                    _screenPrompt.anchorMax = new Vector2(0.5f, 0.86f);
                    _screenPrompt.pivot = new Vector2(0.5f, 0.5f);
                    _screenPrompt.sizeDelta = new Vector2(286f, 40f);
                    var image = root.GetComponent<Image>();
                    image.color = new Color32(8, 13, 30, 224);
                    var outline = root.AddComponent<Outline>();
                    outline.effectColor = new Color32(207, 250, 254, 230);
                    outline.effectDistance = new Vector2(2f, -2f);
                    var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    label.transform.SetParent(root.transform, false);
                    var rect = label.GetComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = new Vector2(8f, 4f);
                    rect.offsetMax = new Vector2(-8f, -4f);
                    _screenPromptText = label.GetComponent<Text>();
                    _screenPromptText.font = promptFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    _screenPromptText.fontSize = 18;
                    _screenPromptText.fontStyle = FontStyle.Bold;
                    _screenPromptText.alignment = TextAnchor.MiddleCenter;
                    _screenPromptText.color = new Color32(240, 253, 255, 255);
                    root.SetActive(false);
                }
            }

            if (_worldPromptCanvas == null && dragonRenderer != null)
            {
                var root = new GameObject("DarrelDragonWorldPrompt", typeof(RectTransform), typeof(Canvas));
                root.transform.SetParent(dragonRenderer.transform.parent, false);
                root.transform.localPosition = _dragonBaseLocalPosition + Vector3.up * 18.5f;
                root.transform.localScale = Vector3.one * 0.08f;
                _worldPromptCanvas = root.GetComponent<Canvas>();
                _worldPromptCanvas.renderMode = RenderMode.WorldSpace;
                _worldPromptCanvas.sortingOrder = 30;
                var rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(214f, 58f);
                var panel = root.AddComponent<Image>();
                panel.color = new Color32(8, 13, 30, 219);
                var outline = root.AddComponent<Outline>();
                outline.effectColor = new Color32(207, 250, 254, 230);
                outline.effectDistance = new Vector2(2f, -2f);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(root.transform, false);
                var labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(6f, 3f);
                labelRect.offsetMax = new Vector2(-6f, -3f);
                _worldPromptText = label.GetComponent<Text>();
                _worldPromptText.font = promptFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _worldPromptText.fontSize = 10;
                _worldPromptText.fontStyle = FontStyle.Bold;
                _worldPromptText.alignment = TextAnchor.MiddleCenter;
                _worldPromptText.color = new Color32(240, 253, 255, 255);
                root.SetActive(false);
            }
        }

        private void PollReturnGate()
        {
            if (_localPlayer == null || _localPlayer.IsDead || Time.unscaledTime < _nextReturnAt ||
                !WofDarrelGroveLayout.IsInsideReturnGate(_localPlayer.transform.position))
            {
                return;
            }
            _nextReturnAt = Time.unscaledTime + 1.2f;
            if (!_localPlayer.RequestDarrelReturnTeleport())
            {
                return;
            }

            var profile = WofSurvivalProfileStore.Load();
            var result = WofDarrelProgressionRules.CompleteGroveReturn(
                profile,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (result.ProfileChanged)
            {
                WofSurvivalProfileStore.Save(profile);
            }
            _questDialog ??= FindFirstObjectByType<WofQuestDialogRuntime>();
            _questDialog?.ShowSystemMessages(result.Messages);
            Debug.Log($"[WOF-AUTOMATION] DARREL_GROVE_RETURN_QUEST changed={result.ProfileChanged} messages={result.Messages.Length} crystals={WofInventoryRules.GetQuantity(profile, "healing-crystals")}");
        }

        private IEnumerator RunReturnGateProbe()
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while ((_localPlayer == null || !_localPlayer.IsSpawned || !_localPlayer.IsOwner) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (_localPlayer == null || !_localPlayer.IsSpawned || !_localPlayer.IsOwner)
            {
                FailReturnGateProbe("local-player-timeout");
                yield break;
            }

            var returnPosition = _localPlayer.transform.position;
            var profile = WofSurvivalProfileStore.Load();
            var drink = WofDarrelProgressionRules.DrinkGardenDraught(profile);
            if (!drink.ProfileChanged || !drink.ShouldTeleport ||
                profile == null || WofInventoryRules.GetQuantity(profile, "garden-draught") != 0)
            {
                FailReturnGateProbe("drink-rejected");
                yield break;
            }
            if (!_localPlayer.RequestDarrelGroveTeleport())
            {
                FailReturnGateProbe("grove-teleport-rejected");
                yield break;
            }
            if (!WofSurvivalProfileStore.Save(profile))
            {
                FailReturnGateProbe("drink-save-failed");
                yield break;
            }
            Debug.Log("[WOF-AUTOMATION] DARREL_GROVE_RETURN_PROBE_DRINK changed=true teleport=true saved=true");

            deadline = Time.realtimeSinceStartup + 10f;
            while ((!_localPlayer.IsDarrelReturnArmed ||
                    Vector3.Distance(_localPlayer.transform.position, WofDarrelGroveLayout.SpawnPosition) > 2f) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (!_localPlayer.IsDarrelReturnArmed ||
                Vector3.Distance(_localPlayer.transform.position, WofDarrelGroveLayout.SpawnPosition) > 2f)
            {
                FailReturnGateProbe("grove-arrival-timeout");
                yield break;
            }
            if (!_localPlayer.PrepareForAutomationDarrelReturnGateProbe())
            {
                FailReturnGateProbe("gate-positioning-rejected");
                yield break;
            }
            Debug.Log("[WOF-AUTOMATION] DARREL_GROVE_RETURN_PROBE_GATE entered=true");

            deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                profile = WofSurvivalProfileStore.Load();
                var completed = profile != null &&
                                Contains(profile.questUnlockedSpells, WofSpellQuestRules.DarrelRewardSpell) &&
                                string.Equals(
                                    WofSpellQuestRules.GetFlag(profile, WofDarrelProgressionRules.GroveQuestFlag),
                                    "completed",
                                    StringComparison.Ordinal) &&
                                WofInventoryRules.GetQuantity(profile, "healing-crystals") == 1;
                var returned = !_localPlayer.IsDarrelReturnArmed &&
                               Vector3.Distance(_localPlayer.transform.position, returnPosition) <= 2f;
                if (!completed || !returned)
                {
                    continue;
                }

                Debug.Log("[WOF-AUTOMATION] DARREL_GROVE_RETURN_PROBE_COMPLETE drink=true grove=true gate=true returned=true completed=true crystals=1");
                yield break;
            }

            FailReturnGateProbe("completion-timeout");
        }

        private static void FailReturnGateProbe(string reason)
        {
            Debug.LogError($"[WOF-AUTOMATION] DARREL_GROVE_RETURN_PROBE_FAILED reason={reason}");
        }

        private static bool IsTrue(string value)
        {
            return string.Equals(value, "true", StringComparison.Ordinal);
        }

        private static bool Contains(string[] values, string expected)
        {
            if (values == null)
            {
                return false;
            }
            for (var index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index], expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static Color ParseHtmlColor(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.white;
        }
    }
}
