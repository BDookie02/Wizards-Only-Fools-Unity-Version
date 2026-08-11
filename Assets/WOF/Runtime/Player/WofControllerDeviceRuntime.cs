using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace WOF
{
    /// <summary>
    /// Reports the native Input System gamepads that the running build can use
    /// and records hot-plug changes without introducing a browser or remapping
    /// compatibility layer.
    /// </summary>
    public sealed class WofControllerDeviceRuntime : MonoBehaviour
    {
        private Gamepad _automationGamepad;
        private string _urgentProbeRoot;
        private string _settingsRemapProbeRoot;

        private void OnEnable()
        {
            InputSystem.onDeviceChange += HandleDeviceChange;
        }

        private void Start()
        {
            LogConnectedControllers("startup");
            ParseAutomationArguments();
            if (!string.IsNullOrWhiteSpace(_urgentProbeRoot))
            {
                StartCoroutine(RunUrgentPlayableControllerProbe());
            }
            else if (!string.IsNullOrWhiteSpace(_settingsRemapProbeRoot))
            {
                StartCoroutine(RunSettingsRemapProbe());
            }
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
            RemoveAutomationGamepad();
        }

        internal static string BuildConnectedControllerSummary()
        {
            var gamepads = Gamepad.all;
            if (gamepads.Count == 0)
            {
                return "count=0";
            }

            var devices = gamepads
                .Select((gamepad, index) =>
                    $"{index}:{Sanitize(gamepad.displayName)}|{Sanitize(gamepad.layout)}|{Sanitize(gamepad.description.product)}")
                .ToArray();
            return $"count={gamepads.Count} devices={string.Join(",", devices)}";
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "unknown"
                : value.Replace(' ', '_').Replace(',', '_').Replace('|', '_');
        }

        private static void LogConnectedControllers(string reason)
        {
            Debug.Log($"[WOF-AUTOMATION] CONTROLLER_DEVICES reason={reason} {BuildConnectedControllerSummary()}");
        }

        private static void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is not Gamepad)
            {
                return;
            }

            LogConnectedControllers(change.ToString());
        }

        private void ParseAutomationArguments()
        {
            const string prefix = "--wof-urgent-controller-probe=";
            const string settingsPrefix = "--wof-settings-remap-probe=";
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                var activePrefix = argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? prefix
                    : argument.StartsWith(settingsPrefix, StringComparison.OrdinalIgnoreCase)
                        ? settingsPrefix
                        : null;
                if (activePrefix == null) continue;
                var path = argument.Substring(activePrefix.Length).Trim('"');
                if (!path.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError("[WOF-AUTOMATION] CONTROLLER_PROBE_FAIL screenshot-root-not-on-d");
                    return;
                }
                Directory.CreateDirectory(path);
                if (activePrefix == prefix) _urgentProbeRoot = path;
                else _settingsRemapProbeRoot = path;
            }
        }

        private IEnumerator RunSettingsRemapProbe()
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while ((WofHud.Instance == null || !WofHud.Instance.IsGameplayVisible) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            if (WofHud.Instance == null || !WofHud.Instance.IsGameplayVisible)
            {
                FailSettingsProbe("gameplay-hud-not-ready");
                yield break;
            }

            _automationGamepad = InputSystem.AddDevice<Gamepad>("WOF Settings Remap QA Controller");
            _automationGamepad.MakeCurrent();
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState());
            yield return null;
            yield return null;

            yield return TapControllerButtonUntil(GamepadButton.Start, () => WofPauseAndScoreboardRuntime.IsPauseOpen, 4f);
            if (!WofPauseAndScoreboardRuntime.IsPauseOpen)
            {
                FailSettingsProbe("start-did-not-open-pause");
                yield break;
            }

            yield return TapControllerButton(GamepadButton.DpadDown);
            yield return TapControllerButton(GamepadButton.A);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return TapControllerButton(GamepadButton.DpadRight);
            for (var index = 0; index < 7; index++) yield return TapControllerButton(GamepadButton.DpadDown);
            yield return TapControllerButton(GamepadButton.A);
            yield return TapControllerButton(GamepadButton.Y);
            yield return new WaitForSecondsRealtime(0.2f);

            var leftCastButton = WofControllerBindings.GetButton(WofControllerActions.LeftCast);
            var menuSelectButton = WofControllerBindings.GetButton(WofControllerActions.MenuSelect);
            if (leftCastButton != WofControllerButtons.Y || menuSelectButton != WofControllerButtons.A)
            {
                FailSettingsProbe($"binding-isolation-failed leftCast={leftCastButton} menuSelect={menuSelectButton}");
                yield break;
            }

            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState().WithButton(GamepadButton.Y));
            yield return null;
            var castActive = WofControllerBindings.WasPressedThisFrame(_automationGamepad, WofControllerActions.LeftCast);
            var selectActive = WofControllerBindings.WasPressedThisFrame(_automationGamepad, WofControllerActions.MenuSelect);
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState());
            yield return null;
            if (!castActive || selectActive)
            {
                FailSettingsProbe($"action-routing-failed cast={castActive} select={selectActive}");
                yield break;
            }

            ScreenCapture.CaptureScreenshot(Path.Combine(_settingsRemapProbeRoot, "controller-remap-left-cast-y.png"));
            yield return new WaitForSecondsRealtime(0.75f);
            yield return TapControllerButton(GamepadButton.B);
            yield return TapControllerButtonUntil(GamepadButton.B, () => !WofPauseAndScoreboardRuntime.IsPauseOpen, 4f);
            if (WofPauseAndScoreboardRuntime.IsPauseOpen)
            {
                FailSettingsProbe("menu-back-did-not-close-settings-and-pause");
                yield break;
            }

            Debug.Log("[WOF-AUTOMATION] SETTINGS_CONTROLLER_REMAP_PASS action=leftCast button=y isolatedFrom=menuSelect");
        }

        private IEnumerator RunUrgentPlayableControllerProbe()
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while ((WofHud.Instance == null || !WofHud.Instance.IsGameplayVisible) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (WofHud.Instance == null || !WofHud.Instance.IsGameplayVisible)
            {
                FailProbe("gameplay-hud-not-ready");
                yield break;
            }

            _automationGamepad = InputSystem.AddDevice<Gamepad>("WOF Urgent Playable QA Controller");
            _automationGamepad.MakeCurrent();
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState());
            yield return null;
            yield return null;

            if (WofHud.Instance.AreMobileControlsVisible)
            {
                FailProbe("touch-controls-visible-with-controller");
                yield break;
            }
            Debug.Log("[WOF-AUTOMATION] MOBILE_CONTROLLER_UI_PASS touchControlsVisible=false");
            yield return CaptureProbeScreenshot("mobile-controller-hud-hidden.png");

            yield return TapControllerButtonUntil(GamepadButton.DpadUp, () => WofSpellMenuRuntime.IsOpen, 5f);
            if (!WofSpellMenuRuntime.IsOpen)
            {
                FailProbe("controller-dpad-up-did-not-open-spell-menu");
                yield break;
            }
            Debug.Log("[WOF-AUTOMATION] CONTROLLER_SPELL_MENU_PASS");
            yield return CaptureProbeScreenshot("controller-spell-menu.png");

            yield return TapControllerButtonUntil(GamepadButton.B, () => !WofSpellMenuRuntime.IsOpen, 5f);
            if (WofSpellMenuRuntime.IsOpen)
            {
                FailProbe("controller-b-did-not-close-spell-menu");
                yield break;
            }

            yield return TapControllerButtonUntil(GamepadButton.DpadLeft, () => WofNavigationMapRuntime.IsExpanded, 5f);
            if (!WofNavigationMapRuntime.IsExpanded)
            {
                FailProbe("controller-dpad-left-did-not-open-navigation-map");
                yield break;
            }
            Debug.Log("[WOF-AUTOMATION] CONTROLLER_NAVIGATION_MAP_PASS");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return CaptureProbeScreenshot("controller-navigation-map.png");

            yield return TapControllerButtonUntil(GamepadButton.Y, () => !WofNavigationMapRuntime.HasWaypoint, 2f);
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState { rightTrigger = 1f });
            var zoomInDeadline = Time.realtimeSinceStartup + 2f;
            while (WofNavigationMapRuntime.ExpandedZoom < 1.35f &&
                   Time.realtimeSinceStartup < zoomInDeadline) yield return null;
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState());
            yield return null;
            if (WofNavigationMapRuntime.ExpandedZoom < 1.35f)
            {
                FailProbe($"controller-map-zoom-in-failed zoom={WofNavigationMapRuntime.ExpandedZoom:F2}");
                yield break;
            }
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState { rightStick = new Vector2(0.75f, 0.45f) });
            yield return new WaitForSecondsRealtime(0.7f);
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState());
            yield return null;
            yield return TapControllerButtonUntil(GamepadButton.X, () => WofNavigationMapRuntime.HasWaypoint, 3f);
            if (!WofNavigationMapRuntime.HasWaypoint)
            {
                FailProbe("controller-x-did-not-set-map-waypoint");
                yield break;
            }
            yield return CaptureProbeScreenshot("controller-map-waypoint-zoomed.png");
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState { leftTrigger = 1f });
            var zoomOutDeadline = Time.realtimeSinceStartup + 3f;
            while (WofNavigationMapRuntime.ExpandedZoom > 1.01f &&
                   Time.realtimeSinceStartup < zoomOutDeadline) yield return null;
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState());
            yield return null;
            if (WofNavigationMapRuntime.ExpandedZoom > 1.01f)
            {
                FailProbe($"controller-map-zoom-out-failed zoom={WofNavigationMapRuntime.ExpandedZoom:F2}");
                yield break;
            }
            Debug.Log("[WOF-AUTOMATION] CONTROLLER_MAP_ZOOM_WAYPOINT_PASS zoom=1.00 waypoint=true");

            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var player = playerObject == null ? null : playerObject.GetComponent<WofPlayerController>();
            if (player == null)
            {
                FailProbe("controller-fast-travel-player-not-ready");
                yield break;
            }
            yield return TapControllerButton(GamepadButton.DpadDown);
            yield return new WaitForSecondsRealtime(0.15f);
            var selectedBaseDestination = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (selectedBaseDestination == null || selectedBaseDestination.name != "TravelBase")
            {
                FailProbe($"controller-base-selection-failed selected={selectedBaseDestination?.name ?? "none"}");
                yield break;
            }
            yield return TapControllerButtonUntil(GamepadButton.A, () => !WofNavigationMapRuntime.IsExpanded, 5f);
            var travelDeadline = Time.realtimeSinceStartup + 5f;
            var basePosition = new Vector3(0f, 15f, 30f);
            while ((player.transform.position - basePosition).sqrMagnitude > 4f &&
                   Time.realtimeSinceStartup < travelDeadline) yield return null;
            if (WofNavigationMapRuntime.IsExpanded || (player.transform.position - basePosition).sqrMagnitude > 4f)
            {
                FailProbe($"controller-fast-travel-failed position={player.transform.position}");
                yield break;
            }
            Debug.Log($"[WOF-AUTOMATION] CONTROLLER_FAST_TRAVEL_PASS destination=Base position={player.transform.position}");
            if (!WofNavigationMapRuntime.HasWaypoint)
            {
                FailProbe("waypoint-was-not-preserved-after-fast-travel");
                yield break;
            }
            yield return CaptureProbeScreenshot("controller-waypoint-compass.png");

            yield return TapControllerButtonUntil(GamepadButton.DpadLeft, () => WofNavigationMapRuntime.IsExpanded, 5f);
            var selectedDestination = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (selectedDestination == null || selectedDestination.name != "TravelLilyCoil")
            {
                FailProbe($"controller-lily-coil-selection-failed selected={selectedDestination?.name ?? "none"}");
                yield break;
            }
            yield return TapControllerButtonUntil(GamepadButton.A, () => !WofNavigationMapRuntime.IsExpanded, 5f);
            var lilyTravelDeadline = Time.realtimeSinceStartup + 12f;
            while (((player.transform.position - WofLilyCoilLayout.PlayableSpawnPosition).sqrMagnitude > 4f ||
                    !SceneManager.GetSceneByName(WofLilyCoilSceneLoader.SceneName).isLoaded) &&
                   Time.realtimeSinceStartup < lilyTravelDeadline) yield return null;
            if (WofNavigationMapRuntime.IsExpanded ||
                (player.transform.position - WofLilyCoilLayout.PlayableSpawnPosition).sqrMagnitude > 4f ||
                !SceneManager.GetSceneByName(WofLilyCoilSceneLoader.SceneName).isLoaded)
            {
                FailProbe($"controller-lily-coil-fast-travel-failed position={player.transform.position}");
                yield break;
            }
            Debug.Log($"[WOF-AUTOMATION] CONTROLLER_LILY_COIL_FAST_TRAVEL_PASS position={player.transform.position}");
            yield return new WaitForSecondsRealtime(0.75f);
            yield return CaptureProbeScreenshot("controller-lily-coil-fast-travel.png");

            yield return TapControllerButtonUntil(GamepadButton.DpadLeft, () => WofNavigationMapRuntime.IsExpanded, 5f);
            yield return TapControllerButtonUntil(GamepadButton.B, () => !WofNavigationMapRuntime.IsExpanded, 5f);
            if (WofNavigationMapRuntime.IsExpanded)
            {
                FailProbe("controller-b-did-not-close-navigation-map");
                yield break;
            }

            if (player == null || !player.PrepareForAutomationNorthGateProbe())
            {
                FailProbe("north-gate-player-not-ready");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.4f);
            var insideZ = player.transform.position.z;
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState { leftStick = Vector2.up });
            var moveDeadline = Time.realtimeSinceStartup + 4.5f;
            while (Time.realtimeSinceStartup < moveDeadline) yield return null;
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState());
            yield return null;
            var outsideZ = player.transform.position.z;
            if (outsideZ > -242f || outsideZ >= insideZ - 20f)
            {
                FailProbe($"north-gate-blocked insideZ={insideZ:F2} outsideZ={outsideZ:F2}");
                yield break;
            }
            Debug.Log($"[WOF-AUTOMATION] NORTH_GATE_TRAVERSAL_PASS insideZ={insideZ:F2} outsideZ={outsideZ:F2}");
            yield return CaptureProbeScreenshot("north-gate-traversed.png");

            var groundedDeadline = Time.realtimeSinceStartup + 3f;
            while (!player.IsGrounded && Time.realtimeSinceStartup < groundedDeadline) yield return null;
            var startY = player.transform.position.y;
            var peakY = startY;
            InputSystem.QueueStateEvent(
                _automationGamepad,
                new GamepadState().WithButton(GamepadButton.A));
            var thrusterDeadline = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < thrusterDeadline)
            {
                peakY = Mathf.Max(peakY, player.transform.position.y);
                yield return null;
            }
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState());
            yield return null;
            if (peakY < startY + 3f)
            {
                FailProbe($"jump-thruster-did-not-lift startY={startY:F2} peakY={peakY:F2}");
                yield break;
            }
            Debug.Log($"[WOF-AUTOMATION] JUMP_THRUSTER_PASS startY={startY:F2} peakY={peakY:F2}");
            yield return CaptureProbeScreenshot("controller-thruster.png");
            Debug.Log("[WOF-AUTOMATION] URGENT_PLAYABLE_PROBE_PASS");
        }

        private IEnumerator TapControllerButton(GamepadButton button)
        {
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(_automationGamepad, new GamepadState());
            yield return null;
        }

        private IEnumerator TapControllerButtonUntil(GamepadButton button, Func<bool> predicate, float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
            {
                yield return TapControllerButton(button);
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        private IEnumerator CaptureProbeScreenshot(string fileName)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_urgentProbeRoot, fileName));
            yield return new WaitForSecondsRealtime(0.75f);
        }

        private static void FailProbe(string reason)
        {
            Debug.LogError($"[WOF-AUTOMATION] URGENT_PLAYABLE_PROBE_FAIL {reason}");
        }

        private static void FailSettingsProbe(string reason)
        {
            Debug.LogError($"[WOF-AUTOMATION] SETTINGS_CONTROLLER_REMAP_FAIL {reason}");
        }

        private void RemoveAutomationGamepad()
        {
            if (_automationGamepad != null && _automationGamepad.added)
            {
                InputSystem.RemoveDevice(_automationGamepad);
            }
            _automationGamepad = null;
        }
    }
}
