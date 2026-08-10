using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF
{
    /// <summary>
    /// Unity can deserialize several additive scenes in parallel, but every completed scene
    /// still has to be integrated on the main thread.  Starting all village scenes together
    /// made those integration spikes land on adjacent (and sometimes the same) frames on
    /// Android.  This gate keeps background IO low-priority and integrates one scene at a
    /// time, with a few recovery frames between scenes.
    /// </summary>
    internal static class WofAdditiveSceneLoadScheduler
    {
        internal const int RecoveryFrameCount = 6;
        internal const int RegistrationFrameCount = 8;
        internal const int ProbePriority = 0;
        internal const int ChicagoPriority = 10;
        internal const int SwampPriority = 20;
        internal const int MountainPriority = 30;
        internal const int GraveyardPriority = 40;
        internal const int LilyCoilPriority = 50;

        private static readonly List<SceneLoadRequest> PendingRequests = new();
        private static bool s_LoadInProgress;
        private static string s_LoadingSceneName;
        private static int s_Sequence;
        private static int s_RequestSequence;

        internal static string LoadingSceneName => s_LoadingSceneName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_LoadInProgress = false;
            s_LoadingSceneName = null;
            s_Sequence = 0;
            s_RequestSequence = 0;
            PendingRequests.Clear();
        }

        internal static IEnumerator LoadSceneAdditively(string sceneName, string failureMarker, int priority)
        {
            if (SceneManager.GetSceneByName(sceneName).isLoaded) yield break;

            var request = new SceneLoadRequest(sceneName, priority, ++s_RequestSequence);
            PendingRequests.Add(request);
            var acquired = false;
            try
            {
                // Every loader registers during the same startup frame.  Waiting briefly here
                // gives the gate the complete request set instead of making component Start
                // order decide which (potentially largest) scene loads first.
                for (var frame = 0; frame < RegistrationFrameCount; frame++) yield return null;
                while (s_LoadInProgress || !ReferenceEquals(GetNextRequest(), request)) yield return null;
                if (SceneManager.GetSceneByName(sceneName).isLoaded) yield break;

                PendingRequests.Remove(request);
                acquired = true;
                s_LoadInProgress = true;
                s_LoadingSceneName = sceneName;
                var sequence = ++s_Sequence;
                var startedAt = Time.realtimeSinceStartup;
                var observedFrames = 0;
                var maximumFrameMilliseconds = 0f;
                Application.backgroundLoadingPriority = ThreadPriority.Low;
                Debug.Log(
                    $"[WOF-AUTOMATION] ADDITIVE_SCENE_LOAD_STARTED sequence={sequence} scene={sceneName} priority={priority}");

                var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (operation == null)
                {
                    Debug.LogError($"[WOF-AUTOMATION] {failureMarker} stage=load-operation");
                    yield break;
                }

                operation.priority = -1;
                while (!operation.isDone)
                {
                    observedFrames++;
                    maximumFrameMilliseconds = Mathf.Max(
                        maximumFrameMilliseconds,
                        Time.unscaledDeltaTime * 1000f);
                    yield return null;
                }

                // Do not let the next scene begin deserializing on the frame immediately
                // following this scene's activation/integration work.
                for (var frame = 0; frame < RecoveryFrameCount; frame++) yield return null;

                Debug.Log(
                    $"[WOF-AUTOMATION] ADDITIVE_SCENE_LOAD_FINISHED sequence={sequence} scene={sceneName} " +
                    $"frames={observedFrames} elapsedMs={(Time.realtimeSinceStartup - startedAt) * 1000f:F0} " +
                    $"maxObservedFrameMs={maximumFrameMilliseconds:F2}");
            }
            finally
            {
                PendingRequests.Remove(request);
                if (acquired)
                {
                    s_LoadingSceneName = null;
                    s_LoadInProgress = false;
                }
            }
        }

        private static SceneLoadRequest GetNextRequest()
        {
            SceneLoadRequest next = null;
            foreach (var request in PendingRequests)
            {
                if (next == null || request.Priority < next.Priority ||
                    request.Priority == next.Priority && request.Sequence < next.Sequence)
                    next = request;
            }
            return next;
        }

        private sealed class SceneLoadRequest
        {
            public SceneLoadRequest(string sceneName, int priority, int sequence)
            {
                SceneName = sceneName;
                Priority = priority;
                Sequence = sequence;
            }

            public string SceneName { get; }
            public int Priority { get; }
            public int Sequence { get; }
        }
    }
}
