using UnityEngine;

namespace WOF
{
    public sealed class WofPerformanceVariantGroup : MonoBehaviour
    {
        [SerializeField] private bool mobileOnly;

        public void Configure(bool isMobileOnly)
        {
            mobileOnly = isMobileOnly;
        }

        private void Awake()
        {
            gameObject.SetActive(mobileOnly == WofPerformanceModeRuntime.IsMobilePerformanceMode);
        }
    }
}
