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
        private const float ExpandedOrthographicSize = WofLilyCoilLayout.SurvivalBlockSize * 0.5f;

        [SerializeField] private WofHud hud;
        [SerializeField] private Transform uiParent;
        [SerializeField] private Font font;
        [SerializeField] private Sprite circularMaskSprite;

        private GameObject _compactRoot;
        private GameObject _expandedRoot;
        private RectTransform _expandedMapFrame;
        private RectTransform _expandedArrow;
        private RectTransform _compactArrow;
        private Text _compactCoordinates;
        private Text _expandedCoordinates;
        private Camera _mapCamera;
        private RenderTexture _mapTexture;
        private WofPlayerController _localPlayer;
        private float _nextPlayerResolveAt;
        private float _nextRenderAt;
        private Vector3 _lastRenderedPosition = new(float.PositiveInfinity, 0f, float.PositiveInfinity);
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private bool _controllerToggleHeld;
        private bool _controllerBackHeld;

        public static WofNavigationMapRuntime Instance { get; private set; }
        public static bool IsExpanded => Instance != null && Instance._expandedRoot != null &&
                                         Instance._expandedRoot.activeSelf;

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
        }

        private void Start()
        {
            BuildView();
            BuildMapCamera();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_mapTexture != null)
            {
                _mapTexture.Release();
                Destroy(_mapTexture);
            }
            if (_mapCamera != null) Destroy(_mapCamera.gameObject);
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
            _compactRoot.SetActive(gameplayVisible && !IsExpanded && !spellMenuOpen);

            if (!gameplayVisible || spellMenuOpen)
            {
                return;
            }

            var keyboardToggle = Keyboard.current?.mKey.wasPressedThisFrame ?? false;
            var gamepad = Gamepad.current;
            var controllerToggleHeld = gamepad?.dpad.left.isPressed ?? false;
            var controllerToggle = controllerToggleHeld && !_controllerToggleHeld;
            _controllerToggleHeld = controllerToggleHeld;
            if (keyboardToggle || (controllerToggle && !IsExpanded))
            {
                SetExpanded(!IsExpanded);
                return;
            }
            var controllerBackHeld = gamepad?.buttonEast.isPressed ?? false;
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

            UpdateMarkers(_localPlayer.transform.position, _localPlayer.transform.eulerAngles.y);
            RenderMapIfDue(_localPlayer.transform.position, IsExpanded);
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
                new Color32(0, 0, 0, 230));
            var compactRect = _compactRoot.GetComponent<RectTransform>();
            compactRect.anchorMin = Vector2.one;
            compactRect.anchorMax = Vector2.one;
            compactRect.pivot = Vector2.one;
            compactRect.anchoredPosition = new Vector2(-16f, -16f);
            compactRect.sizeDelta = new Vector2(176f, 176f);
            var compactImage = _compactRoot.GetComponent<Image>();
            compactImage.sprite = circularMaskSprite;
            compactImage.type = Image.Type.Simple;
            var mask = _compactRoot.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var compactButton = _compactRoot.AddComponent<Button>();
            compactButton.targetGraphic = compactImage;
            compactButton.onClick.AddListener(ToggleExpanded);

            var compactMap = CreateRawImage("LiveMap", _compactRoot.transform);
            SetRect(compactMap.rectTransform, new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.965f));
            CreateCompassLabels(_compactRoot.transform, 10);
            _compactArrow = CreateArrow("PlayerArrow", _compactRoot.transform, new Vector2(0.40f, 0.36f), new Vector2(0.60f, 0.64f));
            _compactCoordinates = CreateText(
                "Coordinates",
                _compactRoot.transform,
                "X:0 Z:0",
                8,
                TextAnchor.MiddleCenter,
                Color.white);
            SetRect(_compactCoordinates.rectTransform, new Vector2(0.20f, 0.02f), new Vector2(0.80f, 0.16f));

            var hint = CreateText(
                "OpenHint",
                uiParent,
                "TAP MAP / M",
                8,
                TextAnchor.MiddleCenter,
                new Color32(136, 136, 136, 255));
            hint.transform.SetParent(_compactRoot.transform, false);
            SetRect(hint.rectTransform, new Vector2(0.36f, -0.17f), new Vector2(1f, -0.02f));

            _expandedRoot = CreatePanel(
                "ExpandedNavigationMap",
                uiParent,
                new Color32(0, 0, 0, 188));
            SetRect(_expandedRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            var title = CreateText(
                "Title",
                _expandedRoot.transform,
                "LIVE MAP",
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
            var close = CreateButton(
                "Close",
                _expandedRoot.transform,
                "CLOSE",
                new Color32(85, 85, 85, 255));
            SetRect(close.GetComponent<RectTransform>(), new Vector2(0.82f, 0.89f), new Vector2(0.96f, 0.97f));
            close.onClick.AddListener(() => SetExpanded(false));

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
            var expandedMap = CreateRawImage("LiveMap", mapFrame.transform);
            SetRect(expandedMap.rectTransform, new Vector2(0.008f, 0.008f), new Vector2(0.992f, 0.992f));
            CreateMapGrid(mapFrame.transform);
            CreateCompassLabels(mapFrame.transform, 15);
            _expandedArrow = CreateArrow("PlayerArrow", mapFrame.transform, new Vector2(0.47f, 0.46f), new Vector2(0.53f, 0.54f));

            _expandedRoot.SetActive(false);
            _compactRoot.SetActive(false);
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
            _mapCamera.backgroundColor = new Color32(9, 5, 16, 255);
            _mapCamera.allowHDR = false;
            _mapCamera.allowMSAA = false;
            _mapCamera.targetTexture = _mapTexture;

            foreach (var rawImage in GetComponentsInChildren<RawImage>(true))
            {
                if (rawImage.name == "LiveMap") rawImage.texture = _mapTexture;
            }
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
            if (_expandedCoordinates != null) _expandedCoordinates.text = coordinateLabel;
            if (_compactArrow != null) _compactArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            if (_expandedArrow != null)
            {
                _expandedArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
                var centerX = GetSurvivalBlockCenter(position.x);
                var centerZ = GetSurvivalBlockCenter(position.z);
                var normalizedX = Mathf.Clamp01(0.5f + (position.x - centerX) / WofLilyCoilLayout.SurvivalBlockSize);
                var normalizedY = Mathf.Clamp01(0.5f + (position.z - centerZ) / WofLilyCoilLayout.SurvivalBlockSize);
                _expandedArrow.anchorMin = new Vector2(normalizedX - 0.03f, normalizedY - 0.04f);
                _expandedArrow.anchorMax = new Vector2(normalizedX + 0.03f, normalizedY + 0.04f);
                _expandedArrow.offsetMin = Vector2.zero;
                _expandedArrow.offsetMax = Vector2.zero;
            }
        }

        private void RenderMapIfDue(Vector3 position, bool expanded)
        {
            if (_mapCamera == null || Time.unscaledTime < _nextRenderAt)
            {
                return;
            }
            var threshold = expanded ? 3.5f : 1.75f;
            if ((position - _lastRenderedPosition).sqrMagnitude < threshold * threshold &&
                _mapCamera.orthographicSize == (expanded ? ExpandedOrthographicSize : CompactOrthographicSize))
            {
                _nextRenderAt = Time.unscaledTime + (WofPerformanceModeRuntime.IsMobilePerformanceMode
                    ? expanded ? 0.9f : 2.2f
                    : expanded ? 0.42f : 0.95f);
                return;
            }

            var centerX = expanded ? GetSurvivalBlockCenter(position.x) : position.x;
            var centerZ = expanded ? GetSurvivalBlockCenter(position.z) : position.z;
            _mapCamera.orthographicSize = expanded ? ExpandedOrthographicSize : CompactOrthographicSize;
            _mapCamera.transform.position = new Vector3(centerX, position.y + 420f, centerZ);
            _mapCamera.Render();
            _lastRenderedPosition = position;
            _nextRenderAt = Time.unscaledTime + (WofPerformanceModeRuntime.IsMobilePerformanceMode
                ? expanded ? 0.9f : 2.2f
                : expanded ? 0.42f : 0.95f);
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
                EventSystem.current?.SetSelectedGameObject(_expandedRoot.transform.Find("Close")?.gameObject);
            }
            else
            {
                EventSystem.current?.SetSelectedGameObject(null);
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
            var size = minSide <= 430
                ? Mathf.Clamp(minSide * 0.26f, 84f, 112f)
                : Mathf.Clamp(minSide * 0.27f, 104f, 176f);
            var compactRect = _compactRoot.GetComponent<RectTransform>();
            compactRect.sizeDelta = new Vector2(size / canvasScale, size / canvasScale);
            compactRect.anchoredPosition = new Vector2(-16f / canvasScale, -16f / canvasScale);
            var expandedSize = Mathf.Min(Screen.width * 0.80f, Screen.height * 0.76f, 800f);
            if (_expandedMapFrame != null)
            {
                _expandedMapFrame.sizeDelta = new Vector2(expandedSize / canvasScale, expandedSize / canvasScale);
            }
            ScaleMapTypographyToPhysicalPixels(_compactRoot, canvasScale, false);
            ScaleMapTypographyToPhysicalPixels(_expandedRoot, canvasScale, true);
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
            var south = CreateText("South", parent, "S", size, TextAnchor.LowerCenter, new Color32(251, 191, 36, 255));
            SetRect(south.rectTransform, new Vector2(0.42f, 0.01f), new Vector2(0.58f, 0.18f));
            var east = CreateText("East", parent, "E", size, TextAnchor.MiddleRight, new Color32(251, 191, 36, 255));
            SetRect(east.rectTransform, new Vector2(0.82f, 0.42f), new Vector2(0.99f, 0.58f));
            var west = CreateText("West", parent, "W", size, TextAnchor.MiddleLeft, new Color32(251, 191, 36, 255));
            SetRect(west.rectTransform, new Vector2(0.01f, 0.42f), new Vector2(0.18f, 0.58f));
        }

        private void CreateMapGrid(Transform parent)
        {
            for (var index = 1; index < 8; index++)
            {
                var amount = index / 8f;
                var vertical = CreatePanel($"GridVertical{index}", parent, new Color32(210, 238, 255, 35));
                SetRect(vertical.GetComponent<RectTransform>(), new Vector2(amount - 0.001f, 0f), new Vector2(amount + 0.001f, 1f));
                vertical.GetComponent<Image>().raycastTarget = false;
                var horizontal = CreatePanel($"GridHorizontal{index}", parent, new Color32(210, 238, 255, 35));
                SetRect(horizontal.GetComponent<RectTransform>(), new Vector2(0f, amount - 0.001f), new Vector2(1f, amount + 0.001f));
                horizontal.GetComponent<Image>().raycastTarget = false;
            }
        }

        private static RectTransform CreateArrow(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(WofMinimapArrowGraphic));
            item.transform.SetParent(parent, false);
            var graphic = item.GetComponent<WofMinimapArrowGraphic>();
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
    }
}
