using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofChicagoCitySceneLoader : MonoBehaviour
    {
        public const string SceneName = "WofChicagoCity";

        private IEnumerator Start()
        {
            if (!SceneManager.GetSceneByName(SceneName).isLoaded)
            {
                yield return WofAdditiveSceneLoadScheduler.LoadSceneAdditively(
                    SceneName,
                    "CHICAGO_CITY_SCENE_FAILED",
                    IsChicagoViewProbeRequested()
                        ? WofAdditiveSceneLoadScheduler.ProbePriority
                        : WofAdditiveSceneLoadScheduler.ChicagoPriority);
            }

            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[WOF-AUTOMATION] CHICAGO_CITY_SCENE_FAILED stage=scene-state");
                yield break;
            }

            if (IsChicagoViewProbeRequested())
            {
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
                        WofChicagoCityLayout.ViewProbeSpawn,
                        0f,
                        -5f))
                {
                    Debug.LogError("[WOF-AUTOMATION] CHICAGO_CITY_SCENE_FAILED stage=probe-position");
                    yield break;
                }
            }

            Debug.Log($"[WOF-AUTOMATION] CHICAGO_CITY_SCENE_READY scene={scene.name} roots={scene.rootCount} origin={WofChicagoCityLayout.WorldOrigin}");
        }

        private static bool IsChicagoViewProbeRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "--wof-chicago-city-view-probe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
