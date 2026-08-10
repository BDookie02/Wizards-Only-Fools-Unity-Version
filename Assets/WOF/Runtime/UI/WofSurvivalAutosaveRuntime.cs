using UnityEngine;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofSurvivalAutosaveRuntime : MonoBehaviour
    {
        public const float IntervalSeconds = 15f;

        private float _nextAutosaveAt;

        private void Awake()
        {
            _nextAutosaveAt = Time.realtimeSinceStartup + IntervalSeconds;
        }

        private void Update()
        {
            var bootstrap = WofBootstrap.Instance;
            if (!IsEligibleSession(bootstrap))
            {
                _nextAutosaveAt = Time.realtimeSinceStartup + IntervalSeconds;
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now < _nextAutosaveAt) return;
            do
            {
                _nextAutosaveAt += IntervalSeconds;
            } while (_nextAutosaveAt <= now);
            SaveCurrentProfile("interval");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveCurrentProfile("pause");
        }

        private void OnApplicationQuit()
        {
            SaveCurrentProfile("quit");
        }

        internal static bool IsEligibleSession(WofBootstrap bootstrap)
        {
            return bootstrap != null && bootstrap.IsSurvivalSession && bootstrap.Mode != WofSessionMode.None;
        }

        internal static bool TrySaveProfile(WofSurvivalProfile profile)
        {
            return profile != null && WofSurvivalProfileStore.Save(profile);
        }

        private static bool SaveCurrentProfile(string reason)
        {
            var bootstrap = WofBootstrap.Instance;
            if (!IsEligibleSession(bootstrap)) return false;
            var profile = WofSurvivalProfileStore.Load();
            if (!TrySaveProfile(profile)) return false;
            Debug.Log(
                $"[WOF-AUTOMATION] SURVIVAL_AUTOSAVE_SAVED reason={reason} version={profile.version} savedAt={profile.savedAtUnixMilliseconds}");
            return true;
        }
    }
}
