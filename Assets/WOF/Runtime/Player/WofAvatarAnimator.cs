using UnityEngine;

namespace WOF
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WofAvatarAnimator : MonoBehaviour
    {
        private const int DirectionCount = 8;
        private const int FramesPerDirection = 4;

        [SerializeField] private WofPlayerController player;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] private Sprite[] sprintFrames;
        [SerializeField] private Sprite[] slideFrames;
        [SerializeField] private Sprite[] crouchFrames;
        [SerializeField] private Sprite[] crouchWalkFrames;
        [SerializeField] private Sprite[] jumpFrames;
        [SerializeField] private Sprite[] castingFrames;
        [SerializeField] private Sprite[] meditateFrames;
        [SerializeField] private Sprite[] damagedFrames;

        private SpriteRenderer _renderer;
        private Vector3 _lastWorldPosition;
        private float _nextFrameAt;
        private int _frame;
        private string _animation = "idle";

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (player == null)
            {
                player = GetComponentInParent<WofPlayerController>();
            }

            _lastWorldPosition = transform.position;
        }

        private void OnEnable()
        {
            _lastWorldPosition = transform.position;
            _frame = 0;
            _nextFrameAt = 0f;
        }

        private void LateUpdate()
        {
            if (_renderer == null || player == null)
            {
                return;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                var toCamera = camera.transform.position - transform.position;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
                }
            }

            var worldPosition = transform.position;
            var horizontalDelta = worldPosition - _lastWorldPosition;
            horizontalDelta.y = 0f;
            _lastWorldPosition = worldPosition;

            var nextAnimation = ResolveAnimation(horizontalDelta);
            if (!string.Equals(nextAnimation, _animation, System.StringComparison.Ordinal))
            {
                var previousAnimation = _animation;
                _animation = nextAnimation;
                _frame = 0;
                _nextFrameAt = 0f;
                if (!player.IsOwner &&
                    (string.Equals(nextAnimation, "meditate", System.StringComparison.Ordinal) ||
                     string.Equals(previousAnimation, "meditate", System.StringComparison.Ordinal)))
                {
                    Debug.Log(
                        $"[WOF-AUTOMATION] REMOTE_AVATAR_ANIMATION owner={player.OwnerClientId} " +
                        $"animation={nextAnimation} frameDelay={ResolveFrameDelay(nextAnimation):F3}");
                }
            }

            var delay = ResolveFrameDelay(_animation);
            if (Time.unscaledTime >= _nextFrameAt)
            {
                _nextFrameAt = Time.unscaledTime + delay;
                _frame = (_frame + 1) % FramesPerDirection;
            }

            var direction = ResolveDirection(camera);
            var frames = ResolveFrames(_animation);
            var index = direction * FramesPerDirection + _frame;
            if (frames != null && index >= 0 && index < frames.Length && frames[index] != null)
            {
                _renderer.sprite = frames[index];
            }

            transform.localScale = player.IsDead ? new Vector3(0.9f, 0.9f, 1f) : Vector3.one;
            transform.localPosition = player.IsDead ? new Vector3(0f, 0.06f, 0f) : new Vector3(0f, 0.62f, 0f);
        }

        private string ResolveAnimation(Vector3 horizontalDelta)
        {
            if (player.IsDead)
            {
                return "damaged";
            }
            if (player.IsMeditating)
            {
                return "meditate";
            }
            if (player.IsSliding)
            {
                return "slide";
            }
            if (player.IsCrouching)
            {
                return horizontalDelta.sqrMagnitude > 0.000004f ? "crouchwalk" : "crouch";
            }
            if (!player.IsGrounded)
            {
                return "jump";
            }
            if (player.IsCasting)
            {
                return "casting";
            }
            if (horizontalDelta.sqrMagnitude > 0.000004f)
            {
                return player.IsSprinting ? "sprint" : "walk";
            }
            return "idle";
        }

        private int ResolveDirection(Camera camera)
        {
            if (camera == null)
            {
                return 0;
            }

            var offset = camera.transform.position - player.transform.position;
            var toCamera = Mathf.Atan2(offset.x, -offset.z);
            var playerYaw = player.transform.eulerAngles.y * Mathf.Deg2Rad;
            var relative = Mathf.Repeat(toCamera - playerYaw, Mathf.PI * 2f);
            return Mathf.RoundToInt(relative / (Mathf.PI / 4f)) % DirectionCount;
        }

        private Sprite[] ResolveFrames(string animation)
        {
            return animation switch
            {
                "walk" => walkFrames,
                "sprint" => sprintFrames,
                "slide" => slideFrames,
                "crouch" => crouchFrames,
                "crouchwalk" => crouchWalkFrames,
                "jump" => jumpFrames,
                "casting" => castingFrames,
                "meditate" => meditateFrames,
                "damaged" => damagedFrames,
                _ => idleFrames
            };
        }

        private static float ResolveFrameDelay(string animation)
        {
            return animation switch
            {
                "sprint" => 0.07f,
                "slide" => 0.07f,
                "crouchwalk" => 0.135f,
                "crouch" => 0.22f,
                "jump" => 0.095f,
                "walk" => 0.12f,
                "casting" => 0.12f,
                "meditate" => WofAstralMeditationRules.AvatarFrameDelaySeconds,
                "damaged" => 0.36f,
                _ => 0.21f
            };
        }
    }
}
