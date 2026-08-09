using UnityEngine;

namespace WOF
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WofMountainLadderZone : MonoBehaviour
    {
        public const float ClimbSpeed = 8.4f;
        public const float PlanarDamping = 0.22f;

        [SerializeField] private string zoneId;

        public void Configure(string value)
        {
            zoneId = value;
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<WofPlayerController>();
            if (player != null) player.SetMountainLadderZone(zoneId, true);
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<WofPlayerController>();
            if (player != null) player.SetMountainLadderZone(zoneId, false);
        }
    }
}
