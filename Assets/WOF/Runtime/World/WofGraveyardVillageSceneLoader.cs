using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofGraveyardVillageSceneLoader : MonoBehaviour
    {
        public const string SceneName = "WofGraveyardVillage";

        private IEnumerator Start()
        {
            var traversalProbe = WofGraveyardTraversalProbe.IsRequested();
            var traversalRoute = traversalProbe
                ? WofGraveyardTraversalRules.BuildChapelRoute()
                : null;
            WofPlayerController player = null;
            if (!SceneManager.GetSceneByName(SceneName).isLoaded)
            {
                yield return WofAdditiveSceneLoadScheduler.LoadSceneAdditively(
                    SceneName,
                    "GRAVEYARD_VILLAGE_SCENE_FAILED",
                    IsViewProbeRequested()
                        ? WofAdditiveSceneLoadScheduler.ProbePriority
                        : WofAdditiveSceneLoadScheduler.GraveyardPriority);
            }

            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[WOF-AUTOMATION] GRAVEYARD_VILLAGE_SCENE_FAILED stage=scene-state");
                yield break;
            }

            if (IsViewProbeRequested())
            {
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

                var variant = traversalProbe ? "traversal" : ResolveVariant();
                var position = traversalProbe
                    ? traversalRoute[0] + Vector3.up * 1.4f
                    : variant switch
                {
                    "interior" => WofGraveyardVillageLayout.ChapelInteriorViewProbeSpawn,
                    "tombs" => WofGraveyardVillageLayout.TombsViewProbeSpawn,
                    "fence" => WofGraveyardVillageLayout.FenceViewProbeSpawn,
                    _ => WofGraveyardVillageLayout.ViewProbeSpawn
                };
                var yaw = traversalProbe
                    ? WofGraveyardTraversalRules.ResolveHeading(traversalRoute[0], traversalRoute[1])
                    : variant == "tombs" ? 0f : 180f;
                var pitch = traversalProbe ? 0f : variant == "exterior" ? -8f :
                    variant == "interior" ? -2f : variant == "fence" ? -9f : -8f;
                var positioned = player != null && (traversalProbe
                    ? player.PrepareForAutomationVillagerInteractionProbe(position, yaw, pitch)
                    : player.PrepareForAutomationStaticViewProbe(position, yaw, pitch));
                if (!positioned)
                {
                    Debug.LogError("[WOF-AUTOMATION] GRAVEYARD_VILLAGE_SCENE_FAILED stage=probe-position");
                    yield break;
                }
                Debug.Log($"[WOF-AUTOMATION] GRAVEYARD_VILLAGE_PROBE_POSITIONED variant={variant} position={position} yaw={yaw:F3} pitch={pitch:F3}");
            }

            Debug.Log($"[WOF-AUTOMATION] GRAVEYARD_VILLAGE_SCENE_READY scene={scene.name} roots={scene.rootCount} origin={WofGraveyardVillageLayout.WorldOrigin}");
            if (traversalProbe)
            {
                yield return WofGraveyardTraversalProbe.Run(player, traversalRoute);
            }
        }

        private static bool IsViewProbeRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "--wof-graveyard-village-view-probe", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("--wof-graveyard-village-view-probe=", StringComparison.OrdinalIgnoreCase) ||
                    WofGraveyardTraversalProbe.IsRequested())
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
                const string prefix = "--wof-graveyard-village-view-probe=";
                if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var value = argument.Substring(prefix.Length).Trim().ToLowerInvariant();
                if (value == "interior" || value == "tombs" || value == "fence") return value;
            }
            return "exterior";
        }
    }
}
