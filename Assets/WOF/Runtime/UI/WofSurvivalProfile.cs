using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofSurvivalProfile
    {
        public int version = 1;
        public string playerName = string.Empty;
        public string skinColor = "#d6cf91";
        public string topColor = "#7c3aed";
        public string pantsColor = "#334155";
        public string shoesColor = "#1f2937";
        public string hatColor = "#7c3aed";
        public string hairColor = "#3f2a1d";
        public string facialHairColor = "#3f2a1d";
        public string topStyle = "simple";
        public string pantsStyle = "pants";
        public string shoesStyle = "boots";
        public string hatStyle = "floppy-wizard";
        public string hairStyle = "none";
        public string facialHairStyle = "none";
        public string eyeStyle = "calm";
        public string mouthStyle = "neutral";
        public int survivalLevel = 1;
        public int survivalXp;
        public string lastMode = "solo-survival";
        public string darrelHealingCrystalsQuestStatus = "unstarted";
        public long darrelHealingCrystalsAssignedAt;
        public long darrelHealingCrystalsCompletedAt;
        public string[] questUnlockedSpells = { WofSpellQuestRules.DefaultUnlockedSpell };
        public WofSpellQuestAssignment[] spellQuestAssignments = Array.Empty<WofSpellQuestAssignment>();
        public WofQuestFlagEntry[] questFlags = Array.Empty<WofQuestFlagEntry>();
        public WofInventoryItemEntry[] inventory = Array.Empty<WofInventoryItemEntry>();
    }

    public static class WofLaunchRules
    {
        private static readonly Regex ControlCharacters = new Regex("[\\x00-\\x1f\\x7f]", RegexOptions.CultureInvariant);
        private static readonly Regex RepeatedWhitespace = new Regex("\\s+", RegexOptions.CultureInvariant);
        private static readonly Regex InvalidNameCharacters = new Regex("[^a-zA-Z0-9 _-]", RegexOptions.CultureInvariant);

        public static string SanitizePlayerName(string value)
        {
            var clean = ControlCharacters.Replace(value ?? string.Empty, string.Empty);
            clean = RepeatedWhitespace.Replace(clean, " ");
            clean = InvalidNameCharacters.Replace(clean, string.Empty).Trim();
            return clean.Length <= 18 ? clean : clean.Substring(0, 18);
        }

        public static int WrapOptionIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return ((index % count) + count) % count;
        }

        public static string FormatOption(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var words = Regex.Replace(value.Trim(), "([a-z])([A-Z])", "$1 $2")
                .Replace('-', ' ')
                .Replace('_', ' ')
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < words.Length; index++)
            {
                var word = words[index];
                words[index] = char.ToUpperInvariant(word[0]) + word.Substring(1);
            }

            return string.Join(" ", words);
        }

        public static string MakeWizardName()
        {
            return $"Wizard {Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()}";
        }

        public static string MakeRoomCode()
        {
            return $"wof-{Guid.NewGuid().ToString("N").Substring(0, 5).ToLowerInvariant()}";
        }
    }

    public static class WofSurvivalProfileStore
    {
        private const string PlayerPrefsKey = "wizards-only-fools-survival-save";

        public static WofSurvivalProfile Load()
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                var json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
#else
                var path = ResolveWindowsDevelopmentPath();
                var json = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
#endif
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                var profile = JsonUtility.FromJson<WofSurvivalProfile>(json);
                if (profile == null || profile.version != 1)
                {
                    return null;
                }

                profile.playerName = WofLaunchRules.SanitizePlayerName(profile.playerName);
                WofCharacterCustomizationRules.Normalize(profile);
                WofSpellQuestRules.NormalizeProfile(profile);
                WofInventoryRules.NormalizeProfile(profile);
                return profile.playerName.Length >= 2 ? profile : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF] Unable to load survival profile: {exception.Message}");
                return null;
            }
        }

        public static bool Save(WofSurvivalProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            profile.playerName = WofLaunchRules.SanitizePlayerName(profile.playerName);
            if (profile.playerName.Length < 2)
            {
                return false;
            }

            profile.version = 1;
            WofCharacterCustomizationRules.Normalize(profile);
            WofSpellQuestRules.NormalizeProfile(profile);
            WofInventoryRules.NormalizeProfile(profile);
            var json = JsonUtility.ToJson(profile, true);
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                PlayerPrefs.SetString(PlayerPrefsKey, json);
                PlayerPrefs.Save();
#else
                var path = ResolveWindowsDevelopmentPath();
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return false;
                }

                Directory.CreateDirectory(directory);
                var temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(temporaryPath, path);
#endif
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF] Unable to save survival profile: {exception.Message}");
                return false;
            }
        }

        public static string ResolveWindowsDevelopmentPath()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                const string prefix = "--wof-profile-root=";
                if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var root = argument.Substring(prefix.Length).Trim('"');
                if (root.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.Combine(root, "survival-save-v1.json");
                }
            }

            if (Directory.Exists("D:\\"))
            {
                return "D:\\WOFUserData\\WizardsOnlyFools\\survival-save-v1.json";
            }

            return Path.Combine(Application.persistentDataPath, "survival-save-v1.json");
        }
    }
}
