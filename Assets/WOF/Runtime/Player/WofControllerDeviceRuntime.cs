using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var path = argument.Substring(prefix.Length).Trim('"');
                if (!path.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError("[WOF-AUTOMATION] URGENT_PLAYABLE_PROBE_FAIL screenshot-root-not-on-d");
                    return;
                }
                Directory.CreateDirectory(path);
                _urgentProbeRoot = path;
            }
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

            yield return TapControllerButtonUntil(GamepadButton.B, () => !WofNavigationMapRuntime.IsExpanded, 5f);
            if (WofNavigationMapRuntime.IsExpanded)
            {
                FailProbe("controller-b-did-not-close-navigation-map");
                yield break;
            }

            var playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var player = playerObject == null ? null : playerObject.GetComponent<WofPlayerController>();
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
