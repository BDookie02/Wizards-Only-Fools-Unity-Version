using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofLilyCoilSceneLoader : MonoBehaviour
    {
        public const string SceneName = "WofLilyCoil";

        private IEnumerator Start()
        {
            if (!SceneManager.GetSceneByName(SceneName).isLoaded)
            {
                yield return WofAdditiveSceneLoadScheduler.LoadSceneAdditively(
                    SceneName,
                    "LILY_COIL_SCENE_FAILED",
                    IsViewProbeRequested()
                        ? WofAdditiveSceneLoadScheduler.ProbePriority
                        : WofAdditiveSceneLoadScheduler.LilyCoilPriority);
            }

            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[WOF-AUTOMATION] LILY_COIL_SCENE_FAILED stage=scene-state");
                yield break;
            }

            if (IsViewProbeRequested())
            {
                WofPlayerController player = null;
                var deadline = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < deadline && player == null)
                {
                    foreach (var candidate in FindObjectsByType<WofPlayerController>(
                                 FindObjectsInactive.Exclude,
                                 FindObjectsSortMode.None))
                    {
                        if (!candidate.IsSpawned || !candidate.IsOwner) continue;
                        player = candidate;
                        break;
                    }
                    if (player == null) yield return null;
                }

                var variant = ResolveVariant();
                var tunnel = variant == "tunnel";
                var position = tunnel
                    ? WofLilyCoilLayout.GetTunnelViewProbeSpawn()
                    : WofLilyCoilLayout.ExteriorViewProbeSpawn;
                var yaw = tunnel ? WofLilyCoilLayout.GetTunnelViewProbeYaw() : 0f;
                var pitch = tunnel ? WofLilyCoilLayout.GetTunnelViewProbePitch() : 0f;
                var positioned = player != null &&
                                 player.PrepareForAutomationStaticViewProbe(position, yaw, pitch);
                if (!positioned)
                {
                    Debug.LogError("[WOF-AUTOMATION] LILY_COIL_SCENE_FAILED stage=probe-position");
                    yield break;
                }
                var probeCamera = player.GetComponentInChildren<Camera>(true);
                if (probeCamera != null) probeCamera.farClipPlane = 1600f;
                Debug.Log($"[WOF-AUTOMATION] LILY_COIL_PROBE_POSITIONED variant={variant} position={position} yaw={yaw:F3} pitch={pitch:F3}");
            }

            Debug.Log($"[WOF-AUTOMATION] LILY_COIL_SCENE_READY scene={scene.name} roots={scene.rootCount} origin={WofLilyCoilLayout.WorldOrigin}");
        }

        private static bool IsViewProbeRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "--wof-lily-coil-view-probe", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("--wof-lily-coil-view-probe=", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string ResolveVariant()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                const string prefix = "--wof-lily-coil-view-probe=";
                if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                return string.Equals(argument.Substring(prefix.Length).Trim(), "tunnel",
                    StringComparison.OrdinalIgnoreCase)
                    ? "tunnel"
                    : "exterior";
            }
            return "exterior";
        }
    }
}
