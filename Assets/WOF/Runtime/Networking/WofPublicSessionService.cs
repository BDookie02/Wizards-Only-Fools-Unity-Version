using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace WOF
{
    internal readonly struct WofPublicSessionResult
    {
        internal WofPublicSessionResult(bool succeeded, string joinCode, string error)
        {
            Succeeded = succeeded;
            JoinCode = joinCode ?? string.Empty;
            Error = error ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal string JoinCode { get; }
        internal string Error { get; }

        internal static WofPublicSessionResult Success(string joinCode)
        {
            return new WofPublicSessionResult(true, joinCode, string.Empty);
        }

        internal static WofPublicSessionResult Failure(string error)
        {
            return new WofPublicSessionResult(false, string.Empty, error);
        }
    }

    internal sealed class WofPublicSessionService
    {
        private readonly NetworkManager _networkManager;
        private readonly UnityTransport _transport;
        private readonly Action<string> _setStatus;
        private ISession _activeSession;

        internal WofPublicSessionService(
            NetworkManager networkManager,
            UnityTransport transport,
            Action<string> setStatus)
        {
            _networkManager = networkManager;
            _transport = transport;
            _setStatus = setStatus;
        }

        internal WofPublicSessionState State { get; private set; }

        internal async Task<WofPublicSessionResult> CreateAsync(string sessionName)
        {
            var availabilityError = GetAvailabilityError();
            if (!string.IsNullOrEmpty(availabilityError))
            {
                return Fail(availabilityError);
            }

            try
            {
                State = WofPublicSessionState.Initializing;
                _setStatus?.Invoke("CONNECTING TO UNITY PUBLIC ONLINE...");
                await EnsureAuthenticatedAsync();

                State = WofPublicSessionState.Creating;
                _setStatus?.Invoke("CREATING PUBLIC RELAY LOBBY...");
                SelectUnityTransport();
                var options = new SessionOptions
                {
                    Name = string.IsNullOrWhiteSpace(sessionName)
                        ? "Wizards Only Fools"
                        : sessionName.Trim(),
                    MaxPlayers = WofGameConstants.MaxPlayers,
                    IsPrivate = false
                }.WithRelayNetwork();

                _activeSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                State = WofPublicSessionState.Connected;
                var joinCode = WofPublicSessionRules.NormalizeJoinCode(_activeSession.Code);
                _setStatus?.Invoke($"PUBLIC LOBBY READY — INVITE CODE {joinCode}");
                return WofPublicSessionResult.Success(joinCode);
            }
            catch (SessionException exception)
            {
                Debug.LogError($"[WOF] Public session create failed: {exception}");
                return Fail(WofPublicSessionRules.FormatSessionError(exception.Error.ToString()));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[WOF] Public session create failed: {exception}");
                return Fail("PUBLIC ONLINE INITIALIZATION FAILED.");
            }
        }

        internal async Task<WofPublicSessionResult> JoinAsync(string joinCode)
        {
            var normalizedCode = WofPublicSessionRules.NormalizeJoinCode(joinCode);
            if (string.IsNullOrEmpty(normalizedCode))
            {
                return Fail(WofPublicSessionRules.JoinCodeRequired);
            }

            var availabilityError = GetAvailabilityError();
            if (!string.IsNullOrEmpty(availabilityError))
            {
                return Fail(availabilityError);
            }

            try
            {
                State = WofPublicSessionState.Initializing;
                _setStatus?.Invoke("CONNECTING TO UNITY PUBLIC ONLINE...");
                await EnsureAuthenticatedAsync();

                State = WofPublicSessionState.Joining;
                _setStatus?.Invoke($"JOINING PUBLIC LOBBY {normalizedCode}...");
                SelectUnityTransport();
                _activeSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(normalizedCode);
                State = WofPublicSessionState.Connected;
                _setStatus?.Invoke($"JOINED PUBLIC LOBBY {normalizedCode}");
                return WofPublicSessionResult.Success(normalizedCode);
            }
            catch (SessionException exception)
            {
                Debug.LogError($"[WOF] Public session join failed: {exception}");
                return Fail(WofPublicSessionRules.FormatSessionError(exception.Error.ToString()));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[WOF] Public session join failed: {exception}");
                return Fail("PUBLIC ONLINE INITIALIZATION FAILED.");
            }
        }

        internal void LeaveOnShutdown()
        {
            if (_activeSession == null)
            {
                return;
            }

            _ = _activeSession.LeaveAsync();
            _activeSession = null;
            State = WofPublicSessionState.Idle;
        }

        private string GetAvailabilityError()
        {
            return WofPublicSessionRules.GetAvailabilityError(
                Application.cloudProjectId,
                _networkManager != null,
                _transport != null,
                _networkManager != null && _networkManager.IsListening);
        }

        private async Task EnsureAuthenticatedAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        private void SelectUnityTransport()
        {
            _networkManager.NetworkConfig ??= new NetworkConfig();
            _networkManager.NetworkConfig.NetworkTransport = _transport;
        }

        private WofPublicSessionResult Fail(string message)
        {
            State = WofPublicSessionState.Failed;
            _setStatus?.Invoke(message);
            return WofPublicSessionResult.Failure(message);
        }
    }
}
