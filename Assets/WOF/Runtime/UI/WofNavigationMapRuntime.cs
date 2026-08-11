using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WOF
{
    public sealed class WofNavigationMapRuntime : MonoBehaviour
    {
        private const float CompactOrthographicSize = 80f;
        private const string ExplorationPlayerPrefsKey = "wof.navigation.explored.v1";
        private const string WaypointPlayerPrefsKey = "wof.navigation.waypoint.v1";
        private const float FullMapAspectRatio = 4096f / 2979f;
        internal const float MinimumExpandedZoom = 1f;
        internal const float MaximumExpandedZoom = 3f;

        [SerializeField] private WofHud hud;
        [SerializeField] private Transform uiParent;
        [SerializeField] private Font font;
        [SerializeField] private Sprite circularMaskSprite;

        private GameObject _compactRoot;
        private GameObject _expandedRoot;
        private RectTransform _compactShadow;
        private RectTransform _compactBorder;
        private RectTransform _compactCoordinatesBadge;
        private RectTransform _compactHintPanel;
        private RectTransform _expandedMapFrame;
        private RectTransform _worldMapViewport;
        private RectTransform _expandedMapContent;
        private RectTransform _destinationPanel;
        private RectTransform _expandedControlsHint;
        private readonly List<Button> _destinationButtons = new();
        private readonly List<RectTransform> _locationMarkers = new();
        private Button _closeButton;
        private RectTransform _expandedArrow;
        private RectTransform _compactArrow;
        private RectTransform _expandedWaypointMarker;
        private RectTransform _expandedMapCursor;
        private RectTransform _compactWaypointArrow;
        private Text _compactCoordinates;
        private Text _expandedCoordinates;
        private RawImage _compactMap;
        private RawImage _expandedWorldMap;
        private WofWorldMapExplorationGraphic _worldMapExploration;
        private Camera _mapCamera;
        private RenderTexture _mapTexture;
        private Material _compactMapColorGradeMaterial;
        private Material _worldMapColorGradeMaterial;
        private WofPlayerController _localPlayer;
        private float _nextPlayerResolveAt;
        private float _nextRenderAt;
        private Vector3 _lastRenderedPosition = new(float.PositiveInfinity, 0f, float.PositiveInfinity);
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private bool _controllerToggleHeld;
        private bool _controllerBackHeld;
        private bool _controllerWaypointHeld;
        private bool _controllerClearWaypointHeld;
        private GameObject _mapPanPreservedSelection;
        private int _mapPanSelectionRestoreFrames;
        private bool _explorationSavePending;
        private float _explorationSaveAt;
        private float _expandedZoom = MinimumExpandedZoom;
        private Vector2 _mapFocusNormalized = new(0.5f, 0.5f);
        private Vector2 _mapCursorNormalized = new(0.5f, 0.5f);
        private bool _hasWaypoint;
        private Vector2 _waypointWorldPosition;
        private bool _mapProbe;
        private bool _waypointProbe;
        private bool _probeApplied;

        public static WofNavigationMapRuntime Instance { get; private set; }
        public static bool IsExpanded => Instance != null && Instance._expandedRoot != null &&
                                         Instance._expandedRoot.activeSelf;
        internal static float ExpandedZoom => Instance == null ? MinimumExpandedZoom : Instance._expandedZoom;
        internal static bool HasWaypoint => Instance != null && Instance._hasWaypoint;

        public void ConfigureGeneratedView(
            WofHud generatedHud,
            Transform generatedUiParent,
            Font generatedFont,
            Sprite generatedCircularMaskSprite)
        {
            hud = generatedHud;
            uiParent = generatedUiParent;
            font = generatedFont;
            circularMaskSprite = generatedCircularMaskSprite;
        }

        private void Awake()
        {
            Instance = this;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-map-probe", StringComparison.OrdinalIgnoreCase)) _mapProbe = true;
                if (argument.Equals("--wof-waypoint-probe", StringComparison.OrdinalIgnoreCase))
                {
                    _mapProbe = true;
                    _waypointProbe = true;
                }
            }
        }

        private void Start()
        {
            BuildView();
            BuildMapCamera();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SaveExplorationNow();
            if (_mapTexture != null)
            {
                _mapTexture.Release();
                Destroy(_mapTexture);
            }
            if (_mapCamera != null) Destroy(_mapCamera.gameObject);
            if (_compactMapColorGradeMaterial != null) Destroy(_compactMapColorGradeMaterial);
            if (_worldMapColorGradeMaterial != null) Destroy(_worldMapColorGradeMaterial);
        }

        private void Update()
        {
            if (_compactRoot == null || _expandedRoot == null || hud == null)
            {
                return;
            }

            RefreshResponsiveLayout();
            var gameplayVisible = hud.IsGameplayVisible || IsExpanded;
            var spellMenuOpen = WofSpellMenuRuntime.IsOpen;
            var pauseOrScoreboardOpen = WofPauseAndScoreboardRuntime.IsAnyMenuOpen;
            _compactRoot.SetActive(gameplayVisible && !IsExpanded && !spellMenuOpen && !pauseOrScoreboardOpen);

            if (!gameplayVisible || spellMenuOpen || pauseOrScoreboardOpen)
            {
                return;
            }

            var keyboardToggle = Keyboard.current?.mKey.wasPressedThisFrame ?? false;
            var gamepad = Gamepad.current;
            var hotbarModifierHeld = WofControllerBindings.IsPressed(gamepad, WofControllerActions.LeftHotbar) ||
                                     WofControllerBindings.IsPressed(gamepad, WofControllerActions.RightHotbar);
            var controllerToggleHeld = !hotbarModifierHeld && WofControllerBindings.IsPressed(gamepad, WofControllerActions.Map);
            var controllerToggle = controllerToggleHeld && !_controllerToggleHeld;
            _controllerToggleHeld = controllerToggleHeld;
            if (keyboardToggle || (controllerToggle && !IsExpanded))
            {
                SetExpanded(!IsExpanded);
                return;
            }
            var controllerBackHeld = WofControllerBindings.IsPressed(gamepad, WofControllerActions.MenuBack);
            var controllerBack = controllerBackHeld && !_controllerBackHeld;
            _controllerBackHeld = controllerBackHeld;
            if (IsExpanded && controllerBack)
            {
                SetExpanded(false);
                return;
            }

            ResolveLocalPlayer();
            if (_localPlayer == null)
            {
                return;
            }

            if (!_probeApplied && _mapProbe && hud.IsGameplayVisible)
            {
                _probeApplied = true;
                SetExpanded(true);
                if (_waypointProbe) SetWaypoint(new Vector2(0.68f, 0.34f));
            }

            if (IsExpanded) UpdateExpandedMapInput(gamepad);

            UpdateMarkers(_localPlayer.transform.position, _localPlayer.transform.eulerAngles.y);
            if (!IsExpanded) RenderCompactMapIfDue(_localPlayer.transform.position);
            if (_explorationSavePending && Time.unscaledTime >= _explorationSaveAt) SaveExplorationNow();
        }

        private void LateUpdate()
        {
            if (!IsExpanded) return;
            if (_mapPanSelectionRestoreFrames > 0 && _mapPanPreservedSelection != null)
            {
                EventSystem.current?.SetSelectedGameObject(_mapPanPreservedSelection);
                _mapPanSelectionRestoreFrames--;
                return;
            }
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null && _destinationButtons.Exists(button => button.gameObject == selected))
            {
                _mapPanPreservedSelection = selected;
            }
        }

        public void ToggleExpanded()
        {
            if (hud != null && hud.IsGameplayVisible && !WofSpellMenuRuntime.IsOpen)
            {
                SetExpanded(!IsExpanded);
            }
        }

        private void BuildView()
        {
            if (_compactRoot != null || uiParent == null || font == null)
            {
                return;
            }

            _compactRoot = CreatePanel(
                "CompactNavigationMap",
                uiParent,
                new Color32(0, 0, 0, 0));
            var compactRect = _compactRoot.GetComponent<RectTransform>();
            compactRect.anchorMin = Vector2.one;
            compactRect.anchorMax = Vector2.one;
            compactRect.pivot = Vector2.one;
            compactRect.anchoredPosition = new Vector2(-16f, -16f);
            compactRect.sizeDelta = new Vector2(176f, 176f);
            var compactImage = _compactRoot.GetComponent<Image>();
            compactImage.raycastTarget = true;
            var compactButton = _compactRoot.AddComponent<Button>();
            compactButton.targetGraphic = compactImage;
            compactButton.onClick.AddListener(ToggleExpanded);

            var shadow = CreateCirclePanel("MapShadow", _compactRoot.transform, new Color32(28, 20, 33, 255));
            _compactShadow = shadow.GetComponent<RectTransform>();
            SetRect(_compactShadow, Vector2.zero, Vector2.one);
            var border = CreateCirclePanel("MapBorder", _compactRoot.transform, new Color32(93, 70, 110, 255));
            _compactBorder = border.GetComponent<RectTransform>();
            SetRect(_compactBorder, Vector2.zero, Vector2.one);
            var mapClip = CreateCirclePanel("MapClip", _compactRoot.transform, new Color32(58, 104, 40, 255));
            SetRect(mapClip.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            var mapMask = mapClip.AddComponent<Mask>();
            mapMask.showMaskGraphic = true;
            _compactMap = CreateRawImage("CompactLiveMap", mapClip.transform);
            SetRect(_compactMap.rectTransform, Vector2.zero, Vector2.one);
            CreateCompassLabels(_compactRoot.transform, 10);
            _compactArrow = CreateArrow("PlayerArrow", _compactRoot.transform, new Vector2(0.40f, 0.36f), new Vector2(0.60f, 0.64f));
            _compactWaypointArrow = CreateWaypointHat(
                "WaypointArrow",
                _compactRoot.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            _compactWaypointArrow.gameObject.SetActive(false);
            var coordinateBadge = CreateCirclePanel("CoordinatesBadge", _compactRoot.transform, new Color32(0, 0, 0, 204));
            _compactCoordinatesBadge = coordinateBadge.GetComponent<RectTransform>();
            _compactCoordinates = CreateText(
                "Coordinates",
                coordinateBadge.transform,
                "X:0 Z:0",
                8,
                TextAnchor.MiddleCenter,
                Color.white);
            SetRect(_compactCoordinates.rectTransform, Vector2.zero, Vector2.one);

            var hintPanel = CreatePanel("OpenHintPanel", _compactRoot.transform, Color.black);
            _compactHintPanel = hintPanel.GetComponent<RectTransform>();
            var hint = CreateText(
                "OpenHint", hintPanel.transform,
                "TAP MAP / M",
                8,
                TextAnchor.MiddleCenter,
                new Color32(136, 136, 136, 255));
            SetRect(hint.rectTransform, Vector2.zero, Vector2.one);
            var hintOutline = hint.gameObject.AddComponent<Outline>();
            hintOutline.effectColor = new Color32(85, 85, 85, 255);
            hintOutline.effectDistance = new Vector2(2f, -2f);

            _expandedRoot = CreatePanel(
                "ExpandedNavigationMap",
                uiParent,
                new Color32(0, 0, 0, 188));
            SetRect(_expandedRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            var title = CreateText(
                "Title",
                _expandedRoot.transform,
                "WORLD MAP",
                28,
                TextAnchor.MiddleLeft,
                Color.white);
            SetRect(title.rectTransform, new Vector2(0.04f, 0.89f), new Vector2(0.34f, 0.98f));
            _expandedCoordinates = CreateText(
                "Coordinates",
                _expandedRoot.transform,
                "X:0 Z:0",
                13,
                TextAnchor.MiddleLeft,
                new Color32(207, 250, 254, 255));
            SetRect(_expandedCoordinates.rectTransform, new Vector2(0.04f, 0.84f), new Vector2(0.34f, 0.91f));
            _closeButton = CreateButton(
                "Close",
                _expandedRoot.transform,
                "CLOSE",
                new Color32(85, 85, 85, 255));
            SetRect(_closeButton.GetComponent<RectTransform>(), new Vector2(0.82f, 0.89f), new Vector2(0.96f, 0.97f));
            _closeButton.onClick.AddListener(() => SetExpanded(false));
            var controlsHint = CreateText(
                "MapControlsHint",
                _expandedRoot.transform,
                "CONTROLLER  RT/LT ZOOM  |  RIGHT STICK CURSOR  |  X SET WAYPOINT  |  Y CLEAR",
                12,
                TextAnchor.MiddleCenter,
                new Color32(207, 250, 254, 255));
            _expandedControlsHint = controlsHint.rectTransform;
            AddBlackTextOutline(controlsHint);

            var destinations = CreatePanel(
                "FastTravelDestinations",
                _expandedRoot.transform,
                new Color32(21, 12, 31, 242));
            _destinationPanel = destinations.GetComponent<RectTransform>();
            var travelTitle = CreateText(
                "FastTravelTitle",
                destinations.transform,
                "FAST TRAVEL",
                20,
                TextAnchor.MiddleCenter,
                new Color32(251, 191, 36, 255));
            SetRect(travelTitle.rectTransform, new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.98f));
            foreach (var record in WofMapFastTravel.MenuDestinations)
            {
                var capturedDestination = record.Destination;
                var isDimension = capturedDestination == WofMapDestination.LilyCoil;
                var button = CreateButton(
                    $"Travel{record.Destination}",
                    destinations.transform,
                    record.Label,
                    isDimension
                        ? new Color32(126, 43, 171, 255)
                        : new Color32(88, 56, 112, 255));
                if (isDimension)
                {
                    var label = button.GetComponentInChildren<Text>();
                    if (label != null)
                    {
                        label.color = new Color32(255, 226, 117, 255);
                        AddBlackTextOutline(label);
                    }
                }
                button.onClick.AddListener(() => TravelTo(capturedDestination));
                _destinationButtons.Add(button);
            }

            var mapFrame = CreatePanel(
                "MapFrame",
                _expandedRoot.transform,
                new Color32(109, 81, 125, 255));
            _expandedMapFrame = mapFrame.GetComponent<RectTransform>();
            _expandedMapFrame.anchorMin = new Vector2(0.5f, 0.5f);
            _expandedMapFrame.anchorMax = new Vector2(0.5f, 0.5f);
            _expandedMapFrame.pivot = new Vector2(0.5f, 0.5f);
            _expandedMapFrame.anchoredPosition = Vector2.zero;
            _expandedMapFrame.sizeDelta = new Vector2(800f, 800f);
            var worldViewport = CreatePanel("WorldMapViewport", mapFrame.transform, new Color32(9, 5, 16, 255));
            _worldMapViewport = worldViewport.GetComponent<RectTransform>();
            SetRect(_worldMapViewport, new Vector2(0.006f, 0.008f), new Vector2(0.994f, 0.992f));
            worldViewport.AddComponent<RectMask2D>();
            var contentObject = new GameObject("WorldMapContent", typeof(RectTransform));
            contentObject.transform.SetParent(worldViewport.transform, false);
            _expandedMapContent = contentObject.GetComponent<RectTransform>();
            SetRect(_expandedMapContent, Vector2.zero, Vector2.one);
            _expandedMapContent.pivot = new Vector2(0.5f, 0.5f);
            _expandedWorldMap = CreateRawImage("WorldMap", _expandedMapContent.transform);
            SetRect(_expandedWorldMap.rectTransform, Vector2.zero, Vector2.one);
            _expandedWorldMap.texture = Resources.Load<Texture2D>("Maps/dagamemap");
            if (_expandedWorldMap.texture == null)
            {
                Debug.LogError("Exact React world map asset Maps/dagamemap is missing.");
            }
            var explorationObject = new GameObject(
                "ExplorationReveal",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(WofWorldMapExplorationGraphic));
            explorationObject.transform.SetParent(_expandedMapContent.transform, false);
            _worldMapExploration = explorationObject.GetComponent<WofWorldMapExplorationGraphic>();
            _worldMapExploration.raycastTarget = false;
            _worldMapExploration.ImportExploredCells(PlayerPrefs.GetString(ExplorationPlayerPrefsKey, string.Empty));
            SetRect(explorationObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            foreach (var record in WofMapFastTravel.Destinations)
            {
                if (!record.ShowOnWorldMap) continue;
                CreateLocationMarker(record);
            }
            var north = CreateText("North", worldViewport.transform, "N", 15, TextAnchor.UpperCenter, new Color32(251, 191, 36, 255));
            SetRect(north.rectTransform, new Vector2(0.46f, 0.92f), new Vector2(0.54f, 0.995f));
            AddBlackTextOutline(north);
            _expandedWaypointMarker = CreateWaypointHat(
                "WaypointMarker",
                _expandedMapContent.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            _expandedWaypointMarker.gameObject.SetActive(false);
            _expandedArrow = CreateArrow("PlayerArrow", _expandedMapContent.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _expandedMapCursor = CreateMapCursor(_expandedMapContent.transform);
            LoadWaypoint();

            _expandedRoot.SetActive(false);
            _compactRoot.SetActive(false);
        }

        private void TravelTo(WofMapDestination destination)
        {
            ResolveLocalPlayer();
            if (_localPlayer == null || !_localPlayer.RequestMapFastTravel(destination))
            {
                Debug.LogWarning($"[WOF-AUTOMATION] MAP_FAST_TRAVEL_UI_FAILED destination={destination}");
                return;
            }

            SetExpanded(false);
            Debug.Log($"[WOF-AUTOMATION] MAP_FAST_TRAVEL_UI destination={destination}");
        }

        private void UpdateExpandedMapInput(Gamepad gamepad)
        {
            var deltaTime = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            var zoomInput = gamepad == null
                ? 0f
                : gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();
            if (Mathf.Abs(zoomInput) > 0.05f)
            {
                SetExpandedZoom(_expandedZoom + zoomInput * 1.45f * deltaTime);
            }

            var keyboard = Keyboard.current;
            if (keyboard?.equalsKey.wasPressedThisFrame ?? false) SetExpandedZoom(_expandedZoom + 0.25f);
            if (keyboard?.minusKey.wasPressedThisFrame ?? false) SetExpandedZoom(_expandedZoom - 0.25f);

            var mouse = Mouse.current;
            if (mouse != null && IsPointerInsideWorldMap(mouse.position.ReadValue()))
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) SetExpandedZoom(_expandedZoom + Mathf.Sign(scroll) * 0.22f);
                if (mouse.leftButton.wasPressedThisFrame && TryGetMapNormalized(mouse.position.ReadValue(), out var pointerMap))
                {
                    _mapCursorNormalized = pointerMap;
                    _mapFocusNormalized = pointerMap;
                    SetWaypoint(pointerMap);
                }
                if (mouse.rightButton.wasPressedThisFrame) ClearWaypoint();
            }

            var cursorInput = gamepad?.rightStick.ReadValue() ?? Vector2.zero;
            if (cursorInput.sqrMagnitude > 0.04f)
            {
                _mapPanSelectionRestoreFrames = 2;
                _mapCursorNormalized += cursorInput * (0.46f / _expandedZoom) * deltaTime;
                _mapCursorNormalized = ClampNormalized(_mapCursorNormalized);
                _mapFocusNormalized = _mapCursorNormalized;
            }

            var waypointHeld = gamepad?.buttonWest.isPressed ?? false;
            if (waypointHeld && !_controllerWaypointHeld) SetWaypoint(_mapCursorNormalized);
            _controllerWaypointHeld = waypointHeld;
            var clearHeld = gamepad?.buttonNorth.isPressed ?? false;
            if (clearHeld && !_controllerClearWaypointHeld) ClearWaypoint();
            _controllerClearWaypointHeld = clearHeld;

            if (keyboard?.spaceKey.wasPressedThisFrame ?? false) SetWaypoint(_mapCursorNormalized);
            if (keyboard?.deleteKey.wasPressedThisFrame ?? false) ClearWaypoint();
            UpdateExpandedMapTransform();
        }

        private void SetExpandedZoom(float value)
        {
            _expandedZoom = ClampExpandedZoom(value);
            UpdateExpandedMapTransform();
        }

        private void SetWaypoint(Vector2 normalized)
        {
            normalized = ClampNormalized(normalized);
            _waypointWorldPosition = WofWorldMapExplorationGraphic.GetWorldPosition(normalized);
            _hasWaypoint = true;
            PlayerPrefs.SetString(
                WaypointPlayerPrefsKey,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:R},{1:R}",
                    _waypointWorldPosition.x,
                    _waypointWorldPosition.y));
            PlayerPrefs.Save();
            UpdateWaypointMapMarker();
            Debug.Log($"[WOF-AUTOMATION] MAP_WAYPOINT_SET x={_waypointWorldPosition.x:F1} z={_waypointWorldPosition.y:F1}");
        }

        private void ClearWaypoint()
        {
            if (!_hasWaypoint) return;
            _hasWaypoint = false;
            PlayerPrefs.DeleteKey(WaypointPlayerPrefsKey);
            PlayerPrefs.Save();
            UpdateWaypointMapMarker();
            Debug.Log("[WOF-AUTOMATION] MAP_WAYPOINT_CLEARED");
        }

        private void LoadWaypoint()
        {
            var serialized = PlayerPrefs.GetString(WaypointPlayerPrefsKey, string.Empty);
            var parts = serialized.Split(',');
            var worldX = 0f;
            var worldZ = 0f;
            _hasWaypoint = parts.Length == 2 &&
                           float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out worldX) &&
                           float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out worldZ) &&
                           worldX >= WofMapFastTravel.MapMinX && worldX <= WofMapFastTravel.MapMaxX &&
                           worldZ >= WofMapFastTravel.MapMinZ && worldZ <= WofMapFastTravel.MapMaxZ;
            if (_hasWaypoint) _waypointWorldPosition = new Vector2(worldX, worldZ);
            UpdateWaypointMapMarker();
        }

        private void UpdateWaypointMapMarker()
        {
            if (_expandedWaypointMarker == null) return;
            _expandedWaypointMarker.gameObject.SetActive(_hasWaypoint);
            if (!_hasWaypoint) return;
            var normalized = WofWorldMapExplorationGraphic.GetMarkerNormalized(
                _waypointWorldPosition.x,
                _waypointWorldPosition.y);
            _expandedWaypointMarker.anchorMin = normalized;
            _expandedWaypointMarker.anchorMax = normalized;
            _expandedWaypointMarker.anchoredPosition = Vector2.zero;
        }

        private bool IsPointerInsideWorldMap(Vector2 screenPosition)
        {
            return _worldMapViewport != null && RectTransformUtility.RectangleContainsScreenPoint(
                _worldMapViewport,
                screenPosition,
                null);
        }

        private bool TryGetMapNormalized(Vector2 screenPosition, out Vector2 normalized)
        {
            normalized = Vector2.zero;
            if (_worldMapViewport == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _worldMapViewport,
                    screenPosition,
                    null,
                    out var localPoint))
            {
                return false;
            }
            var rect = _worldMapViewport.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            var viewportNormalized = new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y));
            normalized = GetMapNormalizedFromViewport(
                viewportNormalized,
                _expandedZoom,
                _expandedMapContent == null ? Vector2.zero : _expandedMapContent.anchoredPosition,
                rect.size);
            normalized = ClampNormalized(normalized);
            return true;
        }

        private void UpdateExpandedMapTransform()
        {
            if (_expandedMapContent == null || _worldMapViewport == null) return;
            var viewportSize = _worldMapViewport.rect.size;
            _expandedMapContent.localScale = Vector3.one * _expandedZoom;
            _expandedMapContent.anchoredPosition = GetExpandedMapPan(_mapFocusNormalized, _expandedZoom, viewportSize);
            if (_expandedMapCursor != null)
            {
                _expandedMapCursor.anchorMin = _mapCursorNormalized;
                _expandedMapCursor.anchorMax = _mapCursorNormalized;
                _expandedMapCursor.anchoredPosition = Vector2.zero;
            }
            var inverseScale = Vector3.one / _expandedZoom;
            if (_expandedArrow != null) _expandedArrow.localScale = inverseScale;
            if (_expandedWaypointMarker != null) _expandedWaypointMarker.localScale = inverseScale;
            if (_expandedMapCursor != null) _expandedMapCursor.localScale = inverseScale;
            foreach (var marker in _locationMarkers) marker.localScale = inverseScale;
        }

        internal static float ClampExpandedZoom(float value)
        {
            return Mathf.Clamp(value, MinimumExpandedZoom, MaximumExpandedZoom);
        }

        internal static Vector2 GetExpandedMapPan(Vector2 focusNormalized, float zoom, Vector2 viewportSize)
        {
            zoom = ClampExpandedZoom(zoom);
            focusNormalized = ClampNormalized(focusNormalized);
            var maximum = Vector2.Scale(viewportSize, Vector2.one * ((zoom - 1f) * 0.5f));
            var desired = Vector2.Scale(new Vector2(0.5f, 0.5f) - focusNormalized, viewportSize) * zoom;
            return new Vector2(
                Mathf.Clamp(desired.x, -maximum.x, maximum.x),
                Mathf.Clamp(desired.y, -maximum.y, maximum.y));
        }

        internal static Vector2 GetMapNormalizedFromViewport(
            Vector2 viewportNormalized,
            float zoom,
            Vector2 contentPan,
            Vector2 viewportSize)
        {
            zoom = ClampExpandedZoom(zoom);
            var panNormalized = new Vector2(
                viewportSize.x <= 0f ? 0f : contentPan.x / viewportSize.x,
                viewportSize.y <= 0f ? 0f : contentPan.y / viewportSize.y);
            return new Vector2(0.5f, 0.5f) +
                   (viewportNormalized - new Vector2(0.5f, 0.5f) - panNormalized) / zoom;
        }

        internal static Vector2 GetWaypointCompassDirection(Vector2 playerWorld, Vector2 waypointWorld)
        {
            var delta = waypointWorld - playerWorld;
            return delta.sqrMagnitude <= 0.0001f ? Vector2.zero : delta.normalized;
        }

        private static Vector2 ClampNormalized(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }

        private void BuildMapCamera()
        {
            if (_mapCamera != null)
            {
                return;
            }
            var mobile = WofPerformanceModeRuntime.IsMobilePerformanceMode;
            var targetSize = mobile ? 112 : 224;
            _mapTexture = new RenderTexture(targetSize, targetSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "WOF Live Navigation Map",
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            _mapTexture.Create();

            var cameraObject = new GameObject("WOF_NavigationMapCamera");
            cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _mapCamera = cameraObject.AddComponent<Camera>();
            _mapCamera.enabled = false;
            _mapCamera.orthographic = true;
            _mapCamera.orthographicSize = CompactOrthographicSize;
            _mapCamera.nearClipPlane = 0.1f;
            _mapCamera.farClipPlane = 900f;
            _mapCamera.clearFlags = CameraClearFlags.SolidColor;
            _mapCamera.backgroundColor = new Color32(58, 104, 40, 255);
            _mapCamera.allowHDR = false;
            _mapCamera.allowMSAA = false;
            _mapCamera.cullingMask &= ~(1 << WofSurvivalBotwGrassRuntime.RenderLayer);
            _mapCamera.targetTexture = _mapTexture;
            if (_compactMap != null) _compactMap.texture = _mapTexture;

            var mapShader = Resources.Load<Shader>("Shaders/WofUiMapColorGrade");
            if (mapShader == null)
            {
                Debug.LogError("Required WOF/UI Map Color Grade shader is missing.");
                return;
            }
            _compactMapColorGradeMaterial = new Material(mapShader) { name = "WOF Compact Map Color Grade" };
            _compactMapColorGradeMaterial.SetFloat("_Saturation", 1.14f);
            _compactMapColorGradeMaterial.SetFloat("_Contrast", 1.10f);
            _compactMapColorGradeMaterial.SetFloat("_Brightness", 0.98f);
            _worldMapColorGradeMaterial = new Material(mapShader) { name = "WOF World Map Color Grade" };
            _worldMapColorGradeMaterial.SetFloat("_Saturation", 1.34f);
            _worldMapColorGradeMaterial.SetFloat("_Contrast", 1.18f);
            _worldMapColorGradeMaterial.SetFloat("_Brightness", 0.98f);
            if (_compactMap != null) _compactMap.material = _compactMapColorGradeMaterial;
            if (_expandedWorldMap != null) _expandedWorldMap.material = _worldMapColorGradeMaterial;
        }

        private void ResolveLocalPlayer()
        {
            if (_localPlayer != null || Time.unscaledTime < _nextPlayerResolveAt)
            {
                return;
            }
            _nextPlayerResolveAt = Time.unscaledTime + 0.5f;
            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            _localPlayer = playerObject == null ? null : playerObject.GetComponent<WofPlayerController>();
        }

        private void UpdateMarkers(Vector3 position, float yaw)
        {
            var coordinateLabel = $"X:{Mathf.RoundToInt(position.x)} Z:{Mathf.RoundToInt(position.z)}";
            if (_compactCoordinates != null) _compactCoordinates.text = coordinateLabel;
            if (_expandedCoordinates != null)
            {
                _expandedCoordinates.text = _hasWaypoint
                    ? $"{coordinateLabel}  |  ZOOM {_expandedZoom:0.0}x  |  WAYPOINT X:{Mathf.RoundToInt(_waypointWorldPosition.x)} Z:{Mathf.RoundToInt(_waypointWorldPosition.y)}"
                    : $"{coordinateLabel}  |  ZOOM {_expandedZoom:0.0}x  |  NO WAYPOINT";
            }
            if (_compactArrow != null) _compactArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            if (_compactWaypointArrow != null)
            {
                var waypointDirection = _hasWaypoint
                    ? GetWaypointCompassDirection(
                        new Vector2(position.x, position.z),
                        _waypointWorldPosition)
                    : Vector2.zero;
                var showWaypoint = _hasWaypoint && waypointDirection.sqrMagnitude > 0.0001f;
                _compactWaypointArrow.gameObject.SetActive(showWaypoint);
                if (showWaypoint)
                {
                    var anchor = new Vector2(0.5f, 0.5f) + waypointDirection * 0.39f;
                    _compactWaypointArrow.anchorMin = anchor;
                    _compactWaypointArrow.anchorMax = anchor;
                    _compactWaypointArrow.anchoredPosition = Vector2.zero;
                    // The hat stays upright; its position around the compass is the direction cue.
                    _compactWaypointArrow.localRotation = Quaternion.identity;
                }
            }
            if (_expandedArrow != null)
            {
                _expandedArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
                var marker = WofWorldMapExplorationGraphic.GetMarkerNormalized(position.x, position.z);
                _expandedArrow.anchorMin = marker;
                _expandedArrow.anchorMax = marker;
                _expandedArrow.anchoredPosition = Vector2.zero;
            }
            if (_worldMapExploration != null && _worldMapExploration.SetWorldPosition(position.x, position.z))
            {
                _explorationSavePending = true;
                _explorationSaveAt = Time.unscaledTime + 1.5f;
            }
            UpdateWaypointMapMarker();
        }

        private void RenderCompactMapIfDue(Vector3 position)
        {
            if (_mapCamera == null || Time.unscaledTime < _nextRenderAt)
            {
                return;
            }
            const float threshold = 1.75f;
            if ((position - _lastRenderedPosition).sqrMagnitude < threshold * threshold &&
                _mapCamera.orthographicSize == CompactOrthographicSize)
            {
                _nextRenderAt = Time.unscaledTime + (WofPerformanceModeRuntime.IsMobilePerformanceMode ? 2.2f : 0.95f);
                return;
            }

            _mapCamera.orthographicSize = CompactOrthographicSize;
            _mapCamera.transform.position = new Vector3(position.x, Mathf.Max(200f, position.y + 80f), position.z);
            _mapCamera.Render();
            _lastRenderedPosition = position;
            _nextRenderAt = Time.unscaledTime + (WofPerformanceModeRuntime.IsMobilePerformanceMode ? 2.2f : 0.95f);
        }

        private void SetExpanded(bool expanded)
        {
            if (_expandedRoot == null || _expandedRoot.activeSelf == expanded)
            {
                return;
            }
            _expandedRoot.SetActive(expanded);
            _compactRoot.SetActive(!expanded && hud.IsGameplayVisible);
            hud.SetGameplaySurfaceBlocked(expanded);
            WofInputRouter.SetGameplaySuppressed(expanded);
            Cursor.lockState = expanded ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = expanded;
            _lastRenderedPosition = new Vector3(float.PositiveInfinity, 0f, float.PositiveInfinity);
            _nextRenderAt = 0f;
            if (expanded)
            {
                ResolveLocalPlayer();
                if (_localPlayer != null)
                {
                    _mapCursorNormalized = WofWorldMapExplorationGraphic.GetMarkerNormalized(
                        _localPlayer.transform.position.x,
                        _localPlayer.transform.position.z);
                    _mapFocusNormalized = _mapCursorNormalized;
                }
                UpdateExpandedMapTransform();
                var selection = _destinationButtons.Count > 0
                    ? _destinationButtons[0].gameObject
                    : _closeButton?.gameObject;
                EventSystem.current?.SetSelectedGameObject(selection);
                _mapPanPreservedSelection = selection;
                _mapPanSelectionRestoreFrames = 0;
            }
            else
            {
                EventSystem.current?.SetSelectedGameObject(null);
                _mapPanPreservedSelection = null;
                _mapPanSelectionRestoreFrames = 0;
                _controllerWaypointHeld = false;
                _controllerClearWaypointHeld = false;
            }
            Debug.Log($"[WOF-AUTOMATION] NAVIGATION_MAP expanded={expanded}");
        }

        private void RefreshResponsiveLayout()
        {
            if (Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight)
            {
                return;
            }
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            var canvas = _compactRoot.GetComponentInParent<Canvas>();
            var canvasScale = Mathf.Max(0.01f, canvas == null ? 1f : canvas.scaleFactor);
            var minSide = Mathf.Min(Screen.width, Screen.height);
            var ultraShort = Screen.height <= 260;
            var shortScreen = Screen.height <= 390;
            var narrow = Screen.width <= 430;
            var tallNarrow = narrow && Screen.height >= 470;
            var size = ultraShort
                ? Mathf.Clamp(minSide * 0.32f, 56f, 70f)
                : tallNarrow
                    ? Mathf.Clamp(minSide * 0.25f, 98f, 132f)
                    : shortScreen || narrow
                        ? Mathf.Clamp(minSide * 0.26f, 84f, 112f)
                        : Mathf.Clamp(minSide * 0.27f, 104f, 176f);
            var inset = ultraShort
                ? Mathf.Clamp(minSide * 0.018f, 3f, 8f)
                : Mathf.Clamp(minSide * 0.02f, 8f, 16f);
            var compactRect = _compactRoot.GetComponent<RectTransform>();
            compactRect.sizeDelta = new Vector2(size / canvasScale, size / canvasScale);
            compactRect.anchoredPosition = new Vector2(-inset / canvasScale, -inset / canvasScale);
            SetPhysicalOffsets(_compactShadow, new Vector2(-2f, -6f), new Vector2(6f, 2f), canvasScale);
            SetPhysicalOffsets(_compactBorder, new Vector2(-4f, -4f), new Vector2(4f, 4f), canvasScale);
            if (_compactCoordinatesBadge != null)
            {
                _compactCoordinatesBadge.anchorMin = new Vector2(0.5f, 0f);
                _compactCoordinatesBadge.anchorMax = new Vector2(0.5f, 0f);
                _compactCoordinatesBadge.pivot = new Vector2(0.5f, 0.5f);
                _compactCoordinatesBadge.sizeDelta = new Vector2(Mathf.Clamp(size * 0.72f, 58f, 112f) / canvasScale, 14f / canvasScale);
                _compactCoordinatesBadge.anchoredPosition = new Vector2(0f, -3f / canvasScale);
            }
            if (_compactHintPanel != null)
            {
                var hintGap = Mathf.Clamp(minSide * 0.055f, 26f, 40f);
                _compactHintPanel.anchorMin = Vector2.right;
                _compactHintPanel.anchorMax = Vector2.right;
                _compactHintPanel.pivot = Vector2.one;
                _compactHintPanel.sizeDelta = new Vector2(88f / canvasScale, 18f / canvasScale);
                _compactHintPanel.anchoredPosition = new Vector2(0f, -hintGap / canvasScale);
            }
            var portrait = Screen.height > Screen.width;
            var expandedWidth = portrait
                ? Mathf.Min(Screen.width * 0.92f, Screen.height * 0.52f * FullMapAspectRatio, 1120f)
                : Mathf.Min(Screen.width * 0.92f, Screen.height * 0.76f * FullMapAspectRatio, 1120f);
            var expandedHeight = expandedWidth / FullMapAspectRatio;
            if (_expandedMapFrame != null)
            {
                var mapCenter = portrait ? new Vector2(0.5f, 0.61f) : new Vector2(0.65f, 0.48f);
                _expandedMapFrame.anchorMin = mapCenter;
                _expandedMapFrame.anchorMax = mapCenter;
                _expandedMapFrame.anchoredPosition = Vector2.zero;
                _expandedMapFrame.sizeDelta = new Vector2(expandedWidth / canvasScale, expandedHeight / canvasScale);
            }
            if (_expandedArrow != null)
            {
                _expandedArrow.sizeDelta = new Vector2(30f / canvasScale, 34f / canvasScale);
            }
            if (_compactWaypointArrow != null)
            {
                _compactWaypointArrow.sizeDelta = new Vector2(16f / canvasScale, 18f / canvasScale);
            }
            if (_expandedWaypointMarker != null)
            {
                _expandedWaypointMarker.sizeDelta = new Vector2(24f / canvasScale, 28f / canvasScale);
            }
            if (_expandedMapCursor != null)
            {
                _expandedMapCursor.sizeDelta = new Vector2(32f / canvasScale, 32f / canvasScale);
            }
            foreach (var marker in _locationMarkers)
            {
                marker.sizeDelta = new Vector2(11f / canvasScale, 11f / canvasScale);
            }
            if (_expandedControlsHint != null)
            {
                SetRect(
                    _expandedControlsHint,
                    portrait ? new Vector2(0.04f, 0.355f) : new Vector2(0.31f, 0.025f),
                    portrait ? new Vector2(0.96f, 0.405f) : new Vector2(0.98f, 0.085f));
            }
            if (_destinationPanel != null)
            {
                SetRect(
                    _destinationPanel,
                    portrait ? new Vector2(0.06f, 0.05f) : new Vector2(0.04f, 0.10f),
                    portrait ? new Vector2(0.94f, 0.35f) : new Vector2(0.30f, 0.82f));
                LayoutDestinationButtons(portrait);
            }
            ScaleMapTypographyToPhysicalPixels(_compactRoot, canvasScale, false);
            ScaleMapTypographyToPhysicalPixels(_expandedRoot, canvasScale, true);
            var compassPhysicalSize = ultraShort
                ? Mathf.Clamp(size * 0.13f, 6f, 8f)
                : Mathf.Clamp(size * 0.10f, 7f, 10f);
            foreach (var label in _compactRoot.GetComponentsInChildren<Text>(true))
            {
                if (label.name is "North" or "South" or "East" or "West")
                    label.fontSize = Mathf.Max(6, Mathf.RoundToInt(compassPhysicalSize / canvasScale));
            }
            UpdateExpandedMapTransform();
        }

        private void LayoutDestinationButtons(bool portrait)
        {
            var rowCount = portrait ? Mathf.CeilToInt(_destinationButtons.Count / 2f) : _destinationButtons.Count;
            var availableHeight = portrait ? 0.78f : 0.80f;
            var rowStride = availableHeight / Mathf.Max(1, rowCount);
            var buttonHeight = Mathf.Min(portrait ? 0.20f : 0.098f, rowStride * 0.78f);
            for (var index = 0; index < _destinationButtons.Count; index++)
            {
                var rect = _destinationButtons[index].GetComponent<RectTransform>();
                if (portrait)
                {
                    var column = index % 2;
                    var row = index / 2;
                    var left = 0.04f + column * 0.48f;
                    var top = 0.82f - row * rowStride;
                    SetRect(rect, new Vector2(left, top - buttonHeight), new Vector2(left + 0.44f, top));
                }
                else
                {
                    var top = 0.84f - index * rowStride;
                    SetRect(rect, new Vector2(0.08f, top - buttonHeight), new Vector2(0.92f, top));
                }
            }
        }

        private static void ScaleMapTypographyToPhysicalPixels(GameObject root, float canvasScale, bool expanded)
        {
            if (root == null) return;
            foreach (var label in root.GetComponentsInChildren<Text>(true))
            {
                var physicalSize = label.name switch
                {
                    "Title" => expanded ? 24 : 10,
                    "Coordinates" => expanded ? 13 : 9,
                    "North" or "South" or "East" or "West" => expanded ? 15 : 11,
                    "OpenHint" => 9,
                    "FastTravelTitle" => expanded ? 18 : 10,
                    _ => expanded ? 12 : 9
                };
                label.fontSize = Mathf.Max(8, Mathf.RoundToInt(physicalSize / canvasScale));
            }
        }

        internal static float GetSurvivalBlockCenter(float value)
        {
            return Mathf.Floor((value + WofLilyCoilLayout.SurvivalBlockSize * 0.5f) /
                               WofLilyCoilLayout.SurvivalBlockSize) * WofLilyCoilLayout.SurvivalBlockSize;
        }

        private void CreateCompassLabels(Transform parent, int size)
        {
            var north = CreateText("North", parent, "N", size, TextAnchor.UpperCenter, new Color32(251, 191, 36, 255));
            SetRect(north.rectTransform, new Vector2(0.42f, 0.82f), new Vector2(0.58f, 0.99f));
            AddBlackTextOutline(north);
            var south = CreateText("South", parent, "S", size, TextAnchor.LowerCenter, new Color32(251, 191, 36, 255));
            SetRect(south.rectTransform, new Vector2(0.42f, 0.01f), new Vector2(0.58f, 0.18f));
            AddBlackTextOutline(south);
            var east = CreateText("East", parent, "E", size, TextAnchor.MiddleRight, new Color32(251, 191, 36, 255));
            SetRect(east.rectTransform, new Vector2(0.82f, 0.42f), new Vector2(0.99f, 0.58f));
            AddBlackTextOutline(east);
            var west = CreateText("West", parent, "W", size, TextAnchor.MiddleLeft, new Color32(251, 191, 36, 255));
            SetRect(west.rectTransform, new Vector2(0.01f, 0.42f), new Vector2(0.18f, 0.58f));
            AddBlackTextOutline(west);
        }

        private void CreateLocationMarker(WofMapDestinationRecord record)
        {
            var item = CreateCirclePanel(
                $"Location{record.Destination}",
                _expandedMapContent.transform,
                new Color32(251, 191, 36, 255));
            var rect = item.GetComponent<RectTransform>();
            var normalized = WofWorldMapExplorationGraphic.GetMarkerNormalized(
                record.Position.x,
                record.Position.z);
            rect.anchorMin = normalized;
            rect.anchorMax = normalized;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(11f, 11f);
            var outline = item.AddComponent<Outline>();
            outline.effectColor = new Color32(28, 20, 33, 255);
            outline.effectDistance = new Vector2(2f, -2f);
            var label = CreateText(
                "LocationLabel",
                item.transform,
                record.Label,
                11,
                TextAnchor.MiddleCenter,
                Color.white);
            label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            label.rectTransform.sizeDelta = new Vector2(116f, 18f);
            AddBlackTextOutline(label);
            _locationMarkers.Add(rect);
        }

        private static RectTransform CreateMapCursor(Transform parent)
        {
            var item = new GameObject(
                "WaypointCursor",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(WofMapCursorGraphic));
            item.transform.SetParent(parent, false);
            var graphic = item.GetComponent<WofMapCursorGraphic>();
            graphic.raycastTarget = false;
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(32f, 32f);
            return rect;
        }

        private static RectTransform CreateArrow(string name, Transform parent, Vector2 min, Vector2 max)
        {
            return CreateArrow(name, parent, min, max, new Color32(255, 235, 59, 255));
        }

        private static RectTransform CreateArrow(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(WofMinimapArrowGraphic));
            item.transform.SetParent(parent, false);
            var graphic = item.GetComponent<WofMinimapArrowGraphic>();
            graphic.raycastTarget = false;
            graphic.color = color;
            var rect = item.GetComponent<RectTransform>();
            SetRect(rect, min, max);
            return rect;
        }

        private static RectTransform CreateWaypointHat(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var item = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(WofWizardHatWaypointGraphic));
            item.transform.SetParent(parent, false);
            var graphic = item.GetComponent<WofWizardHatWaypointGraphic>();
            graphic.raycastTarget = false;
            var rect = item.GetComponent<RectTransform>();
            SetRect(rect, min, max);
            return rect;
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            item.GetComponent<Image>().color = color;
            return item;
        }

        private GameObject CreateCirclePanel(string name, Transform parent, Color color)
        {
            var item = CreatePanel(name, parent, color);
            var image = item.GetComponent<Image>();
            image.sprite = circularMaskSprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            return item;
        }

        private RawImage CreateRawImage(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(6, size / 2);
            text.resizeTextMaxSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Color color)
        {
            var item = CreatePanel(name, parent, color);
            var button = item.AddComponent<Button>();
            button.targetGraphic = item.GetComponent<Image>();
            var text = CreateText("Label", item.transform, label, 14, TextAnchor.MiddleCenter, Color.white);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetPhysicalOffsets(RectTransform rect, Vector2 min, Vector2 max, float canvasScale)
        {
            if (rect == null) return;
            rect.offsetMin = min / canvasScale;
            rect.offsetMax = max / canvasScale;
        }

        private static void AddBlackTextOutline(Text text)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private void SaveExplorationNow()
        {
            if (!_explorationSavePending || _worldMapExploration == null) return;
            PlayerPrefs.SetString(ExplorationPlayerPrefsKey, _worldMapExploration.ExportExploredCells());
            PlayerPrefs.Save();
            _explorationSavePending = false;
        }
    }
}
