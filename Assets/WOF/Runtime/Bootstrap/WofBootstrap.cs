using System;
using System.Collections;
using System.Globalization;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.SinglePlayer;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WOF
{
    public enum WofSessionMode
    {
        None,
        Solo,
        Host,
        Client
    }

    internal static class WofTransportConfiguration
    {
        internal static void SelectSolo(
            NetworkManager networkManager,
            SinglePlayerTransport singlePlayerTransport)
        {
            if (networkManager == null)
            {
                throw new ArgumentNullException(nameof(networkManager));
            }

            if (singlePlayerTransport == null)
            {
                throw new ArgumentNullException(nameof(singlePlayerTransport));
            }

            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = singlePlayerTransport;
        }

        internal static void ConfigureWebSocketMultiplayer(
            NetworkManager networkManager,
            UnityTransport unityTransport,
            string address,
            ushort port,
            bool isHost)
        {
            if (networkManager == null)
            {
                throw new ArgumentNullException(nameof(networkManager));
            }

            if (unityTransport == null)
            {
                throw new ArgumentNullException(nameof(unityTransport));
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("A multiplayer address is required.", nameof(address));
            }

            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = unityTransport;
            unityTransport.UseWebSockets = true;
            unityTransport.UseEncryption = false;
            unityTransport.SetConnectionData(address, port, isHost ? "0.0.0.0" : null);
        }
    }

    public sealed class WofBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;
        [SerializeField] private SinglePlayerTransport singlePlayerTransport;
        [SerializeField] private GameObject launchPanel;
        [SerializeField] private GameObject pressPanel;
        [SerializeField] private GameObject sessionPanel;
        [SerializeField] private Button pressAnywhereButton;
        [SerializeField] private InputField addressInput;
        [SerializeField] private Button soloButton;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Text launchStatus;
        [SerializeField] private Camera menuCamera;
        [SerializeField] private WofHud hud;

        private string _roomCode;
        private float _automaticExitAt = -1f;
        private string _screenshotPath;
        private string _launchScreenshotPath;
        private bool _screenshotTaken;
        private bool _launchScreenshotTaken;
        private bool _combatProbeRequested;
        private bool _combatProbeStarted;
        private bool _combatProbeFailed;
        private bool _clientReplicationProbeActive;
        private ulong _clientReplicationTargetId;
        private int _clientReplicationRequiredDamageSteps;
        private int _clientReplicationObservedDamageSteps;
        private bool _clientReplicationSawDeath;
        private bool _clientReplicationSawRespawnHealth;
        private bool _clientReplicationSawRespawnAlive;
        private bool _hasEnteredLaunchFlow;
        private WofPublicSessionService _publicSessionService;

        public static WofBootstrap Instance { get; private set; }
        public WofSessionMode Mode { get; private set; }
        public string RoomCode => _roomCode;
        public bool IsSurvivalSession { get; private set; } = true;

        private void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            if (transport == null && networkManager != null)
            {
                transport = networkManager.GetComponent<UnityTransport>();
            }

            if (singlePlayerTransport == null && networkManager != null)
            {
                singlePlayerTransport = networkManager.GetComponent<SinglePlayerTransport>();
            }

            if (hud == null)
            {
                hud = FindFirstObjectByType<WofHud>();
            }

            _publicSessionService = new WofPublicSessionService(networkManager, transport, SetLaunchStatus);

            if (GetComponent<WofSurvivalAutosaveRuntime>() == null)
            {
                gameObject.AddComponent<WofSurvivalAutosaveRuntime>();
            }

            if (addressInput != null && string.IsNullOrWhiteSpace(addressInput.text))
            {
                addressInput.text = "127.0.0.1";
            }

            pressAnywhereButton?.onClick.AddListener(ContinueFromPress);

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.ConnectionApprovalCallback = HandleConnectionApproval;

            ParseAutomationArguments();
        }

        private void Start()
        {
            ShowLaunchPanel(true);

            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg == "--wof-solo")
                {
                    StartSolo();
                    return;
                }

                if (arg == "--wof-host")
                {
                    StartHost();
                    return;
                }

                const string clientPrefix = "--wof-client=";
                if (arg.StartsWith(clientPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (addressInput != null)
                    {
                        addressInput.text = arg.Substring(clientPrefix.Length);
                    }

                    StartClient();
                    return;
                }
            }
        }

        public void ContinueFromPress()
        {
            if (_hasEnteredLaunchFlow)
            {
                return;
            }

            _hasEnteredLaunchFlow = true;
            pressPanel?.SetActive(false);
            sessionPanel?.SetActive(true);
            Debug.Log("[WOF] Launch press accepted");
        }

        private void Update()
        {
            if (!_hasEnteredLaunchFlow && pressPanel != null && pressPanel.activeInHierarchy)
            {
                var gamepad = Gamepad.current;
                if (gamepad != null &&
                    (WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.MenuSelect) ||
                     WofControllerBindings.WasPressedThisFrame(gamepad, WofControllerActions.Pause)))
                {
                    ContinueFromPress();
                }
            }

            if (!_launchScreenshotTaken && !string.IsNullOrWhiteSpace(_launchScreenshotPath) &&
                Time.realtimeSinceStartup > 2f)
            {
                _launchScreenshotTaken = true;
                ScreenCapture.CaptureScreenshot(_launchScreenshotPath);
                Debug.Log($"[WOF-AUTOMATION] LAUNCH_SCREENSHOT {_launchScreenshotPath}");
            }

            if (_automaticExitAt > 0f && Time.realtimeSinceStartup >= _automaticExitAt)
            {
                Debug.Log("[WOF-AUTOMATION] AUTO_EXIT");
                Application.Quit(0);
            }

            if (!_screenshotTaken && !string.IsNullOrWhiteSpace(_screenshotPath) &&
                networkManager != null && networkManager.IsListening && Time.realtimeSinceStartup > 2f)
            {
                _screenshotTaken = true;
                ScreenCapture.CaptureScreenshot(_screenshotPath);
                Debug.Log($"[WOF-AUTOMATION] SCREENSHOT {_screenshotPath}");
            }
        }

        private void OnDestroy()
        {
            _publicSessionService?.LeaveOnShutdown();
            WofInputRouter.ResetMobile();
            WofInputRouter.EndControllerGameplay();
            WofInputRouter.SetGameplaySuppressed(false);
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= HandleClientConnected;
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                networkManager.OnServerStarted -= HandleServerStarted;
                if (networkManager.ConnectionApprovalCallback == HandleConnectionApproval)
                {
                    networkManager.ConnectionApprovalCallback = null;
                }
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void StartSolo()
        {
            if (!CanStart(singlePlayerTransport))
            {
                return;
            }

            Mode = WofSessionMode.Solo;
            IsSurvivalSession = true;
            WofTransportConfiguration.SelectSolo(networkManager, singlePlayerTransport);
            StartSelectedTransportAsHost("Solo session");
        }

        public void StartHost()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SetLaunchStatus("WebGL can join a host, but cannot host one.");
#else
            if (!CanStart(transport))
            {
                return;
            }

            Mode = WofSessionMode.Host;
            WofTransportConfiguration.ConfigureWebSocketMultiplayer(
                networkManager,
                transport,
                "127.0.0.1",
                WofGameConstants.DefaultPort,
                isHost: true);
            StartSelectedTransportAsHost("LAN host");
#endif
        }

        public void SetSurvivalSession(bool survival)
        {
            if (networkManager != null && networkManager.IsListening)
            {
                return;
            }
            IsSurvivalSession = survival;
        }

        public void StartClient()
        {
            if (!CanStart(transport))
            {
                return;
            }

            var address = addressInput == null ? "127.0.0.1" : addressInput.text.Trim();
            if (string.IsNullOrWhiteSpace(address))
            {
                address = "127.0.0.1";
            }

            Mode = WofSessionMode.Client;
            WofTransportConfiguration.ConfigureWebSocketMultiplayer(
                networkManager,
                transport,
                address,
                WofGameConstants.DefaultPort,
                isHost: false);
            SetLaunchStatus($"Joining {address}:{WofGameConstants.DefaultPort}...");
            if (!networkManager.StartClient())
            {
                SetLaunchStatus("Unable to start the client.");
                Mode = WofSessionMode.None;
            }
        }

        public async Task<string> StartPublicHostAsync()
        {
            if (_publicSessionService == null)
            {
                SetLaunchStatus(WofPublicSessionRules.NetworkConfigurationRequired);
                return string.Empty;
            }

            Mode = WofSessionMode.Host;
            _roomCode = string.Empty;
            var result = await _publicSessionService.CreateAsync("Wizards Only Fools");
            if (!result.Succeeded)
            {
                Mode = WofSessionMode.None;
                return string.Empty;
            }

            _roomCode = result.JoinCode;
            PublishServerVoiceChannel();
            hud?.SetRoom(ResolveRoomLabel(Mode, string.Empty, _roomCode, WofGameConstants.DefaultPort, true));
            return _roomCode;
        }

        public async Task<bool> StartPublicClientAsync(string joinCode)
        {
            if (_publicSessionService == null)
            {
                SetLaunchStatus(WofPublicSessionRules.NetworkConfigurationRequired);
                return false;
            }

            var normalizedCode = WofPublicSessionRules.NormalizeJoinCode(joinCode);
            Mode = WofSessionMode.Client;
            _roomCode = normalizedCode;
            var result = await _publicSessionService.JoinAsync(normalizedCode);
            if (!result.Succeeded)
            {
                Mode = WofSessionMode.None;
                _roomCode = string.Empty;
                return false;
            }

            _roomCode = result.JoinCode;
            hud?.SetRoom(ResolveRoomLabel(Mode, string.Empty, _roomCode, WofGameConstants.DefaultPort, true));
            return true;
        }

        private void StartSelectedTransportAsHost(string label)
        {
            _roomCode = CreateRoomCode();
            SetLaunchStatus($"Starting {label}...");
            if (!networkManager.StartHost())
            {
                SetLaunchStatus("Unable to start the host.");
                Mode = WofSessionMode.None;
            }
        }

        private void PublishServerVoiceChannel()
        {
            if (networkManager == null || !networkManager.IsServer) return;
            foreach (var client in networkManager.ConnectedClientsList)
            {
                var player = client.PlayerObject == null
                    ? null
                    : client.PlayerObject.GetComponent<WofPlayerController>();
                player?.SetServerVoiceChannel(_roomCode);
            }
        }

        private bool CanStart(NetworkTransport requiredTransport)
        {
            if (networkManager == null || requiredTransport == null)
            {
                SetLaunchStatus("Network configuration is missing.");
                return false;
            }

            if (networkManager.IsListening)
            {
                SetLaunchStatus("A session is already running.");
                return false;
            }

            return true;
        }

        private void HandleServerStarted()
        {
            Debug.Log($"[WOF-AUTOMATION] SERVER_STARTED mode={Mode} port={WofGameConstants.DefaultPort}");
            if (_combatProbeRequested && !_combatProbeStarted)
            {
                StartCoroutine(RunCombatProbe());
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            Debug.Log($"[WOF-AUTOMATION] CLIENT_CONNECTED id={clientId} local={networkManager.LocalClientId}");
            if (clientId != networkManager.LocalClientId)
            {
                return;
            }

            ShowLaunchPanel(false);
            WofInputRouter.BeginControllerGameplay();
            WofInputRouter.SetGameplaySuppressed(false);
            hud?.SetGameplayVisible(true);
            hud?.SetStatus(string.Empty);
            hud?.SetRoom(ResolveRoomLabel(
                Mode,
                addressInput?.text,
                _roomCode,
                WofGameConstants.DefaultPort,
                _publicSessionService?.State == WofPublicSessionState.Connected));
            var viewProbe = WofPerformanceModeRuntime.IsVillagerViewProbe ||
                            WofPerformanceModeRuntime.IsDarrelDialogProbe;
            Cursor.lockState = viewProbe ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = viewProbe;
            Debug.Log($"[WOF-AUTOMATION] SESSION_READY mode={Mode}");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            Debug.Log($"[WOF-AUTOMATION] CLIENT_DISCONNECTED id={clientId}");
            if (networkManager != null && clientId == networkManager.LocalClientId)
            {
                hud?.SetGameplayVisible(false);
                WofInputRouter.EndControllerGameplay();
                WofInputRouter.SetGameplaySuppressed(false);
                ShowLaunchPanel(true);
                SetLaunchStatus("Disconnected. You can start or join another session.");
                Mode = WofSessionMode.None;
            }
        }

        internal static string ResolveRoomLabel(
            WofSessionMode mode,
            string address,
            string roomCode,
            ushort port,
            bool isPublicSession = false)
        {
            if (isPublicSession)
            {
                return mode == WofSessionMode.Client
                    ? $"PUBLIC  {roomCode}"
                    : mode == WofSessionMode.Host
                        ? $"PUBLIC HOST  {roomCode}"
                        : string.Empty;
            }

            return mode == WofSessionMode.Client
                ? $"JOINED  {address}:{port}"
                : mode == WofSessionMode.Host
                    ? $"{roomCode}  |  {port}"
                    : string.Empty;
        }

        private void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var connectedCount = networkManager.ConnectedClientsIds.Count;
            response.Approved = connectedCount < WofGameConstants.MaxPlayers;
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            response.Reason = response.Approved ? string.Empty : "Room is full.";
        }

        private void ShowLaunchPanel(bool visible)
        {
            launchPanel?.SetActive(visible);
            if (menuCamera != null)
            {
                menuCamera.gameObject.SetActive(visible);
            }

            if (visible)
            {
                WofInputRouter.EndControllerGameplay();
                pressPanel?.SetActive(!_hasEnteredLaunchFlow);
                sessionPanel?.SetActive(_hasEnteredLaunchFlow);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (!_hasEnteredLaunchFlow && pressAnywhereButton != null && EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(pressAnywhereButton.gameObject);
                }
            }
        }

        private void SetLaunchStatus(string value)
        {
            if (launchStatus != null)
            {
                launchStatus.text = value;
            }
            Debug.Log($"[WOF] {value}");
        }

        private static string CreateRoomCode()
        {
            var token = Guid.NewGuid().ToString("N").Substring(0, 5).ToLowerInvariant();
            return $"wof-{token}";
        }

        private void ParseAutomationArguments()
        {
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.Equals("--wof-combat-probe", StringComparison.OrdinalIgnoreCase))
                {
                    _combatProbeRequested = true;
                    Debug.Log("[WOF-AUTOMATION] COMBAT_PROBE_ARMED");
                }

                const string exitPrefix = "--wof-auto-exit=";
                if (arg.StartsWith(exitPrefix, StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(arg.Substring(exitPrefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    _automaticExitAt = Time.realtimeSinceStartup + Mathf.Max(2f, seconds);
                }

                const string screenshotPrefix = "--wof-screenshot=";
                if (arg.StartsWith(screenshotPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var path = arg.Substring(screenshotPrefix.Length).Trim('"');
                    if (path.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase))
                    {
                        _screenshotPath = path;
                    }
                }

                const string launchScreenshotPrefix = "--wof-launch-screenshot=";
                if (arg.StartsWith(launchScreenshotPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var path = arg.Substring(launchScreenshotPrefix.Length).Trim('"');
                    if (path.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase))
                    {
                        _launchScreenshotPath = path;
                    }
                }

                if (arg.StartsWith("--wof-launch-stage=", StringComparison.OrdinalIgnoreCase))
                {
                    _hasEnteredLaunchFlow = true;
                }
            }
        }

        internal bool BeginClientReplicationProbe(ulong targetClientId, int requiredDamageSteps)
        {
            if (!_combatProbeRequested || networkManager == null || networkManager.IsServer || Mode != WofSessionMode.Client)
            {
                FailClientReplicationProbe("client-probe-not-authorized-or-not-a-remote-client");
                return false;
            }

            if (_clientReplicationProbeActive)
            {
                FailClientReplicationProbe("client-probe-already-active");
                return false;
            }

            _clientReplicationTargetId = targetClientId;
            _clientReplicationRequiredDamageSteps = requiredDamageSteps;
            _clientReplicationObservedDamageSteps = 0;
            _clientReplicationSawDeath = false;
            _clientReplicationSawRespawnHealth = false;
            _clientReplicationSawRespawnAlive = false;
            _clientReplicationProbeActive = true;
            Debug.Log(
                $"[WOF-AUTOMATION] CLIENT_REPLICATION_PROBE_STARTED observer={networkManager.LocalClientId} target={targetClientId} casts={requiredDamageSteps}");
            return true;
        }

        internal void ObserveClientReplicatedHealth(ulong targetClientId, float previous, float current)
        {
            if (!_clientReplicationProbeActive || targetClientId != _clientReplicationTargetId)
            {
                return;
            }

            if (_clientReplicationSawDeath && current == WofGameConstants.MaxHealth)
            {
                _clientReplicationSawRespawnHealth = true;
                Debug.Log(
                    $"[WOF-AUTOMATION] CLIENT_REPLICATED_RESPAWN_HEALTH observer={networkManager.LocalClientId} target={targetClientId} previous={previous} health={current}");
                TryCompleteClientReplicationProbe();
                return;
            }

            var expectedHealth = Mathf.Max(
                0,
                WofGameConstants.MaxHealth -
                ((_clientReplicationObservedDamageSteps + 1) * WofGameConstants.FireballDamage));
            if (current != expectedHealth)
            {
                FailClientReplicationProbe(
                    $"unexpected-health-target-{targetClientId}-previous-{previous}-current-{current}-expected-{expectedHealth}");
                return;
            }

            _clientReplicationObservedDamageSteps++;
            Debug.Log(
                $"[WOF-AUTOMATION] CLIENT_REPLICATED_DAMAGE observer={networkManager.LocalClientId} target={targetClientId} index={_clientReplicationObservedDamageSteps} health={current}");
            TryCompleteClientReplicationProbe();
        }

        internal void ObserveClientReplicatedDead(ulong targetClientId, bool previous, bool current)
        {
            if (!_clientReplicationProbeActive || targetClientId != _clientReplicationTargetId)
            {
                return;
            }

            if (current)
            {
                _clientReplicationSawDeath = true;
                Debug.Log(
                    $"[WOF-AUTOMATION] CLIENT_REPLICATED_DEATH observer={networkManager.LocalClientId} target={targetClientId}");
            }
            else if (_clientReplicationSawDeath)
            {
                _clientReplicationSawRespawnAlive = true;
                Debug.Log(
                    $"[WOF-AUTOMATION] CLIENT_REPLICATED_RESPAWN_ALIVE observer={networkManager.LocalClientId} target={targetClientId} previous={previous}");
            }

            TryCompleteClientReplicationProbe();
        }

        private void TryCompleteClientReplicationProbe()
        {
            if (!_clientReplicationProbeActive ||
                _clientReplicationObservedDamageSteps != _clientReplicationRequiredDamageSteps ||
                !_clientReplicationSawDeath ||
                !_clientReplicationSawRespawnHealth ||
                !_clientReplicationSawRespawnAlive)
            {
                return;
            }

            _clientReplicationProbeActive = false;
            Debug.Log(
                $"[WOF-AUTOMATION] CLIENT_REPLICATION_PROBE_PASSED observer={networkManager.LocalClientId} target={_clientReplicationTargetId} casts={_clientReplicationObservedDamageSteps}");
        }

        private void FailClientReplicationProbe(string reason)
        {
            _clientReplicationProbeActive = false;
            Debug.LogError($"[WOF-AUTOMATION] CLIENT_REPLICATION_PROBE_FAILED reason={reason}");
            _automaticExitAt = Time.realtimeSinceStartup + 1f;
        }

        private IEnumerator RunCombatProbe()
        {
            _combatProbeStarted = true;
            _combatProbeFailed = false;
            Debug.Log("[WOF-AUTOMATION] COMBAT_PROBE_WAITING_FOR_TWO_PLAYERS");

            const float playerConnectTimeoutSeconds = 25f;
            var connectDeadline = Time.realtimeSinceStartup + playerConnectTimeoutSeconds;
            WofPlayerController attacker = null;
            WofPlayerController target = null;

            while (Time.realtimeSinceStartup < connectDeadline)
            {
                if (networkManager == null || !networkManager.IsServer || !networkManager.IsListening)
                {
                    FailCombatProbe("server-stopped-before-player-setup");
                    yield break;
                }

                var attackerId = networkManager.LocalClientId;
                ulong? targetId = null;
                foreach (var connectedClientId in networkManager.ConnectedClientsIds)
                {
                    if (connectedClientId != attackerId)
                    {
                        targetId = connectedClientId;
                        break;
                    }
                }

                if (targetId.HasValue &&
                    networkManager.ConnectedClients.TryGetValue(attackerId, out var attackerClient) &&
                    networkManager.ConnectedClients.TryGetValue(targetId.Value, out var targetClient) &&
                    attackerClient.PlayerObject != null && targetClient.PlayerObject != null)
                {
                    attacker = attackerClient.PlayerObject.GetComponent<WofPlayerController>();
                    target = targetClient.PlayerObject.GetComponent<WofPlayerController>();
                    if (attacker != null && target != null && attacker.IsSpawned && target.IsSpawned)
                    {
                        break;
                    }
                }

                yield return null;
            }

            if (attacker == null || target == null || !attacker.IsSpawned || !target.IsSpawned)
            {
                FailCombatProbe("two-player-setup-timeout");
                yield break;
            }

            Debug.Log($"[WOF-AUTOMATION] COMBAT_PROBE_STARTED attacker={attacker.OwnerClientId} target={target.OwnerClientId}");

            var campfirePosition = new Vector3(
                WofBaseVillageLayout.CampfireX,
                WofBaseVillageLayout.GetTerrainHeight(
                    WofBaseVillageLayout.CampfireX,
                    WofBaseVillageLayout.CampfireZ),
                WofBaseVillageLayout.CampfireZ);
            if (!target.PrepareForAutomationCampfireProbe(campfirePosition, 0.1f))
            {
                FailCombatProbe("campfire-player-preparation-failed");
                yield break;
            }

            var campfireDeadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < campfireDeadline && target.Health >= WofGameConstants.MaxHealth)
            {
                yield return null;
            }

            var campfireHealth = target.Health;
            var campfireArmor = target.Armor;
            if (Mathf.Abs(campfireHealth - 99.9f) > 0.0001f || Mathf.Abs(campfireArmor) > 0.0001f)
            {
                FailCombatProbe($"campfire-fractional-damage-mismatch-health-{campfireHealth}-armor-{campfireArmor}");
                yield break;
            }

            Debug.Log(
                $"[WOF-AUTOMATION] CAMPFIRE_DAMAGE_PROBE_PASSED target={target.OwnerClientId} health={campfireHealth:F1} armor={campfireArmor:F1} tick={WofBaseVillageLayout.CampfireDamagePerTick:F1}");

            const float probeX = 0f;
            const float attackerZ = 80f;
            const float targetZ = 92f;
            var attackerPosition = new Vector3(
                probeX,
                WofBaseVillageLayout.GetTerrainHeight(probeX, attackerZ) + 0.55f,
                attackerZ);
            var targetPosition = new Vector3(
                probeX,
                WofBaseVillageLayout.GetTerrainHeight(probeX, targetZ) + 0.55f,
                targetZ);
            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 180f))
            {
                FailCombatProbe("server-player-preparation-failed");
                yield break;
            }

            Debug.Log(
                $"[WOF-AUTOMATION] COMBAT_PROBE_POSITIONED attacker={attacker.OwnerClientId} target={target.OwnerClientId}");
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var requiredCasts = Mathf.CeilToInt((float)WofGameConstants.MaxHealth / WofGameConstants.FireballDamage);
            for (var castIndex = 1; castIndex <= requiredCasts; castIndex++)
            {
                const float castAcceptanceTimeoutSeconds = 3f;
                var castDeadline = Time.realtimeSinceStartup + castAcceptanceTimeoutSeconds;
                var castAccepted = false;
                while (Time.realtimeSinceStartup < castDeadline && !castAccepted)
                {
                    if (target == null || !target.IsSpawned || target.IsDead)
                    {
                        break;
                    }

                    castAccepted = attacker.TryAutomationServerFireballAt(target.transform.position + Vector3.up);
                    if (!castAccepted)
                    {
                        yield return null;
                    }
                }

                if (!castAccepted)
                {
                    FailCombatProbe($"cast-{castIndex}-not-accepted");
                    yield break;
                }

                var expectedHealth = Mathf.Max(
                    0,
                    WofGameConstants.MaxHealth - (castIndex * WofGameConstants.FireballDamage));
                Debug.Log(
                    $"[WOF-AUTOMATION] COMBAT_PROBE_CAST_ACCEPTED index={castIndex} expectedHealth={expectedHealth}");

                const float damageTimeoutSeconds = 3f;
                var damageDeadline = Time.realtimeSinceStartup + damageTimeoutSeconds;
                while (Time.realtimeSinceStartup < damageDeadline && target.Health > expectedHealth)
                {
                    yield return null;
                }

                if (target.Health != expectedHealth)
                {
                    FailCombatProbe($"cast-{castIndex}-damage-timeout-health-{target.Health}-expected-{expectedHealth}");
                    yield break;
                }

                Debug.Log(
                    $"[WOF-AUTOMATION] COMBAT_PROBE_DAMAGE_CONFIRMED index={castIndex} health={target.Health}");
            }

            if (!target.IsDead || target.Health != 0)
            {
                FailCombatProbe($"death-state-missing-health-{target.Health}-isDead-{target.IsDead}");
                yield break;
            }

            Debug.Log($"[WOF-AUTOMATION] COMBAT_PROBE_DEATH_CONFIRMED target={target.OwnerClientId}");
            var deathConfirmedAt = Time.realtimeSinceStartup;

            var respawnDeadline = Time.realtimeSinceStartup + WofGameConstants.RespawnDelaySeconds + 3f;
            while (Time.realtimeSinceStartup < respawnDeadline &&
                   (target.IsDead || target.Health != WofGameConstants.MaxHealth))
            {
                yield return null;
            }

            if (target.IsDead || target.Health != WofGameConstants.MaxHealth)
            {
                FailCombatProbe($"respawn-timeout-health-{target.Health}-isDead-{target.IsDead}");
                yield break;
            }

            var respawnElapsedSeconds = Time.realtimeSinceStartup - deathConfirmedAt;
            const float respawnTimingToleranceSeconds = 0.75f;
            if (Mathf.Abs(respawnElapsedSeconds - WofGameConstants.RespawnDelaySeconds) > respawnTimingToleranceSeconds)
            {
                FailCombatProbe($"respawn-timing-out-of-range-elapsed-{respawnElapsedSeconds:F2}");
                yield break;
            }

            Debug.Log(
                $"[WOF-AUTOMATION] COMBAT_PROBE_RESPAWN_CONFIRMED target={target.OwnerClientId} elapsedSeconds={respawnElapsedSeconds:F2}");
            Debug.Log(
                $"[WOF-AUTOMATION] SERVER_COMBAT_PROBE_PASSED attacker={attacker.OwnerClientId} target={target.OwnerClientId} casts={requiredCasts}");

            var clientRpcAttacker = target;
            var clientRpcTarget = attacker;
            if (!clientRpcAttacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !clientRpcTarget.PrepareForAutomationCombatProbe(targetPosition, 180f))
            {
                FailCombatProbe("client-rpc-player-preparation-failed");
                yield break;
            }

            Debug.Log(
                $"[WOF-AUTOMATION] CLIENT_RPC_SERVER_PATH_STARTED attacker={clientRpcAttacker.OwnerClientId} target={clientRpcTarget.OwnerClientId}");
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var clientRpcOrigin = attackerPosition + Vector3.up + (Vector3.forward * 0.8f);
            if (!clientRpcAttacker.BeginAutomationClientCombatProbe(
                    clientRpcOrigin,
                    Vector3.forward,
                    clientRpcTarget.OwnerClientId,
                    requiredCasts))
            {
                FailCombatProbe("client-rpc-probe-start-failed");
                yield break;
            }

            for (var castIndex = 1; castIndex <= requiredCasts; castIndex++)
            {
                var expectedHealth = Mathf.Max(
                    0,
                    WofGameConstants.MaxHealth - (castIndex * WofGameConstants.FireballDamage));
                const float clientRpcDamageTimeoutSeconds = 4f;
                var damageDeadline = Time.realtimeSinceStartup + clientRpcDamageTimeoutSeconds;
                while (Time.realtimeSinceStartup < damageDeadline && clientRpcTarget.Health > expectedHealth)
                {
                    yield return null;
                }

                if (clientRpcTarget.Health != expectedHealth)
                {
                    FailCombatProbe(
                        $"client-rpc-cast-{castIndex}-damage-timeout-health-{clientRpcTarget.Health}-expected-{expectedHealth}");
                    yield break;
                }

                Debug.Log(
                    $"[WOF-AUTOMATION] CLIENT_RPC_SERVER_DAMAGE_CONFIRMED index={castIndex} health={clientRpcTarget.Health}");
            }

            if (!clientRpcTarget.IsDead || clientRpcTarget.Health != 0)
            {
                FailCombatProbe(
                    $"client-rpc-death-state-missing-health-{clientRpcTarget.Health}-isDead-{clientRpcTarget.IsDead}");
                yield break;
            }

            Debug.Log(
                $"[WOF-AUTOMATION] CLIENT_RPC_SERVER_DEATH_CONFIRMED target={clientRpcTarget.OwnerClientId}");
            var clientRpcDeathConfirmedAt = Time.realtimeSinceStartup;
            var clientRpcRespawnDeadline =
                Time.realtimeSinceStartup + WofGameConstants.RespawnDelaySeconds + 3f;
            while (Time.realtimeSinceStartup < clientRpcRespawnDeadline &&
                   (clientRpcTarget.IsDead || clientRpcTarget.Health != WofGameConstants.MaxHealth))
            {
                yield return null;
            }

            if (clientRpcTarget.IsDead || clientRpcTarget.Health != WofGameConstants.MaxHealth)
            {
                FailCombatProbe(
                    $"client-rpc-respawn-timeout-health-{clientRpcTarget.Health}-isDead-{clientRpcTarget.IsDead}");
                yield break;
            }

            var clientRpcRespawnElapsedSeconds = Time.realtimeSinceStartup - clientRpcDeathConfirmedAt;
            if (Mathf.Abs(clientRpcRespawnElapsedSeconds - WofGameConstants.RespawnDelaySeconds) >
                respawnTimingToleranceSeconds)
            {
                FailCombatProbe(
                    $"client-rpc-respawn-timing-out-of-range-elapsed-{clientRpcRespawnElapsedSeconds:F2}");
                yield break;
            }

            Debug.Log(
                $"[WOF-AUTOMATION] CLIENT_RPC_SERVER_RESPAWN_CONFIRMED target={clientRpcTarget.OwnerClientId} elapsedSeconds={clientRpcRespawnElapsedSeconds:F2}");
            Debug.Log(
                $"[WOF-AUTOMATION] CLIENT_RPC_SERVER_PATH_PASSED attacker={clientRpcAttacker.OwnerClientId} target={clientRpcTarget.OwnerClientId} casts={requiredCasts}");

            yield return new WaitForSecondsRealtime(0.5f);
            const string trainingDummyInstanceId = "automation-client-training-dummy";
            const float trainingDummyX = 8f;
            const float trainingDummyZ = 86f;
            var trainingDummyPosition = new Vector3(
                trainingDummyX,
                WofBaseVillageLayout.GetTerrainHeight(trainingDummyX, trainingDummyZ),
                trainingDummyZ);
            if (!clientRpcAttacker.BeginAutomationClientTrainingDummyProbe(
                    trainingDummyInstanceId,
                    trainingDummyPosition))
            {
                FailCombatProbe("client-training-dummy-probe-start-failed");
                yield break;
            }

            Debug.Log(
                $"[WOF-AUTOMATION] TRAINING_DUMMY_TWO_PEER_PROBE_STARTED owner={clientRpcAttacker.OwnerClientId} source={clientRpcTarget.OwnerClientId} instance={trainingDummyInstanceId}");

            const float trainingDummyPlacementTimeoutSeconds = 5f;
            var trainingDummyPlacementDeadline =
                Time.realtimeSinceStartup + trainingDummyPlacementTimeoutSeconds;
            WofEnginePlaceableRecord trainingDummyState = default;
            while (Time.realtimeSinceStartup < trainingDummyPlacementDeadline &&
                   (!clientRpcAttacker.TryGetTrainingDummyState(
                        trainingDummyInstanceId,
                        out trainingDummyState) ||
                    !clientRpcAttacker.HasAutomationClientTrainingDummyPlacementAcknowledgement(
                        trainingDummyInstanceId)))
            {
                yield return null;
            }

            if (!clientRpcAttacker.TryGetTrainingDummyState(
                    trainingDummyInstanceId,
                    out trainingDummyState) ||
                !clientRpcAttacker.HasAutomationClientTrainingDummyPlacementAcknowledgement(
                    trainingDummyInstanceId) ||
                trainingDummyState.trainingDummyHealth != WofTrainingDummyCombatRules.MaxHealth ||
                trainingDummyState.trainingDummyHitSequence != 0 ||
                trainingDummyState.trainingDummyRespawnAt != 0d)
            {
                FailCombatProbe(
                    $"client-training-dummy-placement-timeout-or-state-mismatch-health-{trainingDummyState.trainingDummyHealth:F0}-sequence-{trainingDummyState.trainingDummyHitSequence}-respawn-{trainingDummyState.trainingDummyRespawnAt:F3}");
                yield break;
            }

            Debug.Log(
                $"[WOF-AUTOMATION] TRAINING_DUMMY_SERVER_PLACEMENT_CONFIRMED owner={clientRpcAttacker.OwnerClientId} instance={trainingDummyInstanceId} health={trainingDummyState.trainingDummyHealth:F0}");

            const int trainingDummyRequiredHits = 5;
            var trainingDummyDownAt = -1f;
            for (var hitIndex = 1; hitIndex <= trainingDummyRequiredHits; hitIndex++)
            {
                if (!clientRpcAttacker.ApplyServerTrainingDummySpellImpact(
                        trainingDummyInstanceId,
                        WofSpellId.Fireball,
                        clientRpcTarget.OwnerClientId))
                {
                    FailCombatProbe($"client-training-dummy-hit-{hitIndex}-not-applied");
                    yield break;
                }

                if (!clientRpcAttacker.TryGetTrainingDummyState(
                        trainingDummyInstanceId,
                        out trainingDummyState))
                {
                    FailCombatProbe($"client-training-dummy-hit-{hitIndex}-state-missing");
                    yield break;
                }

                var expectedTrainingDummyHealth = Mathf.Max(
                    0f,
                    WofTrainingDummyCombatRules.MaxHealth -
                    (hitIndex * WofTrainingDummyCombatRules.GetDamage(WofSpellId.Fireball)));
                if (trainingDummyState.trainingDummyHealth != expectedTrainingDummyHealth ||
                    trainingDummyState.trainingDummyHitSequence != hitIndex ||
                    trainingDummyState.trainingDummyLastSpell != (int)WofSpellId.Fireball)
                {
                    FailCombatProbe(
                        $"client-training-dummy-hit-{hitIndex}-state-mismatch-health-{trainingDummyState.trainingDummyHealth:F0}-expected-{expectedTrainingDummyHealth:F0}-sequence-{trainingDummyState.trainingDummyHitSequence}-spell-{trainingDummyState.trainingDummyLastSpell}");
                    yield break;
                }

                Debug.Log(
                    $"[WOF-AUTOMATION] TRAINING_DUMMY_SERVER_DAMAGE_CONFIRMED owner={clientRpcAttacker.OwnerClientId} instance={trainingDummyInstanceId} index={hitIndex} health={trainingDummyState.trainingDummyHealth:F0}");
                if (trainingDummyState.trainingDummyHealth <= 0f)
                {
                    trainingDummyDownAt = Time.realtimeSinceStartup;
                    Debug.Log(
                        $"[WOF-AUTOMATION] TRAINING_DUMMY_SERVER_DOWN_CONFIRMED owner={clientRpcAttacker.OwnerClientId} instance={trainingDummyInstanceId} sequence={trainingDummyState.trainingDummyHitSequence}");
                }

                yield return new WaitForSecondsRealtime(0.35f);
            }

            if (trainingDummyDownAt < 0f || trainingDummyState.trainingDummyRespawnAt <= 0d)
            {
                FailCombatProbe("client-training-dummy-down-state-missing");
                yield break;
            }

            var trainingDummyRespawnDeadline =
                Time.realtimeSinceStartup + WofTrainingDummyCombatRules.RespawnSeconds + 3f;
            while (Time.realtimeSinceStartup < trainingDummyRespawnDeadline)
            {
                if (clientRpcAttacker.TryGetTrainingDummyState(
                        trainingDummyInstanceId,
                        out trainingDummyState) &&
                    trainingDummyState.trainingDummyHealth == WofTrainingDummyCombatRules.MaxHealth &&
                    trainingDummyState.trainingDummyRespawnAt == 0d)
                {
                    break;
                }

                yield return null;
            }

            if (trainingDummyState.trainingDummyHealth != WofTrainingDummyCombatRules.MaxHealth ||
                trainingDummyState.trainingDummyRespawnAt != 0d ||
                trainingDummyState.trainingDummyHitSequence != trainingDummyRequiredHits)
            {
                FailCombatProbe(
                    $"client-training-dummy-respawn-timeout-health-{trainingDummyState.trainingDummyHealth:F0}-sequence-{trainingDummyState.trainingDummyHitSequence}-respawn-{trainingDummyState.trainingDummyRespawnAt:F3}");
                yield break;
            }

            var trainingDummyRespawnElapsedSeconds = Time.realtimeSinceStartup - trainingDummyDownAt;
            if (Mathf.Abs(trainingDummyRespawnElapsedSeconds -
                          (float)WofTrainingDummyCombatRules.RespawnSeconds) >
                respawnTimingToleranceSeconds)
            {
                FailCombatProbe(
                    $"client-training-dummy-respawn-timing-out-of-range-elapsed-{trainingDummyRespawnElapsedSeconds:F2}");
                yield break;
            }

            Debug.Log(
                $"[WOF-AUTOMATION] TRAINING_DUMMY_SERVER_RESPAWN_CONFIRMED owner={clientRpcAttacker.OwnerClientId} instance={trainingDummyInstanceId} elapsedSeconds={trainingDummyRespawnElapsedSeconds:F2}");
            Debug.Log(
                $"[WOF-AUTOMATION] TRAINING_DUMMY_TWO_PEER_SERVER_PATH_PASSED owner={clientRpcAttacker.OwnerClientId} source={clientRpcTarget.OwnerClientId} instance={trainingDummyInstanceId} hits={trainingDummyRequiredHits}");

            yield return RunSpellOutcomeMatrixProbe(
                clientRpcTarget,
                clientRpcAttacker,
                attackerPosition,
                targetPosition);
            if (_combatProbeFailed) yield break;
        }

        private IEnumerator RunSpellOutcomeMatrixProbe(
            WofPlayerController attacker,
            WofPlayerController target,
            Vector3 attackerPosition,
            Vector3 targetPosition)
        {
            Debug.Log(
                $"[WOF-AUTOMATION] SPELL_OUTCOME_MATRIX_STARTED attacker={attacker.OwnerClientId} target={target.OwnerClientId}");

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 180f) ||
                !attacker.TryAutomationServerCastSpell(
                    WofHandSide.Right,
                    WofSpellId.IceSpell,
                    target.transform.position + Vector3.up))
            {
                FailCombatProbe("spell-matrix-flashbang-setup-failed");
                yield break;
            }
            var deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline && target.FlashbangOpacity <= 0f) yield return null;
            if (Mathf.Abs(attacker.FlashbangOpacity - WofSpellRuntimeTuning.IceSpellLocalOpacity) > 0.08f ||
                Mathf.Abs(target.FlashbangOpacity - WofSpellRuntimeTuning.IceSpellRemoteOpacity) > 0.08f)
            {
                FailCombatProbe(
                    $"spell-matrix-flashbang-mismatch-source-{attacker.FlashbangOpacity:F2}-target-{target.FlashbangOpacity:F2}");
                yield break;
            }
            Debug.Log(
                $"[WOF-AUTOMATION] SPELL_OUTCOME_FLASHBANG_PASSED source={attacker.FlashbangOpacity:F2} target={target.FlashbangOpacity:F2}");

            var offsetGrabTargetPosition = targetPosition + Vector3.right * 1.5f;
            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(offsetGrabTargetPosition, 180f) ||
                !attacker.BeginAutomationServerHeldSpell(WofHandSide.Right, WofSpellId.Grab))
            {
                FailCombatProbe("spell-matrix-grab-setup-failed");
                yield break;
            }
            deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline && !target.IsGrabbed) yield return null;
            var initialGrabX = offsetGrabTargetPosition.x;
            while (Time.realtimeSinceStartup < deadline && Mathf.Abs(target.transform.position.x) > 0.45f) yield return null;
            if (!target.IsGrabbed || Mathf.Abs(target.transform.position.x) >= Mathf.Abs(initialGrabX) - 0.5f)
            {
                FailCombatProbe(
                    $"spell-matrix-grab-follow-mismatch-grabbed-{target.IsGrabbed}-x-{target.transform.position.x:F2}");
                yield break;
            }
            var beforeThrowPosition = target.transform.position;
            var grabThrowDirection = Vector3.ProjectOnPlane(attacker.transform.forward, Vector3.up).normalized;
            if (!attacker.ReleaseAutomationServerHeldSpell(WofHandSide.Right))
            {
                FailCombatProbe("spell-matrix-grab-release-failed");
                yield break;
            }
            deadline = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < deadline &&
                   (target.IsGrabbed ||
                    Vector3.Dot(target.transform.position - beforeThrowPosition, grabThrowDirection) <= 0.5f))
                yield return null;
            var grabThrowDistance = Vector3.Dot(
                target.transform.position - beforeThrowPosition,
                grabThrowDirection);
            if (target.IsGrabbed || grabThrowDistance <= 0.5f)
            {
                FailCombatProbe(
                    $"spell-matrix-grab-throw-mismatch-grabbed-{target.IsGrabbed}-distance-{grabThrowDistance:F2}");
                yield break;
            }
            Debug.Log(
                $"[WOF-AUTOMATION] SPELL_OUTCOME_GRAB_PASSED followX={target.transform.position.x:F2} throwDistance={grabThrowDistance:F2}");

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 180f) ||
                !attacker.TryAutomationServerCastSpell(
                    WofHandSide.Right,
                    WofSpellId.Tornado,
                    target.transform.position))
            {
                FailCombatProbe("spell-matrix-tornado-setup-failed");
                yield break;
            }
            var tornadoStart = target.transform.position;
            deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline && target.transform.position.z <= tornadoStart.z + 0.25f)
                yield return null;
            if (target.transform.position.z <= tornadoStart.z + 0.25f)
            {
                FailCombatProbe(
                    $"spell-matrix-tornado-pull-mismatch-start-{tornadoStart}-end-{target.transform.position}");
                yield break;
            }
            Debug.Log(
                $"[WOF-AUTOMATION] SPELL_OUTCOME_TORNADO_PASSED delta={target.transform.position - tornadoStart}");

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 180f))
            {
                FailCombatProbe("spell-matrix-meteor-preparation-failed");
                yield break;
            }
            WofFireballProjectile.ResetAutomationMeteorImpactCount();
            if (!attacker.TryAutomationServerCastSpell(
                    WofHandSide.Right,
                    WofSpellId.MeteorShower,
                    target.transform.position))
            {
                FailCombatProbe("spell-matrix-meteor-cast-failed");
                yield break;
            }
            deadline = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < deadline &&
                   WofFireballProjectile.AppliedMeteorImpactCount < WofSpellRuntimeTuning.MeteorCount)
                yield return null;
            if (WofFireballProjectile.AppliedMeteorImpactCount != WofSpellRuntimeTuning.MeteorCount)
            {
                FailCombatProbe(
                    $"spell-matrix-meteor-count-mismatch-{WofFireballProjectile.AppliedMeteorImpactCount}");
                yield break;
            }
            Debug.Log(
                $"[WOF-AUTOMATION] SPELL_OUTCOME_METEOR_PASSED impacts={WofFireballProjectile.AppliedMeteorImpactCount}");

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 180f))
            {
                FailCombatProbe("spell-matrix-orb-shield-preparation-failed");
                yield break;
            }
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            if (
                !target.TryAutomationServerCastSpell(
                    WofHandSide.Left,
                    WofSpellId.OrbShield,
                    attacker.transform.position) ||
                !target.IsOrbShieldActiveForAutomation)
            {
                FailCombatProbe("spell-matrix-orb-shield-setup-failed");
                yield break;
            }
            var targetPlanarForward = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
            var shieldFrontOrigin = target.transform.position + targetPlanarForward * 12f;
            var shieldRearOrigin = target.transform.position - targetPlanarForward * 12f;
            if (!target.ShouldBlockServerSpellImpact(shieldRearOrigin))
            {
                FailCombatProbe("spell-matrix-orb-shield-did-not-block");
                yield break;
            }

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 180f))
            {
                FailCombatProbe("spell-matrix-disc-front-preparation-failed");
                yield break;
            }
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            if (
                !target.TryAutomationServerCastSpell(
                    WofHandSide.Left,
                    WofSpellId.DiscShield,
                    attacker.transform.position) ||
                !target.IsDiscShieldActiveForAutomation)
            {
                FailCombatProbe("spell-matrix-disc-front-setup-failed");
                yield break;
            }
            targetPlanarForward = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
            shieldFrontOrigin = target.transform.position + targetPlanarForward * 12f;
            if (!target.ShouldBlockServerSpellImpact(shieldFrontOrigin))
            {
                FailCombatProbe("spell-matrix-disc-front-did-not-block");
                yield break;
            }

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 0f))
            {
                FailCombatProbe("spell-matrix-disc-rear-preparation-failed");
                yield break;
            }
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            if (
                !target.TryAutomationServerCastSpell(
                    WofHandSide.Left,
                    WofSpellId.DiscShield,
                    attacker.transform.position))
            {
                FailCombatProbe("spell-matrix-disc-rear-setup-failed");
                yield break;
            }
            targetPlanarForward = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
            shieldRearOrigin = target.transform.position - targetPlanarForward * 12f;
            if (target.ShouldBlockServerSpellImpact(shieldRearOrigin))
            {
                FailCombatProbe("spell-matrix-disc-rear-incorrectly-blocked");
                yield break;
            }
            Debug.Log("[WOF-AUTOMATION] SPELL_OUTCOME_SHIELDS_PASSED orb=true discFront=true discRear=false");

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 180f))
            {
                FailCombatProbe("spell-matrix-kunai-preparation-failed");
                yield break;
            }
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            var kunaiCasterStart = attacker.transform.position;
            var kunaiPullDirection = Vector3.ProjectOnPlane(
                target.transform.position - kunaiCasterStart,
                Vector3.up).normalized;
            if (!attacker.TryAutomationServerCastSpell(
                    WofHandSide.Right,
                    WofSpellId.Kunai,
                    target.transform.position + Vector3.up))
            {
                FailCombatProbe("spell-matrix-kunai-cast-failed");
                yield break;
            }
            deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline &&
                   (target.Health == WofGameConstants.MaxHealth ||
                    Vector3.Dot(attacker.transform.position - kunaiCasterStart, kunaiPullDirection) <= 0.5f))
                yield return null;
            var kunaiPullDistance = Vector3.Dot(
                attacker.transform.position - kunaiCasterStart,
                kunaiPullDirection);
            if (target.Health != WofGameConstants.MaxHealth - 15f ||
                kunaiPullDistance <= 0.5f)
            {
                FailCombatProbe(
                    $"spell-matrix-kunai-mismatch-health-{target.Health}-pullDistance-{kunaiPullDistance:F2}");
                yield break;
            }
            Debug.Log(
                $"[WOF-AUTOMATION] SPELL_OUTCOME_KUNAI_PASSED health={target.Health} pullDistance={kunaiPullDistance:F2}");

            var directSpells = new[]
            {
                WofSpellId.IceShard,
                WofSpellId.ArcaneBeam,
                WofSpellId.RingsOfPower,
                WofSpellId.Flamethrower
            };
            for (var index = 0; index < directSpells.Length; index++)
            {
                var directSpell = directSpells[index];
                if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                    !target.PrepareForAutomationCombatProbe(targetPosition, 180f) ||
                    !attacker.TryAutomationServerCastSpell(
                        WofHandSide.Right,
                        directSpell,
                        target.transform.position + Vector3.up))
                {
                    FailCombatProbe($"spell-matrix-direct-{directSpell}-setup-failed");
                    yield break;
                }

                var expectedHealth = WofGameConstants.MaxHealth -
                                     WofSpellRuntimeTuning.GetPlayerDamage(directSpell);
                deadline = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < deadline && target.Health > expectedHealth + 0.01f)
                    yield return null;
                if (Mathf.Abs(target.Health - expectedHealth) > 0.01f)
                {
                    FailCombatProbe(
                        $"spell-matrix-direct-{directSpell}-health-{target.Health:F2}-expected-{expectedHealth:F2}");
                    yield break;
                }
            }
            Debug.Log("[WOF-AUTOMATION] SPELL_OUTCOME_DIRECT_DAMAGE_PASSED spells=4");

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 180f) ||
                !attacker.TryAutomationServerCastSpell(
                    WofHandSide.Right,
                    WofSpellId.Lightning,
                    target.transform.position))
            {
                FailCombatProbe("spell-matrix-utility-lightning-setup-failed");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.25f);
            if (target.Health != WofGameConstants.MaxHealth)
            {
                FailCombatProbe($"spell-matrix-lightning-damaged-player-{target.Health:F2}");
                yield break;
            }

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 180f) ||
                !attacker.TryAutomationServerCastSpell(
                    WofHandSide.Right,
                    WofSpellId.SmokeBomb,
                    target.transform.position + Vector3.up))
            {
                FailCombatProbe("spell-matrix-utility-smoke-setup-failed");
                yield break;
            }
            deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline &&
                   !WofFireballProjectile.HasAnchoredAutomationSpell(
                       WofSpellId.SmokeBomb,
                       attacker.OwnerClientId)) yield return null;
            if (!WofFireballProjectile.HasAnchoredAutomationSpell(
                    WofSpellId.SmokeBomb,
                    attacker.OwnerClientId) || target.Health != WofGameConstants.MaxHealth)
            {
                FailCombatProbe($"spell-matrix-smoke-cloud-mismatch-health-{target.Health:F2}");
                yield break;
            }

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f))
            {
                FailCombatProbe("spell-matrix-utility-blink-preparation-failed");
                yield break;
            }
            var blinkStart = attacker.transform.position;
            if (!attacker.TryAutomationServerCastSpell(WofHandSide.Right, WofSpellId.Blink, blinkStart))
            {
                FailCombatProbe("spell-matrix-utility-blink-cast-failed");
                yield break;
            }
            var blinkDelta = attacker.transform.position - blinkStart;
            var blinkPlanarDistance = new Vector2(blinkDelta.x, blinkDelta.z).magnitude;
            if (blinkPlanarDistance < WofSpellRuntimeTuning.BlinkMinimumDistance - 0.01f ||
                blinkPlanarDistance > WofSpellRuntimeTuning.BlinkMaximumDistance + 0.01f ||
                Mathf.Abs(blinkDelta.y - WofSpellRuntimeTuning.BlinkUpwardOffset) > 0.01f)
            {
                FailCombatProbe($"spell-matrix-blink-delta-{blinkDelta}-planar-{blinkPlanarDistance:F2}");
                yield break;
            }

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f))
            {
                FailCombatProbe("spell-matrix-utility-heal-preparation-failed");
                yield break;
            }
            attacker.ApplyServerDamage(10f, attacker.OwnerClientId, true);
            if (!attacker.BeginAutomationServerHeldSpell(WofHandSide.Right, WofSpellId.Heal))
            {
                FailCombatProbe("spell-matrix-utility-heal-start-failed");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.55f);
            if (!attacker.ReleaseAutomationServerHeldSpell(WofHandSide.Right) ||
                attacker.Health < 90.7f || attacker.Health > 92.5f)
            {
                FailCombatProbe($"spell-matrix-held-heal-health-{attacker.Health:F2}");
                yield break;
            }
            Debug.Log(
                $"[WOF-AUTOMATION] SPELL_OUTCOME_UTILITY_PASSED lightningHealth={target.Health:F1} smoke=true blink={blinkPlanarDistance:F2} heal={attacker.Health:F2}");

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !attacker.TryAutomationServerCastSpell(WofHandSide.Right, WofSpellId.MagicArmor, attackerPosition) ||
                attacker.Armor != WofGameConstants.MaxArmor)
            {
                FailCombatProbe("spell-matrix-self-magic-armor-failed");
                yield break;
            }
            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !attacker.TryAutomationServerCastSpell(WofHandSide.Right, WofSpellId.SpeedBoost, attackerPosition) ||
                !attacker.IsSpeedBoostActiveForAutomation)
            {
                FailCombatProbe("spell-matrix-self-speed-boost-failed");
                yield break;
            }
            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !attacker.TryAutomationServerCastSpell(WofHandSide.Right, WofSpellId.JumpBoost, attackerPosition) ||
                !attacker.IsJumpBoostActiveForAutomation)
            {
                FailCombatProbe("spell-matrix-self-jump-boost-failed");
                yield break;
            }

            var orbOffAxisPosition = attackerPosition + Vector3.right * 10f;
            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(orbOffAxisPosition, 180f) ||
                !attacker.TryAutomationServerCastSpell(WofHandSide.Right, WofSpellId.MagicGlassOrb, attackerPosition) ||
                !attacker.IsMagicGlassOrbActiveForAutomation)
            {
                FailCombatProbe("spell-matrix-self-magic-glass-orb-failed");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.24f);
            if (WofHud.Instance == null || !WofHud.Instance.MagicGlassOrbHasSignal ||
                WofHud.Instance.MagicGlassOrbIsLocked)
            {
                FailCombatProbe("spell-matrix-magic-glass-orb-off-axis-signal-failed");
                yield break;
            }
            if (!target.RepositionForAutomationCombatProbe(attackerPosition + Vector3.forward * 10f, 180f))
            {
                FailCombatProbe("spell-matrix-magic-glass-orb-lock-reposition-failed");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.24f);
            if (!WofHud.Instance.MagicGlassOrbHasSignal || !WofHud.Instance.MagicGlassOrbIsLocked)
            {
                FailCombatProbe("spell-matrix-magic-glass-orb-lock-signal-failed");
                yield break;
            }
            Debug.Log("[WOF-AUTOMATION] SPELL_OUTCOME_SELF_BUFFS_PASSED armor=50 speed=true jump=true orbSignal=locked");

            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 180f) ||
                !attacker.TryAutomationServerCastSpell(
                    WofHandSide.Right,
                    WofSpellId.TungstonBallsack,
                    target.transform.position + Vector3.up) ||
                !target.IsSlowEffectActive)
            {
                FailCombatProbe("spell-matrix-status-tungston-failed");
                yield break;
            }

            var projectileStatuses = new[] { WofSpellId.Sleep, WofSpellId.Poison, WofSpellId.Acid };
            for (var index = 0; index < projectileStatuses.Length; index++)
            {
                var statusSpell = projectileStatuses[index];
                if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                    !target.PrepareForAutomationCombatProbe(targetPosition, 180f) ||
                    !attacker.TryAutomationServerCastSpell(
                        WofHandSide.Right,
                        statusSpell,
                        target.transform.position + Vector3.up))
                {
                    FailCombatProbe($"spell-matrix-status-{statusSpell}-setup-failed");
                    yield break;
                }
                deadline = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < deadline &&
                       !(statusSpell == WofSpellId.Sleep ? target.IsSleepEffectActive :
                           statusSpell == WofSpellId.Poison ? target.IsPoisonEffectActive :
                           target.IsAcidEffectActive)) yield return null;
                var active = statusSpell == WofSpellId.Sleep ? target.IsSleepEffectActive :
                    statusSpell == WofSpellId.Poison ? target.IsPoisonEffectActive :
                    target.IsAcidEffectActive;
                if (!active)
                {
                    FailCombatProbe($"spell-matrix-status-{statusSpell}-not-active");
                    yield break;
                }
            }

            if (!target.PrepareForAutomationCombatProbe(targetPosition, 180f))
            {
                FailCombatProbe("spell-matrix-toxic-stack-preparation-failed");
                yield break;
            }
            target.ApplyServerStatus(WofSpellId.Poison, attacker.OwnerClientId);
            target.ApplyServerStatus(WofSpellId.Acid, attacker.OwnerClientId);
            var toxicHealthBefore = target.Health;
            yield return new WaitForSecondsRealtime(0.5f);
            var toxicDamage = toxicHealthBefore - target.Health;
            if (!target.IsPoisonEffectActive || !target.IsAcidEffectActive ||
                toxicDamage < 3.5f || toxicDamage > 6.5f)
            {
                FailCombatProbe($"spell-matrix-toxic-stack-damage-{toxicDamage:F2}");
                yield break;
            }
            Debug.Log($"[WOF-AUTOMATION] SPELL_OUTCOME_STATUS_PASSED slow=true sleep=true poison=true acid=true stackedDamage={toxicDamage:F2}");

            var healingTargetPosition = attackerPosition + Vector3.right * 2f;
            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(healingTargetPosition, 180f))
            {
                FailCombatProbe("spell-matrix-healing-crystals-preparation-failed");
                yield break;
            }
            target.ApplyServerDamage(20f, attacker.OwnerClientId, true);
            target.ApplyServerStatus(WofSpellId.Poison, attacker.OwnerClientId);
            target.ApplyServerStatus(WofSpellId.Acid, attacker.OwnerClientId);
            var healingHealthBefore = target.Health;
            if (!attacker.TryAutomationServerCastSpell(
                    WofHandSide.Right,
                    WofSpellId.HealingCrystals,
                    healingTargetPosition))
            {
                FailCombatProbe("spell-matrix-healing-crystals-cast-failed");
                yield break;
            }
            deadline = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < deadline &&
                   (target.IsPoisonEffectActive || target.IsAcidEffectActive ||
                    target.Health <= healingHealthBefore)) yield return null;
            if (target.IsPoisonEffectActive || target.IsAcidEffectActive ||
                target.Health <= healingHealthBefore)
            {
                FailCombatProbe($"spell-matrix-healing-crystals-health-{target.Health:F2}-before-{healingHealthBefore:F2}");
                yield break;
            }
            Debug.Log($"[WOF-AUTOMATION] SPELL_OUTCOME_HEALING_CRYSTALS_PASSED health={target.Health:F2} cleansed=true");

            var firstPortalPosition = targetPosition;
            var secondPortalPosition = targetPosition + Vector3.right * 20f;
            if (!attacker.PrepareForAutomationCombatProbe(attackerPosition, 0f) ||
                !target.PrepareForAutomationCombatProbe(targetPosition, 180f) ||
                !attacker.TryAutomationServerCastSpell(
                    WofHandSide.Right,
                    WofSpellId.Portal,
                    firstPortalPosition) ||
                !WofFireballProjectile.TryAnchorLatestAutomationPortal(
                    attacker.OwnerClientId,
                    firstPortalPosition) ||
                !target.TryAutomationServerCastSpell(
                    WofHandSide.Left,
                    WofSpellId.Portal,
                    secondPortalPosition) ||
                !WofFireballProjectile.TryAnchorLatestAutomationPortal(
                    target.OwnerClientId,
                    secondPortalPosition))
            {
                FailCombatProbe("spell-matrix-portal-pair-setup-failed");
                yield break;
            }
            deadline = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < deadline &&
                   Vector3.Distance(target.transform.position, secondPortalPosition) > 0.2f) yield return null;
            if (Vector3.Distance(target.transform.position, secondPortalPosition) > 0.2f)
            {
                FailCombatProbe($"spell-matrix-portal-first-traversal-position-{target.transform.position}");
                yield break;
            }
            if (!target.RepositionForAutomationCombatProbe(firstPortalPosition, 180f))
            {
                FailCombatProbe("spell-matrix-portal-cooldown-reposition-failed");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.25f);
            var cooldownPlanarDistance = Vector2.Distance(
                new Vector2(target.transform.position.x, target.transform.position.z),
                new Vector2(firstPortalPosition.x, firstPortalPosition.z));
            if (cooldownPlanarDistance > 0.2f)
            {
                FailCombatProbe(
                    $"spell-matrix-portal-one-second-cooldown-failed-planar-{cooldownPlanarDistance:F2}");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.8f);
            deadline = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < deadline &&
                   Vector3.Distance(target.transform.position, secondPortalPosition) > 0.2f) yield return null;
            if (Vector3.Distance(target.transform.position, secondPortalPosition) > 0.2f)
            {
                FailCombatProbe("spell-matrix-portal-post-cooldown-traversal-failed");
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.05f);
            if (!attacker.TryAutomationServerCastSpell(
                    WofHandSide.Right,
                    WofSpellId.Portal,
                    firstPortalPosition + Vector3.forward * 4f))
            {
                FailCombatProbe("spell-matrix-portal-third-cast-failed");
                yield break;
            }
            if (WofFireballProjectile.TryAnchorLatestAutomationPortal(
                    attacker.OwnerClientId,
                    firstPortalPosition + Vector3.forward * 4f) ||
                WofFireballProjectile.ActivePortalEndpointCount != WofSpellRuntimeTuning.PortalMaximumEndpoints)
            {
                FailCombatProbe($"spell-matrix-portal-third-endpoint-count-{WofFireballProjectile.ActivePortalEndpointCount}");
                yield break;
            }
            Debug.Log("[WOF-AUTOMATION] SPELL_OUTCOME_PORTAL_PASSED endpoints=2 crossOwner=true cooldown=1.00");
            Debug.Log("[WOF-AUTOMATION] SPELL_OUTCOME_MATRIX_PASSED cases=12");
        }

        private void FailCombatProbe(string reason)
        {
            _combatProbeFailed = true;
            Debug.LogError($"[WOF-AUTOMATION] COMBAT_PROBE_FAILED reason={reason}");
            _automaticExitAt = Time.realtimeSinceStartup + 1f;
        }
    }
}
