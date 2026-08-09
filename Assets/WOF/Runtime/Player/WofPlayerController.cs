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

        private CharacterController _controller;
        private readonly HashSet<string> _activeMountainLadderZones = new();
        private WofInputCommand _latestServerInput;
        private WofMovementRuntimeState _serverMovementState;
        private WofMovementRuntimeState _predictedMovementState;
        private float _serverVerticalVelocity;
        private float _predictedVerticalVelocity;
        private float _yaw;
        private float _pitch;
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
        private bool _hasDarrelReturnPosition;
        private bool _darrelReturnArmed;
        private Vector3 _darrelReturnPosition;
        private float _darrelReturnYaw;

        public float Health => _health.Value;
        public float Armor => _armor.Value;
        public bool IsDead => _isDead.Value;
        public bool IsGrounded => _controller != null && (!_controller.enabled || _controller.isGrounded);
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

                var velocity = _controller.velocity;
                return velocity.x * velocity.x + velocity.z * velocity.z > 0.0001f;
            }
        }
        public bool IsDarrelReturnArmed => _darrelReturnArmed;
        internal int ActiveMountainLadderZoneCount => _activeMountainLadderZones.Count;
        public Vector3 DamageProbePosition => transform.position + Vector3.up *
                                              WofMovementMath.ResolveCameraHeight(_isSliding.Value, _isCrouching.Value);

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
                var useDarrelProbeSpawn = OwnerClientId == 0 && _darrelDialogProbe && !useDarrelGroveProbeSpawn;
                var useVillagerProbeSpawn = OwnerClientId == 0 && _villagerViewProbe &&
                                            !useDarrelProbeSpawn && !useDarrelGroveProbeSpawn;
                var useChicagoProbeSpawn = OwnerClientId == 0 && _chicagoCityViewProbe;
                var useDesertProbeSpawn = OwnerClientId == 0 && _desertVillageViewProbe && !useChicagoProbeSpawn;
                var spawn = useChicagoProbeSpawn
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
                var spawnYaw = useChicagoProbeSpawn || useDesertProbeSpawn
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
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || _isDead.Value)
            {
                return;
            }

            var look = _treeHouseViewProbe || _villagerViewProbe || _darrelDialogProbe ||
                       _darrelGroveViewProbe || _desertVillageViewProbe || _chicagoCityViewProbe ||
                       _mountainVillageViewProbe
                ? Vector2.zero
                : WofInputRouter.ReadLook();
            _yaw += look.x * WofGameConstants.MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * WofGameConstants.MouseSensitivity, -82f, 82f);
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
                    TryCastFromAuthoritativePoseServer();
                }
                else
                {
                    RequestCastRpc();
                }
            }

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

            var movementFrame = Simulate(
                ref _serverVerticalVelocity,
                ref _serverMovementState,
                _latestServerInput,
                Time.fixedDeltaTime);
            _authoritativePosition.Value = transform.position;
            _authoritativeYaw.Value = _latestServerInput.Yaw;
            _authoritativePitch.Value = _latestServerInput.Pitch;
            _isSprinting.Value = movementFrame.IsSprinting;
            _isSliding.Value = movementFrame.IsSliding;
            _isCrouching.Value = movementFrame.IsCrouching;
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
            transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(transform.eulerAngles.y, _authoritativeYaw.Value, blend), 0f);
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
        private void RequestCastRpc()
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

            TryCastFromAuthoritativePoseServer();
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
            Teleport(position, yaw);
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

        private bool TryCastFromAuthoritativePoseServer()
        {
            if (!WofFireballCastMath.TryResolveAuthoritativeLaunch(
                    _authoritativePosition.Value,
                    _authoritativeYaw.Value,
                    _authoritativePitch.Value,
                    out var origin,
                    out var direction))
            {
                return false;
            }

            return TrySpawnFireballServer(origin, direction);
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

            return TrySpawnFireballServer(origin, direction);
        }

        private bool TrySpawnFireballServer(Vector3 origin, Vector3 direction)
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

            _nextServerCastAt = now + WofGameConstants.GeneralCastCooldownSeconds;
            _castingUntil.Value = now + 0.36d;
            var projectileObject = Instantiate(fireballPrefab, origin, Quaternion.LookRotation(normalizedDirection));
            var projectile = projectileObject.GetComponent<WofFireballProjectile>();
            projectile.InitializeServer(OwnerClientId, normalizedDirection);
            projectileObject.GetComponent<NetworkObject>().Spawn(true);
            Debug.Log($"[WOF-AUTOMATION] FIREBALL_CAST owner={OwnerClientId}");
            return true;
        }

        private WofMovementFrame Simulate(
            ref float verticalVelocity,
            ref WofMovementRuntimeState movementState,
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
                _controller.Move((ladderPlanar * WofGameConstants.WalkSpeed + Vector3.up * verticalVelocity) * deltaTime);
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

            if (command.Jump && (Time.time - _lastGroundedAt) <= WofGameConstants.GroundCoyoteSeconds)
            {
                verticalVelocity = WofGameConstants.JumpSpeed;
                _lastGroundedAt = float.NegativeInfinity;
            }

            verticalVelocity += WofGameConstants.Gravity * deltaTime;
            var heading = Quaternion.Euler(0f, command.Yaw, 0f);
            var planar = heading * (Vector3.right * command.Move.x + Vector3.forward * command.Move.y);
            var velocity = planar * movementFrame.Speed + Vector3.up * verticalVelocity;
            _controller.Move(velocity * deltaTime);
            transform.rotation = Quaternion.Euler(0f, command.Yaw, 0f);
            ApplyCameraHeight(movementFrame.IsSliding, movementFrame.IsCrouching);
            return movementFrame;
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
            Debug.Log($"[WOF-AUTOMATION] PLAYER_RESPAWNED id={OwnerClientId}");
        }

        private void Teleport(Vector3 position, float yaw)
        {
            WofMovementMath.Reset(ref _serverMovementState);
            WofMovementMath.Reset(ref _predictedMovementState);
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
        }
    }
}
