using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WOF
{
    /// <summary>
    /// Reports the native Input System gamepads that the running build can use
    /// and records hot-plug changes without introducing a browser or remapping
    /// compatibility layer.
    /// </summary>
    public sealed class WofControllerDeviceRuntime : MonoBehaviour
    {
        private void OnEnable()
        {
            InputSystem.onDeviceChange += HandleDeviceChange;
        }

        private void Start()
        {
            LogConnectedControllers("startup");
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
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
    }
}
