using UnityEngine;
using UnityEngine.EventSystems;

namespace WOF
{
    public sealed class WofVirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform handle;
        private RectTransform _baseRect;
        private int? _activePointerId;

        private void Awake()
        {
            _baseRect = transform as RectTransform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId.HasValue)
            {
                return;
            }

            _activePointerId = eventData.pointerId;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_activePointerId != eventData.pointerId || _baseRect == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _baseRect, eventData.position, eventData.pressEventCamera, out var local))
            {
                return;
            }

            var radius = Mathf.Max(1f, Mathf.Min(_baseRect.rect.width, _baseRect.rect.height) * 0.36f);
            var value = WofInputRouter.ResolveJoystickValue(local, radius);
            WofInputRouter.SetMobileMove(value);
            if (handle != null)
            {
                handle.anchoredPosition = value * radius;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_activePointerId != eventData.pointerId)
            {
                return;
            }

            ReleasePointer();
        }

        private void OnDisable()
        {
            ReleasePointer();
        }

        private void ReleasePointer()
        {
            _activePointerId = null;
            WofInputRouter.SetMobileMove(Vector2.zero);
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }
    }
}
