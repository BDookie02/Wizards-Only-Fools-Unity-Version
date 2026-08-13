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

            WofPlayerController probePlayer = null;
            if (IsChicagoViewProbeRequested() || WofChicagoTraversalProbe.IsRequested())
            {
                var deadline = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < deadline && probePlayer == null)
                {
                    var players = FindObjectsByType<WofPlayerController>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
                    foreach (var candidate in players)
                    {
                        if (candidate.IsSpawned && candidate.IsOwner)
                        {
                            probePlayer = candidate;
                            break;
                        }
                    }
                    if (probePlayer == null) yield return null;
                }

                if (probePlayer == null || !probePlayer.PrepareForAutomationVillagerInteractionProbe(
                        WofChicagoCityLayout.ViewProbeSpawn,
                        0f,
                        -5f))
                {
                    Debug.LogError("[WOF-AUTOMATION] CHICAGO_CITY_SCENE_FAILED stage=probe-position");
                    yield break;
                }

                if (WofChicagoTraversalProbe.IsRequested())
                {
                    yield return WofChicagoTraversalProbe.Run(
                        probePlayer,
                        WofChicagoTraversalRules.BuildBeanParkRoute());
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
