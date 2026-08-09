using UnityEngine;
using UnityEngine.EventSystems;

namespace WOF
{
    public sealed class WofMobileLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private float sensitivity = 0.035f;
        private int? _activePointerId;

        public void OnPointerDown(PointerEventData eventData)
        {
            _activePointerId ??= eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_activePointerId != eventData.pointerId)
            {
                return;
            }

            WofInputRouter.AddMobileLook(eventData.delta * sensitivity);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_activePointerId == eventData.pointerId)
            {
                _activePointerId = null;
            }
        }

        private void OnDisable()
        {
            _activePointerId = null;
        }
    }
}
