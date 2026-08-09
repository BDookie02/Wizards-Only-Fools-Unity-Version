using UnityEngine;
using UnityEngine.EventSystems;

namespace WOF
{
    public enum WofMobileAction
    {
        CastLeft,
        CastRight,
        Jump
    }

    public sealed class WofMobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private WofMobileAction action;
        private int? _activePointerId;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId.HasValue)
            {
                return;
            }

            _activePointerId = eventData.pointerId;
            if (action == WofMobileAction.CastLeft)
            {
                WofInputRouter.QueueMobileCast(WofHandSide.Left);
            }
            else if (action == WofMobileAction.CastRight)
            {
                WofInputRouter.QueueMobileCast(WofHandSide.Right);
            }
            else
            {
                WofInputRouter.SetMobileJump(true);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_activePointerId != eventData.pointerId)
            {
                return;
            }

            _activePointerId = null;
            if (action == WofMobileAction.Jump)
            {
                WofInputRouter.SetMobileJump(false);
            }
        }

        private void OnDisable()
        {
            _activePointerId = null;
            if (action == WofMobileAction.Jump)
            {
                WofInputRouter.SetMobileJump(false);
            }
        }
    }
}
