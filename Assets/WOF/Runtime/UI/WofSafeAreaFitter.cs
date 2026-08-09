using UnityEngine;

namespace WOF
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class WofSafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            ApplyIfChanged(force: true);
        }

        private void Update()
        {
            ApplyIfChanged(force: false);
        }

        internal static Rect CalculateNormalizedAnchors(Rect safeArea, Vector2 screenSize)
        {
            if (!IsFinite(screenSize.x) || !IsFinite(screenSize.y) ||
                screenSize.x <= 0f || screenSize.y <= 0f ||
                !IsFinite(safeArea.xMin) || !IsFinite(safeArea.yMin) ||
                !IsFinite(safeArea.xMax) || !IsFinite(safeArea.yMax) ||
                safeArea.width <= 0f || safeArea.height <= 0f)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            var xMin = Mathf.Clamp01(safeArea.xMin / screenSize.x);
            var yMin = Mathf.Clamp01(safeArea.yMin / screenSize.y);
            var xMax = Mathf.Clamp01(safeArea.xMax / screenSize.x);
            var yMax = Mathf.Clamp01(safeArea.yMax / screenSize.y);
            if (xMax <= xMin || yMax <= yMin)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void ApplyIfChanged(bool force)
        {
            var safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (!force && safeArea == _lastSafeArea && screenSize == _lastScreenSize)
            {
                return;
            }

            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
            _rectTransform ??= GetComponent<RectTransform>();
            var anchors = CalculateNormalizedAnchors(safeArea, screenSize);
            _rectTransform.anchorMin = anchors.min;
            _rectTransform.anchorMax = anchors.max;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
