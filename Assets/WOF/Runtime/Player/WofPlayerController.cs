using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WOF
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class WofPlayerController : NetworkBehaviour
    {
        private static readonly Vector3 VillagerViewProbeSpawn = new(43f, 5f, 64f);
        private const float VillagerViewProbeYaw = 90f;
        private const float VillagerViewProbePitch = 0f;
        private static readonly Vector3 DarrelDialogProbeSpawn = new(-64f, 2f, -53f);
        private const float DarrelDialogProbeYaw = 0f;
        private const float DarrelDialogProbePitch = 0f;

        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener playerAudioListener;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private GameObject fireballPrefab;

        private readonly NetworkVariable<Vector3> _authoritativePosition = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _authoritativeYaw = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _authoritativePitch = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _health = new(
            WofGameConstants.MaxHealth,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _armor = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _leftMana = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _rightMana = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isDead = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isMeditating = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _castingUntil = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isSprinting = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isSliding = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isCrouching = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isVClipEnabled = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isInLilyCoilTube = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _lilyCoilTubeT = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _lilyCoilSurfaceAngle = new(
            Mathf.PI,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _leftEquippedSpell = new(
            (int)WofSpellLoadout.ReactDefaultLeft,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _rightEquippedSpell = new(
            (int)WofSpellLoadout.ReactDefaultRight,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _speedBoostUntil = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _jumpBoostUntil = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _discShieldUntil = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _orbShieldUntil = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _slowUntil = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _sleepUntil = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _poisonUntil = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _acidUntil = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _magicGlassOrbUntil = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _flashbangOpacity = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isGrabbed = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<WofNetworkEnginePlaceableRecord> _enginePlaceables = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CharacterController _controller;
        private readonly HashSet<string> _activeMountainLadderZones = new();
        private WofInputCommand _latestServerInput;
        private WofMovementRuntimeState _serverMovementState;
        private WofMovementRuntimeState _predictedMovementState;
        private WofAstralMeditationState _localMeditationState;
        private WofLilyCoilMovementState _serverLilyCoilState;
        private WofLilyCoilMovementState _predictedLilyCoilState;
        private float _serverVerticalVelocity;
        private float _predictedVerticalVelocity;
        private float _yaw;
        private float _pitch;
        private float _lilyCoilViewYaw;
        private float _lilyCoilViewPitch;
        private bool _lastLilyCoilGrounded = true;
        private bool _lastLilyCoilMoving;
        private float _lastGroundedAt;
        private double _nextServerLeftCastAt;
        private double _nextServerRightCastAt;
        private bool _serverLeftCastActive;
        private bool _serverRightCastActive;
        private float _serverLeftChannelTimer;
        private float _serverRightChannelTimer;
        private WofPlayerController _serverLeftGrabTarget;
        private WofPlayerController _serverRightGrabTarget;
        private WofPlayerController _serverGrabCaster;
        private WofHandSide _serverGrabCasterHand;
        private float _serverGrabDistance;
        private double _serverGrabUntil;
        private Vector3 _serverExternalPullVelocity;
        private int _serverExternalPullFrames;
        private Vector3 _serverImpulsePlanarVelocity;
        private float _serverFlashbangInitialOpacity;
        private double _serverFlashbangStartedAt;
        private double _serverFlashbangEndsAt;
        private bool _localLeftCastActive;
        private bool _localRightCastActive;
        private double _localNextLeftCastAt;
        private double _localNextRightCastAt;
        private uint _inputSequence;
        private Vector3 _automationServerCastDirection;
        private int _remainingAutomationServerCasts;
        private Unity.Collections.FixedString64Bytes _automationTrainingDummyInstanceId;
        private bool _automationTrainingDummyClientPlacementObserved;
        private bool _treeHouseViewProbe;
        private bool _treeHouseViewProbeLogged;
        private float _treeHouseViewProbeYaw = WofTreeHouseVillageLayout.DefaultPlayerYawDegrees;
        private bool _villagerViewProbe;
        private bool _darrelDialogProbe;
        private bool _darrelGroveViewProbe;
        private bool _darrelGroveBackyardViewProbe;
        private bool _darrelGroveWaterfallViewProbe;
        private bool _desertVillageViewProbe;
        private bool _chicagoCityViewProbe;
        private bool _mountainVillageViewProbe;
        private bool _grassViewProbe;
        private bool _grassOverheadViewProbe;
        private bool _hasDarrelReturnPosition;
        private bool _darrelReturnArmed;
        private Vector3 _darrelReturnPosition;
        private float _darrelReturnYaw;
        private bool? _pendingVClipEnabled;
        private bool _vclipMovementLogged;
        private bool _meditationPresentationLogged;
        private double _nextManaDecayAt;
        private readonly Dictionary<string, double> _manaFlowerCooldowns = new();

        public float Health => _health.Value;
        public float Armor => _armor.Value;
        public float LeftMana => _leftMana.Value;
        public float RightMana => _rightMana.Value;
        public bool CanRechargeMana => _leftMana.Value < WofManaRules.MaximumPower ||
                                       _rightMana.Value < WofManaRules.MaximumPower;
        public bool IsDead => _isDead.Value;
        public bool IsMeditating => IsOwner ? _localMeditationState.IsActive : _isMeditating.Value;
        public bool HasActiveSpellShield => IsTimedBuffActive(_discShieldUntil.Value) ||
                                            IsTimedBuffActive(_orbShieldUntil.Value);
        public bool IsSleepEffectActive => IsTimedBuffActive(_sleepUntil.Value);
        public bool IsSlowEffectActive => IsTimedBuffActive(_slowUntil.Value);
        public bool IsPoisonEffectActive => IsTimedBuffActive(_poisonUntil.Value);
        public bool IsAcidEffectActive => IsTimedBuffActive(_acidUntil.Value);
        public float FlashbangOpacity => _flashbangOpacity.Value;
        public bool IsGrabbed => _isGrabbed.Value;
        public bool IsGrounded => IsMeditating || (!IsVClipEnabled && (IsLocalLilyCoilActive
            ? _lastLilyCoilGrounded
            : _controller != null && (!_controller.enabled || _controller.isGrounded)));
        public bool IsCasting => IsSpawned && NetworkManager != null &&
                                 NetworkManager.ServerTime.Time < _castingUntil.Value;
        public bool IsSprinting => _isSprinting.Value;
        public bool IsSliding => _isSliding.Value;
        public bool IsCrouching => _isCrouching.Value;
        public bool IsVClipEnabled => IsOwner && _pendingVClipEnabled.HasValue
            ? _pendingVClipEnabled.Value
            : _isVClipEnabled.Value;
        public bool IsMoving
        {
            get
            {
                if (IsMeditating)
                {
                    return false;
                }
                if (_controller == null)
                {
                    return false;
                }

                if (IsLocalLilyCoilActive) return _lastLilyCoilMoving;
                var velocity = _controller.velocity;
                return velocity.x * velocity.x + velocity.z * velocity.z > 0.0001f;
            }
        }
        public bool IsDarrelReturnArmed => _darrelReturnArmed;
        public WofSpellId LeftEquippedSpell => ResolveSpell(_leftEquippedSpell.Value, WofSpellLoadout.ReactDefaultLeft);
        public WofSpellId RightEquippedSpell => ResolveSpell(_rightEquippedSpell.Value, WofSpellLoadout.ReactDefaultRight);
        public int EnginePlaceableCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < _enginePlaceables.Count; index++)
                    if (!IsTrainingDummy(_enginePlaceables[index])) count++;
                return count;
            }
        }
        internal int ActiveMountainLadderZoneCount => _activeMountainLadderZones.Count;
        internal bool IsLilyCoilTubeActive => IsLocalLilyCoilActive;
        internal float LilyCoilTubeProgress => IsServer
            ? _serverLilyCoilState.T
            : _predictedLilyCoilState.T;
        public Vector3 DamageProbePosition => transform.position + transform.up *
                                              WofMovementMath.ResolveCameraHeight(_isSliding.Value, _isCrouching.Value);
        private bool IsLocalLilyCoilActive => IsServer
            ? _serverLilyCoilState.Active
            : _predictedLilyCoilState.Active;

        private void Awake()
        {
            foreach (var argument in System.Environment.GetCommandLineArgs())
            {
                if (argument.Equals("--wof-treehouse-view-probe", System.StringComparison.OrdinalIgnoreCase))
                {
                    _treeHouseViewProbe = true;
                }
                else if (argument.Equals("--wof-treehouse-view-probe=zero", System.StringComparison.OrdinalIgnoreCase))
                {
                    _treeHouseViewProbe = true;
                    _treeHouseViewProbeYaw = 0f;
                }
                else if (argument.Equals("--wof-darrel-grove-view-probe=backyard", System.StringComparison.OrdinalIgnoreCase))
                {
                    _darrelGroveViewProbe = true;
                    _darrelGroveBackyardViewProbe = true;
                }
                else if (argument.Equals("--wof-darrel-grove-view-probe=waterfall", System.StringComparison.OrdinalIgnoreCase))
                {
                    _darrelGroveViewProbe = true;
                    _darrelGroveWaterfallViewProbe = true;
                }
                else if (argument.Equals("--wof-darrel-grove-view-probe", System.StringComparison.OrdinalIgnoreCase) ||
                         argument.Equals("--wof-darrel-dragon-controller-probe", System.StringComparison.OrdinalIgnoreCase))
                {
                    _darrelGroveViewProbe = true;
                }
                else if (argument.Equals("--wof-desert-village-view-probe", System.StringComparison.OrdinalIgnoreCase))
                {
                    _desertVillageViewProbe = true;
                }
                else if (argument.Equals("--wof-chicago-city-view-probe", System.StringComparison.OrdinalIgnoreCase))
                {
                    _chicagoCityViewProbe = true;
                }
                else if (argument.Equals("--wof-mountain-village-view-probe", System.StringComparison.OrdinalIgnoreCase))
                {
                    _mountainVillageViewProbe = true;
                }
                else if (argument.StartsWith("--wof-mountain-village-view-probe=", System.StringComparison.OrdinalIgnoreCase))
                {
                    _mountainVillageViewProbe = true;
                }
                else if (argument.Equals("--wof-grass-view-probe", System.StringComparison.OrdinalIgnoreCase))
                {
                    _grassViewProbe = true;
                }
                else if (argument.Equals("--wof-grass-view-probe=overhead", System.StringComparison.OrdinalIgnoreCase))
                {
                    _grassViewProbe = true;
                    _grassOverheadViewProbe = true;
                }
            }
            _villagerViewProbe = WofPerformanceModeRuntime.IsVillagerViewProbe;
            _darrelDialogProbe = WofPerformanceModeRuntime.IsDarrelDialogProbe;

            _controller = GetComponent<CharacterController>();
            if (cameraPivot == null)
            {
                cameraPivot = transform.Find("CameraPivot");
            }

            if (playerCamera == null && cameraPivot != null)
            {
                playerCamera = cameraPivot.GetComponentInChildren<Camera>(true);
            }

            if (playerAudioListener == null && playerCamera != null)
            {
                playerAudioListener = playerCamera.GetComponent<AudioListener>();
            }

            if (visualRoot == null)
            {
                var child = transform.Find("VisualRoot");
                visualRoot = child == null ? null : child.gameObject;
            }
        }

        public override void OnNetworkSpawn()
        {
            _health.OnValueChanged += HandleHealthChanged;
            _armor.OnValueChanged += HandleArmorChanged;
            _leftMana.OnValueChanged += HandleManaChanged;
            _rightMana.OnValueChanged += HandleManaChanged;
            _isDead.OnValueChanged += HandleDeadChanged;
            _isMeditating.OnValueChanged += HandleMeditatingChanged;
            _isVClipEnabled.OnValueChanged += HandleVClipEnabledChanged;
            _leftEquippedSpell.OnValueChanged += HandleEquippedSpellChanged;
            _rightEquippedSpell.OnValueChanged += HandleEquippedSpellChanged;

            var hasLocalControl = IsServer || IsOwner;
            _controller.enabled = hasLocalControl && !_isDead.Value;
            _controller.detectCollisions = !IsVClipEnabled;
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(IsOwner);
            }
            if (playerAudioListener != null)
            {
                playerAudioListener.enabled = IsOwner;
            }
            if (visualRoot != null)
            {
                visualRoot.SetActive(!IsOwner);
            }

            if (IsServer)
            {
                var angle = OwnerClientId * 1.618f;
                var spawnOffset = OwnerClientId == 0
                    ? Vector3.zero
                    : new Vector3(Mathf.Sin(angle) * 4f, 0f, Mathf.Cos(angle) * 4f);
                var useDarrelGroveProbeSpawn = OwnerClientId == 0 && _darrelGroveViewProbe;
                var useGrassProbeSpawn = OwnerClientId == 0 && _grassViewProbe;
                var useDarrelProbeSpawn = OwnerClientId == 0 && _darrelDialogProbe && !useDarrelGroveProbeSpawn;
                var useVillagerProbeSpawn = OwnerClientId == 0 && _villagerViewProbe &&
                                            !useDarrelProbeSpawn && !useDarrelGroveProbeSpawn;
                var useChicagoProbeSpawn = OwnerClientId == 0 && _chicagoCityViewProbe;
                var useDesertProbeSpawn = OwnerClientId == 0 && _desertVillageViewProbe && !useChicagoProbeSpawn;
                var spawn = useGrassProbeSpawn
                    ? new Vector3(0f, _grassOverheadViewProbe ? 128f : 80f, -360f)
                    : useChicagoProbeSpawn
                    ? WofChicagoCityLayout.ViewProbeSpawn
                    : useDesertProbeSpawn
                    ? WofDesertVillageLayout.ViewProbeSpawn
                    : useDarrelGroveProbeSpawn
                    ? _darrelGroveWaterfallViewProbe
                        ? WofDarrelGroveLayout.SpawnPosition
                    : _darrelGroveBackyardViewProbe
                        ? WofDarrelGroveLayout.WorldOrigin + new Vector3(80f, 20.5f, 150f)
                        : WofDarrelGroveLayout.SpawnPosition
                    : useDarrelProbeSpawn
                        ? DarrelDialogProbeSpawn
                    : useVillagerProbeSpawn
                        ? VillagerViewProbeSpawn
                    : WofTreeHouseVillageLayout.DefaultPlayerSpawn + spawnOffset;
                var spawnYaw = useGrassProbeSpawn
                    ? 180f
                    : useChicagoProbeSpawn || useDesertProbeSpawn
                    ? 0f
                    : useDarrelGroveProbeSpawn
                    ? _darrelGroveWaterfallViewProbe ? 180f
                    : _darrelGroveBackyardViewProbe ? 208f
                    : WofDarrelGroveLayout.UnitySpawnYawDegrees
                    : useDarrelProbeSpawn
                        ? DarrelDialogProbeYaw
                    : useVillagerProbeSpawn
                        ? VillagerViewProbeYaw
                    : WofTreeHouseVillageLayout.DefaultPlayerYawDegrees;
                Teleport(spawn, spawnYaw);
                Debug.Log($"[WOF-AUTOMATION] PLAYER_SPAWN id={OwnerClientId} position={spawn}");
                _health.Value = WofGameConstants.MaxHealth;
                _armor.Value = 0;
                _leftMana.Value = 0f;
                _rightMana.Value = 0f;
                _nextManaDecayAt = NetworkManager.ServerTime.Time + 1d;
                _manaFlowerCooldowns.Clear();
                _isDead.Value = false;
                _isMeditating.Value = false;
                _castingUntil.Value = 0d;
                _leftEquippedSpell.Value = (int)WofSpellLoadout.ReactDefaultLeft;
                _rightEquippedSpell.Value = (int)WofSpellLoadout.ReactDefaultRight;
                _speedBoostUntil.Value = 0d;
                _jumpBoostUntil.Value = 0d;
                _discShieldUntil.Value = 0d;
                _orbShieldUntil.Value = 0d;
                _slowUntil.Value = 0d;
                _sleepUntil.Value = 0d;
                _poisonUntil.Value = 0d;
                _acidUntil.Value = 0d;
                _magicGlassOrbUntil.Value = 0d;
                _flashbangOpacity.Value = 0f;
                _isGrabbed.Value = false;
            }

            if (IsOwner)
            {
                WofAstralMeditationRules.SetAuthoritativeActive(
                    ref _localMeditationState,
                    _isMeditating.Value);
                _yaw = _authoritativeYaw.Value;
                _pitch = _authoritativePitch.Value;
                if (_chicagoCityViewProbe)
                {
                    _yaw = 0f;
                    _pitch = -5f;
                    ApplyCameraRotation();
                    Debug.Log($"[WOF-AUTOMATION] CHICAGO_CITY_VIEW_PROBE_READY position={transform.position} yaw={_yaw} pitch={_pitch} origin={WofChicagoCityLayout.WorldOrigin}");
                }
                else if (_grassViewProbe)
                {
                    _yaw = 180f;
                    _pitch = _grassOverheadViewProbe ? 82f : -8f;
                    ApplyCameraRotation();
                    Debug.Log($"[WOF-AUTOMATION] GRASS_VIEW_PROBE_READY variant={(_grassOverheadViewProbe ? "overhead" : "ground")} position={transform.position} yaw={_yaw} pitch={_pitch}");
                }
                else if (_desertVillageViewProbe)
                {
                    _yaw = 0f;
                    _pitch = -6f;
                    ApplyCameraRotation();
                    Debug.Log($"[WOF-AUTOMATION] DESERT_VILLAGE_VIEW_PROBE_READY position={transform.position} yaw={_yaw} pitch={_pitch} origin={WofDesertVillageLayout.WorldOrigin}");
                }
                else if (_darrelDialogProbe)
                {
                    _yaw = DarrelDialogProbeYaw;
                    _pitch = DarrelDialogProbePitch;
                    ApplyCameraRotation();
                    Debug.Log($"[WOF-AUTOMATION] DARREL_DIALOG_PROBE_READY position={transform.position} yaw={_yaw} pitch={_pitch} target={WofQuestDialogRules.DarrelNpcId}");
                }
                else if (_darrelGroveViewProbe)
                {
                    _yaw = _darrelGroveWaterfallViewProbe ? 180f
                        : _darrelGroveBackyardViewProbe ? 208f
                        : WofDarrelGroveLayout.UnitySpawnYawDegrees;
                    _pitch = _darrelGroveWaterfallViewProbe ? -8f
                        : _darrelGroveBackyardViewProbe ? -5f
                        : -4f;
                    ApplyCameraRotation();
                    Debug.Log($"[WOF-AUTOMATION] DARREL_GROVE_VIEW_PROBE_READY position={transform.position} yaw={_yaw} pitch={_pitch} dragon={WofDarrelGroveLayout.DragonWorldPosition}");
                }
                else if (_treeHouseViewProbe)
                {
                    _yaw = _treeHouseViewProbeYaw;
                    _pitch = -24f;
                    ApplyCameraRotation();
                    Debug.Log($"[WOF-AUTOMATION] TREEHOUSE_VIEW_PROBE position={transform.position} yaw={_yaw} pitch={_pitch}");
                }
                else if (_villagerViewProbe)
                {
                    _yaw = VillagerViewProbeYaw;
                    _pitch = VillagerViewProbePitch;
                    ApplyCameraRotation();
                    Debug.Log($"[WOF-AUTOMATION] VILLAGER_VIEW_PROBE_READY position={transform.position} yaw={_yaw} pitch={_pitch} target=48-64");
                }
                PublishHud();
            }
        }

        public override void OnNetworkDespawn()
        {
            _health.OnValueChanged -= HandleHealthChanged;
            _armor.OnValueChanged -= HandleArmorChanged;
            _leftMana.OnValueChanged -= HandleManaChanged;
            _rightMana.OnValueChanged -= HandleManaChanged;
            _isDead.OnValueChanged -= HandleDeadChanged;
            _isMeditating.OnValueChanged -= HandleMeditatingChanged;
            _isVClipEnabled.OnValueChanged -= HandleVClipEnabledChanged;
            _leftEquippedSpell.OnValueChanged -= HandleEquippedSpellChanged;
            _rightEquippedSpell.OnValueChanged -= HandleEquippedSpellChanged;
            if (IsOwner)
            {
                WofAstralMeditationRules.SetAuthoritativeActive(ref _localMeditationState, false);
                ClearOwnerCastPresentation();
                WofHud.Instance?.SetMagicHandsVisible(true);
                WofHud.Instance?.SetFlashbangOpacity(0f);
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            WofHud.Instance?.SetFlashbangOpacity(_flashbangOpacity.Value);

            UpdateAstralMeditationInput();
            if (_isDead.Value)
            {
                return;
            }

            if (IsMeditating)
            {
                ApplyCameraRotation();
                ApplyCameraHeight(false, false);
                TryLogMeditationPresentation();
                PublishMovementHud();
                return;
            }

            var look = _treeHouseViewProbe || _villagerViewProbe || _darrelDialogProbe ||
                       _darrelGroveViewProbe || _desertVillageViewProbe || _chicagoCityViewProbe ||
                       _mountainVillageViewProbe || _grassViewProbe
                ? Vector2.zero
                : WofInputRouter.ReadLook();
            if (IsLocalLilyCoilActive)
            {
                _lilyCoilViewYaw += look.x * WofGameConstants.MouseSensitivity;
                _lilyCoilViewPitch = Mathf.Clamp(
                    _lilyCoilViewPitch - look.y * WofGameConstants.MouseSensitivity,
                    -82f,
                    82f);
                _yaw = _lilyCoilViewYaw;
                _pitch = _lilyCoilViewPitch;
            }
            else
            {
                _yaw += look.x * WofGameConstants.MouseSensitivity;
                _pitch = Mathf.Clamp(_pitch - look.y * WofGameConstants.MouseSensitivity, -82f, 82f);
            }
            ApplyCameraRotation();

            var move = WofInputRouter.ReadMove();
            var command = new WofInputCommand
            {
                Move = move,
                Yaw = _yaw,
                Pitch = _pitch,
                Jump = WofInputRouter.ReadJump(),
                Sprint = WofInputRouter.ReadSprint(move),
                Slide = WofInputRouter.ReadSlide(),
                Sequence = ++_inputSequence
            };

            if (IsServer)
            {
                _latestServerInput = command;
            }
            else
            {
                SubmitInputRpc(command);
                Simulate(
                    ref _predictedVerticalVelocity,
                    ref _predictedMovementState,
                    ref _predictedLilyCoilState,
                    command,
                    Time.deltaTime);
            }

            if (WofNavigationRecorderRuntime.IsActive)
            {
                RecordNavigationSample(command);
            }

            HandleOwnerCastInput(WofInputRouter.ReadCastFrame());

            PublishMovementHud();

            if (_treeHouseViewProbe && !_treeHouseViewProbeLogged && Time.realtimeSinceStartup > 1.5f)
            {
                _treeHouseViewProbeLogged = true;
                var livePitch = cameraPivot == null ? float.NaN : cameraPivot.localEulerAngles.x;
                Debug.Log($"[WOF-AUTOMATION] TREEHOUSE_VIEW_LIVE position={transform.position} internalYaw={_yaw:F1} commandYaw={_latestServerInput.Yaw:F1} yaw={transform.eulerAngles.y:F1} forward={transform.forward} pitch={livePitch:F1}");
            }
        }

        private void HandleOwnerCastInput(WofCastInputFrame frame)
        {
            if (WofInputRouter.GameplaySuppressed)
            {
                ReleaseOwnerCast(WofHandSide.Left);
                ReleaseOwnerCast(WofHandSide.Right);
                return;
            }

            if ((frame.LeftPressed || frame.RightPressed) && WofDarrelGroveRuntime.TryInteractWithDragon(this))
            {
                return;
            }

            if (frame.LeftPressed) StartOwnerCast(WofHandSide.Left);
            if (frame.RightPressed) StartOwnerCast(WofHandSide.Right);
            if (frame.LeftReleased) ReleaseOwnerCast(WofHandSide.Left);
            if (frame.RightReleased) ReleaseOwnerCast(WofHandSide.Right);
        }

        private void StartOwnerCast(WofHandSide hand)
        {
            var spell = hand == WofHandSide.Left ? LeftEquippedSpell : RightEquippedSpell;
            var now = Time.unscaledTimeAsDouble;
            var nextCastAt = hand == WofHandSide.Left ? _localNextLeftCastAt : _localNextRightCastAt;
            if (now < nextCastAt) return;
            if (WofSpellCastingRules.ShouldConsumeCooldownOnStart(spell))
            {
                SetLocalNextCastAt(hand, now + WofSpellRuntimeTuning.GetCastCooldownSeconds(spell));
            }
            var remainsActive = WofSpellCastingRules.KeepsHandActiveAfterStart(spell);
            if (hand == WofHandSide.Left) _localLeftCastActive = remainsActive;
            else _localRightCastActive = remainsActive;

            if (remainsActive)
            {
                WofHud.Instance?.SetHandCasting(hand, true);
            }
            else
            {
                WofHud.Instance?.PlayFiringPose(hand, WofSpellLoadout.SelfBuffHandChargeSeconds);
            }

            if (IsServer) TryStartCastServer(hand);
            else RequestCastStartRpc(hand);
        }

        private void ReleaseOwnerCast(WofHandSide hand)
        {
            var active = hand == WofHandSide.Left ? _localLeftCastActive : _localRightCastActive;
            if (!active) return;
            if (hand == WofHandSide.Left) _localLeftCastActive = false;
            else _localRightCastActive = false;
            WofHud.Instance?.SetHandCasting(hand, false);

            var spell = hand == WofHandSide.Left ? LeftEquippedSpell : RightEquippedSpell;
            if (WofSpellCastingRules.ShouldConsumeCooldownOnRelease(spell))
            {
                SetLocalNextCastAt(
                    hand,
                    Time.unscaledTimeAsDouble + WofSpellRuntimeTuning.GetCastCooldownSeconds(spell));
            }

            if (IsServer) ReleaseCastServer(hand);
            else RequestCastReleaseRpc(hand);
        }

        private void SetLocalNextCastAt(WofHandSide hand, double value)
        {
            if (hand == WofHandSide.Left) _localNextLeftCastAt = value;
            else _localNextRightCastAt = value;
        }

        private void ClearOwnerCastPresentation()
        {
            _localLeftCastActive = false;
            _localRightCastActive = false;
            WofHud.Instance?.SetHandCasting(WofHandSide.Left, false);
            WofHud.Instance?.SetHandCasting(WofHandSide.Right, false);
        }

        private void UpdateAstralMeditationInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var leftHeld = keyboard.leftCtrlKey.isPressed;
            var rightHeld = keyboard.rightCtrlKey.isPressed;
            var pressed = keyboard.leftCtrlKey.wasPressedThisFrame ||
                          keyboard.rightCtrlKey.wasPressedThisFrame;
            var released = keyboard.leftCtrlKey.wasReleasedThisFrame ||
                           keyboard.rightCtrlKey.wasReleasedThisFrame;
            var now = Time.unscaledTimeAsDouble;

            if (pressed)
            {
                var holdWasStarted = _localMeditationState.ExitHoldStartedAt >= 0d;
                var transition = WofAstralMeditationRules.HandleControlPressed(
                    ref _localMeditationState,
                    now,
                    !_isDead.Value && !WofInputRouter.GameplaySuppressed);
                ApplyOwnerMeditationTransition(transition);
                if (transition == WofAstralMeditationTransition.None &&
                    !holdWasStarted && _localMeditationState.ExitHoldStartedAt >= 0d)
                {
                    Debug.Log($"[WOF-AUTOMATION] ASTRAL_MEDITATION_EXIT_HOLD_STARTED owner={OwnerClientId}");
                }
            }

            if (released)
            {
                var holdStartedAt = _localMeditationState.ExitHoldStartedAt;
                WofAstralMeditationRules.HandleControlReleased(
                    ref _localMeditationState,
                    leftHeld || rightHeld);
                if (!leftHeld && !rightHeld && _localMeditationState.IsActive)
                {
                    if (holdStartedAt >= 0d)
                    {
                        Debug.Log(
                            $"[WOF-AUTOMATION] ASTRAL_MEDITATION_SHORT_HOLD_CANCELLED owner={OwnerClientId} elapsed={Mathf.Max(0f, (float)(now - holdStartedAt)):F2}");
                    }
                    else
                    {
                        Debug.Log($"[WOF-AUTOMATION] ASTRAL_MEDITATION_EXIT_ARMED owner={OwnerClientId}");
                    }
                }
            }

            ApplyOwnerMeditationTransition(
                WofAstralMeditationRules.UpdateExitHold(ref _localMeditationState, now));
        }

        private void ApplyOwnerMeditationTransition(WofAstralMeditationTransition transition)
        {
            if (transition == WofAstralMeditationTransition.None)
            {
                return;
            }

            var active = transition == WofAstralMeditationTransition.Entered;
            _meditationPresentationLogged = false;
            if (active)
            {
                WofInputRouter.ResetTransientGameplayActions();
                WofMovementMath.Reset(ref _predictedMovementState);
                _predictedVerticalVelocity = 0f;
            }

            if (IsServer)
            {
                SetMeditatingServer(active);
            }
            else
            {
                SetMeditatingRpc(active);
            }

            ApplyCameraHeight(false, false);
            PublishHud();
            Debug.Log(
                $"[WOF-AUTOMATION] ASTRAL_MEDITATION_LOCAL owner={OwnerClientId} " +
                $"active={active.ToString().ToLowerInvariant()} cameraHeight={(cameraPivot == null ? float.NaN : cameraPivot.localPosition.y):F3} " +
                $"handsVisible={(WofHud.Instance?.AreMagicHandsVisible ?? false).ToString().ToLowerInvariant()} " +
                $"position={transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}");
        }

        private void TryLogMeditationPresentation()
        {
            if (_meditationPresentationLogged || cameraPivot == null ||
                Mathf.Abs(cameraPivot.localPosition.y - WofMovementMath.UnityMeditationCameraHeight) > 0.01f)
            {
                return;
            }

            _meditationPresentationLogged = true;
            Debug.Log(
                $"[WOF-AUTOMATION] ASTRAL_MEDITATION_PRESENTATION owner={OwnerClientId} active=true " +
                $"cameraHeight={cameraPivot.localPosition.y:F3} " +
                $"handsVisible={(WofHud.Instance?.AreMagicHandsVisible ?? false).ToString().ToLowerInvariant()} " +
                $"position={transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}");
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            RespawnExpiredTrainingDummies();
            if (_isDead.Value || !_controller.enabled) return;

            UpdateServerFlashbang();
            if (UpdateServerGrabbedState(Time.fixedDeltaTime))
            {
                return;
            }
            ApplyToxicStatusDamage(Time.fixedDeltaTime);
            ApplyManaDecay();
            if (_isDead.Value)
            {
                CancelServerCasting();
                return;
            }
            if (_isMeditating.Value)
            {
                CancelServerCasting();
                _latestServerInput.Move = Vector2.zero;
                _latestServerInput.Jump = false;
                _latestServerInput.Sprint = false;
                _latestServerInput.Slide = false;
                _authoritativePosition.Value = transform.position;
                _authoritativeYaw.Value = _latestServerInput.Yaw;
                _authoritativePitch.Value = _latestServerInput.Pitch;
                _isSprinting.Value = false;
                _isSliding.Value = false;
                _isCrouching.Value = false;
                return;
            }
            var simulatedInput = _latestServerInput;
            if (IsTimedBuffActive(_sleepUntil.Value))
            {
                CancelServerCasting();
                simulatedInput.Move = Vector2.zero;
                simulatedInput.Jump = false;
                simulatedInput.Sprint = false;
                simulatedInput.Slide = false;
            }
            else
            {
                UpdateServerChannelledCasts(Time.fixedDeltaTime);
            }
            var movementFrame = Simulate(
                ref _serverVerticalVelocity,
                ref _serverMovementState,
                ref _serverLilyCoilState,
                simulatedInput,
                Time.fixedDeltaTime);
            _authoritativePosition.Value = transform.position;
            _authoritativeYaw.Value = _latestServerInput.Yaw;
            _authoritativePitch.Value = _latestServerInput.Pitch;
            _isSprinting.Value = movementFrame.IsSprinting;
            _isSliding.Value = movementFrame.IsSliding;
            _isCrouching.Value = movementFrame.IsCrouching;
            _isInLilyCoilTube.Value = _serverLilyCoilState.Active;
            _lilyCoilTubeT.Value = _serverLilyCoilState.T;
            _lilyCoilSurfaceAngle.Value = _serverLilyCoilState.SurfaceAngle;
        }

        private void RecordNavigationSample(WofInputCommand command)
        {
            var vclip = IsVClipEnabled;
            var hasPlanarInput = command.Move.sqrMagnitude > 0f;
            var moving = hasPlanarInput || (vclip && (command.Jump || command.Slide));
            var sprinting = moving && command.Sprint && !IsSliding && !IsCrouching;
            var velocity = vclip
                ? WofMovementMath.ResolveVClipVelocity(
                    command.Move,
                    command.Yaw,
                    command.Jump,
                    command.Slide,
                    command.Sprint,
                    IsSpeedBoostActive,
                    IsTimedBuffActive(_slowUntil.Value))
                : _controller.velocity;
            var aimDirection = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            var bootstrap = WofBootstrap.Instance;
            var gameMode = bootstrap == null || !bootstrap.IsSurvivalSession
                ? "custom-lobby"
                : bootstrap.Mode == WofSessionMode.Solo
                    ? "solo-survival"
                    : "multiplayer-survival";
            WofNavigationRecorderRuntime.Record(
                gameMode,
                transform.position,
                new Vector3(command.Pitch, command.Yaw, 0f),
                aimDirection,
                velocity,
                command.Move,
                sprinting,
                command.Jump,
                command.Slide,
                vclip,
                IsGrounded,
                moving,
                IsSliding,
                IsCrouching,
                WofSpellMenuRuntime.IsOpen);
        }

        private void LateUpdate()
        {
            if (!IsSpawned || IsServer || _isDead.Value)
            {
                return;
            }

            var target = _authoritativePosition.Value;
            if (IsOwner)
            {
                var error = Vector3.Distance(transform.position, target);
                if (error > 2f)
                {
                    SetControllerPosition(target);
                }
                else if (error > 0.2f)
                {
                    SetControllerPosition(Vector3.Lerp(transform.position, target, 0.08f));
                }
                return;
            }

            var blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target, blend);
            if (_isInLilyCoilTube.Value)
            {
                var frame = WofLilyCoilLayout.GetFrame(_lilyCoilTubeT.Value);
                var radial = WofLilyCoilLayout.GetRadial(frame, _lilyCoilSurfaceAngle.Value);
                var tubeRotation = Quaternion.LookRotation(frame.Tangent, -radial);
                transform.rotation = Quaternion.Slerp(transform.rotation, tubeRotation, blend);
            }
            else
            {
                transform.rotation = Quaternion.Euler(
                    0f,
                    Mathf.LerpAngle(transform.eulerAngles.y, _authoritativeYaw.Value, blend),
                    0f);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        private void SubmitInputRpc(WofInputCommand command)
        {
            if (command.Sequence <= _latestServerInput.Sequence)
            {
                return;
            }

            command.Move = Vector2.ClampMagnitude(command.Move, 1f);
            command.Pitch = Mathf.Clamp(command.Pitch, -82f, 82f);
            if (_isMeditating.Value)
            {
                command.Move = Vector2.zero;
                command.Jump = false;
                command.Sprint = false;
                command.Slide = false;
            }
            _latestServerInput = command;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SetMeditatingRpc(bool active)
        {
            SetMeditatingServer(active);
        }

        private void SetMeditatingServer(bool active)
        {
            if (!IsServer || !IsSpawned || (active && _isDead.Value))
            {
                return;
            }

            if (_isMeditating.Value == active)
            {
                return;
            }

            if (active)
            {
                CancelServerCasting();
                _latestServerInput.Move = Vector2.zero;
                _latestServerInput.Jump = false;
                _latestServerInput.Sprint = false;
                _latestServerInput.Slide = false;
                _serverVerticalVelocity = 0f;
                WofMovementMath.Reset(ref _serverMovementState);
                _isSprinting.Value = false;
                _isSliding.Value = false;
                _isCrouching.Value = false;
                _castingUntil.Value = 0d;
            }

            _isMeditating.Value = active;
            Debug.Log(
                $"[WOF-AUTOMATION] ASTRAL_MEDITATION_CHANGED owner={OwnerClientId} active={active.ToString().ToLowerInvariant()}");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestCastRpc(WofHandSide hand = WofHandSide.Right)
        {
            if (_isMeditating.Value)
            {
                return;
            }
            if (_remainingAutomationServerCasts > 0)
            {
                if (TryCastFromServerDirection(_automationServerCastDirection))
                {
                    _remainingAutomationServerCasts--;
                    if (_remainingAutomationServerCasts == 0)
                    {
                        _automationServerCastDirection = Vector3.zero;
                    }
                }
                return;
            }

            TryCastFromAuthoritativePoseServer(hand);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestCastStartRpc(WofHandSide hand)
        {
            TryStartCastServer(hand);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestCastReleaseRpc(WofHandSide hand)
        {
            ReleaseCastServer(hand);
        }

        public void EquipSpell(WofHandSide hand, WofSpellId spell)
        {
            if (!IsOwner || !IsSpawned || !WofSpellLoadout.IsValid((int)spell))
            {
                return;
            }

            if (IsServer)
            {
                SetEquippedSpellServer(hand, spell);
            }
            else
            {
                EquipSpellRpc(hand, (int)spell);
            }
        }

        public bool SetVClipEnabled(bool enabled)
        {
            if (!IsOwner || !IsSpawned || _isDead.Value)
            {
                return false;
            }

            _pendingVClipEnabled = enabled;
            if (enabled) _vclipMovementLogged = false;
            ApplyVClipCollisionState(enabled);
            if (IsServer)
            {
                SetVClipEnabledServer(enabled);
            }
            else
            {
                SetVClipEnabledRpc(enabled);
            }
            Debug.Log($"[WOF-AUTOMATION] VCLIP_REQUEST enabled={enabled.ToString().ToLowerInvariant()}");
            return true;
        }

        public void CopyEnginePlaceables(List<WofEnginePlaceableRecord> destination)
        {
            if (destination == null) return;
            for (var index = 0; index < _enginePlaceables.Count; index++)
                destination.Add(_enginePlaceables[index].ToRuntimeRecord());
        }

        internal bool TryGetTrainingDummyState(string instanceId, out WofEnginePlaceableRecord record)
        {
            if (!string.IsNullOrEmpty(instanceId))
            {
                for (var index = 0; index < _enginePlaceables.Count; index++)
                {
                    var networkRecord = _enginePlaceables[index];
                    if (!IsTrainingDummy(networkRecord) ||
                        networkRecord.InstanceId.ToString() != instanceId) continue;
                    record = networkRecord.ToRuntimeRecord();
                    return true;
                }
            }

            record = default;
            return false;
        }

        public void CopyPersistentEnginePlaceables(List<WofEnginePlaceableRecord> destination)
        {
            if (destination == null) return;
            for (var index = 0; index < _enginePlaceables.Count; index++)
            {
                if (IsTrainingDummy(_enginePlaceables[index])) continue;
                destination.Add(_enginePlaceables[index].ToRuntimeRecord());
            }
        }

        public bool RequestEnginePlaceableUpsert(WofEnginePlaceableRecord record, string replaceInstanceId = null)
        {
            if (!IsOwner || !IsSpawned) return false;
            var networkRecord = new WofNetworkEnginePlaceableRecord(record);
            var replaceId = new Unity.Collections.FixedString64Bytes(replaceInstanceId ?? string.Empty);
            if (IsServer) UpsertEnginePlaceableServer(networkRecord, replaceId);
            else UpsertEnginePlaceableRpc(networkRecord, replaceId);
            return true;
        }

        public bool RequestDeleteEnginePlaceable(string instanceId)
        {
            if (!IsOwner || !IsSpawned || string.IsNullOrWhiteSpace(instanceId)) return false;
            var fixedId = new Unity.Collections.FixedString64Bytes(instanceId);
            if (IsServer) DeleteEnginePlaceableServer(fixedId);
            else DeleteEnginePlaceableRpc(fixedId);
            return true;
        }

        public bool RequestClearEnginePlaceables()
        {
            if (!IsOwner || !IsSpawned) return false;
            if (IsServer) ClearPersistentEnginePlaceablesServer();
            else ClearEnginePlaceablesRpc();
            return true;
        }

        public bool RequestReplaceEnginePlaceables(IReadOnlyList<WofEnginePlaceableRecord> records)
        {
            if (!IsOwner || !IsSpawned) return false;
            var count = Mathf.Min(records?.Count ?? 0, WofEnginePlaceableCatalog.MaximumPlacedObjects);
            var payload = new WofNetworkEnginePlaceableRecord[count];
            for (var index = 0; index < count; index++) payload[index] = new WofNetworkEnginePlaceableRecord(records[index]);
            if (IsServer) ReplaceEnginePlaceablesServer(payload);
            else ReplaceEnginePlaceablesRpc(payload);
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void UpsertEnginePlaceableRpc(
            WofNetworkEnginePlaceableRecord record,
            Unity.Collections.FixedString64Bytes replaceInstanceId)
        {
            UpsertEnginePlaceableServer(record, replaceInstanceId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void DeleteEnginePlaceableRpc(Unity.Collections.FixedString64Bytes instanceId)
        {
            DeleteEnginePlaceableServer(instanceId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ClearEnginePlaceablesRpc()
        {
            ClearPersistentEnginePlaceablesServer();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ReplaceEnginePlaceablesRpc(WofNetworkEnginePlaceableRecord[] records)
        {
            ReplaceEnginePlaceablesServer(records);
        }

        private void UpsertEnginePlaceableServer(
            WofNetworkEnginePlaceableRecord record,
            Unity.Collections.FixedString64Bytes replaceInstanceId)
        {
            var runtime = record.ToRuntimeRecord();
            var definition = WofEnginePlaceableCatalog.Find(runtime.placeableId);
            if (definition == null || string.IsNullOrWhiteSpace(runtime.instanceId) ||
                !float.IsFinite(runtime.x) || !float.IsFinite(runtime.y) ||
                !float.IsFinite(runtime.z) || !float.IsFinite(runtime.yaw)) return;

            if (runtime.placeableId == "training-spell-dummy" &&
                (runtime.trainingDummyHealth <= 0f ||
                 !float.IsFinite(runtime.trainingDummyHealth)) &&
                runtime.trainingDummyRespawnAt <= 0d)
            {
                runtime.trainingDummyHealth = WofTrainingDummyCombatRules.MaxHealth;
                runtime.trainingDummyRespawnAt = 0d;
                runtime.trainingDummyHitSequence = 0;
                runtime.trainingDummyLastSpell = -1;
                record = new WofNetworkEnginePlaceableRecord(runtime);
            }

            var replace = replaceInstanceId.ToString();
            if (!string.IsNullOrEmpty(replace))
            {
                runtime.instanceId = replace;
                record = new WofNetworkEnginePlaceableRecord(runtime);
            }
            for (var index = 0; index < _enginePlaceables.Count; index++)
            {
                if (_enginePlaceables[index].InstanceId.ToString() != runtime.instanceId) continue;
                _enginePlaceables[index] = record;
                return;
            }
            if (!IsTrainingDummy(record) && EnginePlaceableCount >= WofEnginePlaceableCatalog.MaximumPlacedObjects)
            {
                for (var index = 0; index < _enginePlaceables.Count; index++)
                {
                    if (IsTrainingDummy(_enginePlaceables[index])) continue;
                    _enginePlaceables.RemoveAt(index);
                    break;
                }
            }
            _enginePlaceables.Add(record);
        }

        private void DeleteEnginePlaceableServer(Unity.Collections.FixedString64Bytes instanceId)
        {
            for (var index = _enginePlaceables.Count - 1; index >= 0; index--)
            {
                if (_enginePlaceables[index].InstanceId.Equals(instanceId)) _enginePlaceables.RemoveAt(index);
            }
        }

        private void ReplaceEnginePlaceablesServer(WofNetworkEnginePlaceableRecord[] records)
        {
            ClearPersistentEnginePlaceablesServer();
            if (records == null) return;
            var count = Mathf.Min(records.Length, WofEnginePlaceableCatalog.MaximumPlacedObjects);
            for (var index = 0; index < count; index++)
                UpsertEnginePlaceableServer(records[index], default);
        }

        private void ClearPersistentEnginePlaceablesServer()
        {
            for (var index = _enginePlaceables.Count - 1; index >= 0; index--)
                if (!IsTrainingDummy(_enginePlaceables[index])) _enginePlaceables.RemoveAt(index);
        }

        private static bool IsTrainingDummy(WofNetworkEnginePlaceableRecord record)
        {
            return record.PlaceableId.ToString() == "training-spell-dummy";
        }

        public bool ApplyServerTrainingDummySpellImpact(
            string instanceId,
            WofSpellId spell,
            ulong sourceClientId)
        {
            if (!IsServer || string.IsNullOrEmpty(instanceId)) return false;
            var now = NetworkManager.ServerTime.Time;
            for (var index = 0; index < _enginePlaceables.Count; index++)
            {
                var networkRecord = _enginePlaceables[index];
                if (!IsTrainingDummy(networkRecord) || networkRecord.InstanceId.ToString() != instanceId) continue;
                var runtime = networkRecord.ToRuntimeRecord();
                var result = WofTrainingDummyCombatRules.Apply(
                    runtime.trainingDummyHealth,
                    runtime.trainingDummyHitSequence,
                    spell,
                    now);
                if (!result.Applied) return false;

                runtime.trainingDummyHealth = result.Health;
                runtime.trainingDummyRespawnAt = result.RespawnAt;
                runtime.trainingDummyHitSequence = result.HitSequence;
                runtime.trainingDummyLastSpell = (int)spell;
                _enginePlaceables[index] = new WofNetworkEnginePlaceableRecord(runtime);
                Debug.Log(
                    $"[WOF-AUTOMATION] TRAINING_DUMMY_HIT owner={OwnerClientId} instance={instanceId} " +
                    $"source={sourceClientId} spell={spell} damage={WofTrainingDummyCombatRules.GetDamage(spell):F0} " +
                    $"health={result.Health:F0} down={result.IsDown.ToString().ToLowerInvariant()}");
                return true;
            }
            return false;
        }

        private void RespawnExpiredTrainingDummies()
        {
            var now = NetworkManager.ServerTime.Time;
            for (var index = 0; index < _enginePlaceables.Count; index++)
            {
                var networkRecord = _enginePlaceables[index];
                if (!IsTrainingDummy(networkRecord) ||
                    !WofTrainingDummyCombatRules.IsRespawnDue(
                        networkRecord.TrainingDummyHealth,
                        networkRecord.TrainingDummyRespawnAt,
                        now)) continue;

                var runtime = networkRecord.ToRuntimeRecord();
                runtime.trainingDummyHealth = WofTrainingDummyCombatRules.MaxHealth;
                runtime.trainingDummyRespawnAt = 0d;
                _enginePlaceables[index] = new WofNetworkEnginePlaceableRecord(runtime);
                Debug.Log(
                    $"[WOF-AUTOMATION] TRAINING_DUMMY_RESPAWN owner={OwnerClientId} " +
                    $"instance={runtime.instanceId} health={runtime.trainingDummyHealth:F0}");
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SetVClipEnabledRpc(bool enabled)
        {
            SetVClipEnabledServer(enabled);
        }

        private void SetVClipEnabledServer(bool enabled)
        {
            _isVClipEnabled.Value = enabled;
            ApplyVClipCollisionState(enabled);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void EquipSpellRpc(WofHandSide hand, int spellValue)
        {
            if (!WofSpellLoadout.IsValid(spellValue))
            {
                return;
            }
            SetEquippedSpellServer(hand, (WofSpellId)spellValue);
        }

        private void SetEquippedSpellServer(WofHandSide hand, WofSpellId spell)
        {
            if (!IsServer)
            {
                return;
            }
            if (hand == WofHandSide.Left) _leftEquippedSpell.Value = (int)spell;
            else _rightEquippedSpell.Value = (int)spell;
            Debug.Log($"[WOF] SPELL_EQUIPPED owner={OwnerClientId} hand={hand} spell={spell}");
            if (IsOwner) PublishHud();
        }

        public void ApplyServerDamage(float amount, ulong sourceClientId, bool bypassArmor = false)
        {
            if (!IsServer || _isDead.Value)
            {
                return;
            }

            var result = WofDamageMath.Apply(_health.Value, _armor.Value, amount, bypassArmor);
            _armor.Value = result.Armor;
            _health.Value = result.Health;
            Debug.Log($"[WOF-AUTOMATION] DAMAGE target={OwnerClientId} source={sourceClientId} amount={amount} health={result.Health} armor={result.Armor}");

            if (result.IsDead)
            {
                CancelServerCasting();
                EndServerGrab(applyThrow: false, Vector3.zero);
                SetMeditatingServer(false);
                _isDead.Value = true;
                Debug.Log($"[WOF-AUTOMATION] PLAYER_DIED id={OwnerClientId}");
                StartCoroutine(RespawnAfterDelay());
            }
        }

        public void ApplyServerSpellImpact(WofSpellId spell, ulong sourceClientId)
        {
            if (!IsServer || _isDead.Value) return;
            if (spell is WofSpellId.Sleep or WofSpellId.Poison or WofSpellId.Acid)
            {
                ApplyServerStatus(spell, sourceClientId);
                return;
            }

            var damage = WofSpellRuntimeTuning.GetPlayerDamage(spell);
            if (damage > 0f) ApplyServerDamage(damage, sourceClientId);
        }

        public bool ShouldBlockServerSpellImpact(Vector3 incomingOrigin)
        {
            if (!IsServer || _isDead.Value || NetworkManager == null) return false;
            var now = NetworkManager.ServerTime.Time;
            if (now < _orbShieldUntil.Value) return true;
            return now < _discShieldUntil.Value &&
                   WofSpellOutcomeRules.DiscShieldBlocks(
                       transform.position,
                       transform.forward,
                       incomingOrigin);
        }

        public void ApplyServerStatus(WofSpellId spell, ulong sourceClientId)
        {
            if (!IsServer || _isDead.Value) return;
            var now = NetworkManager.ServerTime.Time;
            var until = now + WofSpellRuntimeTuning.GetStatusDurationSeconds(spell);
            switch (spell)
            {
                case WofSpellId.TungstonBallsack:
                    _slowUntil.Value = until;
                    break;
                case WofSpellId.Sleep:
                    _sleepUntil.Value = until;
                    break;
                case WofSpellId.Poison:
                    _poisonUntil.Value = until;
                    break;
                case WofSpellId.Acid:
                    _acidUntil.Value = until;
                    break;
                default:
                    return;
            }
            Debug.Log($"[WOF] STATUS_APPLIED target={OwnerClientId} source={sourceClientId} spell={spell} until={until:F2}");
        }

        public void ApplyServerHealing(float amount, bool clearToxicEffects)
        {
            if (!IsServer || _isDead.Value || amount <= 0f) return;
            _health.Value = Mathf.Min(WofGameConstants.MaxHealth, _health.Value + amount);
            if (clearToxicEffects)
            {
                _poisonUntil.Value = 0d;
                _acidUntil.Value = 0d;
            }
        }

        public void ApplyServerFlashbang(float initialOpacity)
        {
            if (!IsServer || _isDead.Value || NetworkManager == null) return;
            var opacity = Mathf.Clamp01(initialOpacity);
            if (opacity <= 0f) return;
            var now = NetworkManager.ServerTime.Time;
            _serverFlashbangInitialOpacity = opacity;
            _serverFlashbangStartedAt = now;
            _serverFlashbangEndsAt = now + WofSpellRuntimeTuning.GetLifetimeSeconds(WofSpellId.IceSpell);
            _flashbangOpacity.Value = opacity;
            Debug.Log($"[WOF] FLASHBANG_APPLIED target={OwnerClientId} opacity={opacity:F2}");
        }

        public void ApplyServerPortalTeleport(Vector3 position)
        {
            if (!IsServer || _isDead.Value) return;
            var yaw = _authoritativeYaw.Value;
            ResetTeleportMotion();
            Teleport(position, yaw);
            ApplyQuestTeleportOwnerRpc(position, yaw);
        }

        public void ApplyServerKunaiPull(Vector3 hitPoint)
        {
            if (!IsServer || _isDead.Value) return;
            var direction = hitPoint - transform.position;
            if (direction.sqrMagnitude <= 0.001f) return;
            direction.Normalize();
            var velocity = direction * WofSpellRuntimeTuning.KunaiPullSpeed;
            velocity.y += WofSpellRuntimeTuning.KunaiPullVerticalBoost;
            _serverImpulsePlanarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            _serverVerticalVelocity = velocity.y;
            Debug.Log($"[WOF] KUNAI_PULL owner={OwnerClientId} velocity={velocity}");
        }

        public void ApplyServerGrabPull(Vector3 casterPosition)
        {
            if (!IsServer || _isDead.Value) return;
            var direction = casterPosition - transform.position;
            if (direction.sqrMagnitude <= 0.001f) return;
            _controller.Move(direction.normalized * Mathf.Min(8f, direction.magnitude * 0.35f));
        }

        public void ApplyServerTornadoPull(Vector3 center)
        {
            if (!IsServer || _isDead.Value) return;
            var velocity = WofSpellOutcomeRules.ResolveTornadoPullVelocity(transform.position, center);
            if (velocity.sqrMagnitude <= 0.000001f) return;
            _serverExternalPullVelocity = velocity;
            _serverExternalPullFrames = WofSpellRuntimeTuning.ExternalPullFrames;
        }

        private void UpdateServerFlashbang()
        {
            if (!IsServer || _flashbangOpacity.Value <= 0f || NetworkManager == null) return;
            var now = NetworkManager.ServerTime.Time;
            if (now >= _serverFlashbangEndsAt)
            {
                _flashbangOpacity.Value = 0f;
                return;
            }
            _flashbangOpacity.Value = WofSpellOutcomeRules.ResolveIceSpellOpacityAtTime(
                _serverFlashbangInitialOpacity,
                (float)(now - _serverFlashbangStartedAt));
        }

        private bool BeginServerGrab(
            WofPlayerController caster,
            WofHandSide casterHand,
            float distance)
        {
            if (!IsServer || caster == null || caster == this || _isDead.Value || NetworkManager == null)
            {
                return false;
            }

            EndServerGrab(applyThrow: false, Vector3.zero);
            _serverGrabCaster = caster;
            _serverGrabCasterHand = casterHand;
            _serverGrabDistance = WofSpellOutcomeRules.ClampGrabDistance(distance);
            _serverGrabUntil = NetworkManager.ServerTime.Time + WofSpellRuntimeTuning.GrabMaximumDurationSeconds;
            _serverImpulsePlanarVelocity = Vector3.zero;
            _serverExternalPullFrames = 0;
            _isGrabbed.Value = true;
            CancelServerCasting();
            Debug.Log(
                $"[WOF] GRAB_APPLIED caster={caster.OwnerClientId} target={OwnerClientId} hand={casterHand} distance={_serverGrabDistance:F2}");
            return true;
        }

        private bool UpdateServerGrabbedState(float deltaSeconds)
        {
            if (!IsServer || !_isGrabbed.Value) return false;
            if (_serverGrabCaster == null || !_serverGrabCaster.IsSpawned ||
                _serverGrabCaster.IsDead || NetworkManager.ServerTime.Time >= _serverGrabUntil ||
                !_serverGrabCaster.IsServerCastActive(_serverGrabCasterHand))
            {
                var releaseDirection = _serverGrabCaster != null &&
                                _serverGrabCaster.TryResolveAuthoritativeSpellLaunch(out _, out var resolved)
                    ? resolved
                    : transform.forward;
                EndServerGrab(applyThrow: true, releaseDirection);
                return false;
            }

            if (!_serverGrabCaster.TryResolveAuthoritativeSpellLaunch(out var origin, out var direction))
            {
                return true;
            }

            CancelServerCasting();
            var holdPoint = WofSpellOutcomeRules.ResolveGrabHoldPoint(origin, direction, _serverGrabDistance);
            var nextPosition = WofSpellOutcomeRules.ResolveGrabFollowPosition(
                transform.position,
                holdPoint,
                deltaSeconds);
            SetControllerPosition(nextPosition);
            _serverVerticalVelocity = 0f;
            _latestServerInput.Move = Vector2.zero;
            _latestServerInput.Jump = false;
            _latestServerInput.Sprint = false;
            _latestServerInput.Slide = false;
            _authoritativePosition.Value = transform.position;
            _authoritativeYaw.Value = _latestServerInput.Yaw;
            _authoritativePitch.Value = _latestServerInput.Pitch;
            _isSprinting.Value = false;
            _isSliding.Value = false;
            _isCrouching.Value = false;
            return true;
        }

        private void EndServerGrab(bool applyThrow, Vector3 direction)
        {
            if (!IsServer || !_isGrabbed.Value) return;
            var caster = _serverGrabCaster;
            var casterHand = _serverGrabCasterHand;
            _serverGrabCaster = null;
            _serverGrabUntil = 0d;
            _isGrabbed.Value = false;
            if (applyThrow)
            {
                var velocity = WofSpellOutcomeRules.ResolveGrabThrowVelocity(direction);
                _serverImpulsePlanarVelocity = new Vector3(velocity.x, 0f, velocity.z);
                _serverVerticalVelocity = velocity.y;
                Debug.Log(
                    $"[WOF] GRAB_THROW caster={(caster == null ? ulong.MaxValue : caster.OwnerClientId)} target={OwnerClientId} hand={casterHand} velocity={velocity}");
            }
        }

        private void ApplyToxicStatusDamage(float deltaTime)
        {
            if (!IsServer || _isDead.Value || deltaTime <= 0f) return;
            var now = NetworkManager.ServerTime.Time;
            var activeCount = (_poisonUntil.Value > now ? 1 : 0) + (_acidUntil.Value > now ? 1 : 0);
            if (activeCount <= 0) return;
            ApplyServerDamage(
                WofSpellRuntimeTuning.ToxicDamagePerSecond * activeCount * deltaTime,
                OwnerClientId,
                bypassArmor: true);
        }

        public void RequestQuestFatalDamage()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }
            if (IsServer)
            {
                ApplyServerDamage(WofGameConstants.MaxHealth, OwnerClientId, true);
                return;
            }
            RequestQuestFatalDamageRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestQuestFatalDamageRpc()
        {
            ApplyServerDamage(WofGameConstants.MaxHealth, OwnerClientId, true);
        }

        public bool RequestDarrelGroveTeleport()
        {
            if (!IsOwner || !IsSpawned || _isDead.Value)
            {
                return false;
            }
            if (IsServer)
            {
                TeleportToDarrelGroveServer();
            }
            else
            {
                RequestDarrelGroveTeleportRpc();
            }
            _darrelReturnArmed = true;
            return true;
        }

        public bool RequestManaFlowerCollection(int chunkX, int chunkZ, int flowerIndex)
        {
            if (!IsOwner || !IsSpawned || _isDead.Value || !CanRechargeMana ||
                !WofSurvivalAmbientMath.TryGetManaFlower(chunkX, chunkZ, flowerIndex, out _))
                return false;
            if (IsServer) CollectManaFlowerServer(chunkX, chunkZ, flowerIndex);
            else RequestManaFlowerCollectionRpc(chunkX, chunkZ, flowerIndex);
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestManaFlowerCollectionRpc(int chunkX, int chunkZ, int flowerIndex)
        {
            CollectManaFlowerServer(chunkX, chunkZ, flowerIndex);
        }

        private void CollectManaFlowerServer(int chunkX, int chunkZ, int flowerIndex)
        {
            if (!IsServer || _isDead.Value ||
                !WofSurvivalAmbientMath.TryGetManaFlower(chunkX, chunkZ, flowerIndex, out var flower)) return;
            var horizontal = new Vector2(transform.position.x - flower.Position.x,
                transform.position.z - flower.Position.z);
            if (horizontal.sqrMagnitude > flower.Radius * flower.Radius) return;
            var now = NetworkManager.ServerTime.Time;
            if (_manaFlowerCooldowns.TryGetValue(flower.Id, out var until) && until > now) return;
            var recharge = WofManaRules.RechargeMostEmpty(_leftMana.Value, _rightMana.Value);
            if (!recharge.Changed) return;
            _leftMana.Value = recharge.Left;
            _rightMana.Value = recharge.Right;
            until = now + WofManaRules.FlowerRespawnSeconds;
            _manaFlowerCooldowns[flower.Id] = until;
            ConfirmManaFlowerCollectionOwnerRpc(chunkX, chunkZ, flowerIndex, until);
            Debug.Log($"[WOF-AUTOMATION] MANA_FLOWER_COLLECTED owner={OwnerClientId} id={flower.Id} hand={recharge.RechargedHand} until={until:F2}");
        }

        [Rpc(SendTo.Owner)]
        private void ConfirmManaFlowerCollectionOwnerRpc(int chunkX, int chunkZ, int flowerIndex, double until)
        {
            WofSurvivalAmbientLifeRuntime.MarkFlowerCollected(chunkX, chunkZ, flowerIndex, until);
        }

        public bool RequestMapFastTravel(WofMapDestination destination)
        {
            if (!IsOwner || !IsSpawned || _isDead.Value || !WofMapFastTravel.TryGet(destination, out _))
            {
                return false;
            }

            if (IsServer)
            {
                ApplyMapFastTravelServer(destination);
            }
            else
            {
                RequestMapFastTravelRpc((int)destination);
            }
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestMapFastTravelRpc(int destinationValue)
        {
            if (!WofMapFastTravel.IsValid(destinationValue))
            {
                Debug.LogWarning($"[WOF-AUTOMATION] MAP_FAST_TRAVEL_REJECTED owner={OwnerClientId} destination={destinationValue}");
                return;
            }
            ApplyMapFastTravelServer((WofMapDestination)destinationValue);
        }

        private void ApplyMapFastTravelServer(WofMapDestination destination)
        {
            if (!IsServer || !IsSpawned || _isDead.Value ||
                !WofMapFastTravel.TryGet(destination, out var record))
            {
                return;
            }

            var yaw = _authoritativeYaw.Value;
            ResetTeleportMotion();
            Teleport(record.Position, yaw);
            ApplyQuestTeleportOwnerRpc(record.Position, yaw);
            Debug.Log($"[WOF-AUTOMATION] MAP_FAST_TRAVEL owner={OwnerClientId} destination={destination} position={record.Position} yaw={yaw:F1}");
        }

        public bool RequestDarrelReturnTeleport()
        {
            if (!IsOwner || !IsSpawned || _isDead.Value || !_darrelReturnArmed)
            {
                return false;
            }
            _darrelReturnArmed = false;
            if (IsServer)
            {
                TeleportFromDarrelGroveServer();
            }
            else
            {
                RequestDarrelReturnTeleportRpc();
            }
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestDarrelGroveTeleportRpc()
        {
            TeleportToDarrelGroveServer();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestDarrelReturnTeleportRpc()
        {
            TeleportFromDarrelGroveServer();
        }

        private void TeleportToDarrelGroveServer()
        {
            if (!IsServer || !IsSpawned || _isDead.Value)
            {
                return;
            }
            var spawn = WofDarrelQuestSpawnStore.Load();
            var position = spawn.Position;
            var yaw = spawn.YawDegrees;
            if (!WofQuestDevRules.IsFinite(position) || !float.IsFinite(yaw)) return;
            _hasDarrelReturnPosition = true;
            _darrelReturnPosition = _authoritativePosition.Value;
            _darrelReturnYaw = _authoritativeYaw.Value;
            ResetTeleportMotion();
            Teleport(position, yaw);
            ApplyQuestTeleportOwnerRpc(position, yaw);
            Debug.Log($"[WOF-AUTOMATION] DARREL_GROVE_TELEPORT owner={OwnerClientId} position={position} yaw={yaw:F1}");
        }

        private void TeleportFromDarrelGroveServer()
        {
            if (!IsServer || !IsSpawned || _isDead.Value || !_hasDarrelReturnPosition)
            {
                return;
            }
            var position = _darrelReturnPosition;
            var yaw = _darrelReturnYaw;
            _hasDarrelReturnPosition = false;
            ResetTeleportMotion();
            Teleport(position, yaw);
            ApplyQuestTeleportOwnerRpc(position, yaw);
            Debug.Log($"[WOF-AUTOMATION] DARREL_GROVE_RETURN owner={OwnerClientId} position={position} yaw={yaw:F1}");
        }

        [Rpc(SendTo.Owner)]
        private void ApplyQuestTeleportOwnerRpc(Vector3 position, float yaw)
        {
            _predictedVerticalVelocity = 0f;
            WofMovementMath.Reset(ref _predictedMovementState);
            WofLilyCoilMovement.Reset(ref _predictedLilyCoilState);
            SetControllerPosition(position);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _yaw = yaw;
            _pitch = 0f;
            ApplyCameraHeight(false, false);
            ApplyCameraRotation();
        }

        private void ResetTeleportMotion()
        {
            _serverVerticalVelocity = 0f;
            _serverExternalPullVelocity = Vector3.zero;
            _serverExternalPullFrames = 0;
            _serverImpulsePlanarVelocity = Vector3.zero;
            _predictedVerticalVelocity = 0f;
            _latestServerInput = default;
            WofMovementMath.Reset(ref _serverMovementState);
            WofMovementMath.Reset(ref _predictedMovementState);
            WofLilyCoilMovement.Reset(ref _serverLilyCoilState);
            WofLilyCoilMovement.Reset(ref _predictedLilyCoilState);
        }

        internal bool PrepareForAutomationCombatProbe(Vector3 position, float yaw)
        {
            if (!IsServer || !IsSpawned)
            {
                return false;
            }

            WofFireballProjectile.DespawnAllAutomationServerProjectiles();
            _serverVerticalVelocity = 0f;
            _predictedVerticalVelocity = 0f;
            _latestServerInput = default;
            _nextServerLeftCastAt = 0d;
            _nextServerRightCastAt = 0d;
            CancelServerCasting();
            EndServerGrab(applyThrow: false, Vector3.zero);
            _serverExternalPullVelocity = Vector3.zero;
            _serverExternalPullFrames = 0;
            _serverImpulsePlanarVelocity = Vector3.zero;
            _automationServerCastDirection = Vector3.zero;
            _remainingAutomationServerCasts = 0;
            _health.Value = WofGameConstants.MaxHealth;
            _armor.Value = 0;
            _isDead.Value = false;
            _castingUntil.Value = 0d;
            _discShieldUntil.Value = 0d;
            _orbShieldUntil.Value = 0d;
            _slowUntil.Value = 0d;
            _sleepUntil.Value = 0d;
            _poisonUntil.Value = 0d;
            _acidUntil.Value = 0d;
            _magicGlassOrbUntil.Value = 0d;
            _flashbangOpacity.Value = 0f;
            Teleport(position, yaw);
            return true;
        }

        internal bool TryAutomationServerCastSpell(
            WofHandSide hand,
            WofSpellId spell,
            Vector3 aimPoint)
        {
            if (!IsServer || !IsSpawned || _isDead.Value) return false;
            SetEquippedSpellServer(hand, spell);
            SetNextServerCastAt(hand, 0d);
            if (!TryResolveAuthoritativeSpellLaunch(out var origin, out var fallbackDirection)) return false;
            var requestedDirection = aimPoint - origin;
            var direction = requestedDirection.sqrMagnitude > 0.000001f
                ? requestedDirection.normalized
                : fallbackDirection;
            return TryCastResolvedSpellServer(hand, spell, origin, direction);
        }

        internal bool BeginAutomationServerHeldSpell(WofHandSide hand, WofSpellId spell)
        {
            if (!IsServer || !IsSpawned || _isDead.Value) return false;
            SetEquippedSpellServer(hand, spell);
            SetNextServerCastAt(hand, 0d);
            return TryStartCastServer(hand);
        }

        internal bool ReleaseAutomationServerHeldSpell(WofHandSide hand)
        {
            return IsServer && IsSpawned && ReleaseCastServer(hand);
        }

        internal bool IsDiscShieldActiveForAutomation => IsTimedBuffActive(_discShieldUntil.Value);
        internal bool IsOrbShieldActiveForAutomation => IsTimedBuffActive(_orbShieldUntil.Value);

        internal bool PrepareForAutomationNorthGateProbe()
        {
            if (!IsServer || !IsOwner || !IsSpawned || _isDead.Value)
            {
                return false;
            }

            var position = new Vector3(0f, 5f, -216f);
            const float yaw = 180f;
            Teleport(position, yaw);
            _yaw = yaw;
            _pitch = 0f;
            ApplyCameraRotation();
            Debug.Log($"[WOF-AUTOMATION] NORTH_GATE_PROBE_READY position={transform.position} yaw={yaw:F1}");
            return true;
        }

        internal bool PrepareForAutomationVillagerInteractionProbe(Vector3 position, float yaw, float pitch)
        {
            if (!PrepareForAutomationCombatProbe(position, yaw))
            {
                return false;
            }

            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -82f, 82f);
            _latestServerInput.Yaw = _yaw;
            _latestServerInput.Pitch = _pitch;
            ApplyCameraHeight(false, false);
            ApplyCameraRotation();
            return true;
        }

        internal bool PrepareForAutomationStaticViewProbe(Vector3 position, float yaw, float pitch)
        {
            if (!PrepareForAutomationVillagerInteractionProbe(position, yaw, pitch))
            {
                return false;
            }

            _controller.enabled = false;
            return true;
        }

        internal bool ApplyAutomationMovementHeading(float yaw, float pitch = 0f)
        {
            if (!IsServer || !IsOwner || !IsSpawned || _isDead.Value || !_controller.enabled)
            {
                return false;
            }

            _yaw = Mathf.Repeat(yaw, 360f);
            _pitch = Mathf.Clamp(pitch, -82f, 82f);
            _latestServerInput.Yaw = _yaw;
            _latestServerInput.Pitch = _pitch;
            _authoritativeYaw.Value = _yaw;
            _authoritativePitch.Value = _pitch;
            ApplyCameraRotation();
            return true;
        }

        internal bool PrepareForAutomationCampfireProbe(Vector3 position, float armor)
        {
            if (!PrepareForAutomationCombatProbe(position, 0f))
            {
                return false;
            }

            _armor.Value = Mathf.Clamp(armor, 0f, WofGameConstants.MaxArmor);
            return true;
        }

        internal bool PrepareForAutomationDarrelReturnGateProbe()
        {
            if (!IsServer || !IsOwner || !IsSpawned || _isDead.Value ||
                !_darrelReturnArmed || !_hasDarrelReturnPosition)
            {
                return false;
            }

            ResetTeleportMotion();
            var gateCenter = WofDarrelGroveLayout.ReturnGateWorldPosition + Vector3.up * 8f;
            Teleport(gateCenter, 180f);
            ApplyQuestTeleportOwnerRpc(gateCenter, 180f);
            return true;
        }

        internal bool TryAutomationServerFireballAt(Vector3 targetPoint)
        {
            if (!IsServer || !IsSpawned)
            {
                return false;
            }

            if (!WofFireballCastMath.TryResolveTrustedServerTargetedLaunch(
                    _authoritativePosition.Value,
                    targetPoint,
                    out var origin,
                    out var direction))
            {
                return false;
            }

            return TrySpawnFireballServer(origin, direction);
        }

        internal bool BeginAutomationClientCombatProbe(
            Vector3 origin,
            Vector3 direction,
            ulong targetClientId,
            int requiredCasts)
        {
            if (!IsServer || !IsSpawned || OwnerClientId == NetworkManager.LocalClientId ||
                !WofFireballCastMath.IsFinite(origin) ||
                !WofFireballCastMath.TryNormalizeFiniteDirection(direction, out var normalizedDirection) ||
                requiredCasts <= 0)
            {
                return false;
            }

            _automationServerCastDirection = normalizedDirection;
            _remainingAutomationServerCasts = requiredCasts;
            BeginAutomationClientCombatProbeRpc(origin, normalizedDirection, targetClientId, requiredCasts);
            return true;
        }

        internal bool BeginAutomationClientTrainingDummyProbe(string instanceId, Vector3 position)
        {
            if (!IsServer || !IsSpawned || OwnerClientId == NetworkManager.LocalClientId ||
                string.IsNullOrEmpty(instanceId) || !WofFireballCastMath.IsFinite(position))
            {
                return false;
            }

            _automationTrainingDummyInstanceId = instanceId;
            _automationTrainingDummyClientPlacementObserved = false;
            BeginAutomationClientTrainingDummyProbeRpc(_automationTrainingDummyInstanceId, position);
            return true;
        }

        internal bool HasAutomationClientTrainingDummyPlacementAcknowledgement(string instanceId)
        {
            return IsServer && _automationTrainingDummyClientPlacementObserved &&
                   _automationTrainingDummyInstanceId.ToString() == instanceId;
        }

        [Rpc(SendTo.Owner)]
        private void BeginAutomationClientCombatProbeRpc(
            Vector3 origin,
            Vector3 direction,
            ulong targetClientId,
            int requiredCasts)
        {
            if (!IsOwner || IsServer)
            {
                Debug.LogError(
                    "[WOF-AUTOMATION] CLIENT_REPLICATION_PROBE_FAILED reason=cast-directive-received-without-remote-ownership");
                return;
            }

            if (!WofFireballCastMath.IsFinite(origin) ||
                !WofFireballCastMath.TryNormalizeFiniteDirection(direction, out var normalizedDirection))
            {
                Debug.LogError(
                    "[WOF-AUTOMATION] CLIENT_REPLICATION_PROBE_FAILED reason=invalid-server-cast-directive");
                return;
            }

            var bootstrap = WofBootstrap.Instance;
            if (bootstrap == null)
            {
                Debug.LogError(
                    "[WOF-AUTOMATION] CLIENT_REPLICATION_PROBE_FAILED reason=client-bootstrap-missing");
                return;
            }

            if (!bootstrap.BeginClientReplicationProbe(targetClientId, requiredCasts))
            {
                return;
            }

            StartCoroutine(RunAutomationClientCastRequests(origin, normalizedDirection, targetClientId, requiredCasts));
        }

        private IEnumerator RunAutomationClientCastRequests(
            Vector3 origin,
            Vector3 direction,
            ulong targetClientId,
            int requiredCasts)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            for (var castIndex = 1; castIndex <= requiredCasts; castIndex++)
            {
                if (!IsSpawned || !IsOwner || IsServer)
                {
                    Debug.LogError(
                        $"[WOF-AUTOMATION] CLIENT_REPLICATION_PROBE_FAILED reason=client-cast-loop-lost-ownership-index-{castIndex}");
                    yield break;
                }

                RequestCastRpc();
                Debug.Log(
                    $"[WOF-AUTOMATION] CLIENT_CAST_RPC_SENT owner={OwnerClientId} target={targetClientId} index={castIndex}");

                if (castIndex < requiredCasts)
                {
                    yield return new WaitForSecondsRealtime(WofGameConstants.GeneralCastCooldownSeconds + 0.25f);
                }
            }
        }

        [Rpc(SendTo.Owner)]
        private void BeginAutomationClientTrainingDummyProbeRpc(
            Unity.Collections.FixedString64Bytes instanceId,
            Vector3 position)
        {
            if (!IsOwner || IsServer)
            {
                FailAutomationClientTrainingDummyProbe(
                    "placement-directive-received-without-remote-ownership");
                return;
            }

            if (instanceId.Length == 0 || !WofFireballCastMath.IsFinite(position))
            {
                FailAutomationClientTrainingDummyProbe("invalid-placement-directive");
                return;
            }

            StartCoroutine(RunAutomationClientTrainingDummyProbe(instanceId, position));
        }

        private IEnumerator RunAutomationClientTrainingDummyProbe(
            Unity.Collections.FixedString64Bytes instanceId,
            Vector3 position)
        {
            var instance = instanceId.ToString();
            var placement = new WofEnginePlaceableRecord
            {
                instanceId = instance,
                placeableId = "training-spell-dummy",
                label = "Spell Dummy",
                x = position.x,
                y = position.y,
                z = position.z,
                yaw = 0f,
                trainingDummyHealth = WofTrainingDummyCombatRules.MaxHealth,
                trainingDummyRespawnAt = 0d,
                trainingDummyHitSequence = 0,
                trainingDummyLastSpell = -1
            };
            if (!RequestEnginePlaceableUpsert(placement))
            {
                FailAutomationClientTrainingDummyProbe("owner-upsert-request-rejected");
                yield break;
            }

            Debug.Log(
                $"[WOF-AUTOMATION] CLIENT_TRAINING_DUMMY_UPSERT_SENT owner={OwnerClientId} instance={instance}");

            const float replicationTimeoutSeconds = 20f;
            var deadline = Time.realtimeSinceStartup + replicationTimeoutSeconds;
            var sawPlacement = false;
            var sawDown = false;
            var observedHitSequence = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!IsSpawned || !IsOwner || IsServer)
                {
                    FailAutomationClientTrainingDummyProbe("client-lost-remote-ownership");
                    yield break;
                }

                if (!TryGetTrainingDummyState(instance, out var state))
                {
                    yield return null;
                    continue;
                }

                if (!sawPlacement)
                {
                    if (state.trainingDummyHealth != WofTrainingDummyCombatRules.MaxHealth ||
                        state.trainingDummyHitSequence != 0 || state.trainingDummyRespawnAt != 0d)
                    {
                        FailAutomationClientTrainingDummyProbe(
                            $"invalid-initial-state-health-{state.trainingDummyHealth:F0}-sequence-{state.trainingDummyHitSequence}-respawn-{state.trainingDummyRespawnAt:F3}");
                        yield break;
                    }

                    sawPlacement = true;
                    AcknowledgeAutomationClientTrainingDummyPlacementRpc(instanceId);
                    Debug.Log(
                        $"[WOF-AUTOMATION] CLIENT_TRAINING_DUMMY_PLACEMENT_REPLICATED observer={NetworkManager.LocalClientId} owner={OwnerClientId} instance={instance} health={state.trainingDummyHealth:F0}");
                }

                if (state.trainingDummyHitSequence > observedHitSequence)
                {
                    var expectedSequence = observedHitSequence + 1;
                    var expectedHealth = Mathf.Max(
                        0f,
                        WofTrainingDummyCombatRules.MaxHealth -
                        (expectedSequence * WofTrainingDummyCombatRules.GetDamage(WofSpellId.Fireball)));
                    if (state.trainingDummyHitSequence != expectedSequence ||
                        state.trainingDummyHealth != expectedHealth ||
                        state.trainingDummyLastSpell != (int)WofSpellId.Fireball)
                    {
                        FailAutomationClientTrainingDummyProbe(
                            $"unexpected-hit-state-sequence-{state.trainingDummyHitSequence}-expected-{expectedSequence}-health-{state.trainingDummyHealth:F0}-expected-health-{expectedHealth:F0}-spell-{state.trainingDummyLastSpell}");
                        yield break;
                    }

                    observedHitSequence = state.trainingDummyHitSequence;
                    Debug.Log(
                        $"[WOF-AUTOMATION] CLIENT_TRAINING_DUMMY_DAMAGE_REPLICATED observer={NetworkManager.LocalClientId} owner={OwnerClientId} instance={instance} index={observedHitSequence} health={state.trainingDummyHealth:F0}");
                    if (state.trainingDummyHealth <= 0f)
                    {
                        sawDown = true;
                        Debug.Log(
                            $"[WOF-AUTOMATION] CLIENT_TRAINING_DUMMY_DOWN_REPLICATED observer={NetworkManager.LocalClientId} owner={OwnerClientId} instance={instance} sequence={observedHitSequence}");
                    }
                }

                if (sawDown && observedHitSequence == 5 &&
                    state.trainingDummyHealth == WofTrainingDummyCombatRules.MaxHealth &&
                    state.trainingDummyRespawnAt == 0d)
                {
                    Debug.Log(
                        $"[WOF-AUTOMATION] CLIENT_TRAINING_DUMMY_RESPAWN_REPLICATED observer={NetworkManager.LocalClientId} owner={OwnerClientId} instance={instance} health={state.trainingDummyHealth:F0}");
                    Debug.Log(
                        $"[WOF-AUTOMATION] CLIENT_TRAINING_DUMMY_REPLICATION_PASSED observer={NetworkManager.LocalClientId} owner={OwnerClientId} instance={instance} hits={observedHitSequence}");
                    yield break;
                }

                yield return null;
            }

            FailAutomationClientTrainingDummyProbe(
                $"replication-timeout-placement-{sawPlacement}-hits-{observedHitSequence}-down-{sawDown}");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void AcknowledgeAutomationClientTrainingDummyPlacementRpc(
            Unity.Collections.FixedString64Bytes instanceId)
        {
            if (!IsServer || instanceId.Length == 0 ||
                !_automationTrainingDummyInstanceId.Equals(instanceId) ||
                !TryGetTrainingDummyState(instanceId.ToString(), out var state) ||
                state.trainingDummyHealth != WofTrainingDummyCombatRules.MaxHealth ||
                state.trainingDummyHitSequence != 0)
            {
                Debug.LogError(
                    "[WOF-AUTOMATION] COMBAT_PROBE_FAILED reason=invalid-client-training-dummy-placement-acknowledgement");
                return;
            }

            _automationTrainingDummyClientPlacementObserved = true;
            Debug.Log(
                $"[WOF-AUTOMATION] CLIENT_TRAINING_DUMMY_PLACEMENT_ACKNOWLEDGED owner={OwnerClientId} instance={instanceId}");
        }

        private static void FailAutomationClientTrainingDummyProbe(string reason)
        {
            Debug.LogError(
                $"[WOF-AUTOMATION] CLIENT_TRAINING_DUMMY_REPLICATION_FAILED reason={reason}");
        }

        private bool TryStartCastServer(WofHandSide hand)
        {
            if (!IsServer || _isMeditating.Value || _isDead.Value || IsServerCastActive(hand))
            {
                return false;
            }

            var spell = GetServerEquippedSpell(hand);
            var now = NetworkManager.ServerTime.Time;
            if (!WofFireballCastMath.IsFinite(now) || now < GetNextServerCastAt(hand))
            {
                return false;
            }

            var startMode = WofSpellCastingRules.GetStartMode(spell);
            if (startMode == WofSpellCastStartMode.Immediate &&
                !TryCastFromAuthoritativePoseServer(hand, spell))
            {
                return false;
            }

            if (!WofSpellCastingRules.KeepsHandActiveAfterStart(spell))
            {
                return true;
            }

            SetServerCastActive(hand, true);
            _castingUntil.Value = double.MaxValue;
            Debug.Log($"[WOF] SPELL_CAST_STARTED owner={OwnerClientId} hand={hand} spell={spell} mode={startMode}");
            return true;
        }

        private bool ReleaseCastServer(WofHandSide hand)
        {
            if (!IsServer || !IsServerCastActive(hand)) return false;
            var spell = GetServerEquippedSpell(hand);
            if (spell == WofSpellId.Grab)
            {
                ReleaseServerGrabTarget(hand, applyThrow: true);
            }
            SetServerCastActive(hand, false);
            RefreshServerCastingPose();
            Debug.Log($"[WOF] SPELL_CAST_RELEASED owner={OwnerClientId} hand={hand} spell={spell}");
            if (_isMeditating.Value || _isDead.Value || WofSpellCastingRules.SuppressesReleaseEffect(spell))
            {
                return true;
            }
            return TryCastFromAuthoritativePoseServer(hand, spell);
        }

        private void UpdateServerChannelledCasts(float deltaSeconds)
        {
            UpdateServerChannelledCast(WofHandSide.Left, deltaSeconds);
            UpdateServerChannelledCast(WofHandSide.Right, deltaSeconds);
        }

        private void UpdateServerChannelledCast(WofHandSide hand, float deltaSeconds)
        {
            if (!IsServerCastActive(hand) || deltaSeconds <= 0f) return;
            var spell = GetServerEquippedSpell(hand);
            if (spell == WofSpellId.Heal)
            {
                ref var healTimer = ref GetServerChannelTimer(hand);
                var firstTick = healTimer <= 0f;
                healTimer += deltaSeconds;
                ApplyServerHealing(
                    WofSpellCastingRules.GetHealAmount(
                        deltaSeconds,
                        WofSpellRuntimeTuning.HealSpellHealPerSecond),
                    clearToxicEffects: true);
                if (firstTick)
                {
                    Debug.Log(
                        $"[WOF] CHANNEL_SPELL_TICK owner={OwnerClientId} hand={hand} spell={WofSpellId.Heal} rate={WofSpellRuntimeTuning.HealSpellHealPerSecond:F1}");
                }
                return;
            }
            if (spell != WofSpellId.Flamethrower) return;

            ref var timer = ref GetServerChannelTimer(hand);
            if (!WofSpellCastingRules.AdvanceFlamethrowerTimer(timer, deltaSeconds, out timer)) return;
            TrySpawnChannelledFlamethrowerServer(hand);
        }

        private bool TrySpawnChannelledFlamethrowerServer(WofHandSide hand)
        {
            if (fireballPrefab == null ||
                !TryResolveAuthoritativeSpellLaunch(out var origin, out var direction))
            {
                return false;
            }
            direction += new Vector3(
                (UnityEngine.Random.value - 0.5f) * 0.15f,
                (UnityEngine.Random.value - 0.5f) * 0.15f,
                (UnityEngine.Random.value - 0.5f) * 0.15f);
            if (!WofFireballCastMath.TryNormalizeFiniteDirection(direction, out var normalized)) return false;
            SpawnSpellObject(WofSpellId.Flamethrower, origin, normalized);
            Debug.Log($"[WOF] CHANNEL_SPELL_TICK owner={OwnerClientId} hand={hand} spell={WofSpellId.Flamethrower}");
            return true;
        }

        private void CancelServerCasting()
        {
            if (!_serverLeftCastActive && !_serverRightCastActive) return;
            ReleaseServerGrabTarget(WofHandSide.Left, applyThrow: false);
            ReleaseServerGrabTarget(WofHandSide.Right, applyThrow: false);
            SetServerCastActive(WofHandSide.Left, false);
            SetServerCastActive(WofHandSide.Right, false);
            RefreshServerCastingPose();
        }

        private bool IsServerCastActive(WofHandSide hand)
        {
            return hand == WofHandSide.Left ? _serverLeftCastActive : _serverRightCastActive;
        }

        private void SetServerCastActive(WofHandSide hand, bool active)
        {
            if (hand == WofHandSide.Left)
            {
                _serverLeftCastActive = active;
                _serverLeftChannelTimer = 0f;
            }
            else
            {
                _serverRightCastActive = active;
                _serverRightChannelTimer = 0f;
            }
        }

        private ref float GetServerChannelTimer(WofHandSide hand)
        {
            if (hand == WofHandSide.Left) return ref _serverLeftChannelTimer;
            return ref _serverRightChannelTimer;
        }

        private WofSpellId GetServerEquippedSpell(WofHandSide hand)
        {
            return hand == WofHandSide.Left ? LeftEquippedSpell : RightEquippedSpell;
        }

        private WofPlayerController GetServerGrabTarget(WofHandSide hand)
        {
            return hand == WofHandSide.Left ? _serverLeftGrabTarget : _serverRightGrabTarget;
        }

        private void SetServerGrabTarget(WofHandSide hand, WofPlayerController target)
        {
            if (hand == WofHandSide.Left) _serverLeftGrabTarget = target;
            else _serverRightGrabTarget = target;
        }

        private void ReleaseServerGrabTarget(WofHandSide hand, bool applyThrow)
        {
            var target = GetServerGrabTarget(hand);
            SetServerGrabTarget(hand, null);
            if (target == null || !target.IsSpawned || target._serverGrabCaster != this ||
                target._serverGrabCasterHand != hand)
            {
                return;
            }

            TryResolveAuthoritativeSpellLaunch(out _, out var direction);
            target.EndServerGrab(applyThrow, direction);
        }

        private void RefreshServerCastingPose()
        {
            _castingUntil.Value = _serverLeftCastActive || _serverRightCastActive
                ? double.MaxValue
                : NetworkManager.ServerTime.Time;
        }

        private bool TryCastFromAuthoritativePoseServer(WofHandSide hand = WofHandSide.Right)
        {
            return TryCastFromAuthoritativePoseServer(hand, GetServerEquippedSpell(hand));
        }

        private bool TryCastFromAuthoritativePoseServer(WofHandSide hand, WofSpellId equippedSpell)
        {
            if (_isMeditating.Value || _isDead.Value)
            {
                return false;
            }
            if (_serverLilyCoilState.Active)
            {
                var frame = WofLilyCoilLayout.GetFrame(_serverLilyCoilState.T);
                var radial = WofLilyCoilLayout.GetRadial(frame, _serverLilyCoilState.SurfaceAngle);
                var playerUp = -radial;
                var bodyRotation = Quaternion.LookRotation(frame.Tangent, playerUp);
                var viewRotation = bodyRotation * Quaternion.Euler(
                    _authoritativePitch.Value,
                    _authoritativeYaw.Value,
                    0f);
                var eyeHeight = _isSliding.Value
                    ? WofMovementMath.ReactLowCameraHeight
                    : WofMovementMath.ReactStandingCameraHeight;
                if (!WofFireballCastMath.TryResolveOrientedLaunch(
                        _authoritativePosition.Value,
                        playerUp,
                        eyeHeight,
                        viewRotation * Vector3.forward,
                        out var tubeOrigin,
                        out var tubeDirection))
                {
                    return false;
                }
                return TryCastResolvedSpellServer(hand, equippedSpell, tubeOrigin, tubeDirection);
            }

            if (!WofFireballCastMath.TryResolveAuthoritativeLaunch(
                    _authoritativePosition.Value,
                    _authoritativeYaw.Value,
                    _authoritativePitch.Value,
                    out var origin,
                    out var direction))
            {
                return false;
            }

            return TryCastResolvedSpellServer(hand, equippedSpell, origin, direction);
        }

        private bool TryResolveAuthoritativeSpellLaunch(out Vector3 origin, out Vector3 direction)
        {
            if (_serverLilyCoilState.Active)
            {
                var frame = WofLilyCoilLayout.GetFrame(_serverLilyCoilState.T);
                var radial = WofLilyCoilLayout.GetRadial(frame, _serverLilyCoilState.SurfaceAngle);
                var playerUp = -radial;
                var bodyRotation = Quaternion.LookRotation(frame.Tangent, playerUp);
                var viewRotation = bodyRotation * Quaternion.Euler(
                    _authoritativePitch.Value,
                    _authoritativeYaw.Value,
                    0f);
                var eyeHeight = _isSliding.Value
                    ? WofMovementMath.ReactLowCameraHeight
                    : WofMovementMath.ReactStandingCameraHeight;
                return WofFireballCastMath.TryResolveOrientedLaunch(
                    _authoritativePosition.Value,
                    playerUp,
                    eyeHeight,
                    viewRotation * Vector3.forward,
                    out origin,
                    out direction);
            }

            return WofFireballCastMath.TryResolveAuthoritativeLaunch(
                _authoritativePosition.Value,
                _authoritativeYaw.Value,
                _authoritativePitch.Value,
                out origin,
                out direction);
        }

        private bool TryCastResolvedSpellServer(
            WofHandSide hand,
            WofSpellId spell,
            Vector3 origin,
            Vector3 direction)
        {
            return WofSpellRuntimeTuning.GetMode(spell) switch
            {
                WofSpellRuntimeMode.Self => TryApplySelfSpellServer(hand, spell),
                WofSpellRuntimeMode.Hitscan => TryCastHitscanSpellServer(hand, spell, origin, direction),
                WofSpellRuntimeMode.GroundArea => TrySpawnAreaSpellServer(hand, spell, origin, direction),
                _ => TrySpawnSpellServer(hand, spell, origin, direction)
            };
        }

        private bool TryApplySelfSpellServer(WofHandSide hand, WofSpellId spell)
        {
            if (!TryBeginSpellCastServer(hand, spell, WofSpellLoadout.SelfBuffHandChargeSeconds, out var now))
            {
                return false;
            }

            switch (spell)
            {
                case WofSpellId.Heal:
                    ApplyServerHealing(WofSpellRuntimeTuning.HealSpellHealPerSecond, clearToxicEffects: true);
                    break;
                case WofSpellId.Blink:
                {
                    var angle = UnityEngine.Random.value * Mathf.PI * 2f;
                    var distance = Mathf.Lerp(
                        WofSpellRuntimeTuning.BlinkMinimumDistance,
                        WofSpellRuntimeTuning.BlinkMaximumDistance,
                        UnityEngine.Random.value);
                    var target = _authoritativePosition.Value +
                                 new Vector3(Mathf.Cos(angle) * distance,
                                     WofSpellRuntimeTuning.BlinkUpwardOffset,
                                     Mathf.Sin(angle) * distance);
                    ResetTeleportMotion();
                    Teleport(target, _authoritativeYaw.Value);
                    ApplyQuestTeleportOwnerRpc(target, _authoritativeYaw.Value);
                    break;
                }
                case WofSpellId.MagicArmor:
                    _armor.Value = WofGameConstants.MaxArmor;
                    break;
                case WofSpellId.SpeedBoost:
                    _speedBoostUntil.Value = now + WofSpellLoadout.SelfBuffDurationSeconds;
                    break;
                case WofSpellId.JumpBoost:
                    _jumpBoostUntil.Value = now + WofSpellLoadout.SelfBuffDurationSeconds;
                    _serverVerticalVelocity = Mathf.Max(
                        _serverVerticalVelocity,
                        WofGameConstants.JumpSpeed * WofSpellLoadout.JumpBoostMultiplier);
                    break;
                case WofSpellId.MagicGlassOrb:
                    _magicGlassOrbUntil.Value = double.MaxValue;
                    break;
                default:
                    SetNextServerCastAt(hand, now);
                    return false;
            }
            Debug.Log($"[WOF] SELF_SPELL_CAST owner={OwnerClientId} hand={hand} spell={spell}");
            return true;
        }

        private bool TryCastHitscanSpellServer(
            WofHandSide hand,
            WofSpellId spell,
            Vector3 origin,
            Vector3 direction)
        {
            if (!TryBeginSpellCastServer(hand, spell, 0.36f, out _)) return false;
            var normalized = direction.normalized;
            if (spell == WofSpellId.ArcaneBeam)
            {
                foreach (var player in FindObjectsByType<WofPlayerController>(FindObjectsSortMode.None))
                {
                    if (!IsValidSpellTarget(player)) continue;
                    var toTarget = player.transform.position + Vector3.up - origin;
                    var projection = Mathf.Clamp(Vector3.Dot(toTarget, normalized), 0f, WofSpellRuntimeTuning.HitscanRange);
                    var closest = origin + normalized * projection;
                    if (projection > 0f &&
                        Vector3.SqrMagnitude(player.transform.position + Vector3.up - closest) <=
                        WofSpellRuntimeTuning.HitscanRadius * WofSpellRuntimeTuning.HitscanRadius &&
                        !player.ShouldBlockServerSpellImpact(origin))
                        player.ApplyServerDamage(35f, OwnerClientId);
                }
                WofTrainingDummyRuntime.ApplyServerHitscanSpellImpact(
                    origin,
                    normalized,
                    WofSpellRuntimeTuning.HitscanRange,
                    WofSpellRuntimeTuning.HitscanRadius,
                    spell,
                    OwnerClientId);
                SpawnSpellObject(spell, origin + normalized * 8f, normalized);
            }
            else
            {
                var range = spell == WofSpellId.Grab
                    ? WofSpellRuntimeTuning.GrabRange
                    : WofSpellRuntimeTuning.DirectStatusRange;
                var radius = spell == WofSpellId.Grab
                    ? WofSpellRuntimeTuning.GrabRadius
                    : WofSpellRuntimeTuning.DirectStatusRadius;
                var target = FindNearestSpellTarget(origin, normalized, range, radius);
                if (target != null)
                {
                    if (spell == WofSpellId.Grab)
                    {
                        var targetPoint = target.transform.position + Vector3.up * 0.85f;
                        var distance = Vector3.Dot(targetPoint - origin, normalized);
                        if (target.BeginServerGrab(this, hand, distance)) SetServerGrabTarget(hand, target);
                    }
                    else target.ApplyServerStatus(WofSpellId.TungstonBallsack, OwnerClientId);
                }
                SpawnSpellObject(spell, origin + normalized * 3f, normalized);
            }
            Debug.Log($"[WOF] HITSCAN_SPELL_CAST owner={OwnerClientId} hand={hand} spell={spell}");
            return true;
        }

        private bool TrySpawnAreaSpellServer(
            WofHandSide hand,
            WofSpellId spell,
            Vector3 origin,
            Vector3 direction)
        {
            if (!TryBeginSpellCastServer(hand, spell, 0.36f, out var now)) return false;
            var flatDirection = new Vector3(direction.x, 0f, direction.z);
            if (flatDirection.sqrMagnitude < 0.001f) flatDirection = transform.forward;
            flatDirection.Normalize();
            var position = spell switch
            {
                WofSpellId.Tornado => _authoritativePosition.Value +
                                      flatDirection * WofSpellRuntimeTuning.TornadoSummonDistance + Vector3.up * 0.2f,
                WofSpellId.MeteorShower => _authoritativePosition.Value +
                                           flatDirection * WofSpellRuntimeTuning.MeteorSummonDistance + Vector3.up * 0.2f,
                WofSpellId.HealingCrystals => _authoritativePosition.Value,
                WofSpellId.DiscShield or WofSpellId.OrbShield => _authoritativePosition.Value + Vector3.up,
                _ => origin
            };
            if (spell == WofSpellId.DiscShield) _discShieldUntil.Value = now + 10d;
            if (spell == WofSpellId.OrbShield) _orbShieldUntil.Value = now + 10d;
            SpawnSpellObject(spell, position, flatDirection);
            Debug.Log($"[WOF] AREA_SPELL_CAST owner={OwnerClientId} hand={hand} spell={spell} position={position}");
            return true;
        }

        private bool TryCastFromServerDirection(Vector3 serverDirection)
        {
            if (!WofFireballCastMath.TryResolveTrustedServerDirectedLaunch(
                    _authoritativePosition.Value,
                    serverDirection,
                    out var origin,
                    out var direction))
            {
                return false;
            }

            return TrySpawnSpellServer(WofHandSide.Right, WofSpellId.Fireball, origin, direction);
        }

        private bool TrySpawnFireballServer(Vector3 origin, Vector3 direction)
        {
            return TrySpawnSpellServer(WofHandSide.Right, WofSpellId.Fireball, origin, direction);
        }

        private bool TrySpawnSpellServer(
            WofHandSide hand,
            WofSpellId spell,
            Vector3 origin,
            Vector3 direction)
        {
            if (!IsServer || _isDead.Value || fireballPrefab == null)
            {
                return false;
            }

            var now = NetworkManager.ServerTime.Time;
            if (!WofFireballCastMath.IsFinite(now) ||
                !WofFireballCastMath.IsFinite(GetNextServerCastAt(hand)) ||
                !WofFireballCastMath.IsFinite(_authoritativePosition.Value) ||
                !WofFireballCastMath.IsFinite(origin) ||
                !WofFireballCastMath.TryNormalizeFiniteDirection(direction, out var normalizedDirection) ||
                Vector3.Distance(origin, _authoritativePosition.Value) > 3.5f ||
                now < GetNextServerCastAt(hand))
            {
                return false;
            }

            SetNextServerCastAt(hand, now + WofSpellRuntimeTuning.GetCastCooldownSeconds(spell));
            _castingUntil.Value = now + 0.36d;
            SpawnSpellObject(spell, origin, normalizedDirection);
            Debug.Log($"[WOF-AUTOMATION] SPELL_CAST owner={OwnerClientId} hand={hand} spell={spell}");
            return true;
        }

        private GameObject SpawnSpellObject(WofSpellId spell, Vector3 position, Vector3 direction)
        {
            var normalized = direction.sqrMagnitude > 0.000001f ? direction.normalized : transform.forward;
            var projectileObject = Instantiate(fireballPrefab, position, Quaternion.LookRotation(normalized));
            projectileObject.GetComponent<WofFireballProjectile>()
                .InitializeServer(OwnerClientId, normalized, spell);
            projectileObject.GetComponent<NetworkObject>().Spawn(true);
            return projectileObject;
        }

        private bool TryBeginSpellCastServer(
            WofHandSide hand,
            WofSpellId spell,
            float poseSeconds,
            out double now)
        {
            now = NetworkManager.ServerTime.Time;
            if (!IsServer || _isDead.Value || fireballPrefab == null || now < GetNextServerCastAt(hand)) return false;
            SetNextServerCastAt(hand, now + WofSpellRuntimeTuning.GetCastCooldownSeconds(spell));
            _castingUntil.Value = _serverLeftCastActive || _serverRightCastActive
                ? double.MaxValue
                : now + poseSeconds;
            return true;
        }

        private double GetNextServerCastAt(WofHandSide hand)
        {
            return hand == WofHandSide.Left ? _nextServerLeftCastAt : _nextServerRightCastAt;
        }

        private void SetNextServerCastAt(WofHandSide hand, double value)
        {
            if (hand == WofHandSide.Left) _nextServerLeftCastAt = value;
            else _nextServerRightCastAt = value;
        }

        private bool IsValidSpellTarget(WofPlayerController player)
        {
            return player != null && player.IsSpawned && !player.IsDead && player.OwnerClientId != OwnerClientId;
        }

        private WofPlayerController FindNearestSpellTarget(
            Vector3 origin,
            Vector3 direction,
            float range,
            float radius)
        {
            WofPlayerController best = null;
            var bestProjection = float.MaxValue;
            var radiusSquared = radius * radius;
            foreach (var player in FindObjectsByType<WofPlayerController>(FindObjectsSortMode.None))
            {
                if (!IsValidSpellTarget(player)) continue;
                var point = player.transform.position + Vector3.up;
                var toTarget = point - origin;
                var projection = Vector3.Dot(toTarget, direction);
                if (projection <= 1.25f || projection > range || projection >= bestProjection) continue;
                if (Vector3.SqrMagnitude(point - (origin + direction * projection)) > radiusSquared) continue;
                best = player;
                bestProjection = projection;
            }
            return best;
        }

        private WofMovementFrame Simulate(
            ref float verticalVelocity,
            ref WofMovementRuntimeState movementState,
            ref WofLilyCoilMovementState lilyCoilState,
            WofInputCommand command,
            float deltaTime)
        {
            if (!_controller.enabled)
            {
                return new WofMovementFrame(
                    WofGameConstants.WalkSpeed,
                    false,
                    movementState.IsSliding,
                    movementState.IsCrouching);
            }

            if (IsMeditating)
            {
                WofMovementMath.Reset(ref movementState);
                verticalVelocity = 0f;
                ApplyCameraHeight(false, false);
                return new WofMovementFrame(WofGameConstants.WalkSpeed, false, false, false);
            }

            if (IsVClipEnabled)
            {
                WofMovementMath.ResetForVClip(ref movementState);
                WofLilyCoilMovement.Reset(ref lilyCoilState);
                _lastLilyCoilGrounded = false;
                _lastLilyCoilMoving = false;
                _lastGroundedAt = float.NegativeInfinity;
                transform.rotation = Quaternion.Euler(0f, command.Yaw, 0f);
                var vclipVelocity = WofMovementMath.ResolveVClipVelocity(
                    command.Move,
                    command.Yaw,
                    command.Jump,
                    command.Slide,
                    command.Sprint,
                    IsSpeedBoostActive,
                    IsTimedBuffActive(_slowUntil.Value));
                if (!_vclipMovementLogged && IsOwner && vclipVelocity.sqrMagnitude > 0.0001f)
                {
                    _vclipMovementLogged = true;
                    Debug.Log($"[WOF-AUTOMATION] VCLIP_MOVEMENT velocity={vclipVelocity} collisions={_controller.detectCollisions.ToString().ToLowerInvariant()}");
                }
                verticalVelocity = vclipVelocity.y;
                SetControllerPosition(transform.position + vclipVelocity * deltaTime);
                ApplyCameraHeight(false, false);
                var sprinting = command.Sprint &&
                                (command.Move.sqrMagnitude > 0f || command.Jump || command.Slide);
                return new WofMovementFrame(
                    vclipVelocity.sqrMagnitude > 0f ? vclipVelocity.magnitude : WofGameConstants.WalkSpeed,
                    sprinting,
                    false,
                    false);
            }

            if (WofLilyCoilLayout.IsInsideTubeRealm(transform.position))
            {
                return SimulateLilyCoilTube(
                    ref verticalVelocity,
                    ref movementState,
                    ref lilyCoilState,
                    command,
                    deltaTime);
            }

            if (lilyCoilState.Active)
            {
                WofLilyCoilMovement.Reset(ref lilyCoilState);
                _lastLilyCoilGrounded = true;
                _lastLilyCoilMoving = false;
                if (IsOwner)
                {
                    _yaw = transform.eulerAngles.y;
                    _pitch = 0f;
                    ApplyCameraHeight(false, false);
                }
            }

            transform.rotation = Quaternion.Euler(0f, command.Yaw, 0f);
            if (_activeMountainLadderZones.Count > 0)
            {
                var headingOnLadder = Quaternion.Euler(0f, command.Yaw, 0f);
                var ladderPlanar = headingOnLadder *
                                   (Vector3.right * command.Move.x + Vector3.forward * command.Move.y);
                var ladderVerticalInput = Mathf.Clamp(command.Move.y + (command.Jump ? 1f : 0f), -1f, 1f);
                if (Mathf.Abs(ladderVerticalInput) > 0.0001f)
                {
                    ladderPlanar *= WofMountainLadderZone.PlanarDamping;
                }
                verticalVelocity = ladderVerticalInput * WofMountainLadderZone.ClimbSpeed;
                var ladderSpeed = WofGameConstants.WalkSpeed *
                                  (IsTimedBuffActive(_slowUntil.Value)
                                      ? WofSpellRuntimeTuning.TungstonSlowMultiplier
                                      : 1f);
                _controller.Move((ladderPlanar * ladderSpeed + Vector3.up * verticalVelocity) * deltaTime);
                transform.rotation = Quaternion.Euler(0f, command.Yaw, 0f);
                ApplyCameraHeight(false, false);
                return new WofMovementFrame(WofGameConstants.WalkSpeed, false, false, false);
            }
            if (_controller.isGrounded)
            {
                _lastGroundedAt = Time.time;
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = -2f;
                }
            }

            var controllerVelocity = _controller.velocity;
            var effectiveGrounded = (Time.time - _lastGroundedAt) <= WofGameConstants.GroundCoyoteSeconds;
            var movementFrame = WofMovementMath.ResolveFrame(
                ref movementState,
                command.Move,
                command.Sprint,
                command.Slide,
                command.Jump,
                effectiveGrounded,
                verticalVelocity,
                controllerVelocity.x * controllerVelocity.x + controllerVelocity.z * controllerVelocity.z,
                Time.time,
                deltaTime);

            if (WofMovementMath.ApplyJumpThruster(
                    ref movementState,
                    command.Jump,
                    _controller.isGrounded,
                    effectiveGrounded,
                    IsJumpBoostActive,
                    ref verticalVelocity,
                    deltaTime))
            {
                _lastGroundedAt = float.NegativeInfinity;
            }

            verticalVelocity += WofGameConstants.Gravity * deltaTime;
            var heading = Quaternion.Euler(0f, command.Yaw, 0f);
            var planar = heading * (Vector3.right * command.Move.x + Vector3.forward * command.Move.y);
            var movementSpeed = movementFrame.IsSliding
                ? movementFrame.Speed
                : movementFrame.Speed * (IsSpeedBoostActive ? WofSpellLoadout.SpeedBoostMultiplier : 1f);
            if (IsTimedBuffActive(_slowUntil.Value))
            {
                movementSpeed *= WofSpellRuntimeTuning.TungstonSlowMultiplier;
            }
            var velocity = planar * movementSpeed + Vector3.up * verticalVelocity;
            if (_serverExternalPullFrames > 0)
            {
                velocity.x += _serverExternalPullVelocity.x;
                velocity.z += _serverExternalPullVelocity.z;
                verticalVelocity = _serverExternalPullVelocity.y;
                velocity.y = verticalVelocity;
                _serverExternalPullFrames--;
                if (_serverExternalPullFrames <= 0) _serverExternalPullVelocity = Vector3.zero;
            }
            velocity += _serverImpulsePlanarVelocity;
            _controller.Move(velocity * deltaTime);
            if (_controller.isGrounded && _serverImpulsePlanarVelocity.sqrMagnitude > 0f)
            {
                _serverImpulsePlanarVelocity = Vector3.zero;
            }
            transform.rotation = Quaternion.Euler(0f, command.Yaw, 0f);
            ApplyCameraHeight(movementFrame.IsSliding, movementFrame.IsCrouching);
            return new WofMovementFrame(
                movementSpeed,
                movementFrame.IsSprinting,
                movementFrame.IsSliding,
                movementFrame.IsCrouching);
        }

        private WofMovementFrame SimulateLilyCoilTube(
            ref float verticalVelocity,
            ref WofMovementRuntimeState movementState,
            ref WofLilyCoilMovementState lilyCoilState,
            WofInputCommand command,
            float deltaTime)
        {
            var entering = !lilyCoilState.Active;
            if (entering)
            {
                WofLilyCoilMovement.Enter(ref lilyCoilState, transform.position);
                WofMovementMath.Reset(ref movementState);
                verticalVelocity = 0f;
                if (IsOwner)
                {
                    _lilyCoilViewYaw = 0f;
                    _lilyCoilViewPitch = 0f;
                    _yaw = 0f;
                    _pitch = 0f;
                }
            }

            var viewYaw = entering ? 0f : command.Yaw;
            var viewPitch = entering ? 0f : command.Pitch;
            var tubeFrame = WofLilyCoilMovement.Simulate(
                ref lilyCoilState,
                command.Move,
                viewYaw,
                viewPitch,
                command.Sprint,
                command.Slide,
                command.Jump,
                Time.time,
                deltaTime);

            SetControllerPose(tubeFrame.Position, tubeFrame.BodyRotation);
            _lastLilyCoilGrounded = tubeFrame.IsGrounded;
            _lastLilyCoilMoving = tubeFrame.IsMoving;
            if (IsOwner && cameraPivot != null)
            {
                cameraPivot.localRotation = tubeFrame.CameraLocalRotation;
                var cameraLocalPosition = cameraPivot.localPosition;
                cameraLocalPosition.y = tubeFrame.IsSliding
                    ? WofMovementMath.ReactLowCameraHeight
                    : WofMovementMath.ReactStandingCameraHeight;
                cameraPivot.localPosition = cameraLocalPosition;
            }

            return new WofMovementFrame(
                tubeFrame.MoveSpeed,
                tubeFrame.IsSprinting,
                tubeFrame.IsSliding,
                false);
        }

        public void SetMountainLadderZone(string zoneId, bool active)
        {
            if (string.IsNullOrWhiteSpace(zoneId)) return;
            if (active) _activeMountainLadderZones.Add(zoneId);
            else _activeMountainLadderZones.Remove(zoneId);
        }

        private void ApplyCameraRotation()
        {
            if (cameraPivot == null)
            {
                return;
            }

            if (IsLocalLilyCoilActive)
            {
                cameraPivot.localRotation = Quaternion.Euler(_lilyCoilViewPitch, _lilyCoilViewYaw, 0f);
                return;
            }

            var localYaw = Mathf.DeltaAngle(transform.eulerAngles.y, _yaw);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, localYaw, 0f);
        }

        private void ApplyCameraHeight(bool isSliding, bool isCrouching)
        {
            if (!IsOwner || cameraPivot == null)
            {
                return;
            }

            var localPosition = cameraPivot.localPosition;
            var targetHeight = _grassOverheadViewProbe
                ? 26f
                : IsMeditating
                    ? WofMovementMath.UnityMeditationCameraHeight
                    : WofMovementMath.ResolveCameraHeight(isSliding, isCrouching);
            localPosition.y = IsMeditating && !_grassOverheadViewProbe
                ? Mathf.Lerp(localPosition.y, targetHeight, WofAstralMeditationRules.CameraLerpAlpha)
                : targetHeight;
            cameraPivot.localPosition = localPosition;
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(WofGameConstants.RespawnDelaySeconds);
            if (!IsServer || !IsSpawned)
            {
                yield break;
            }

            _serverVerticalVelocity = 0f;
            _serverExternalPullVelocity = Vector3.zero;
            _serverExternalPullFrames = 0;
            _serverImpulsePlanarVelocity = Vector3.zero;
            _latestServerInput = default;
            Teleport(
                WofTreeHouseVillageLayout.DefaultPlayerSpawn,
                WofTreeHouseVillageLayout.DefaultPlayerYawDegrees);
            _armor.Value = 0;
            _health.Value = WofGameConstants.MaxHealth;
            _isDead.Value = false;
            _discShieldUntil.Value = 0d;
            _orbShieldUntil.Value = 0d;
            _slowUntil.Value = 0d;
            _sleepUntil.Value = 0d;
            _poisonUntil.Value = 0d;
            _acidUntil.Value = 0d;
            _flashbangOpacity.Value = 0f;
            _isGrabbed.Value = false;
            Debug.Log($"[WOF-AUTOMATION] PLAYER_RESPAWNED id={OwnerClientId}");
        }

        private void Teleport(Vector3 position, float yaw)
        {
            WofMovementMath.Reset(ref _serverMovementState);
            WofMovementMath.Reset(ref _predictedMovementState);
            WofLilyCoilMovement.Reset(ref _serverLilyCoilState);
            WofLilyCoilMovement.Reset(ref _predictedLilyCoilState);
            _lastLilyCoilGrounded = true;
            _lastLilyCoilMoving = false;
            if (IsServer)
            {
                _isSprinting.Value = false;
                _isSliding.Value = false;
                _isCrouching.Value = false;
            }
            ApplyCameraHeight(false, false);

            var shouldEnable = IsServer || IsOwner;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _controller.enabled = shouldEnable && !_isDead.Value;
            _authoritativePosition.Value = position;
            _authoritativeYaw.Value = yaw;
            _authoritativePitch.Value = 0f;
        }

        private void SetControllerPose(Vector3 position, Quaternion rotation)
        {
            var wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = wasEnabled;
        }

        private void SetControllerPosition(Vector3 position)
        {
            var wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = wasEnabled;
        }

        private void HandleHealthChanged(float previous, float current)
        {
            WofBootstrap.Instance?.ObserveClientReplicatedHealth(OwnerClientId, previous, current);
            if (IsOwner)
            {
                PublishHud();
            }
        }

        private void HandleArmorChanged(float previous, float current)
        {
            if (IsOwner)
            {
                PublishHud();
            }
        }

        private void HandleManaChanged(float previous, float current)
        {
            if (IsOwner) PublishHud();
        }

        private void HandleEquippedSpellChanged(int previous, int current)
        {
            if (IsOwner)
            {
                PublishHud();
            }
        }

        private void HandleMeditatingChanged(bool previous, bool current)
        {
            if (!IsOwner)
            {
                return;
            }

            if (_localMeditationState.IsActive != current)
            {
                WofAstralMeditationRules.SetAuthoritativeActive(
                    ref _localMeditationState,
                    current);
            }
            if (current)
            {
                ClearOwnerCastPresentation();
                WofInputRouter.ResetTransientGameplayActions();
                WofMovementMath.Reset(ref _predictedMovementState);
                _predictedVerticalVelocity = 0f;
            }
            ApplyCameraHeight(false, false);
            PublishHud();
        }

        private void HandleDeadChanged(bool previous, bool current)
        {
            WofBootstrap.Instance?.ObserveClientReplicatedDead(OwnerClientId, previous, current);
            var needsController = (IsServer || IsOwner) && !current;
            _controller.enabled = needsController;
            _controller.detectCollisions = !IsVClipEnabled;
            if (visualRoot != null)
            {
                visualRoot.SetActive(!IsOwner);
            }

            if (IsOwner)
            {
                if (current)
                {
                    ClearOwnerCastPresentation();
                    WofAstralMeditationRules.SetAuthoritativeActive(ref _localMeditationState, false);
                }
                WofMovementMath.Reset(ref _predictedMovementState);
                ApplyCameraHeight(false, false);
                WofHud.Instance?.SetStatus(current ? "YOU DIED — respawning..." : string.Empty);
                if (!current)
                {
                    _predictedVerticalVelocity = 0f;
                    SetControllerPosition(_authoritativePosition.Value);
                }
            }
        }

        private void HandleVClipEnabledChanged(bool previous, bool current)
        {
            if (IsOwner && _pendingVClipEnabled == current)
            {
                _pendingVClipEnabled = null;
            }
            ApplyVClipCollisionState(IsVClipEnabled);
            if (!current)
            {
                _serverVerticalVelocity = 0f;
                _predictedVerticalVelocity = 0f;
            }
            Debug.Log($"[WOF-AUTOMATION] VCLIP_CHANGED owner={OwnerClientId} enabled={current.ToString().ToLowerInvariant()}");
        }

        private void ApplyVClipCollisionState(bool enabled)
        {
            if (_controller != null)
            {
                _controller.detectCollisions = !enabled;
            }
        }

        private void PublishHud()
        {
            WofHud.Instance?.SetVitals(_health.Value, _armor.Value);
            WofHud.Instance?.SetMana(
                _leftMana.Value / WofManaRules.MaximumPower,
                _rightMana.Value / WofManaRules.MaximumPower);
            WofHud.Instance?.SetEquippedSpells(
                WofSpellLoadout.GetDisplayName(LeftEquippedSpell),
                WofSpellLoadout.GetDisplayName(RightEquippedSpell));
            WofHud.Instance?.SetMagicHandsVisible(!IsMeditating);
            WofHud.Instance?.SetHeldSpellVisibility(
                !IsMeditating && LeftEquippedSpell == WofSpellId.Fireball,
                !IsMeditating && RightEquippedSpell == WofSpellId.Fireball);
            PublishMovementHud();
        }

        private void PublishMovementHud()
        {
            if (!IsOwner)
            {
                return;
            }
            var fuel = IsServer ? _serverMovementState.ThrusterFuel : _predictedMovementState.ThrusterFuel;
            if (IsLocalLilyCoilActive)
            {
                fuel = IsServer ? _serverLilyCoilState.ThrusterFuel : _predictedLilyCoilState.ThrusterFuel;
            }
            WofHud.Instance?.SetAether(fuel);
        }

        private bool IsSpeedBoostActive => IsTimedBuffActive(_speedBoostUntil.Value);
        private bool IsJumpBoostActive => IsTimedBuffActive(_jumpBoostUntil.Value);

        private bool IsTimedBuffActive(double until)
        {
            return IsSpawned && NetworkManager != null && NetworkManager.ServerTime.Time < until;
        }

        private void ApplyManaDecay()
        {
            if (!IsServer || NetworkManager == null) return;
            var now = NetworkManager.ServerTime.Time;
            if (_nextManaDecayAt <= 0d) _nextManaDecayAt = now + 1d;
            if (now < _nextManaDecayAt) return;
            var elapsedSeconds = Mathf.Max(1, (int)System.Math.Floor(now - _nextManaDecayAt) + 1);
            _nextManaDecayAt += elapsedSeconds;
            _leftMana.Value = WofManaRules.Decay(_leftMana.Value, elapsedSeconds);
            _rightMana.Value = WofManaRules.Decay(_rightMana.Value, elapsedSeconds);
        }

        private static WofSpellId ResolveSpell(int value, WofSpellId fallback)
        {
            return WofSpellLoadout.IsValid(value) ? (WofSpellId)value : fallback;
        }
    }
}
