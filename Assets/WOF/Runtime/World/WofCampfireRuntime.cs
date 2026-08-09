using Unity.Netcode;
using UnityEngine;

namespace WOF
{
    public sealed class WofCampfireRuntime : MonoBehaviour
    {
        private const ulong EnvironmentalDamageSource = ulong.MaxValue;

        [SerializeField] private Transform fireLightTransform;
        [SerializeField] private Light fireLight;

        private float _nextDamageAt;

        public void Configure(Transform lightTransform, Light light)
        {
            fireLightTransform = lightTransform;
            fireLight = light;
        }

        private void OnEnable()
        {
            _nextDamageAt = Time.time + WofBaseVillageLayout.CampfireDamageTickSeconds;
        }

        private void Update()
        {
            ApplyFlicker(Time.timeSinceLevelLoad);

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer || Time.time < _nextDamageAt)
            {
                return;
            }

            _nextDamageAt = Time.time + WofBaseVillageLayout.CampfireDamageTickSeconds;
            foreach (var client in networkManager.ConnectedClientsList)
            {
                var playerObject = client.PlayerObject;
                var player = playerObject == null ? null : playerObject.GetComponent<WofPlayerController>();
                if (player == null || player.IsDead ||
                    !WofBaseVillageLayout.IsWithinCampfireDamageRadius(player.DamageProbePosition))
                {
                    continue;
                }

                player.ApplyServerDamage(
                    WofBaseVillageLayout.CampfireDamagePerTick,
                    EnvironmentalDamageSource);
            }
        }

        internal void ApplyFlicker(float elapsedSeconds)
        {
            var flicker = WofBaseVillageLayout.GetCampfireFlicker(elapsedSeconds);
            if (fireLight != null)
            {
                fireLight.intensity = flicker.Intensity;
            }

            if (fireLightTransform != null)
            {
                var localPosition = fireLightTransform.localPosition;
                localPosition.y = flicker.LightY;
                fireLightTransform.localPosition = localPosition;
            }
        }
    }
}
