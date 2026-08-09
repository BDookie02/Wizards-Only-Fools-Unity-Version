using Unity.Netcode;
using UnityEngine;

namespace WOF
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class WofFireballProjectile : NetworkBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] frames;

        private readonly NetworkVariable<Vector3> _direction = new(
            Vector3.forward,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> _sourceClientId = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private double _despawnAt;
        private float _animationClock;
        private int _frameIndex;

        public void InitializeServer(ulong sourceClientId, Vector3 direction)
        {
            _sourceClientId.Value = sourceClientId;
            _direction.Value = direction.normalized;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _despawnAt = NetworkManager.ServerTime.Time + WofGameConstants.FireballLifetimeSeconds;
            }
        }

        private void Update()
        {
            AnimateSprite();
            if (!IsServer)
            {
                FaceCamera();
                return;
            }

            if (NetworkManager.ServerTime.Time >= _despawnAt)
            {
                NetworkObject.Despawn(true);
                return;
            }

            var direction = _direction.Value.normalized;
            var distance = WofGameConstants.FireballSpeed * Time.deltaTime;
            if (Physics.SphereCast(transform.position, 0.32f, direction, out var hit, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                var player = hit.collider.GetComponentInParent<WofPlayerController>();
                if (player == null || player.OwnerClientId != _sourceClientId.Value)
                {
                    player?.ApplyServerDamage(WofGameConstants.FireballDamage, _sourceClientId.Value);
                    NetworkObject.Despawn(true);
                    return;
                }
            }

            transform.position += direction * distance;
        }

        private void AnimateSprite()
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            _animationClock += Time.deltaTime;
            if (_animationClock >= 0.1f)
            {
                _animationClock -= 0.1f;
                _frameIndex = (_frameIndex + 1) % frames.Length;
                spriteRenderer.sprite = frames[_frameIndex];
            }
        }

        private void FaceCamera()
        {
            var camera = Camera.main;
            if (camera != null && spriteRenderer != null)
            {
                spriteRenderer.transform.rotation = Quaternion.LookRotation(spriteRenderer.transform.position - camera.transform.position);
            }
        }
    }
}

