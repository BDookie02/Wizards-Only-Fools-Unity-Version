using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

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
        private readonly NetworkVariable<bool> _isDead = new(
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

        private CharacterController _controller;
        private readonly HashSet<string> _activeMountainLadderZones = new();
        private WofInputCommand _latestServerInput;
        private WofMovementRuntimeState _serverMovementState;
        private WofMovementRuntimeState _predictedMovementState;
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
        private double _nextServerCastAt;
        private uint _inputSequence;
        private Vector3 _automationServerCastDirection;
        private int _remainingAutomationServerCasts;
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
        private bool _hasDarrelReturnPosition;
        private bool _darrelReturnArmed;
        private Vector3 _darrelReturnPosition;
        private float _darrelReturnYaw;

        public float Health => _health.Value;
        public float Armor => _armor.Value;
        public bool IsDead => _isDead.Value;
        public bool HasActiveSpellShield => IsTimedBuffActive(_discShieldUntil.Value) ||
                                            IsTimedBuffActive(_orbShieldUntil.Value);
        public bool IsGrounded => IsLocalLilyCoilActive
            ? _lastLilyCoilGrounded
            : _controller != null && (!_controller.enabled || _controller.isGrounded);
        public bool IsCasting => IsSpawned && NetworkManager != null &&
                                 NetworkManager.ServerTime.Time < _castingUntil.Value;
        public bool IsSprinting => _isSprinting.Value;
        public bool IsSliding => _isSliding.Value;
        public bool IsCrouching => _isCrouching.Value;
        public bool IsMoving
        {
            get
            {
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
        internal int ActiveMountainLadderZoneCount => _activeMountainLadderZones.Count;
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
            _isDead.OnValueChanged += HandleDeadChanged;
            _leftEquippedSpell.OnValueChanged += HandleEquippedSpellChanged;
            _rightEquippedSpell.OnValueChanged += HandleEquippedSpellChanged;

            var hasLocalControl = IsServer || IsOwner;
            _controller.enabled = hasLocalControl && !_isDead.Value;
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
                    ? new Vector3(0f, 80f, -360f)
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
                _isDead.Value = false;
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
            }

            if (IsOwner)
            {
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
                    _pitch = -8f;
                    ApplyCameraRotation();
                    Debug.Log($"[WOF-AUTOMATION] GRASS_VIEW_PROBE_READY position={transform.position} yaw={_yaw} pitch={_pitch}");
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
            _isDead.OnValueChanged -= HandleDeadChanged;
            _leftEquippedSpell.OnValueChanged -= HandleEquippedSpellChanged;
            _rightEquippedSpell.OnValueChanged -= HandleEquippedSpellChanged;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || _isDead.Value)
            {
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

            if (WofInputRouter.ConsumeCast(out var castingHand))
            {
                if (WofDarrelGroveRuntime.TryInteractWithDragon(this))
                {
                    return;
                }
                WofHud.Instance?.PlayFiringPose(castingHand);
                if (IsServer)
                {
                    TryCastFromAuthoritativePoseServer(castingHand);
                }
                else
                {
                    RequestCastRpc(castingHand);
                }
            }

            PublishMovementHud();

            if (_treeHouseViewProbe && !_treeHouseViewProbeLogged && Time.realtimeSinceStartup > 1.5f)
            {
                _treeHouseViewProbeLogged = true;
                var livePitch = cameraPivot == null ? float.NaN : cameraPivot.localEulerAngles.x;
                Debug.Log($"[WOF-AUTOMATION] TREEHOUSE_VIEW_LIVE position={transform.position} internalYaw={_yaw:F1} commandYaw={_latestServerInput.Yaw:F1} yaw={transform.eulerAngles.y:F1} forward={transform.forward} pitch={livePitch:F1}");
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || _isDead.Value || !_controller.enabled)
            {
                return;
            }

            ApplyToxicStatusDamage(Time.fixedDeltaTime);
            if (_isDead.Value) return;
            var simulatedInput = _latestServerInput;
            if (IsTimedBuffActive(_sleepUntil.Value))
            {
                simulatedInput.Move = Vector2.zero;
                simulatedInput.Jump = false;
                simulatedInput.Sprint = false;
                simulatedInput.Slide = false;
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
            _latestServerInput = command;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestCastRpc(WofHandSide hand = WofHandSide.Right)
        {
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
            _serverVerticalVelocity = direction.y * 60f + 5f;
            _controller.Move(new Vector3(direction.x * 2.4f, Mathf.Max(0f, direction.y * 2f), direction.z * 2.4f));
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
            var direction = center - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f) return;
            _controller.Move(direction.normalized * 2.4f + Vector3.up * 0.4f);
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
            _hasDarrelReturnPosition = true;
            _darrelReturnPosition = _authoritativePosition.Value;
            _darrelReturnYaw = _authoritativeYaw.Value;
            ResetTeleportMotion();
            Teleport(WofDarrelGroveLayout.SpawnPosition, WofDarrelGroveLayout.UnitySpawnYawDegrees);
            ApplyQuestTeleportOwnerRpc(
                WofDarrelGroveLayout.SpawnPosition,
                WofDarrelGroveLayout.UnitySpawnYawDegrees);
            Debug.Log($"[WOF-AUTOMATION] DARREL_GROVE_TELEPORT owner={OwnerClientId} position={WofDarrelGroveLayout.SpawnPosition} yaw={WofDarrelGroveLayout.UnitySpawnYawDegrees:F1}");
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

            _serverVerticalVelocity = 0f;
            _predictedVerticalVelocity = 0f;
            _latestServerInput = default;
            _nextServerCastAt = 0d;
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
            Teleport(position, yaw);
            return true;
        }

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

        private bool TryCastFromAuthoritativePoseServer(WofHandSide hand = WofHandSide.Right)
        {
            var equippedSpell = hand == WofHandSide.Left ? LeftEquippedSpell : RightEquippedSpell;
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
            if (!TryBeginSpellCastServer(spell, WofSpellLoadout.SelfBuffHandChargeSeconds, out var now))
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
                    _nextServerCastAt = now;
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
            if (!TryBeginSpellCastServer(spell, 0.36f, out _)) return false;
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
                        !player.HasActiveSpellShield)
                        player.ApplyServerDamage(35f, OwnerClientId);
                }
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
                    if (spell == WofSpellId.Grab) target.ApplyServerGrabPull(_authoritativePosition.Value);
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
            if (!TryBeginSpellCastServer(spell, 0.36f, out var now)) return false;
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
                !WofFireballCastMath.IsFinite(_nextServerCastAt) ||
                !WofFireballCastMath.IsFinite(_authoritativePosition.Value) ||
                !WofFireballCastMath.IsFinite(origin) ||
                !WofFireballCastMath.TryNormalizeFiniteDirection(direction, out var normalizedDirection) ||
                Vector3.Distance(origin, _authoritativePosition.Value) > 3.5f ||
                now < _nextServerCastAt)
            {
                return false;
            }

            _nextServerCastAt = now + WofSpellRuntimeTuning.GetCastCooldownSeconds(spell);
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

        private bool TryBeginSpellCastServer(WofSpellId spell, float poseSeconds, out double now)
        {
            now = NetworkManager.ServerTime.Time;
            if (!IsServer || _isDead.Value || fireballPrefab == null || now < _nextServerCastAt) return false;
            _nextServerCastAt = now + WofSpellRuntimeTuning.GetCastCooldownSeconds(spell);
            _castingUntil.Value = now + poseSeconds;
            return true;
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
            var movementFrame = WofMovementMath.ResolveFrame(
                ref movementState,
                command.Move,
                command.Sprint,
                command.Slide,
                command.Jump,
                _controller.isGrounded,
                verticalVelocity,
                controllerVelocity.x * controllerVelocity.x + controllerVelocity.z * controllerVelocity.z,
                Time.time,
                deltaTime);

            var effectiveGrounded = (Time.time - _lastGroundedAt) <= WofGameConstants.GroundCoyoteSeconds;
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
            _controller.Move(velocity * deltaTime);
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
            localPosition.y = WofMovementMath.ResolveCameraHeight(isSliding, isCrouching);
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

        private void HandleEquippedSpellChanged(int previous, int current)
        {
            if (IsOwner)
            {
                PublishHud();
            }
        }

        private void HandleDeadChanged(bool previous, bool current)
        {
            WofBootstrap.Instance?.ObserveClientReplicatedDead(OwnerClientId, previous, current);
            var needsController = (IsServer || IsOwner) && !current;
            _controller.enabled = needsController;
            if (visualRoot != null)
            {
                visualRoot.SetActive(!IsOwner);
            }

            if (IsOwner)
            {
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

        private void PublishHud()
        {
            WofHud.Instance?.SetVitals(_health.Value, _armor.Value);
            WofHud.Instance?.SetEquippedSpells(
                WofSpellLoadout.GetDisplayName(LeftEquippedSpell),
                WofSpellLoadout.GetDisplayName(RightEquippedSpell));
            WofHud.Instance?.SetHeldSpellVisibility(
                LeftEquippedSpell == WofSpellId.Fireball,
                RightEquippedSpell == WofSpellId.Fireball);
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

        private static WofSpellId ResolveSpell(int value, WofSpellId fallback)
        {
            return WofSpellLoadout.IsValid(value) ? (WofSpellId)value : fallback;
        }
    }
}
