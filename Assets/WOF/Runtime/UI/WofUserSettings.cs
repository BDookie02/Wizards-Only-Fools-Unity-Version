using System;
using System.IO;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofUserSettings
    {
        public int version = 1;
        public float mouseSensitivity = WofUserSettingsRules.DefaultMouseSensitivity;
        public float controllerLookSensitivity = WofUserSettingsRules.DefaultControllerLookSensitivity;
        public float hudTextScale = WofUserSettingsRules.DefaultHudTextScale;
        public bool keyboardArrowLookEnabled = true;
        public bool voiceChatEnabled;
        public string voiceInputMode = "openMic";
        public string voicePushToTalkKey = "V";
        public float voiceOutputVolume = WofUserSettingsRules.DefaultVoiceOutputVolume;
        public float voiceProximityRange = WofUserSettingsRules.DefaultVoiceProximityRange;
        public WofControllerBindingEntry[] controllerBindings = WofControllerBindingRules.CreateDefaults();
    }

    public static class WofUserSettingsRules
    {
        public const float DefaultMouseSensitivity = 0.002f;
        public const float DefaultControllerLookSensitivity = 5.3f;
        public const float DefaultHudTextScale = 1f;
        public const float DefaultVoiceOutputVolume = 0.85f;
        public const float DefaultVoiceProximityRange = 28f;

        public static void Normalize(WofUserSettings settings)
        {
            if (settings == null) return;
            settings.version = 1;
            settings.mouseSensitivity = ClampFinite(settings.mouseSensitivity, 0.0005f, 0.006f, DefaultMouseSensitivity);
            settings.controllerLookSensitivity = ClampFinite(settings.controllerLookSensitivity, 0.8f, 6f, DefaultControllerLookSensitivity);
            settings.hudTextScale = ClampFinite(settings.hudTextScale, 0.75f, 1.45f, DefaultHudTextScale);
            settings.voiceInputMode = settings.voiceInputMode == "pushToTalk" ? "pushToTalk" : "openMic";
            settings.voicePushToTalkKey = string.IsNullOrWhiteSpace(settings.voicePushToTalkKey)
                ? "V"
                : settings.voicePushToTalkKey.Trim().ToUpperInvariant();
            settings.voiceOutputVolume = ClampFinite(settings.voiceOutputVolume, 0f, 1f, DefaultVoiceOutputVolume);
            settings.voiceProximityRange = ClampFinite(settings.voiceProximityRange, 8f, 64f, DefaultVoiceProximityRange);
            settings.controllerBindings = WofControllerBindingRules.Normalize(settings.controllerBindings);
            // Unity voice transport has not been ported. Never restore a stale UI-only enabled state.
            settings.voiceChatEnabled = false;
        }

        private static float ClampFinite(float value, float minimum, float maximum, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : Mathf.Clamp(value, minimum, maximum);
        }
    }

    public static class WofUserSettingsStore
    {
        private const string WebGlPlayerPrefsKey = "wizards-only-fools-unity-settings";

        public static WofUserSettings Load()
        {
            var settings = new WofUserSettings();
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                var json = PlayerPrefs.GetString(WebGlPlayerPrefsKey, string.Empty);
#else
                var path = ResolvePath();
                var json = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
#endif
                if (!string.IsNullOrWhiteSpace(json))
                {
                    settings = JsonUtility.FromJson<WofUserSettings>(json) ?? settings;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF] Unable to load user settings: {exception.Message}");
            }

            WofUserSettingsRules.Normalize(settings);
            return settings;
        }

        public static bool Save(WofUserSettings settings)
        {
            if (settings == null) return false;
            WofUserSettingsRules.Normalize(settings);
            var json = JsonUtility.ToJson(settings, true);
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                PlayerPrefs.SetString(WebGlPlayerPrefsKey, json);
                PlayerPrefs.Save();
#else
                var path = ResolvePath();
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directory)) return false;
                Directory.CreateDirectory(directory);
                var temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporaryPath, path);
#endif
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF] Unable to save user settings: {exception.Message}");
                return false;
            }
        }

        public static string ResolvePath()
        {
            var profilePath = WofSurvivalProfileStore.ResolveWindowsDevelopmentPath();
            var directory = Path.GetDirectoryName(profilePath);
            return Path.Combine(directory ?? Application.persistentDataPath, "settings-v1.json");
        }
    }
}
