using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofSurvivalProfile
    {
        public int version = WofSurvivalProfileStore.CurrentVersion;
        public long savedAtUnixMilliseconds;
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
        public const int CurrentVersion = 2;
        private const string PlayerPrefsKey = "wizards-only-fools-survival-save";
        private const string PlayerPrefsBackupKey = PlayerPrefsKey + "-backup";

        public static WofSurvivalProfile Load()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return LoadFromPlayerPrefs();
#else
            return LoadFromPath(ResolveWindowsDevelopmentPath());
#endif
        }

        public static bool Save(WofSurvivalProfile profile)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return SaveToPlayerPrefs(profile);
#else
            return SaveToPath(ResolveWindowsDevelopmentPath(), profile);
#endif
        }

        internal static WofSurvivalProfile LoadFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var backupPath = GetBackupPath(path);
            try
            {
                if (TryReadProfile(path, out var primary, out var primaryMigrated))
                {
                    if (primaryMigrated)
                    {
                        SaveToPath(path, primary);
                        Debug.Log($"[WOF-AUTOMATION] PROFILE_MIGRATED from=1 to={CurrentVersion}");
                    }
                    return primary;
                }

                var primaryExists = File.Exists(path);
                var backupExists = File.Exists(backupPath);
                if (!primaryExists && !backupExists) return null;
                var corruptPath = primaryExists ? QuarantineCorruptFile(path) : "missing-primary";
                if (!TryReadProfile(backupPath, out var backup, out _))
                {
                    Debug.LogWarning($"[WOF] Survival profile was corrupt and no valid backup was available: {corruptPath}");
                    return null;
                }

                if (!SaveToPath(path, backup))
                {
                    Debug.LogWarning("[WOF] Loaded the survival backup but could not restore the primary file.");
                }
                Debug.Log($"[WOF-AUTOMATION] PROFILE_RECOVERED source=backup corrupt={corruptPath}");
                return backup;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF] Unable to load survival profile: {exception.Message}");
                return null;
            }
        }

        internal static bool SaveToPath(
            string path,
            WofSurvivalProfile profile,
            long? savedAtUnixMilliseconds = null)
        {
            if (!PrepareForSave(profile, savedAtUnixMilliseconds)) return false;
            if (string.IsNullOrWhiteSpace(path)) return false;

            var temporaryPath = path + ".tmp";
            var backupPath = GetBackupPath(path);
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directory)) return false;
                Directory.CreateDirectory(directory);
                WriteAllTextDurably(temporaryPath, JsonUtility.ToJson(profile, true));
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF] Unable to save survival profile: {exception.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
                catch
                {
                    // A stranded temporary file is ignored on load and can be overwritten by the next save.
                }
            }
        }

        internal static string GetBackupPath(string path)
        {
            return path + ".bak";
        }

        internal static bool TryDeserialize(string json, out WofSurvivalProfile profile, out bool migrated)
        {
            profile = null;
            migrated = false;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var envelope = JsonUtility.FromJson<WofSurvivalProfileVersionEnvelope>(json);
                if (envelope == null || (envelope.version != 1 && envelope.version != CurrentVersion))
                    return false;
                profile = JsonUtility.FromJson<WofSurvivalProfile>(json);
                if (profile == null) return false;
                migrated = profile.version == 1;
                profile.version = CurrentVersion;
                NormalizeProfile(profile);
                if (profile.playerName.Length < 2)
                {
                    profile = null;
                    migrated = false;
                    return false;
                }
                return true;
            }
            catch
            {
                profile = null;
                migrated = false;
                return false;
            }
        }

        private static WofSurvivalProfile LoadFromPlayerPrefs()
        {
            try
            {
                var json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
                if (TryDeserialize(json, out var primary, out var migrated))
                {
                    if (migrated) SaveToPlayerPrefs(primary);
                    return primary;
                }

                var backupJson = PlayerPrefs.GetString(PlayerPrefsBackupKey, string.Empty);
                if (!TryDeserialize(backupJson, out var backup, out _)) return null;
                SaveToPlayerPrefs(backup);
                Debug.Log("[WOF-AUTOMATION] PROFILE_RECOVERED source=webgl-backup");
                return backup;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF] Unable to load survival profile: {exception.Message}");
                return null;
            }
        }

        private static bool SaveToPlayerPrefs(WofSurvivalProfile profile)
        {
            if (!PrepareForSave(profile, null)) return false;
            try
            {
                var previous = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
                if (TryDeserialize(previous, out _, out _))
                    PlayerPrefs.SetString(PlayerPrefsBackupKey, previous);
                PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(profile, true));
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF] Unable to save survival profile: {exception.Message}");
                return false;
            }
        }

        private static bool PrepareForSave(WofSurvivalProfile profile, long? savedAtUnixMilliseconds)
        {
            if (profile == null) return false;
            NormalizeProfile(profile);
            if (profile.playerName.Length < 2) return false;
            profile.version = CurrentVersion;
            profile.savedAtUnixMilliseconds = savedAtUnixMilliseconds ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return true;
        }

        private static void NormalizeProfile(WofSurvivalProfile profile)
        {
            profile.playerName = WofLaunchRules.SanitizePlayerName(profile.playerName);
            profile.survivalLevel = Mathf.Clamp(profile.survivalLevel, 1, 999);
            profile.survivalXp = Mathf.Clamp(profile.survivalXp, 0, 99999999);
            profile.lastMode = profile.lastMode == "multiplayer-survival"
                ? "multiplayer-survival"
                : "solo-survival";
            profile.savedAtUnixMilliseconds = Math.Max(0L, profile.savedAtUnixMilliseconds);
            WofCharacterCustomizationRules.Normalize(profile);
            WofSpellQuestRules.NormalizeProfile(profile);
            WofInventoryRules.NormalizeProfile(profile);
        }

        private static bool TryReadProfile(
            string path,
            out WofSurvivalProfile profile,
            out bool migrated)
        {
            profile = null;
            migrated = false;
            return File.Exists(path) && TryDeserialize(File.ReadAllText(path), out profile, out migrated);
        }

        private static string QuarantineCorruptFile(string path)
        {
            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var stem = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff");
            for (var suffix = 0; suffix < 1000; suffix++)
            {
                var suffixText = suffix == 0 ? string.Empty : $"-{suffix}";
                var candidate = Path.Combine(directory, $"{stem}.corrupt-{timestamp}{suffixText}{extension}");
                if (File.Exists(candidate)) continue;
                File.Move(path, candidate);
                return candidate;
            }
            throw new IOException("Could not allocate a quarantine path for the corrupt survival profile.");
        }

        private static void WriteAllTextDurably(string path, string value)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream);
            writer.Write(value);
            writer.Flush();
            stream.Flush(true);
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

        [Serializable]
        private sealed class WofSurvivalProfileVersionEnvelope
        {
            public int version;
        }
    }
}
