using UnityEngine;

namespace WOF
{
    public sealed class WofLaunchStageLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform savePanel;
        [SerializeField] private RectTransform newWizardPanel;
        [SerializeField] private RectTransform multiplayerPanel;
        [SerializeField] private RectTransform lobbyPanel;
        [SerializeField] private RectTransform newTitle;
        [SerializeField] private RectTransform preview;
        [SerializeField] private RectTransform playerName;
        [SerializeField] private RectTransform xpCard;
        [SerializeField] private RectTransform[] optionCards;
        [SerializeField] private RectTransform[] actionButtons;

        private Canvas _canvas;
        private Vector2Int _lastScreenSize;
        private float _lastScaleFactor;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            Apply();
        }

        private void LateUpdate()
        {
            var scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (screenSize != _lastScreenSize || !Mathf.Approximately(scaleFactor, _lastScaleFactor))
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            var scaleFactor = Mathf.Max(0.0001f, _canvas != null ? _canvas.scaleFactor : 1f);
            var mobile = Screen.width < 768;
            ConfigureCenteredPanel(savePanel, Mathf.Min(Screen.width * 0.92f, 720f), Mathf.Min(Screen.height - 24f, 300f), scaleFactor);
            ConfigureCenteredPanel(multiplayerPanel, Mathf.Min(Screen.width * 0.92f, 720f), Mathf.Min(Screen.height - 24f, 300f), scaleFactor);
            ConfigureCenteredPanel(lobbyPanel, Mathf.Min(Screen.width * 0.92f, 720f), Mathf.Min(Screen.height - 24f, 480f), scaleFactor);
            ConfigureCenteredPanel(
                newWizardPanel,
                Mathf.Min(Screen.width * 0.94f, 900f),
                Mathf.Min(Screen.height - 24f, mobile ? 800f : 520f),
                scaleFactor);

            if (mobile)
            {
                SetNormalized(newTitle, new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.99f));
                SetNormalized(preview, new Vector2(0.28f, 0.72f), new Vector2(0.72f, 0.91f));
                SetNormalized(playerName, new Vector2(0.04f, 0.64f), new Vector2(0.96f, 0.71f));
                ConfigureGrid(optionCards, 2, new Rect(0.04f, 0.35f, 0.92f, 0.27f), 0.02f, 0.018f);
                SetNormalized(xpCard, new Vector2(0.04f, 0.26f), new Vector2(0.96f, 0.32f));
                ConfigureVertical(actionButtons, new Rect(0.04f, 0.03f, 0.92f, 0.20f), 0.012f);
            }
            else
            {
                SetNormalized(newTitle, new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.98f));
                SetNormalized(preview, new Vector2(0.02f, 0.32f), new Vector2(0.27f, 0.84f));
                SetNormalized(playerName, new Vector2(0.31f, 0.73f), new Vector2(0.98f, 0.84f));
                ConfigureGrid(optionCards, 2, new Rect(0.31f, 0.34f, 0.67f, 0.35f), 0.018f, 0.025f);
                SetNormalized(xpCard, new Vector2(0.31f, 0.25f), new Vector2(0.98f, 0.31f));
                ConfigureHorizontal(actionButtons, new Rect(0.02f, 0.04f, 0.96f, 0.21f), 0.015f);
            }

            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastScaleFactor = scaleFactor;
        }

        private static void ConfigureCenteredPanel(RectTransform panel, float widthPixels, float heightPixels, float scaleFactor)
        {
            if (panel == null)
            {
                return;
            }

            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(widthPixels / scaleFactor, heightPixels / scaleFactor);
        }

        private static void ConfigureGrid(RectTransform[] cards, int columns, Rect bounds, float horizontalGap, float verticalGap)
        {
            if (cards == null || cards.Length == 0)
            {
                return;
            }

            var rows = Mathf.CeilToInt(cards.Length / (float)columns);
            var cellWidth = (bounds.width - horizontalGap * (columns - 1)) / columns;
            var cellHeight = (bounds.height - verticalGap * (rows - 1)) / rows;
            for (var index = 0; index < cards.Length; index++)
            {
                var column = index % columns;
                var row = index / columns;
                var x = bounds.x + column * (cellWidth + horizontalGap);
                var yTop = bounds.y + bounds.height - row * (cellHeight + verticalGap);
                SetNormalized(cards[index], new Vector2(x, yTop - cellHeight), new Vector2(x + cellWidth, yTop));
            }
        }

        private static void ConfigureHorizontal(RectTransform[] items, Rect bounds, float gap)
        {
            if (items == null || items.Length == 0)
            {
                return;
            }

            var width = (bounds.width - gap * (items.Length - 1)) / items.Length;
            for (var index = 0; index < items.Length; index++)
            {
                var x = bounds.x + index * (width + gap);
                SetNormalized(items[index], new Vector2(x, bounds.y), new Vector2(x + width, bounds.y + bounds.height));
            }
        }

        private static void ConfigureVertical(RectTransform[] items, Rect bounds, float gap)
        {
            if (items == null || items.Length == 0)
            {
                return;
            }

            var height = (bounds.height - gap * (items.Length - 1)) / items.Length;
            for (var index = 0; index < items.Length; index++)
            {
                var yTop = bounds.y + bounds.height - index * (height + gap);
                SetNormalized(items[index], new Vector2(bounds.x, yTop - height), new Vector2(bounds.x + bounds.width, yTop));
            }
        }

        private static void SetNormalized(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
