using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    public sealed class WofGameWorldLightingRuntime : MonoBehaviour
    {
        [SerializeField] private Light directionalLight;

        public void Configure(Light light)
        {
            directionalLight = light;
        }

        private void Awake()
        {
            if (!WofPerformanceModeRuntime.IsMobilePerformanceMode)
            {
                if (directionalLight != null)
                {
                    directionalLight.intensity = WofGameWorldLightingLayout.ClassicDirectionalIntensity;
                }
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = WofGameWorldLightingLayout.GetClassicAmbientColor();
                return;
            }

            if (directionalLight != null)
            {
                directionalLight.intensity = WofGameWorldLightingLayout.MobileDirectionalIntensity;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = WofGameWorldLightingLayout.GetMobileAmbientSkyColor();
            RenderSettings.ambientEquatorColor = WofGameWorldLightingLayout.GetMobileAmbientEquatorColor();
            RenderSettings.ambientGroundColor = WofGameWorldLightingLayout.GetMobileAmbientGroundColor();
        }
    }
}
