using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofEnginePlaceableSlot
    {
        public string slotId;
        public string label;
        public long savedAt;
        public List<WofEnginePlaceableRecord> objects = new();
    }

    [Serializable]
    public sealed class WofEnginePlaceableStorageDocument
    {
        public int version = 1;
        public List<WofEnginePlaceableRecord> current = new();
        public List<WofEnginePlaceableSlot> slots = new();
    }

    public static class WofEnginePlaceableStorage
    {
        internal const string StorageFileName = "engine-placeables-v1.json";

        public static string GetStorageDirectory()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "EnginePlaceables"));
        }

        public static WofEnginePlaceableStorageDocument Load()
        {
            try
            {
                var path = Path.Combine(GetStorageDirectory(), StorageFileName);
                if (!File.Exists(path)) return new WofEnginePlaceableStorageDocument();
                return Sanitize(JsonUtility.FromJson<WofEnginePlaceableStorageDocument>(File.ReadAllText(path)));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF-AUTOMATION] ENGINE_STORAGE_LOAD_WARNING error=\"{exception.Message}\"");
                return new WofEnginePlaceableStorageDocument();
            }
        }

        public static bool Save(WofEnginePlaceableStorageDocument document)
        {
            try
            {
                Directory.CreateDirectory(GetStorageDirectory());
                var sanitized = Sanitize(document);
                File.WriteAllText(
                    Path.Combine(GetStorageDirectory(), StorageFileName),
                    JsonUtility.ToJson(sanitized, true));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WOF-AUTOMATION] ENGINE_STORAGE_SAVE_WARNING error=\"{exception.Message}\"");
                return false;
            }
        }

        public static string SanitizeSlotId(string value)
        {
            var normalized = Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), "[^a-z0-9-]", "-");
            normalized = Regex.Replace(normalized, "-+", "-");
            if (normalized.Length == 0) return "slot-1";
            return normalized.Length > 32 ? normalized.Substring(0, 32) : normalized;
        }

        public static string GetSlotLabel(string slotId, string label = null)
        {
            var trimmed = (label ?? string.Empty).Trim();
            if (trimmed.Length > 0) return trimmed.Length > 36 ? trimmed.Substring(0, 36) : trimmed;
            var match = Regex.Match(slotId ?? string.Empty, "^slot-(\\d+)$");
            return match.Success ? $"Slot {match.Groups[1].Value}" : slotId;
        }

        public static List<WofEnginePlaceableRecord> SanitizeObjects(IReadOnlyList<WofEnginePlaceableRecord> source)
        {
            var valid = new List<WofEnginePlaceableRecord>();
            if (source == null) return valid;
            var start = Mathf.Max(0, source.Count - WofEnginePlaceableCatalog.MaximumPlacedObjects);
            for (var index = start; index < source.Count; index++)
            {
                var record = source[index];
                if (record.placeableId == "training-spell-dummy") continue;
                var definition = WofEnginePlaceableCatalog.Find(record.placeableId);
                if (definition == null || !float.IsFinite(record.x) || !float.IsFinite(record.y) ||
                    !float.IsFinite(record.z) || !float.IsFinite(record.yaw)) continue;
                record.instanceId = string.IsNullOrWhiteSpace(record.instanceId)
                    ? $"engine-{record.placeableId}-{index}"
                    : record.instanceId;
                record.label = string.IsNullOrWhiteSpace(record.label) ? definition.Name : record.label;
                valid.Add(record);
            }
            return valid;
        }

        public static WofEnginePlaceableStorageDocument Sanitize(WofEnginePlaceableStorageDocument source)
        {
            var result = new WofEnginePlaceableStorageDocument
            {
                current = SanitizeObjects(source?.current)
            };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (source?.slots == null) return result;
            for (var index = 0; index < source.slots.Count && result.slots.Count < WofEnginePlaceableCatalog.MaximumSaveSlots; index++)
            {
                var slot = source.slots[index];
                if (slot == null) continue;
                var slotId = SanitizeSlotId(slot.slotId);
                if (!seen.Add(slotId)) continue;
                result.slots.Add(new WofEnginePlaceableSlot
                {
                    slotId = slotId,
                    label = GetSlotLabel(slotId, slot.label),
                    savedAt = Math.Max(0, slot.savedAt),
                    objects = SanitizeObjects(slot.objects)
                });
            }
            result.slots.Sort((left, right) => right.savedAt.CompareTo(left.savedAt));
            return result;
        }

        public static WofEnginePlaceableSlot FindSlot(WofEnginePlaceableStorageDocument document, string slotId)
        {
            var safeId = SanitizeSlotId(slotId);
            if (document?.slots == null) return null;
            for (var index = 0; index < document.slots.Count; index++)
            {
                if (document.slots[index].slotId == safeId) return document.slots[index];
            }
            return null;
        }

        public static void SaveSlot(
            WofEnginePlaceableStorageDocument document,
            string slotId,
            string label,
            IReadOnlyList<WofEnginePlaceableRecord> objects,
            long savedAt)
        {
            var safeId = SanitizeSlotId(slotId);
            document.slots ??= new List<WofEnginePlaceableSlot>();
            document.slots.RemoveAll(slot => slot != null && SanitizeSlotId(slot.slotId) == safeId);
            document.slots.Add(new WofEnginePlaceableSlot
            {
                slotId = safeId,
                label = GetSlotLabel(safeId, label),
                savedAt = Math.Max(0, savedAt),
                objects = SanitizeObjects(objects)
            });
            document.slots.Sort((left, right) => right.savedAt.CompareTo(left.savedAt));
            if (document.slots.Count > WofEnginePlaceableCatalog.MaximumSaveSlots)
                document.slots.RemoveRange(WofEnginePlaceableCatalog.MaximumSaveSlots,
                    document.slots.Count - WofEnginePlaceableCatalog.MaximumSaveSlots);
        }

        public static bool DeleteSlot(WofEnginePlaceableStorageDocument document, string slotId)
        {
            var safeId = SanitizeSlotId(slotId);
            return document?.slots != null && document.slots.RemoveAll(
                slot => slot != null && SanitizeSlotId(slot.slotId) == safeId) > 0;
        }
    }
}
