using UnityEngine;
using UnityEngine.UI;

namespace WOF
{
    /// <summary>
    /// Converts the canonical launch screen's CSS clamp/vmin/flex measurements
    /// into physical-pixel UI positions, independent of CanvasScaler or DPI.
    /// </summary>
    public sealed class WofLaunchPressLayout : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private Text prompt;
        [SerializeField] private Text controllerPrompt;

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
            var minimumViewport = Mathf.Min(Screen.width, Screen.height);
            var titlePixels = Mathf.Clamp(minimumViewport * 0.08f, 32f, 88f);
            var promptPixels = Mathf.Clamp(minimumViewport * 0.03f, 15.2f, 26.4f);
            var controllerPixels = Mathf.Clamp(minimumViewport * 0.018f, 10.4f, 14.4f);
            var titleHeight = titlePixels * 0.85f * 3f;
            var promptHeight = promptPixels * 1.5f;
            var controllerHeight = controllerPixels * 1.5f;
            var totalHeight = titleHeight + 32f + promptHeight + 12f + controllerHeight;
            var top = (Screen.height - totalHeight) * 0.5f;
            var contentWidth = Mathf.Max(1f, Screen.width - 32f);

            ConfigureText(title, titlePixels, contentWidth, titleHeight, top, scaleFactor);
            top += titleHeight + 32f;
            ConfigureText(prompt, promptPixels, contentWidth, promptHeight, top, scaleFactor);
            top += promptHeight + 12f;
            ConfigureText(controllerPrompt, controllerPixels, contentWidth, controllerHeight, top, scaleFactor);

            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastScaleFactor = scaleFactor;
        }

        private static void ConfigureText(
            Text text,
            float fontPixels,
            float widthPixels,
            float heightPixels,
            float topPixels,
            float scaleFactor)
        {
            if (text == null)
            {
                return;
            }

            text.resizeTextForBestFit = false;
            // Unity's legacy Text truncates the third Press Start 2P line even
            // when the CSS-equivalent line-height fits. The browser allows the
            // glyph extents to exceed the line box, so match that behavior.
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(fontPixels / scaleFactor));
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(widthPixels / scaleFactor, heightPixels / scaleFactor);
            rect.anchoredPosition = new Vector2(0f, -(topPixels + heightPixels * 0.5f) / scaleFactor);
        }
    }
}
