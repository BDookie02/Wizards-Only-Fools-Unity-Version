using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofVoiceChatRuntime : MonoBehaviour
    {
        private sealed class RemoteVoice
        {
            internal VivoxParticipant Participant;
            internal GameObject TapObject;
            internal AudioSource AudioSource;
            internal ulong OwnerClientId;
            internal Transform PlayerTransform;
        }

        private static string s_LastStatus = "STATUS: VOICE OFF";
        private readonly Dictionary<string, RemoteVoice> _remoteVoices = new(StringComparer.Ordinal);
        private WofPlayerController _owner;
        private WofUserSettings _settings;
        private IVivoxService _service;
        private VivoxParticipant _selfParticipant;
        private string _requestedChannel = string.Empty;
        private string _joinedChannel = string.Empty;
        private int _revision;
        private bool _reconciling;
        private bool _destroying;
        private bool _eventsSubscribed;
        private bool _recovering;
        private bool _desiredTransmitting;
        private bool _appliedTransmitting;
        private bool _transmissionBusy;
        private float _nextProximityUpdate;
        private float _nextPositionUpdate;
        private string _status = s_LastStatus;

        public static WofVoiceChatRuntime Instance { get; private set; }
        public static string StatusText => Instance == null ? s_LastStatus : Instance._status;
        public static bool IsConnected => Instance != null && !string.IsNullOrEmpty(Instance._joinedChannel);
        public static bool IsTalking => Instance != null && Instance.ResolveSelfTalking();

        internal void Configure(WofPlayerController owner, string channelName)
        {
            _owner = owner;
            _requestedChannel = channelName ?? string.Empty;
            Instance = this;
            ReloadSettings();
        }

        internal void SetChannelName(string channelName)
        {
            var normalized = channelName ?? string.Empty;
            if (string.Equals(_requestedChannel, normalized, StringComparison.Ordinal)) return;
            _requestedChannel = normalized;
            QueueReconcile();
        }

        public static void ApplySavedSettings()
        {
            Instance?.ReloadSettings();
        }

        private void ReloadSettings()
        {
            _settings = WofUserSettingsStore.Load();
            QueueReconcile();
        }

        private void Update()
        {
            if (_destroying || _settings == null || string.IsNullOrEmpty(_joinedChannel) || _service == null)
                return;

            var keyboardHeld = ReadKeyboardPushToTalk(_settings.voicePushToTalkKey);
            var controllerHeld = WofControllerBindings.IsPressed(
                Gamepad.current,
                WofControllerActions.VoicePushToTalk,
                0.5f);
            var shouldTransmit = WofVoiceChatRules.ShouldTransmit(
                _settings.voiceChatEnabled,
                _settings.voiceInputMode,
                keyboardHeld,
                controllerHeld,
                WofInputRouter.GameplaySuppressed);
            SetDesiredTransmission(shouldTransmit);

            if (Time.unscaledTime >= _nextProximityUpdate)
            {
                _nextProximityUpdate = Time.unscaledTime + WofVoiceChatRules.DefaultRefreshSeconds;
                UpdateRemoteVolumes();
                RefreshConnectedStatus();
            }
            if (Time.unscaledTime >= _nextPositionUpdate)
            {
                _nextPositionUpdate = Time.unscaledTime + WofVoiceChatRules.PositionRefreshSeconds;
                UpdateVivoxPosition();
            }
        }

        private void OnDestroy()
        {
            _destroying = true;
            _revision++;
            if (Instance == this) Instance = null;
            UnsubscribeEvents();
            DestroyRemoteTaps();
            _ = ShutdownVoiceAsync();
        }

        private void QueueReconcile()
        {
            _revision++;
            if (!_reconciling) _ = ReconcileLoopAsync();
        }

        private async Task ReconcileLoopAsync()
        {
            _reconciling = true;
            try
            {
                var appliedRevision = -1;
                while (!_destroying && appliedRevision != _revision)
                {
                    appliedRevision = _revision;
                    await ReconcileAsync();
                }
            }
            finally
            {
                _reconciling = false;
            }
        }

        private async Task ReconcileAsync()
        {
            if (_settings == null || !_settings.voiceChatEnabled)
            {
                await ShutdownVoiceAsync();
                SetStatus("STATUS: VOICE OFF\n\nENABLE VOICE CHAT TO JOIN PROXIMITY AUDIO.");
                return;
            }
            if (string.IsNullOrEmpty(_requestedChannel))
            {
                await ShutdownVoiceAsync();
                SetStatus("STATUS: WAITING FOR MULTIPLAYER SESSION\n\nVOICE JOINS THE SAME SESSION AS THE GAME.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Application.cloudProjectId))
            {
                await ShutdownVoiceAsync();
                SetStatus("STATUS: UNITY CLOUD LINK REQUIRED\n\nLINK THIS UNITY PROJECT AND ENABLE VIVOX; VOICE IS FAILING CLOSED.");
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(_joinedChannel) &&
                    !string.Equals(_joinedChannel, _requestedChannel, StringComparison.Ordinal))
                {
                    await ShutdownVoiceAsync();
                }
                if (string.Equals(_joinedChannel, _requestedChannel, StringComparison.Ordinal))
                {
                    UpdateRemoteVolumes();
                    RefreshConnectedStatus();
                    return;
                }

                SetStatus("STATUS: CONNECTING TO PROXIMITY VOICE...");
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (_destroying) return;

                _service = VivoxService.Instance ?? throw new InvalidOperationException("Vivox service was not registered.");
                SubscribeEvents();
                if (_service.InitializationState != VivoxInitializationState.Initialized)
                    await _service.InitializeAsync();
                if (!_service.IsLoggedIn)
                {
                    var profile = WofSurvivalProfileStore.Load();
                    await _service.LoginAsync(new LoginOptions
                    {
                        DisplayName = WofVoiceChatRules.CreateParticipantDisplayName(
                            _owner == null ? 0UL : _owner.OwnerClientId,
                            profile?.playerName),
                        ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.StateChange
                    });
                }
                if (_destroying) return;

                var properties = new Channel3DProperties(
                    WofVoiceChatRules.SharedChannelAudibleDistance,
                    WofVoiceChatRules.SharedChannelAudibleDistance,
                    1f,
                    AudioFadeModel.LinearByDistance);
                await _service.JoinPositionalChannelAsync(
                    _requestedChannel,
                    ChatCapability.AudioOnly,
                    properties,
                    new ChannelOptions { MakeActiveChannelUponJoining = false });
                if (_destroying) return;

                _joinedChannel = _requestedChannel;
                AttachExistingParticipants();
                UpdateVivoxPosition();
                _appliedTransmitting = false;
                SetDesiredTransmission(_settings.voiceInputMode != "pushToTalk");
                UpdateRemoteVolumes();
                RefreshConnectedStatus();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[WOF] Vivox proximity voice failed: {exception}");
                await ShutdownVoiceAsync();
                SetStatus("STATUS: VOICE CONNECTION FAILED\n\nCHECK UNITY VIVOX CONFIGURATION AND NETWORK ACCESS.");
            }
        }

        private async Task ShutdownVoiceAsync()
        {
            DestroyRemoteTaps();
            _selfParticipant = null;
            _recovering = false;
            _desiredTransmitting = false;
            _appliedTransmitting = false;
            var channel = _joinedChannel;
            _joinedChannel = string.Empty;
            if (_service == null) return;
            try
            {
                if (_service.IsLoggedIn)
                {
                    await _service.SetChannelTransmissionModeAsync(TransmissionMode.None);
                    if (!string.IsNullOrEmpty(channel) && _service.ActiveChannels.ContainsKey(channel))
                        await _service.LeaveChannelAsync(channel);
                    await _service.LogoutAsync();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF] Vivox shutdown did not finish cleanly: {exception.Message}");
            }
        }

        private void SubscribeEvents()
        {
            if (_eventsSubscribed || _service == null) return;
            _service.ParticipantAddedToChannel += HandleParticipantAdded;
            _service.ParticipantRemovedFromChannel += HandleParticipantRemoved;
            _service.ConnectionRecovering += HandleConnectionRecovering;
            _service.ConnectionRecovered += HandleConnectionRecovered;
            _service.ConnectionFailedToRecover += HandleConnectionFailed;
            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_eventsSubscribed || _service == null) return;
            _service.ParticipantAddedToChannel -= HandleParticipantAdded;
            _service.ParticipantRemovedFromChannel -= HandleParticipantRemoved;
            _service.ConnectionRecovering -= HandleConnectionRecovering;
            _service.ConnectionRecovered -= HandleConnectionRecovered;
            _service.ConnectionFailedToRecover -= HandleConnectionFailed;
            _eventsSubscribed = false;
        }

        private void HandleParticipantAdded(VivoxParticipant participant)
        {
            if (participant == null || participant.ChannelName != _joinedChannel) return;
            if (participant.IsSelf)
            {
                _selfParticipant = participant;
                return;
            }
            AttachRemoteParticipant(participant);
        }

        private void HandleParticipantRemoved(VivoxParticipant participant)
        {
            if (participant == null) return;
            if (participant == _selfParticipant) _selfParticipant = null;
            if (!_remoteVoices.TryGetValue(participant.PlayerId, out var remote)) return;
            if (remote.TapObject != null) Destroy(remote.TapObject);
            _remoteVoices.Remove(participant.PlayerId);
        }

        private void AttachExistingParticipants()
        {
            if (_service == null || !_service.ActiveChannels.TryGetValue(_joinedChannel, out var participants)) return;
            foreach (var participant in participants) HandleParticipantAdded(participant);
        }

        private void AttachRemoteParticipant(VivoxParticipant participant)
        {
            if (_remoteVoices.ContainsKey(participant.PlayerId)) return;
            if (!WofVoiceChatRules.TryParseOwnerClientId(participant.DisplayName, out var ownerClientId))
            {
                Debug.LogWarning($"[WOF] Ignoring voice participant with an unrecognized display identity: {participant.DisplayName}");
                return;
            }

            var tapObject = participant.CreateVivoxParticipantTap(
                $"WOF Voice {ownerClientId}",
                silenceInChannelAudioMix: true);
            var audioSource = participant.ParticipantTapAudioSource;
            if (tapObject == null || audioSource == null)
            {
                Debug.LogWarning($"[WOF] Vivox audio tap could not be created for player {ownerClientId}.");
                return;
            }
            audioSource.playOnAwake = true;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0f;
            DontDestroyOnLoad(tapObject);
            _remoteVoices[participant.PlayerId] = new RemoteVoice
            {
                Participant = participant,
                TapObject = tapObject,
                AudioSource = audioSource,
                OwnerClientId = ownerClientId
            };
        }

        private void DestroyRemoteTaps()
        {
            foreach (var remote in _remoteVoices.Values)
            {
                if (remote.TapObject != null) Destroy(remote.TapObject);
            }
            _remoteVoices.Clear();
        }

        private void UpdateRemoteVolumes()
        {
            if (_owner == null || _settings == null) return;
            foreach (var remote in _remoteVoices.Values)
            {
                if (remote.AudioSource == null) continue;
                if (remote.PlayerTransform == null)
                    remote.PlayerTransform = ResolvePlayerTransform(remote.OwnerClientId);
                if (remote.PlayerTransform == null)
                {
                    remote.AudioSource.volume = 0f;
                    continue;
                }
                var distance = Vector3.Distance(_owner.transform.position, remote.PlayerTransform.position);
                remote.AudioSource.volume = WofVoiceChatRules.CalculateProximityVolume(
                    distance,
                    _settings.voiceProximityRange,
                    _settings.voiceOutputVolume);
            }
        }

        private static Transform ResolvePlayerTransform(ulong ownerClientId)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || networkManager.SpawnManager == null) return null;
            var playerObject = networkManager.SpawnManager.GetPlayerNetworkObject(ownerClientId);
            return playerObject == null ? null : playerObject.transform;
        }

        private void UpdateVivoxPosition()
        {
            if (_service == null || _owner == null || string.IsNullOrEmpty(_joinedChannel)) return;
            try
            {
                _service.Set3DPosition(_owner.gameObject, _joinedChannel);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF] Vivox position update failed: {exception.Message}");
            }
        }

        private void SetDesiredTransmission(bool transmit)
        {
            _desiredTransmitting = transmit;
            if (!_transmissionBusy && _desiredTransmitting != _appliedTransmitting)
                _ = ApplyTransmissionLoopAsync();
        }

        private async Task ApplyTransmissionLoopAsync()
        {
            _transmissionBusy = true;
            try
            {
                while (!_destroying && _service != null && _service.IsLoggedIn &&
                       _desiredTransmitting != _appliedTransmitting)
                {
                    var desired = _desiredTransmitting;
                    await _service.SetChannelTransmissionModeAsync(
                        desired ? TransmissionMode.All : TransmissionMode.None);
                    _appliedTransmitting = desired;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF] Vivox transmission mode update failed: {exception.Message}");
            }
            finally
            {
                _transmissionBusy = false;
            }
        }

        private bool ResolveSelfTalking()
        {
            return _appliedTransmitting && _selfParticipant != null && _selfParticipant.SpeechDetected;
        }

        private void RefreshConnectedStatus()
        {
            if (_recovering || string.IsNullOrEmpty(_joinedChannel) || _settings == null) return;
            var mode = _settings.voiceInputMode == "pushToTalk" ? "PUSH TO TALK" : "OPEN MIC";
            var activity = ResolveSelfTalking() ? "TALKING" : "QUIET";
            SetStatus($"STATUS: CONNECTED / {activity}\n\n{mode} - {_remoteVoices.Count} NEARBY PLAYER{(_remoteVoices.Count == 1 ? string.Empty : "S")}");
        }

        private void HandleConnectionRecovering()
        {
            _recovering = true;
            SetStatus("STATUS: VOICE RECONNECTING...");
        }

        private void HandleConnectionRecovered()
        {
            _recovering = false;
            RefreshConnectedStatus();
        }

        private void HandleConnectionFailed()
        {
            _recovering = false;
            SetStatus("STATUS: VOICE CONNECTION LOST\n\nREJOINING PROXIMITY VOICE...");
            _joinedChannel = string.Empty;
            QueueReconcile();
        }

        private void SetStatus(string value)
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_status, normalized, StringComparison.Ordinal)) return;
            _status = normalized;
            s_LastStatus = _status;
            Debug.Log($"[WOF-AUTOMATION] VOICE_STATUS {_status.Replace("\n", " | ")}");
        }

        private static bool ReadKeyboardPushToTalk(string configuredKey)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || string.IsNullOrWhiteSpace(configuredKey)) return false;
            if (Enum.TryParse(configuredKey, true, out Key key) && key != Key.None)
                return keyboard[key]?.isPressed ?? false;
            return keyboard.FindKeyOnCurrentKeyboardLayout(configuredKey)?.isPressed ?? false;
        }
    }
}
