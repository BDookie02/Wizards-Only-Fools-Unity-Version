using System;
using System.Collections;
using System.Globalization;
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
        private bool _clientReplicationProbeActive;
        private ulong _clientReplicationTargetId;
        private int _clientReplicationRequiredDamageSteps;
        private int _clientReplicationObservedDamageSteps;
        private bool _clientReplicationSawDeath;
        private bool _clientReplicationSawRespawnHealth;
        private bool _clientReplicationSawRespawnAlive;
        private bool _hasEnteredLaunchFlow;

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
                WofGameConstants.DefaultPort));
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
            ushort port)
        {
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
        }

        private void FailCombatProbe(string reason)
        {
            Debug.LogError($"[WOF-AUTOMATION] COMBAT_PROBE_FAILED reason={reason}");
            _automaticExitAt = Time.realtimeSinceStartup + 1f;
        }
    }
}
