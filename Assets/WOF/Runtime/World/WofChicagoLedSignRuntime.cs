using UnityEngine;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofChicagoLedSignRuntime : MonoBehaviour
    {
        [SerializeField] private Transform revolvingSign;
        [SerializeField] private Renderer ledRenderer;
        [SerializeField] private float updateInterval = 1f / 24f;
        private float _lastUpdate = float.NegativeInfinity;

        public void Configure(Transform sign, Renderer renderer, float interval)
        {
            revolvingSign = sign;
            ledRenderer = renderer;
            updateInterval = interval;
        }

        private void Update()
        {
            var elapsed = Time.timeSinceLevelLoad;
            if (elapsed - _lastUpdate < updateInterval) return;
            _lastUpdate = elapsed;
            if (revolvingSign != null)
            {
                revolvingSign.localRotation = Quaternion.Euler(0f, elapsed * 0.55f * Mathf.Rad2Deg, 0f);
            }
            if (ledRenderer != null && ledRenderer.sharedMaterial != null)
            {
                var material = ledRenderer.sharedMaterial;
                var offset = new Vector2(-elapsed * 0.12f, 0f);
                if (material.HasProperty("_BaseMap")) material.SetTextureOffset("_BaseMap", offset);
                if (material.HasProperty("_MainTex")) material.SetTextureOffset("_MainTex", offset);
            }
        }
    }
}
