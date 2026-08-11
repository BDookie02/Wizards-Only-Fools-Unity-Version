using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WOF
{
    public static class WofQuestDevStore
    {
        public const int CurrentVersion = 1;
        private const string FileName = "quest-npc-programs-v1.json";
        private const string PlayerPrefsKey = "wizards-only-fools-quest-npc-programs";
        private static WofQuestNpcProgramCollection s_Cache;

        public static WofQuestNpcProgram FindProgram(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return null;
            var data = LoadCollection();
            for (var index = 0; index < data.programs.Length; index++)
            {
                var program = data.programs[index];
                if (program != null && string.Equals(program.npcId, npcId, StringComparison.Ordinal))
                {
                    return WofQuestDevRules.CloneProgram(program);
                }
            }
            return null;
        }

        public static WofQuestNpcProgram[] LoadPrograms()
        {
            var data = LoadCollection();
            var programs = new WofQuestNpcProgram[data.programs.Length];
            for (var index = 0; index < programs.Length; index++)
            {
                programs[index] = WofQuestDevRules.CloneProgram(data.programs[index]);
            }
            return programs;
        }

        public static bool SaveProgram(WofQuestNpcProgram program, WofQuestNpcEditorTarget? target = null)
        {
            var clean = WofQuestDevRules.SanitizeProgram(
                program,
                target,
                now: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (clean == null) return false;
            var data = LoadCollection();
            var replaced = false;
            var programs = new List<WofQuestNpcProgram>(data.programs.Length + 1);
            for (var index = 0; index < data.programs.Length; index++)
            {
                var existing = data.programs[index];
                if (existing == null) continue;
                if (string.Equals(existing.npcId, clean.npcId, StringComparison.Ordinal))
                {
                    programs.Add(clean);
                    replaced = true;
                }
                else
                {
                    programs.Add(existing);
                }
            }
            if (!replaced && programs.Count < WofQuestDevRules.MaximumProgramCount) programs.Add(clean);
            data.programs = programs.ToArray();
            SaveCollection(data);
            return true;
        }

        public static bool RemoveProgram(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return false;
            var data = LoadCollection();
            var programs = new List<WofQuestNpcProgram>(data.programs.Length);
            var removed = false;
            for (var index = 0; index < data.programs.Length; index++)
            {
                var program = data.programs[index];
                if (program != null && string.Equals(program.npcId, npcId, StringComparison.Ordinal))
                {
                    removed = true;
                    continue;
                }
                if (program != null) programs.Add(program);
            }
            if (!removed) return false;
            data.programs = programs.ToArray();
            if (string.Equals(data.claimedDarrelNpcId, npcId, StringComparison.Ordinal))
            {
                data.claimedDarrelNpcId = string.Empty;
            }
            SaveCollection(data);
            return true;
        }

        public static WofQuestNpcProgram ClaimDarrel(WofQuestNpcEditorTarget target)
        {
            var darrelTarget = new WofQuestNpcEditorTarget(
                target.NpcId,
                target.TownId,
                target.HutId,
                "Darrel",
                target.Theme,
                target.Position);
            var program = WofQuestDevRules.CreateDefaultProgram(darrelTarget);
            var data = LoadCollection();
            data.claimedDarrelNpcId = target.NpcId;
            SaveCollection(data);
            SaveProgram(program, darrelTarget);
            return program;
        }

        public static bool IsDarrelNpc(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return false;
            var data = LoadCollection();
            if (!string.IsNullOrWhiteSpace(data.claimedDarrelNpcId))
            {
                return string.Equals(data.claimedDarrelNpcId, npcId, StringComparison.Ordinal);
            }
            return string.Equals(npcId, WofQuestDialogRules.DarrelNpcId, StringComparison.Ordinal);
        }

        public static string ClaimedDarrelNpcId => LoadCollection().claimedDarrelNpcId;

        internal static string DefaultPath => Path.Combine(ResolveStorageRoot(), FileName);

        internal static WofQuestNpcProgramCollection LoadFromPath(string path)
        {
            if (!File.Exists(path)) return NewCollection();
            try
            {
                return Deserialize(File.ReadAllText(path, Encoding.UTF8));
            }
            catch
            {
                return NewCollection();
            }
        }

        internal static void SaveToPath(string path, WofQuestNpcProgramCollection data)
        {
            data = NormalizeCollection(data);
            WriteDurable(path, JsonUtility.ToJson(data, true));
        }

        internal static void ResetCacheForTests()
        {
            s_Cache = null;
        }

        private static WofQuestNpcProgramCollection LoadCollection()
        {
            if (s_Cache != null) return s_Cache;
#if UNITY_WEBGL && !UNITY_EDITOR
            s_Cache = PlayerPrefs.HasKey(PlayerPrefsKey)
                ? Deserialize(PlayerPrefs.GetString(PlayerPrefsKey))
                : NewCollection();
#else
            s_Cache = LoadFromPath(DefaultPath);
#endif
            return s_Cache;
        }

        private static void SaveCollection(WofQuestNpcProgramCollection data)
        {
            s_Cache = NormalizeCollection(data);
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(s_Cache));
            PlayerPrefs.Save();
#else
            SaveToPath(DefaultPath, s_Cache);
#endif
        }

        private static WofQuestNpcProgramCollection Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return NewCollection();
            var data = JsonUtility.FromJson<WofQuestNpcProgramCollection>(json);
            return data == null || data.version != CurrentVersion ? NewCollection() : NormalizeCollection(data);
        }

        private static WofQuestNpcProgramCollection NormalizeCollection(WofQuestNpcProgramCollection data)
        {
            data ??= NewCollection();
            data.version = CurrentVersion;
            data.claimedDarrelNpcId ??= string.Empty;
            var source = data.programs ?? Array.Empty<WofQuestNpcProgram>();
            var programs = new List<WofQuestNpcProgram>(Math.Min(source.Length, WofQuestDevRules.MaximumProgramCount));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.Length && programs.Count < WofQuestDevRules.MaximumProgramCount; index++)
            {
                var clean = WofQuestDevRules.SanitizeProgram(source[index]);
                if (clean != null && seen.Add(clean.npcId)) programs.Add(clean);
            }
            data.programs = programs.ToArray();
            return data;
        }

        private static WofQuestNpcProgramCollection NewCollection()
        {
            return new WofQuestNpcProgramCollection
            {
                version = CurrentVersion,
                claimedDarrelNpcId = string.Empty,
                programs = Array.Empty<WofQuestNpcProgram>()
            };
        }

        private static string ResolveStorageRoot()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], "--wof-quest-dev-root", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    return Path.GetFullPath(arguments[index + 1]);
                }
            }
            return Application.persistentDataPath;
        }

        internal static void WriteDurable(string path, string json)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Quest storage path needs a directory.");
            Directory.CreateDirectory(directory);
            var temporaryPath = fullPath + ".tmp";
            var backupPath = fullPath + ".bak";
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json ?? string.Empty);
                writer.Flush();
                stream.Flush(true);
            }
            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, backupPath, true);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }
        }
    }

    public static class WofDarrelQuestSpawnStore
    {
        public const int CurrentVersion = 1;
        private const string FileName = "darrel-quest-spawn-v1.json";
        private const string PlayerPrefsKey = "wizards-only-fools-darrel-quest-spawn";
        private const string JsonNumber = @"-?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?";
        private static WofDarrelQuestSpawnData s_Cache;

        public static WofDarrelQuestSpawn Load()
        {
            var data = LoadData();
            if (data.hasOverride && WofQuestDevRules.IsFinite(data.position) && float.IsFinite(data.yawDegrees))
            {
                return new WofDarrelQuestSpawn(data.position, data.yawDegrees, true);
            }
            return AuthoredSpawn;
        }

        public static WofDarrelQuestSpawn SaveOverride(Vector3 position, float yawDegrees)
        {
            if (!WofQuestDevRules.IsFinite(position) || !float.IsFinite(yawDegrees)) return AuthoredSpawn;
            var data = new WofDarrelQuestSpawnData
            {
                version = CurrentVersion,
                hasOverride = true,
                position = position,
                yawDegrees = yawDegrees
            };
            SaveData(data);
            return new WofDarrelQuestSpawn(position, yawDegrees, true);
        }

        public static void Clear()
        {
            s_Cache = new WofDarrelQuestSpawnData { version = CurrentVersion };
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
#else
            var path = DefaultPath;
            if (File.Exists(path)) File.Delete(path);
#endif
        }

        internal static string DefaultPath => Path.Combine(
            Path.GetDirectoryName(WofQuestDevStore.DefaultPath) ?? Application.persistentDataPath,
            FileName);

        internal static void ResetCacheForTests()
        {
            s_Cache = null;
        }

        internal static WofDarrelQuestSpawn LoadFromPath(string path)
        {
            if (!File.Exists(path)) return AuthoredSpawn;
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var data = TryDeserialize(json);
                return data != null && data.version == CurrentVersion && data.hasOverride &&
                       WofQuestDevRules.IsFinite(data.position) && float.IsFinite(data.yawDegrees)
                    ? new WofDarrelQuestSpawn(data.position, data.yawDegrees, true)
                    : AuthoredSpawn;
            }
            catch
            {
                return AuthoredSpawn;
            }
        }

        internal static void SaveToPath(string path, WofDarrelQuestSpawn spawn)
        {
            var data = new WofDarrelQuestSpawnData
            {
                version = CurrentVersion,
                hasOverride = spawn.IsOverride,
                position = spawn.Position,
                yawDegrees = spawn.YawDegrees
            };
            WofQuestDevStore.WriteDurable(path, JsonUtility.ToJson(data, true));
        }

        private static WofDarrelQuestSpawnData LoadData()
        {
            if (s_Cache != null) return s_Cache;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                try { s_Cache = TryDeserialize(PlayerPrefs.GetString(PlayerPrefsKey)); }
                catch { s_Cache = null; }
            }
#else
            if (File.Exists(DefaultPath))
            {
                try { s_Cache = TryDeserialize(File.ReadAllText(DefaultPath, Encoding.UTF8)); }
                catch { s_Cache = null; }
            }
#endif
            if (s_Cache == null || s_Cache.version != CurrentVersion) s_Cache = new WofDarrelQuestSpawnData { version = CurrentVersion };
            return s_Cache;
        }

        private static void SaveData(WofDarrelQuestSpawnData data)
        {
            s_Cache = data;
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
#else
            WofQuestDevStore.WriteDurable(DefaultPath, JsonUtility.ToJson(data, true));
#endif
        }

        private static WofDarrelQuestSpawnData TryDeserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var data = JsonUtility.FromJson<WofDarrelQuestSpawnData>(json);
            if (data == null || !data.hasOverride) return data;

            // JsonUtility silently converts a quoted/non-numeric vector component to zero.
            // Reject that malformed save instead of accepting a plausible but incorrect spawn.
            return HasNumericProperty(json, "x") &&
                   HasNumericProperty(json, "y") &&
                   HasNumericProperty(json, "z") &&
                   HasNumericProperty(json, "yawDegrees")
                ? data
                : null;
        }

        private static bool HasNumericProperty(string json, string property)
        {
            var pattern = "\\\"" + Regex.Escape(property) + "\\\"\\s*:\\s*" + JsonNumber + "\\s*[,}]";
            return Regex.IsMatch(json, pattern, RegexOptions.CultureInvariant);
        }

        private static WofDarrelQuestSpawn AuthoredSpawn => new(
            WofDarrelGroveLayout.SpawnPosition,
            WofDarrelGroveLayout.UnitySpawnYawDegrees,
            false);
    }
}
