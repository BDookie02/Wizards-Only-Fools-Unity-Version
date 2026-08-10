using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSwampVillageSceneLoader : MonoBehaviour
    {
        public const string SceneName = "WofSwampVillage";

        private IEnumerator Start()
        {
            if (!SceneManager.GetSceneByName(SceneName).isLoaded)
            {
                yield return WofAdditiveSceneLoadScheduler.LoadSceneAdditively(
                    SceneName,
                    "SWAMP_VILLAGE_SCENE_FAILED",
                    IsViewProbeRequested()
                        ? WofAdditiveSceneLoadScheduler.ProbePriority
                        : WofAdditiveSceneLoadScheduler.SwampPriority);
            }

            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[WOF-AUTOMATION] SWAMP_VILLAGE_SCENE_FAILED stage=scene-state");
                yield break;
            }

            if (IsViewProbeRequested())
            {
                var controllerProbe = IsControllerProbeRequested();
                var deadline = Time.realtimeSinceStartup + 20f;
                WofPlayerController player = null;
                while (Time.realtimeSinceStartup < deadline && player == null)
                {
                    var players = FindObjectsByType<WofPlayerController>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
                    foreach (var candidate in players)
                    {
                        if (candidate.IsSpawned && candidate.IsOwner)
                        {
                            player = candidate;
                            break;
                        }
                    }
                    if (player == null) yield return null;
                }

                if (player == null || !player.PrepareForAutomationVillagerInteractionProbe(
                        controllerProbe
                            ? WofSwampVillageLayout.FirstVillagerControllerProbeSpawn
                            : WofSwampVillageLayout.ViewProbeSpawn,
                        0f,
                        controllerProbe ? 26f : -4f))
                {
                    Debug.LogError("[WOF-AUTOMATION] SWAMP_VILLAGE_SCENE_FAILED stage=probe-position");
                    yield break;
                }
            }

            Debug.Log($"[WOF-AUTOMATION] SWAMP_VILLAGE_SCENE_READY scene={scene.name} roots={scene.rootCount} origin={WofSwampVillageLayout.WorldOrigin}");
        }

        private static bool IsViewProbeRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "--wof-swamp-village-view-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsControllerProbeRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "--wof-swamp-villager-controller-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
