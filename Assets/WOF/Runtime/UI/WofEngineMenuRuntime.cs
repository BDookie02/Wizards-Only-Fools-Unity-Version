using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace WOF
{
    public readonly struct WofEngineMenuActionResult
    {
        public WofEngineMenuActionResult(bool ok, string message)
        {
            Ok = ok;
            Message = message ?? string.Empty;
        }

        public bool Ok { get; }
        public string Message { get; }
    }

    [DisallowMultipleComponent]
    public sealed class WofEngineMenuRuntime : MonoBehaviour
    {
        private static readonly Color32 Cyan50 = new(236, 254, 255, 255);
        private static readonly Color32 CyanMuted = new(207, 250, 254, 128);
        private static readonly Color32 CyanBorder = new(165, 243, 252, 140);
        private static readonly Color32 CyanDimBorder = new(165, 243, 252, 51);
        private static readonly Color32 Yellow = new(254, 240, 138, 255);
        private static readonly Color32 YellowFill = new(254, 240, 138, 30);
        private static readonly Color32 Panel = new(7, 16, 23, 247);

        [SerializeField] private WofHud hud;
        [SerializeField] private Transform uiParent;
        [SerializeField] private Font font;

        private GameObject _overlay;
        private RectTransform _pixelRoot;
        private RectTransform _panel;
        private Transform _cardRoot;
        private Transform _categoryRoot;
        private Transform _placedContent;
        private Transform _slotRoot;
        private InputField _search;
        private InputField _slotLabel;
        private Text _shownCount;
        private Text _selectedName;
        private Text _selectedMeta;
        private Text _placementStatus;
        private Text _placedHeader;
        private Text _slotHeader;
        private Button _placeButton;
        private Button _snapButton;
        private Button _rotateMinusButton;
        private Button _rotatePlusButton;
        private Button _clearPlacedButton;
        private Button[] _gridButtons;
        private readonly List<Button> _categoryButtons = new();
        private readonly List<Text> _categoryLabels = new();
        private readonly List<GameObject> _cardObjects = new();
        private readonly List<GameObject> _placedRows = new();
        private readonly List<Button> _slotButtons = new();
        private readonly List<Text> _slotLabels = new();
        private readonly List<WofEnginePlaceableRecord> _allRecords = new();
        private readonly List<WofEnginePlaceableRecord> _localRecords = new();
        private readonly Dictionary<string, GameObject> _worldVisuals = new(StringComparer.Ordinal);
        private WofEnginePlaceableStorageDocument _storage;
        private WofEnginePlaceableDefinition _selected;
        private WofEnginePlaceableCategory _activeCategory = WofEnginePlaceableCategory.Huts;
        private WofPlayerController _localPlayer;
        private GameObject _worldRoot;
        private GameObject _preview;
        private string _selectedPlacedObjectId = string.Empty;
        private string _selectedSlotId = "slot-1";
        private string _lastNetworkSignature = string.Empty;
        private string _lastLocalSignature = string.Empty;
        private float _nextNetworkRefreshAt;
        private float _gridSize = 2f;
        private float _yawRadians;
        private bool _snapToGrid = true;
        private bool _initialStorageApplied;
        private bool _developerModeEnabled;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private Gamepad _controllerProbeGamepad;
        private string _controllerProbeRoot;

        public static WofEngineMenuRuntime Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._overlay != null && Instance._overlay.activeSelf;
        public int PlacedObjectCount => _allRecords.Count;
        public string PlacementStatus => _placementStatus == null ? string.Empty : _placementStatus.text;

        public void ConfigureGeneratedView(WofHud generatedHud, Transform generatedUiParent, Font generatedFont)
        {
            hud = generatedHud;
            uiParent = generatedUiParent;
            font = generatedFont;
        }

        private void Awake()
        {
            Instance = this;
            hud ??= FindFirstObjectByType<WofHud>();
            uiParent ??= transform;
            font ??= GetComponentInChildren<Text>(true)?.font;
            _storage = WofEnginePlaceableStorage.Load();
            BuildView();
        }

        private void Start()
        {
            _worldRoot = new GameObject("ReactEnginePlacedObjects");
            ParseControllerProbeArgument();
            if (!string.IsNullOrWhiteSpace(_controllerProbeRoot))
                StartCoroutine(RunControllerProbe());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (IsOpen) RestoreGameplay();
            RemoveControllerProbeGamepad();
            if (_worldRoot != null) Destroy(_worldRoot);
            WofEnginePlaceableVisualFactory.ClearMaterialCache();
        }

        private void Update()
        {
            if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height) RefreshPixelScale();
            ResolveLocalPlayer();
            if (Time.unscaledTime >= _nextNetworkRefreshAt)
            {
                _nextNetworkRefreshAt = Time.unscaledTime + 0.12f;
                RefreshNetworkObjects();
            }

            var keyboard = Keyboard.current;
            if (IsOpen)
            {
                if ((keyboard?.escapeKey.wasPressedThisFrame ?? false) ||
                    ControllerBackPressed(Gamepad.current)) Close();
                return;
            }

            if (keyboard != null && keyboard.lKey.wasPressedThisFrame && _developerModeEnabled && CanOpen()) Open(false);
        }

        internal static bool ControllerBackPressed(Gamepad gamepad)
        {
            return WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.MenuBack);
        }

        public void Open(bool enableDeveloperMode = true)
        {
            if (enableDeveloperMode) _developerModeEnabled = true;
            if (IsOpen || !CanOpen()) return;
            WofInputRouter.SetGameplaySuppressed(true);
            hud.SetGameplaySurfaceBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _overlay.SetActive(true);
            RefreshCategoryButtons();
            RebuildCatalogCards();
            RefreshPlacementPanel();
            RefreshPlacedObjectList();
            RefreshSlotPanel();
            RefreshPixelScale();
            EventSystem.current?.SetSelectedGameObject(_categoryButtons.FirstOrDefault()?.gameObject);
            Debug.Log("[WOF-AUTOMATION] ENGINE_MENU open=true");
        }

        public void Close()
        {
            if (!IsOpen) return;
            _overlay.SetActive(false);
            ClearPreview();
            RestoreGameplay();
            Debug.Log("[WOF-AUTOMATION] ENGINE_MENU open=false");
        }

        public WofEngineMenuActionResult RequestDirectPlacement(string placeableId)
        {
            _developerModeEnabled = true;
            var definition = WofEnginePlaceableCatalog.Find((placeableId ?? string.Empty).ToLowerInvariant());
            if (definition == null) return new WofEngineMenuActionResult(false, "PLACE BLOCKED: unknown placeable");
            _selected = definition;
            _gridSize = WofEnginePlaceableCatalog.GetDefaultGridSize(definition);
            var result = PlaceSelected();
            return result.Ok
                ? new WofEngineMenuActionResult(true, $"PLACED: {result.Message}")
                : new WofEngineMenuActionResult(false, $"PLACE BLOCKED: {result.Message}");
        }

        private bool CanOpen()
        {
            return hud != null && hud.IsGameplayVisible && !WofInputRouter.GameplaySuppressed &&
                   !WofSpellMenuRuntime.IsOpen && !WofNavigationMapRuntime.IsExpanded &&
                   !WofPauseAndScoreboardRuntime.IsAnyMenuOpen;
        }

        private void RestoreGameplay()
        {
            hud?.SetGameplaySurfaceBlocked(false);
            WofInputRouter.SetGameplaySuppressed(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void ResolveLocalPlayer()
        {
            if (_localPlayer != null && _localPlayer.IsOwner && !_localPlayer.IsDead)
            {
                ApplyInitialStorageIfReady();
                return;
            }
            _localPlayer = null;
            var players = FindObjectsByType<WofPlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var index = 0; index < players.Length; index++)
            {
                if (!players[index].IsOwner || players[index].IsDead) continue;
                _localPlayer = players[index];
                break;
            }
            ApplyInitialStorageIfReady();
        }

        private void ApplyInitialStorageIfReady()
        {
            if (_initialStorageApplied || _localPlayer == null || !_localPlayer.IsSpawned) return;
            _initialStorageApplied = true;
            if (_localPlayer.EnginePlaceableCount == 0 && _storage.current.Count > 0)
                _localPlayer.RequestReplaceEnginePlaceables(_storage.current);
        }

        private void RefreshNetworkObjects()
        {
            _allRecords.Clear();
            _localRecords.Clear();
            var players = FindObjectsByType<WofPlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var index = 0; index < players.Length; index++)
            {
                players[index].CopyEnginePlaceables(_allRecords);
                if (players[index].IsOwner) players[index].CopyPersistentEnginePlaceables(_localRecords);
            }

            var signature = BuildSignature(_allRecords);
            if (signature != _lastNetworkSignature)
            {
                _lastNetworkSignature = signature;
                SyncWorldVisuals();
                if (IsOpen) RefreshPlacedObjectList();
            }

            var localSignature = BuildSignature(_localRecords);
            if (localSignature != _lastLocalSignature)
            {
                _lastLocalSignature = localSignature;
                _storage.current = WofEnginePlaceableStorage.SanitizeObjects(_localRecords);
                WofEnginePlaceableStorage.Save(_storage);
            }
        }

        private static string BuildSignature(IReadOnlyList<WofEnginePlaceableRecord> records)
        {
            if (records.Count == 0) return string.Empty;
            var parts = new string[records.Count];
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                parts[index] = $"{record.instanceId}:{record.placeableId}:{record.x:F3}:{record.y:F3}:{record.z:F3}:{record.yaw:F4}:" +
                               $"{record.trainingDummyHealth:F2}:{record.trainingDummyRespawnAt:F3}:" +
                               $"{record.trainingDummyHitSequence}:{record.trainingDummyLastSpell}";
            }
            return string.Join("|", parts);
        }

        private void SyncWorldVisuals()
        {
            if (_worldRoot == null) return;
            var wanted = new HashSet<string>(_allRecords.Select(record => record.instanceId), StringComparer.Ordinal);
            foreach (var pair in _worldVisuals.ToArray())
            {
                if (wanted.Contains(pair.Key)) continue;
                if (pair.Value != null) Destroy(pair.Value);
                _worldVisuals.Remove(pair.Key);
            }
            for (var index = 0; index < _allRecords.Count; index++)
            {
                var record = _allRecords[index];
                var definition = WofEnginePlaceableCatalog.Find(record.placeableId);
                if (definition == null) continue;
                if (!_worldVisuals.TryGetValue(record.instanceId, out var visual) || visual == null)
                {
                    visual = WofEnginePlaceableVisualFactory.Create(definition, _worldRoot.transform,
                        record.instanceId, 1f, true);
                    _worldVisuals[record.instanceId] = visual;
                }
                visual.transform.SetPositionAndRotation(record.Position,
                    Quaternion.Euler(0f, record.yaw * Mathf.Rad2Deg, 0f));
                if (record.placeableId == "training-spell-dummy")
                    visual.GetComponent<WofTrainingDummyRuntime>()?.ApplyState(record);
            }
            Debug.Log($"[WOF-AUTOMATION] ENGINE_PLACEABLE_SYNC count={_allRecords.Count}");
        }

        private WofEngineMenuActionResult PreviewSelected(string replaceInstanceId = null)
        {
            if (_selected == null) return new WofEngineMenuActionResult(false, "Select an object first");
            var plan = ResolvePlacementPlan(_selected);
            var collision = plan.Ok
                ? WofEnginePlacementMath.FindCollision(_selected, plan.Position.x, plan.Position.z,
                    plan.YawRadians, _allRecords, replaceInstanceId)
                : null;
            var reason = !plan.Ok ? plan.Reason : collision.HasValue ? $"overlaps {collision.Value.label}" : string.Empty;
            ClearPreview();
            var previewPosition = plan.Ok ? plan.Position : ResolveFallbackPreviewPosition(_selected);
            _preview = WofEnginePlaceableVisualFactory.Create(_selected, _worldRoot.transform,
                "EnginePlacementPreview", plan.Ok && !collision.HasValue ? 0.42f : 0.22f, false);
            _preview.transform.SetPositionAndRotation(previewPosition,
                Quaternion.Euler(0f, plan.YawRadians * Mathf.Rad2Deg, 0f));
            AddFootprintRing(_preview.transform, _selected.FootprintRadius,
                reason.Length == 0 ? new Color32(110, 231, 183, 220) : new Color32(248, 113, 113, 220));
            SetStatus(reason.Length == 0 ? "Ready" : $"Blocked: {reason}", reason.Length > 0);
            return new WofEngineMenuActionResult(reason.Length == 0, reason);
        }

        private WofEngineMenuActionResult PlaceSelected(string replaceInstanceId = null)
        {
            if (_selected == null) return new WofEngineMenuActionResult(false, "Select an object first");
            ResolveLocalPlayer();
            if (_localPlayer == null) return new WofEngineMenuActionResult(false, "player position unavailable");
            var plan = ResolvePlacementPlan(_selected);
            if (!plan.Ok)
            {
                SetStatus($"Blocked: {plan.Reason}", true);
                return new WofEngineMenuActionResult(false, plan.Reason);
            }
            if (_selected.Id == "training-spell-dummy")
            {
                var instanceId = $"engine-training-spell-dummy-{_localPlayer.OwnerClientId}";
                var dummy = new WofEnginePlaceableRecord
                {
                    instanceId = instanceId,
                    placeableId = _selected.Id,
                    label = _selected.Name,
                    x = plan.Position.x,
                    y = plan.Position.y,
                    z = plan.Position.z,
                    yaw = plan.YawRadians,
                    trainingDummyHealth = WofTrainingDummyCombatRules.MaxHealth,
                    trainingDummyRespawnAt = 0d,
                    trainingDummyHitSequence = 0,
                    trainingDummyLastSpell = -1
                };
                if (!_localPlayer.RequestEnginePlaceableUpsert(dummy, instanceId))
                    return new WofEngineMenuActionResult(false, "player position unavailable");
                SetStatus($"Placed: {_selected.Name}", false);
                Debug.Log($"[WOF-AUTOMATION] ENGINE_TRAINING_DUMMY_SPAWN x={dummy.x:F2} y={dummy.y:F2} z={dummy.z:F2} yaw={dummy.yaw:F4} persistent=false");
                ClearPreview();
                return new WofEngineMenuActionResult(true, _selected.Name);
            }
            var collision = WofEnginePlacementMath.FindCollision(_selected, plan.Position.x, plan.Position.z,
                plan.YawRadians, _allRecords, replaceInstanceId);
            if (collision.HasValue)
            {
                var reason = $"overlaps {collision.Value.label}";
                SetStatus($"Blocked: {reason}", true);
                PreviewSelected(replaceInstanceId);
                return new WofEngineMenuActionResult(false, reason);
            }
            var replacing = !string.IsNullOrEmpty(replaceInstanceId);
            var randomSuffix = Guid.NewGuid().ToString("N").Substring(0, 5);
            var record = new WofEnginePlaceableRecord
            {
                instanceId = replacing ? replaceInstanceId : $"engine-{_selected.Id}-{randomSuffix}",
                placeableId = _selected.Id,
                label = _selected.Name,
                x = plan.Position.x,
                y = plan.Position.y,
                z = plan.Position.z,
                yaw = plan.YawRadians
            };
            if (!_localPlayer.RequestEnginePlaceableUpsert(record, replaceInstanceId))
                return new WofEngineMenuActionResult(false, "player position unavailable");
            var label = replacing ? $"moved {_selected.Name}" : _selected.Name;
            SetStatus($"Placed: {label}", false);
            Debug.Log($"[WOF-AUTOMATION] ENGINE_PLACEABLE_PLACED id={record.placeableId} instance={record.instanceId} x={record.x:F2} y={record.y:F2} z={record.z:F2}");
            ClearPreview();
            return new WofEngineMenuActionResult(true, label);
        }

        private WofEnginePlacementPlan ResolvePlacementPlan(WofEnginePlaceableDefinition definition)
        {
            ResolveLocalPlayer();
            if (_localPlayer == null)
                return new WofEnginePlacementPlan(false, "player position unavailable", Vector3.zero, 0f, 0f, false);
            var playerPosition = _localPlayer.transform.position;
            var playerYawRadians = _localPlayer.transform.eulerAngles.y * Mathf.Deg2Rad;
            if (definition.Id == "training-spell-dummy")
                return WofEnginePlacementMath.PlanTrainingDummy(playerPosition, playerYawRadians);
            var camera = Camera.main;
            var forward = camera != null ? camera.transform.forward : _localPlayer.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = _localPlayer.transform.forward;
            forward.Normalize();
            var distance = Mathf.Max(WofEnginePlacementMath.DefaultPlayerPlacementDistance,
                definition.FootprintRadius + 4f);
            var target = playerPosition + forward * distance;
            return WofEnginePlacementMath.Plan(
                definition,
                ResolveGroundHeight,
                playerPosition,
                playerYawRadians,
                _snapToGrid,
                _gridSize,
                target.x,
                target.z,
                _yawRadians);
        }

        private float ResolveGroundHeight(float x, float z)
        {
            if (WofBootstrap.Instance != null && !WofBootstrap.Instance.IsSurvivalSession)
                return WofBaseVillageLayout.GetTerrainHeight(x, z);
            var chunkX = WofSurvivalTerrainMath.GetChunkCoordinate(x);
            var chunkZ = WofSurvivalTerrainMath.GetChunkCoordinate(z);
            return (float)WofSurvivalTerrainMath.GetTerrainHeight(
                chunkX, chunkZ,
                x - chunkX * WofSurvivalTerrainMath.BlockSize,
                z - chunkZ * WofSurvivalTerrainMath.BlockSize);
        }

        private Vector3 ResolveFallbackPreviewPosition(WofEnginePlaceableDefinition definition)
        {
            if (_localPlayer == null) return Vector3.zero;
            var forward = Camera.main != null ? Camera.main.transform.forward : _localPlayer.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            var distance = Mathf.Max(7f, definition.FootprintRadius + 4f);
            var result = _localPlayer.transform.position + forward * distance;
            result.y = ResolveGroundHeight(result.x, result.z);
            return result;
        }

        private void ClearPreview()
        {
            if (_preview != null) Destroy(_preview);
            _preview = null;
        }

        private static void AddFootprintRing(Transform parent, float radius, Color color)
        {
            var ring = new GameObject("PlacementFootprint", typeof(LineRenderer));
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            var line = ring.GetComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = 49;
            line.widthMultiplier = 0.09f;
            line.material = new Material(Shader.Find("Sprites/Default")) { color = color };
            line.startColor = line.endColor = color;
            for (var index = 0; index < line.positionCount; index++)
            {
                var angle = index * Mathf.PI * 2f / (line.positionCount - 1);
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        private void SelectPlaceable(WofEnginePlaceableDefinition definition)
        {
            _selected = definition;
            _gridSize = WofEnginePlaceableCatalog.GetDefaultGridSize(definition);
            _yawRadians = GetPlayerYawRadians();
            RefreshPlacementPanel();
            RebuildCatalogCards();
            var restoredCard = _cardObjects.FirstOrDefault(card =>
                card != null && card.name == $"Placeable-{definition.Id}");
            EventSystem.current?.SetSelectedGameObject(restoredCard);
            PreviewSelected();
        }

        private void ParseControllerProbeArgument()
        {
            const string prefix = "--wof-engine-menu-controller-probe=";
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var path = argument.Substring(prefix.Length).Trim('"');
                if (!path.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError("[WOF-AUTOMATION] ENGINE_MENU_CONTROLLER_PROBE_FAIL screenshot-root-not-on-d");
                    return;
                }
                Directory.CreateDirectory(path);
                _controllerProbeRoot = path;
                return;
            }
        }

        private IEnumerator RunControllerProbe()
        {
            var deadline = Time.realtimeSinceStartup + 25f;
            while ((hud == null || !hud.IsGameplayVisible || _localPlayer == null) &&
                   Time.realtimeSinceStartup < deadline)
            {
                ResolveLocalPlayer();
                yield return null;
            }
            if (hud == null || !hud.IsGameplayVisible || _localPlayer == null)
            {
                FailControllerProbe("gameplay-or-player-not-ready");
                yield break;
            }

            _controllerProbeGamepad = InputSystem.AddDevice<Gamepad>("WOF Engine Menu QA Controller");
            _controllerProbeGamepad.MakeCurrent();
            InputSystem.QueueStateEvent(_controllerProbeGamepad, new GamepadState());
            yield return null;
            yield return null;

            var placedBefore = PlacedObjectCount;
            Open();
            yield return new WaitForSecondsRealtime(0.35f);
            if (!IsOpen || CurrentControllerSelectionName() != WofEnginePlaceableCategory.Huts.ToString())
            {
                FailControllerProbe($"open-selection-failed selected={CurrentControllerSelectionName()}");
                yield break;
            }

            for (var index = 0; index < 4; index++)
                yield return TapControllerButton(GamepadButton.DpadDown);
            if (CurrentControllerSelectionName() != WofEnginePlaceableCategory.Training.ToString())
            {
                FailControllerProbe($"category-navigation-failed selected={CurrentControllerSelectionName()}");
                yield break;
            }

            yield return TapControllerButton(GamepadButton.A);
            if (_activeCategory != WofEnginePlaceableCategory.Training)
            {
                FailControllerProbe($"category-select-failed active={_activeCategory}");
                yield break;
            }

            yield return TapControllerButton(GamepadButton.DpadRight);
            if (CurrentControllerSelectionName() != "Placeable-training-spell-dummy")
            {
                FailControllerProbe($"catalog-navigation-failed selected={CurrentControllerSelectionName()}");
                yield break;
            }

            yield return TapControllerButton(GamepadButton.A);
            if (_selected?.Id != "training-spell-dummy" ||
                CurrentControllerSelectionName() != "Placeable-training-spell-dummy")
            {
                FailControllerProbe($"catalog-select-failed definition={_selected?.Id ?? "none"} selected={CurrentControllerSelectionName()}");
                yield break;
            }
            yield return CaptureControllerProbeScreenshot("engine-menu-controller-selected.png");

            yield return TapControllerButton(GamepadButton.DpadRight);
            yield return TapControllerButton(GamepadButton.DpadDown);
            yield return TapControllerButton(GamepadButton.DpadDown);
            if (CurrentControllerSelectionName() != "PlaceSelected")
            {
                FailControllerProbe($"place-button-navigation-failed selected={CurrentControllerSelectionName()}");
                yield break;
            }

            yield return TapControllerButton(GamepadButton.A);
            var placementDeadline = Time.realtimeSinceStartup + 4f;
            while (PlacedObjectCount <= placedBefore && Time.realtimeSinceStartup < placementDeadline)
                yield return null;
            if (PlacedObjectCount <= placedBefore ||
                !PlacementStatus.StartsWith("Placed: Spell Dummy", StringComparison.Ordinal))
            {
                FailControllerProbe($"place-submit-failed before={placedBefore} after={PlacedObjectCount} status={PlacementStatus}");
                yield break;
            }
            yield return CaptureControllerProbeScreenshot("engine-menu-controller-placed.png");

            yield return TapControllerButton(GamepadButton.B);
            if (IsOpen)
            {
                FailControllerProbe("menu-back-did-not-close");
                yield break;
            }

            Debug.Log($"[WOF-AUTOMATION] ENGINE_MENU_CONTROLLER_PROBE_COMPLETE navigation=true select=true place=true back=true placed={PlacedObjectCount}");
            RemoveControllerProbeGamepad();
        }

        private IEnumerator TapControllerButton(GamepadButton button)
        {
            InputSystem.QueueStateEvent(_controllerProbeGamepad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(_controllerProbeGamepad, new GamepadState());
            yield return null;
            yield return new WaitForSecondsRealtime(0.18f);
        }

        private IEnumerator CaptureControllerProbeScreenshot(string fileName)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_controllerProbeRoot, fileName));
            yield return new WaitForSecondsRealtime(0.75f);
        }

        private static string CurrentControllerSelectionName()
        {
            return EventSystem.current?.currentSelectedGameObject?.name ?? "none";
        }

        private void FailControllerProbe(string reason)
        {
            Debug.LogError($"[WOF-AUTOMATION] ENGINE_MENU_CONTROLLER_PROBE_FAIL {reason}");
            RemoveControllerProbeGamepad();
        }

        private void RemoveControllerProbeGamepad()
        {
            if (_controllerProbeGamepad != null && _controllerProbeGamepad.added)
                InputSystem.RemoveDevice(_controllerProbeGamepad);
            _controllerProbeGamepad = null;
        }

        private float GetPlayerYawRadians()
        {
            ResolveLocalPlayer();
            return _localPlayer == null ? 0f : _localPlayer.transform.eulerAngles.y * Mathf.Deg2Rad;
        }

        private void SelectCategory(WofEnginePlaceableCategory category)
        {
            _activeCategory = category;
            RefreshCategoryButtons();
            RebuildCatalogCards();
        }

        private void RotateSelected(float delta)
        {
            _yawRadians += delta;
            if (_selected != null) PreviewSelected();
            RefreshPlacementPanel();
        }

        private void SelectGridSize(float size)
        {
            _gridSize = size;
            if (_selected != null) PreviewSelected();
            RefreshPlacementPanel();
        }

        private void ToggleSnap()
        {
            _snapToGrid = !_snapToGrid;
            if (_selected != null) PreviewSelected();
            RefreshPlacementPanel();
        }

        private void ClearPlaced()
        {
            ResolveLocalPlayer();
            if (_localPlayer == null || !_localPlayer.RequestClearEnginePlaceables())
            {
                SetStatus("Blocked: player position unavailable", true);
                return;
            }
            _selectedPlacedObjectId = string.Empty;
            SetStatus("Placed: cleared objects", false);
            ClearPreview();
            Debug.Log("[WOF-AUTOMATION] ENGINE_PLACEABLE_EDIT action=clear");
        }

        private void PreviewPlacedObject()
        {
            var record = FindLocalRecord(_selectedPlacedObjectId);
            if (!record.HasValue) return;
            _selected = WofEnginePlaceableCatalog.Find(record.Value.placeableId);
            _yawRadians = record.Value.yaw;
            ClearPreview();
            _preview = WofEnginePlaceableVisualFactory.Create(_selected, _worldRoot.transform,
                "EnginePlacementPreview", 0.42f, false);
            _preview.transform.SetPositionAndRotation(record.Value.Position,
                Quaternion.Euler(0f, record.Value.yaw * Mathf.Rad2Deg, 0f));
            AddFootprintRing(_preview.transform, _selected.FootprintRadius, new Color32(110, 231, 183, 220));
            SetStatus("Ready", false);
            RefreshPlacementPanel();
            Debug.Log($"[WOF-AUTOMATION] ENGINE_PLACEABLE_EDIT action=preview instance={record.Value.instanceId}");
        }

        private void MovePlacedObject()
        {
            var record = FindLocalRecord(_selectedPlacedObjectId);
            if (!record.HasValue) return;
            _selected = WofEnginePlaceableCatalog.Find(record.Value.placeableId);
            _yawRadians = record.Value.yaw;
            var preview = PreviewSelected(record.Value.instanceId);
            if (preview.Ok) PlaceSelected(record.Value.instanceId);
            RefreshPlacementPanel();
            Debug.Log($"[WOF-AUTOMATION] ENGINE_PLACEABLE_EDIT action=move instance={record.Value.instanceId}");
        }

        private void DeletePlacedObject()
        {
            ResolveLocalPlayer();
            var record = FindLocalRecord(_selectedPlacedObjectId);
            if (!record.HasValue || _localPlayer == null) return;
            _localPlayer.RequestDeleteEnginePlaceable(record.Value.instanceId);
            SetStatus($"Placed: deleted {record.Value.label}", false);
            _selectedPlacedObjectId = string.Empty;
            ClearPreview();
            Debug.Log($"[WOF-AUTOMATION] ENGINE_PLACEABLE_EDIT action=delete instance={record.Value.instanceId}");
        }

        private WofEnginePlaceableRecord? FindLocalRecord(string instanceId)
        {
            for (var index = 0; index < _localRecords.Count; index++)
                if (_localRecords[index].instanceId == instanceId) return _localRecords[index];
            return null;
        }

        private void SaveSelectedSlot()
        {
            var label = _slotLabel == null ? string.Empty : _slotLabel.text;
            WofEnginePlaceableStorage.SaveSlot(_storage, _selectedSlotId, label, _localRecords,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            WofEnginePlaceableStorage.Save(_storage);
            var slot = WofEnginePlaceableStorage.FindSlot(_storage, _selectedSlotId);
            SetStatus($"Placed: saved {slot?.label ?? _selectedSlotId}", false);
            RefreshSlotPanel();
            Debug.Log($"[WOF-AUTOMATION] ENGINE_SLOT action=save slot={_selectedSlotId} count={_localRecords.Count}");
        }

        private void LoadSelectedSlot()
        {
            var slot = WofEnginePlaceableStorage.FindSlot(_storage, _selectedSlotId);
            if (slot == null)
            {
                SetStatus($"Blocked: {WofEnginePlaceableStorage.GetSlotLabel(_selectedSlotId)} is empty", true);
                return;
            }
            ResolveLocalPlayer();
            if (_localPlayer == null || !_localPlayer.RequestReplaceEnginePlaceables(slot.objects))
            {
                SetStatus("Blocked: player position unavailable", true);
                return;
            }
            SetStatus($"Placed: loaded {slot.label}", false);
            Debug.Log($"[WOF-AUTOMATION] ENGINE_SLOT action=load slot={_selectedSlotId} count={slot.objects.Count}");
        }

        private void DeleteSelectedSlot()
        {
            var slot = WofEnginePlaceableStorage.FindSlot(_storage, _selectedSlotId);
            if (slot == null || !WofEnginePlaceableStorage.DeleteSlot(_storage, _selectedSlotId))
            {
                SetStatus("Blocked: slot delete failed", true);
                return;
            }
            WofEnginePlaceableStorage.Save(_storage);
            SetStatus($"Placed: deleted {slot.label}", false);
            RefreshSlotPanel();
            Debug.Log($"[WOF-AUTOMATION] ENGINE_SLOT action=delete slot={_selectedSlotId}");
        }

        private void SelectSlot(int index)
        {
            _selectedSlotId = $"slot-{index + 1}";
            var slot = WofEnginePlaceableStorage.FindSlot(_storage, _selectedSlotId);
            _slotLabel.SetTextWithoutNotify(slot?.label ?? $"Slot {index + 1}");
            RefreshSlotPanel();
        }

        private void BuildView()
        {
            if (font == null) return;
            _overlay = CreatePanel("EngineMenuOverlay", uiParent, new Color32(0, 0, 0, 199));
            SetFull(_overlay.GetComponent<RectTransform>());
            _pixelRoot = new GameObject("EngineMenuPixelRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            _pixelRoot.SetParent(_overlay.transform, false);
            SetCentered(_pixelRoot, 0f, 0f);
            _panel = CreatePanel("EngineMenuPanel", _pixelRoot, Panel).GetComponent<RectTransform>();
            SetCentered(_panel, 1080f, 690f);
            AddOutline(_panel.gameObject, CyanBorder, 2f);

            var header = CreatePanel("Header", _panel, new Color32(3, 17, 24, 245));
            SetTopStretch(header.GetComponent<RectTransform>(), 0f, 64f, 0f, 0f);
            AddBottomBorder(header.transform, CyanDimBorder);
            var kicker = CreateText("Kicker", header.transform, "DEV MODE", 10, TextAnchor.MiddleLeft, CyanMuted);
            SetTopLeft(kicker.rectTransform, 16f, 7f, 700f, 18f);
            var title = CreateText("Title", header.transform, "GAME ENGINE MENU", 22, TextAnchor.MiddleLeft, Cyan50);
            SetTopLeft(title.rectTransform, 16f, 25f, 700f, 30f);
            var close = CreateButton("Close", header.transform, "CLOSE", new Color32(34, 211, 238, 26), CyanBorder);
            SetTopRight(close.GetComponent<RectTransform>(), 14f, 14f, 90f, 36f);
            close.onClick.AddListener(Close);

            BuildCategories();
            BuildCatalog();
            BuildPlacementPanel();

            var footer = CreatePanel("Footer", _panel, new Color32(0, 0, 0, 45));
            SetBottomStretch(footer.GetComponent<RectTransform>(), 0f, 30f, 0f, 0f);
            AddTopBorder(footer.transform, CyanDimBorder);
            var footerText = CreateText("Text", footer.transform,
                "L OR /ENGINE OPENS DEV MODE     D-PAD + A NAVIGATE     B CLOSES     SELECT AN ITEM TO PREVIEW, THEN PLACE SELECTED",
                8, TextAnchor.MiddleCenter, new Color32(207, 250, 254, 92));
            SetFullInset(footerText.rectTransform, 8f);

            _overlay.SetActive(false);
            RefreshPixelScale();
        }

        private void BuildCategories()
        {
            var panel = CreatePanel("Categories", _panel, new Color32(0, 0, 0, 25));
            SetTopLeft(panel.GetComponent<RectTransform>(), 12f, 76f, 150f, 570f);
            _categoryRoot = panel.transform;
            for (var index = 0; index < WofEnginePlaceableCatalog.OrderedCategories.Count; index++)
            {
                var category = WofEnginePlaceableCatalog.OrderedCategories[index];
                var captured = category;
                var button = CreateButton(category.ToString(), panel.transform, string.Empty,
                    new Color32(0, 0, 0, 64), CyanDimBorder);
                SetTopStretch(button.GetComponent<RectTransform>(), index * 48f, 40f, 0f, 0f);
                button.onClick.AddListener(() => SelectCategory(captured));
                var label = CreateText("Label", button.transform, string.Empty, 10, TextAnchor.MiddleLeft, CyanMuted);
                SetFullInset(label.rectTransform, 10f);
                _categoryButtons.Add(button);
                _categoryLabels.Add(label);
            }
        }

        private void BuildCatalog()
        {
            var panel = CreatePanel("Catalog", _panel, new Color32(0, 0, 0, 18));
            SetTopLeft(panel.GetComponent<RectTransform>(), 174f, 76f, 632f, 570f);
            _shownCount = CreateText("Count", panel.transform, "0 PLACEABLES", 8, TextAnchor.MiddleLeft, CyanMuted);
            SetTopLeft(_shownCount.rectTransform, 0f, 0f, 160f, 22f);
            var searchBorder = CreatePanel("SearchBorder", panel.transform, CyanDimBorder);
            SetTopRight(searchBorder.GetComponent<RectTransform>(), 0f, 0f, 360f, 34f);
            var searchFill = CreatePanel("Fill", searchBorder.transform, new Color32(0, 0, 0, 190));
            SetFullInset(searchFill.GetComponent<RectTransform>(), 1f);
            var placeholder = CreateText("Placeholder", searchFill.transform, "Search placeables", 10,
                TextAnchor.MiddleLeft, new Color32(207, 250, 254, 65));
            SetFullInset(placeholder.rectTransform, 9f);
            var inputText = CreateText("Text", searchFill.transform, string.Empty, 10, TextAnchor.MiddleLeft, Cyan50);
            SetFullInset(inputText.rectTransform, 9f);
            _search = searchBorder.AddComponent<InputField>();
            _search.targetGraphic = searchFill.GetComponent<Image>();
            _search.textComponent = inputText;
            _search.placeholder = placeholder;
            _search.characterLimit = 90;
            _search.onValueChanged.AddListener(_ =>
            {
                RefreshCategoryButtons();
                RebuildCatalogCards();
            });
            _cardRoot = new GameObject("Cards", typeof(RectTransform)).transform;
            _cardRoot.SetParent(panel.transform, false);
            SetTopLeft((RectTransform)_cardRoot, 0f, 46f, 632f, 520f);
        }

        private void BuildPlacementPanel()
        {
            var panel = CreatePanel("Placement", _panel, new Color32(254, 240, 138, 13));
            SetTopLeft(panel.GetComponent<RectTransform>(), 818f, 76f, 250f, 570f);
            AddOutline(panel, new Color32(254, 249, 195, 64));
            var heading = CreateText("Heading", panel.transform, "PLACEMENT", 10, TextAnchor.MiddleLeft, Yellow);
            SetTopLeft(heading.rectTransform, 10f, 6f, 230f, 20f);
            _selectedName = CreateText("Selected", panel.transform,
                "Select an object to preview it on the grid.", 9, TextAnchor.UpperLeft, CyanMuted);
            SetTopLeft(_selectedName.rectTransform, 10f, 29f, 230f, 38f);
            _selectedName.horizontalOverflow = HorizontalWrapMode.Wrap;
            _selectedMeta = CreateText("Meta", panel.transform, string.Empty, 7, TextAnchor.UpperLeft,
                new Color32(207, 250, 254, 92));
            SetTopLeft(_selectedMeta.rectTransform, 10f, 69f, 230f, 33f);
            _selectedMeta.horizontalOverflow = HorizontalWrapMode.Wrap;
            _placementStatus = CreateText("Status", panel.transform, "Ready", 8, TextAnchor.MiddleLeft, CyanMuted);
            SetTopLeft(_placementStatus.rectTransform, 10f, 105f, 230f, 27f);

            _gridButtons = new Button[4];
            var gridOptions = new[] { 1f, 2f, 4f, 8f };
            for (var index = 0; index < gridOptions.Length; index++)
            {
                var size = gridOptions[index];
                var button = CreateButton($"Grid{size}", panel.transform, $"GRID {size:0}",
                    new Color32(0, 0, 0, 76), CyanDimBorder);
                SetTopLeft(button.GetComponent<RectTransform>(), 10f + index * 58f, 140f, 54f, 28f);
                button.onClick.AddListener(() => SelectGridSize(size));
                _gridButtons[index] = button;
            }
            _rotateMinusButton = CreateButton("RotateMinus", panel.transform, "ROTATE -", new Color32(0, 0, 0, 76), CyanDimBorder);
            SetTopLeft(_rotateMinusButton.GetComponent<RectTransform>(), 10f, 174f, 72f, 28f);
            _rotateMinusButton.onClick.AddListener(() => RotateSelected(-Mathf.PI / 8f));
            _snapButton = CreateButton("Snap", panel.transform, "SNAP ON", new Color32(16, 185, 129, 28), CyanDimBorder);
            SetTopLeft(_snapButton.GetComponent<RectTransform>(), 87f, 174f, 72f, 28f);
            _snapButton.onClick.AddListener(ToggleSnap);
            _rotatePlusButton = CreateButton("RotatePlus", panel.transform, "ROTATE +", new Color32(0, 0, 0, 76), CyanDimBorder);
            SetTopLeft(_rotatePlusButton.GetComponent<RectTransform>(), 164f, 174f, 76f, 28f);
            _rotatePlusButton.onClick.AddListener(() => RotateSelected(Mathf.PI / 8f));
            _placeButton = CreateButton("PlaceSelected", panel.transform, "PLACE SELECTED", YellowFill, Yellow);
            SetTopLeft(_placeButton.GetComponent<RectTransform>(), 10f, 208f, 230f, 32f);
            _placeButton.onClick.AddListener(() => PlaceSelected());
            _clearPlacedButton = CreateButton("ClearPlaced", panel.transform, "CLEAR PLACED", new Color32(239, 68, 68, 20), new Color32(254, 202, 202, 90));
            SetTopLeft(_clearPlacedButton.GetComponent<RectTransform>(), 10f, 246f, 230f, 27f);
            _clearPlacedButton.onClick.AddListener(ClearPlaced);
            RefreshPlacementControllerNavigation();

            _placedHeader = CreateText("PlacedHeading", panel.transform, "PLACED OBJECTS", 8,
                TextAnchor.MiddleLeft, CyanMuted);
            SetTopLeft(_placedHeader.rectTransform, 10f, 278f, 230f, 18f);
            var placedViewport = CreatePanel("PlacedViewport", panel.transform, new Color32(0, 0, 0, 45));
            SetTopLeft(placedViewport.GetComponent<RectTransform>(), 10f, 299f, 230f, 99f);
            var mask = placedViewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            _placedContent = new GameObject("Content", typeof(RectTransform)).transform;
            _placedContent.SetParent(placedViewport.transform, false);
            var contentRect = (RectTransform)_placedContent;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;
            var scroll = placedViewport.AddComponent<ScrollRect>();
            scroll.viewport = placedViewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            var preview = CreateButton("PreviewPlaced", panel.transform, "PREVIEW", new Color32(0, 0, 0, 76), CyanDimBorder);
            SetTopLeft(preview.GetComponent<RectTransform>(), 10f, 404f, 72f, 26f);
            preview.onClick.AddListener(PreviewPlacedObject);
            var move = CreateButton("MovePlaced", panel.transform, "MOVE", YellowFill, Yellow);
            SetTopLeft(move.GetComponent<RectTransform>(), 87f, 404f, 72f, 26f);
            move.onClick.AddListener(MovePlacedObject);
            var delete = CreateButton("DeletePlaced", panel.transform, "DELETE", new Color32(239, 68, 68, 20), new Color32(254, 202, 202, 90));
            SetTopLeft(delete.GetComponent<RectTransform>(), 164f, 404f, 76f, 26f);
            delete.onClick.AddListener(DeletePlacedObject);

            _slotHeader = CreateText("SlotHeading", panel.transform, "SAVE SLOTS 0/6", 8,
                TextAnchor.MiddleLeft, CyanMuted);
            SetTopLeft(_slotHeader.rectTransform, 10f, 437f, 230f, 18f);
            _slotRoot = new GameObject("Slots", typeof(RectTransform)).transform;
            _slotRoot.SetParent(panel.transform, false);
            SetTopLeft((RectTransform)_slotRoot, 10f, 458f, 230f, 58f);
            for (var index = 0; index < WofEnginePlaceableCatalog.MaximumSaveSlots; index++)
            {
                var captured = index;
                var slotButton = CreateButton($"Slot{index + 1}", _slotRoot, string.Empty,
                    new Color32(0, 0, 0, 76), CyanDimBorder);
                SetTopLeft(slotButton.GetComponent<RectTransform>(), (index % 3) * 77f, (index / 3) * 29f, 73f, 25f);
                slotButton.onClick.AddListener(() => SelectSlot(captured));
                var slotText = CreateText("Text", slotButton.transform, $"SLOT {index + 1}\nEMPTY", 6,
                    TextAnchor.MiddleLeft, CyanMuted);
                SetFullInset(slotText.rectTransform, 4f);
                _slotButtons.Add(slotButton);
                _slotLabels.Add(slotText);
            }
            var slotInputBorder = CreatePanel("SlotLabelBorder", panel.transform, CyanDimBorder);
            SetTopLeft(slotInputBorder.GetComponent<RectTransform>(), 10f, 520f, 230f, 24f);
            var slotFill = CreatePanel("Fill", slotInputBorder.transform, new Color32(0, 0, 0, 110));
            SetFullInset(slotFill.GetComponent<RectTransform>(), 1f);
            var slotTextValue = CreateText("Text", slotFill.transform, "Slot 1", 8, TextAnchor.MiddleLeft, Cyan50);
            SetFullInset(slotTextValue.rectTransform, 6f);
            _slotLabel = slotInputBorder.AddComponent<InputField>();
            _slotLabel.targetGraphic = slotFill.GetComponent<Image>();
            _slotLabel.textComponent = slotTextValue;
            _slotLabel.characterLimit = 36;
            _slotLabel.text = "Slot 1";
            var save = CreateButton("SaveSlot", panel.transform, "SAVE", new Color32(16, 185, 129, 28), new Color32(167, 243, 208, 100));
            SetTopLeft(save.GetComponent<RectTransform>(), 10f, 548f, 72f, 20f);
            save.onClick.AddListener(SaveSelectedSlot);
            var load = CreateButton("LoadSlot", panel.transform, "LOAD", new Color32(34, 211, 238, 20), CyanDimBorder);
            SetTopLeft(load.GetComponent<RectTransform>(), 87f, 548f, 72f, 20f);
            load.onClick.AddListener(LoadSelectedSlot);
            var deleteSlot = CreateButton("DeleteSlot", panel.transform, "DELETE", new Color32(239, 68, 68, 20), new Color32(254, 202, 202, 90));
            SetTopLeft(deleteSlot.GetComponent<RectTransform>(), 164f, 548f, 76f, 20f);
            deleteSlot.onClick.AddListener(DeleteSelectedSlot);
        }

        private void RefreshCategoryButtons()
        {
            var query = NormalizeQuery();
            for (var index = 0; index < _categoryButtons.Count; index++)
            {
                var category = WofEnginePlaceableCatalog.OrderedCategories[index];
                var count = WofEnginePlaceableCatalog.All.Count(definition =>
                    definition.Category == category && MatchesQuery(definition, query));
                var active = category == _activeCategory;
                SetButtonColors(_categoryButtons[index], active ? YellowFill : new Color32(0, 0, 0, 64),
                    active ? Yellow : CyanDimBorder);
                _categoryLabels[index].text = $"{WofEnginePlaceableCatalog.GetCategoryLabel(category).ToUpperInvariant()}                 {count}";
                _categoryLabels[index].color = active ? Yellow : CyanMuted;
            }
        }

        private void RebuildCatalogCards()
        {
            foreach (var card in _cardObjects) if (card != null) Destroy(card);
            _cardObjects.Clear();
            var query = NormalizeQuery();
            var definitions = WofEnginePlaceableCatalog.All.Where(definition =>
                definition.Category == _activeCategory && MatchesQuery(definition, query)).ToArray();
            _shownCount.text = $"{definitions.Length} PLACEABLE{(definitions.Length == 1 ? string.Empty : "S")}";
            for (var index = 0; index < definitions.Length; index++)
            {
                var definition = definitions[index];
                var captured = definition;
                var selected = _selected == definition;
                var card = CreateButton($"Placeable-{definition.Id}", _cardRoot, string.Empty,
                    selected ? YellowFill : new Color32(0, 0, 0, 72), selected ? Yellow : CyanDimBorder);
                var column = index % 4;
                var row = index / 4;
                SetTopLeft(card.GetComponent<RectTransform>(), column * 158f, row * 225f, 150f, 212f);
                card.onClick.AddListener(() => SelectPlaceable(captured));
                var preview = new GameObject("Preview", typeof(RectTransform), typeof(WofEnginePlaceableThumbnailGraphic));
                preview.transform.SetParent(card.transform, false);
                SetTopLeft(preview.GetComponent<RectTransform>(), 5f, 5f, 140f, 94f);
                preview.GetComponent<WofEnginePlaceableThumbnailGraphic>().Configure(definition);
                var name = CreateText("Name", card.transform, definition.Name.ToUpperInvariant(), 9,
                    TextAnchor.UpperLeft, selected ? Yellow : Cyan50);
                SetTopLeft(name.rectTransform, 7f, 104f, 136f, 32f);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                var description = CreateText("Description", card.transform, definition.Description, 7,
                    TextAnchor.UpperLeft, CyanMuted);
                SetTopLeft(description.rectTransform, 7f, 139f, 136f, 42f);
                description.horizontalOverflow = HorizontalWrapMode.Wrap;
                var meta = CreateText("Meta", card.transform,
                    $"R{definition.FootprintRadius:0.#} / SLOPE {definition.MaxSlopeDelta:0.##}",
                    6, TextAnchor.LowerLeft, new Color32(207, 250, 254, 85));
                SetTopLeft(meta.rectTransform, 7f, 184f, 136f, 18f);
                _cardObjects.Add(card.gameObject);
            }
            RefreshCatalogControllerNavigation();
        }

        private void RefreshCatalogControllerNavigation()
        {
            if (_cardObjects.Count == 0 || _gridButtons == null || _gridButtons.Length == 0) return;
            var activeCategoryIndex = -1;
            for (var index = 0; index < WofEnginePlaceableCatalog.OrderedCategories.Count; index++)
            {
                if (WofEnginePlaceableCatalog.OrderedCategories[index] != _activeCategory) continue;
                activeCategoryIndex = index;
                break;
            }
            var activeCategory = activeCategoryIndex >= 0 && activeCategoryIndex < _categoryButtons.Count
                ? _categoryButtons[activeCategoryIndex]
                : _categoryButtons.FirstOrDefault();
            for (var index = 0; index < _cardObjects.Count; index++)
            {
                var card = _cardObjects[index].GetComponent<Button>();
                var column = index % 4;
                var row = index / 4;
                var rowStart = row * 4;
                var rowEnd = Mathf.Min(rowStart + 3, _cardObjects.Count - 1);
                card.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = column > 0
                        ? _cardObjects[index - 1].GetComponent<Button>()
                        : activeCategory,
                    selectOnRight = index < rowEnd
                        ? _cardObjects[index + 1].GetComponent<Button>()
                        : _gridButtons[0],
                    selectOnUp = row > 0
                        ? _cardObjects[index - 4].GetComponent<Button>()
                        : _search,
                    selectOnDown = index + 4 < _cardObjects.Count
                        ? _cardObjects[index + 4].GetComponent<Button>()
                        : _placeButton
                };
            }
            RefreshPlacementControllerNavigation();
        }

        private void RefreshPlacementControllerNavigation()
        {
            if (_gridButtons == null || _gridButtons.Length != 4 || _rotateMinusButton == null ||
                _snapButton == null || _rotatePlusButton == null || _placeButton == null ||
                _clearPlacedButton == null) return;

            var selectedCardObject = _selected == null
                ? _cardObjects.FirstOrDefault()
                : _cardObjects.FirstOrDefault(card => card != null && card.name == $"Placeable-{_selected.Id}");
            var selectedCard = selectedCardObject == null ? null : selectedCardObject.GetComponent<Button>();
            for (var index = 0; index < _gridButtons.Length; index++)
            {
                var down = index == 0 ? _rotateMinusButton : index == 3 ? _rotatePlusButton : _snapButton;
                _gridButtons[index].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = index == 0 ? selectedCard : _gridButtons[index - 1],
                    selectOnRight = index == _gridButtons.Length - 1 ? null : _gridButtons[index + 1],
                    selectOnUp = _search,
                    selectOnDown = down
                };
            }

            _rotateMinusButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = selectedCard,
                selectOnRight = _snapButton,
                selectOnUp = _gridButtons[0],
                selectOnDown = _placeButton
            };
            _snapButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = _rotateMinusButton,
                selectOnRight = _rotatePlusButton,
                selectOnUp = _gridButtons[1],
                selectOnDown = _placeButton
            };
            _rotatePlusButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = _snapButton,
                selectOnRight = null,
                selectOnUp = _gridButtons[3],
                selectOnDown = _placeButton
            };
            _placeButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = selectedCard,
                selectOnRight = null,
                selectOnUp = _snapButton,
                selectOnDown = _clearPlacedButton
            };
        }

        private void RefreshPlacementPanel()
        {
            if (_selectedName == null) return;
            if (_selected == null)
            {
                _selectedName.text = "Select an object to preview it on the grid.";
                _selectedMeta.text = string.Empty;
                _placeButton.interactable = false;
            }
            else
            {
                _selectedName.text = _selected.Name;
                _selectedMeta.text = $"{_selected.Id.ToUpperInvariant()}\nFOOTPRINT {_selected.FootprintRadius:0.#} / SLOPE {_selected.MaxSlopeDelta:0.##} / YAW {_selected.YawMode.ToString().ToLowerInvariant()}";
                _placeButton.interactable = true;
            }
            _snapButton.GetComponentInChildren<Text>().text = _snapToGrid ? "SNAP ON" : "SNAP OFF";
            SetButtonColors(_snapButton, _snapToGrid ? new Color32(16, 185, 129, 28) : new Color32(0, 0, 0, 76), CyanDimBorder);
            for (var index = 0; index < _gridButtons.Length; index++)
            {
                var active = Mathf.Approximately(_gridSize, new[] { 1f, 2f, 4f, 8f }[index]);
                SetButtonColors(_gridButtons[index], active ? YellowFill : new Color32(0, 0, 0, 76),
                    active ? Yellow : CyanDimBorder);
            }
        }

        private void RefreshPlacedObjectList()
        {
            foreach (var row in _placedRows) if (row != null) Destroy(row);
            _placedRows.Clear();
            _placedHeader.text = $"PLACED OBJECTS  {_localRecords.Count}/{WofEnginePlaceableCatalog.MaximumPlacedObjects}";
            var contentRect = (RectTransform)_placedContent;
            contentRect.sizeDelta = new Vector2(0f, Mathf.Max(99f, _localRecords.Count * 32f));
            if (_localRecords.Count == 0)
            {
                var empty = CreateText("Empty", _placedContent, "No editor objects placed.", 7,
                    TextAnchor.MiddleCenter, new Color32(207, 250, 254, 70));
                SetTopStretch(empty.rectTransform, 0f, 90f, 0f, 0f);
                _placedRows.Add(empty.gameObject);
                return;
            }
            for (var index = 0; index < _localRecords.Count; index++)
            {
                var record = _localRecords[index];
                var capturedId = record.instanceId;
                var selected = record.instanceId == _selectedPlacedObjectId;
                var row = CreateButton($"Placed-{index}", _placedContent,
                    $"{record.label.ToUpperInvariant()}     X {Mathf.RoundToInt(record.x)}  Z {Mathf.RoundToInt(record.z)}",
                    selected ? YellowFill : new Color32(0, 0, 0, 70), selected ? Yellow : CyanDimBorder);
                SetTopStretch(row.GetComponent<RectTransform>(), index * 32f, 29f, 0f, 0f);
                row.onClick.AddListener(() =>
                {
                    _selectedPlacedObjectId = capturedId;
                    Debug.Log($"[WOF-AUTOMATION] ENGINE_PLACEABLE_EDIT action=select instance={capturedId}");
                    RefreshPlacedObjectList();
                });
                _placedRows.Add(row.gameObject);
            }
        }

        private void RefreshSlotPanel()
        {
            if (_slotHeader == null) return;
            _slotHeader.text = $"SAVE SLOTS  {_storage.slots.Count}/{WofEnginePlaceableCatalog.MaximumSaveSlots}";
            for (var index = 0; index < _slotButtons.Count; index++)
            {
                var slotId = $"slot-{index + 1}";
                var slot = WofEnginePlaceableStorage.FindSlot(_storage, slotId);
                var selected = slotId == _selectedSlotId;
                _slotLabels[index].text = slot == null
                    ? $"SLOT {index + 1}\nEMPTY"
                    : $"{slot.label.ToUpperInvariant()}\n{slot.objects.Count} SAVED";
                _slotLabels[index].color = selected ? Yellow : CyanMuted;
                SetButtonColors(_slotButtons[index], selected ? YellowFill : new Color32(0, 0, 0, 76),
                    selected ? Yellow : CyanDimBorder);
            }
        }

        private string NormalizeQuery()
        {
            return (_search?.text ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool MatchesQuery(WofEnginePlaceableDefinition definition, string query)
        {
            return query.Length == 0 || definition.SearchText.Contains(query, StringComparison.Ordinal);
        }

        private void SetStatus(string message, bool blocked)
        {
            if (_placementStatus == null) return;
            _placementStatus.text = message;
            _placementStatus.color = blocked ? new Color32(254, 202, 202, 220) : CyanMuted;
        }

        private void RefreshPixelScale()
        {
            if (_pixelRoot == null) return;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            var canvas = GetComponentInParent<Canvas>();
            var canvasScale = canvas == null ? 1f : Mathf.Max(0.01f, canvas.scaleFactor);
            var fit = Mathf.Min(1f, Screen.width * 0.94f / 1080f, Screen.height * 0.92f / 690f);
            var scale = fit / canvasScale;
            _pixelRoot.localScale = new Vector3(scale, scale, 1f);
        }

        private Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Color fill, Color border)
        {
            var borderObject = CreatePanel(name, parent, border);
            var button = borderObject.AddComponent<Button>();
            var inner = CreatePanel("Fill", borderObject.transform, FlattenButtonFill(fill));
            SetFullInset(inner.GetComponent<RectTransform>(), 1f);
            button.targetGraphic = inner.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            if (label.Length > 0)
            {
                var text = CreateText("Label", inner.transform, label, 7, TextAnchor.MiddleCenter, Cyan50);
                SetFullInset(text.rectTransform, 3f);
            }
            return button;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            item.GetComponent<Image>().color = color;
            return item;
        }

        private static void SetButtonColors(Button button, Color fill, Color border)
        {
            if (button == null) return;
            button.GetComponent<Image>().color = border;
            if (button.transform.childCount > 0)
            {
                var image = button.transform.GetChild(0).GetComponent<Image>();
                if (image != null) image.color = FlattenButtonFill(fill);
            }
        }

        private static Color FlattenButtonFill(Color fill)
        {
            // The React menu composites translucent fills over its dark panel. Unity's border is the
            // button's full background, so preserving alpha here incorrectly washes the entire card
            // with the border color. Pre-composite once against the same dark panel color instead.
            var background = (Color)Panel;
            return new Color(
                fill.r * fill.a + background.r * (1f - fill.a),
                fill.g * fill.a + background.g * (1f - fill.a),
                fill.b * fill.a + background.b * (1f - fill.a),
                1f);
        }

        private static void AddOutline(GameObject target, Color color, float distance = 1f)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private static void AddBottomBorder(Transform parent, Color color)
        {
            var border = CreatePanel("BottomBorder", parent, color);
            SetBottomStretch(border.GetComponent<RectTransform>(), 0f, 1f, 0f, 0f);
        }

        private static void AddTopBorder(Transform parent, Color color)
        {
            var border = CreatePanel("TopBorder", parent, color);
            SetTopStretch(border.GetComponent<RectTransform>(), 0f, 1f, 0f, 0f);
        }

        private static void SetFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void SetFullInset(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetCentered(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetTopRight(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetTopStretch(RectTransform rect, float top, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetBottomStretch(RectTransform rect, float bottom, float height, float left, float right)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }
    }

    public sealed class WofEnginePlaceableThumbnailGraphic : MaskableGraphic
    {
        private WofEnginePlaceableDefinition _definition;

        public void Configure(WofEnginePlaceableDefinition definition)
        {
            _definition = definition;
            color = Color.white;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (_definition == null) return;
            var rect = rectTransform.rect;
            var width = rect.width;
            var height = rect.height;
            Vector2 Point(float x, float y) => new(rect.xMin + x, rect.yMin + y);
            Rect Shape(float x, float y, float shapeWidth, float shapeHeight) =>
                new(rect.xMin + x, rect.yMin + y, shapeWidth, shapeHeight);
            AddQuad(vertexHelper, rect, new Color32(0, 0, 0, 90));
            if (_definition.Id == "campfire-small")
            {
                AddRotatedQuad(vertexHelper, Point(width * 0.5f, height * 0.28f), new Vector2(70f, 10f), 35f, _definition.BaseColor);
                AddRotatedQuad(vertexHelper, Point(width * 0.5f, height * 0.28f), new Vector2(70f, 10f), -35f, new Color32(58, 37, 21, 255));
                AddTriangle(vertexHelper, Point(width * 0.5f, height * 0.82f), Point(width * 0.3f, height * 0.24f),
                    Point(width * 0.7f, height * 0.24f), _definition.AccentColor);
            }
            else if (_definition.Category == WofEnginePlaceableCategory.Nature)
            {
                AddCircle(vertexHelper, Point(width * 0.45f, height * 0.47f), height * 0.34f, 18, _definition.AccentColor);
                AddCircle(vertexHelper, Point(width * 0.67f, height * 0.57f), height * 0.21f, 14, _definition.HighlightColor);
            }
            else if (_definition.Category == WofEnginePlaceableCategory.Magic)
            {
                AddCircle(vertexHelper, Point(width * 0.5f, height * 0.25f), width * 0.31f, 18, _definition.BaseColor);
                AddRing(vertexHelper, Point(width * 0.5f, height * 0.62f), height * 0.24f, height * 0.045f, 22, _definition.AccentColor);
                AddCircle(vertexHelper, Point(width * 0.5f, height * 0.62f), height * 0.06f, 12, _definition.HighlightColor);
            }
            else if (_definition.Id == "training-spell-dummy")
            {
                AddQuad(vertexHelper, Shape(width * 0.38f, height * 0.18f, width * 0.24f, height * 0.48f), _definition.AccentColor);
                AddQuad(vertexHelper, Shape(width * 0.42f, height * 0.66f, width * 0.16f, height * 0.15f), new Color32(253, 230, 138, 255));
                AddQuad(vertexHelper, Shape(width * 0.25f, height * 0.83f, width * 0.5f, height * 0.035f), _definition.AccentColor);
            }
            else
            {
                AddQuad(vertexHelper, Shape(width * 0.26f, height * 0.15f, width * 0.48f, height * 0.47f), _definition.BaseColor);
                AddTriangle(vertexHelper, Point(width * 0.5f, height * 0.88f), Point(width * 0.18f, height * 0.6f),
                    Point(width * 0.82f, height * 0.6f), _definition.AccentColor);
                AddQuad(vertexHelper, Shape(width * 0.44f, height * 0.15f, width * 0.12f, height * 0.25f), new Color32(29, 18, 11, 255));
            }
        }

        private static void AddQuad(VertexHelper vh, Rect rect, Color color)
        {
            var start = vh.currentVertCount;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddRotatedQuad(VertexHelper vh, Vector2 center, Vector2 size, float degrees, Color color)
        {
            var rotation = Quaternion.Euler(0f, 0f, degrees);
            var half = size * 0.5f;
            var corners = new[] { new Vector2(-half.x, -half.y), new Vector2(-half.x, half.y), new Vector2(half.x, half.y), new Vector2(half.x, -half.y) };
            var start = vh.currentVertCount;
            for (var index = 0; index < corners.Length; index++) vh.AddVert(center + (Vector2)(rotation * corners[index]), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            var start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private static void AddCircle(VertexHelper vh, Vector2 center, float radius, int segments, Color color)
        {
            var centerIndex = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            for (var index = 0; index <= segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (index > 0) vh.AddTriangle(centerIndex, centerIndex + index, centerIndex + index + 1);
            }
        }

        private static void AddRing(VertexHelper vh, Vector2 center, float radius, float thickness, int segments, Color color)
        {
            var start = vh.currentVertCount;
            for (var index = 0; index <= segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(center + direction * (radius - thickness), color, Vector2.zero);
                vh.AddVert(center + direction * (radius + thickness), color, Vector2.zero);
                if (index == 0) continue;
                var current = start + index * 2;
                var previous = current - 2;
                vh.AddTriangle(previous, previous + 1, current);
                vh.AddTriangle(current, previous + 1, current + 1);
            }
        }
    }
}
