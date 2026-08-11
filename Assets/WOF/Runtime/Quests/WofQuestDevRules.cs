using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WOF
{
    public readonly struct WofQuestEventPreset
    {
        public WofQuestEventPreset(string label, string line)
        {
            Label = label;
            Line = line;
        }

        public string Label { get; }
        public string Line { get; }
    }

    public enum WofQuestEventBuilderKind
    {
        Message,
        StartQuest,
        CompleteQuest,
        SetFlag
    }

    public readonly struct WofQuestPointRemoval
    {
        public WofQuestPointRemoval(WofQuestScriptPoint[] points, string nextSelectedPointId)
        {
            Points = points;
            NextSelectedPointId = nextSelectedPointId;
        }

        public WofQuestScriptPoint[] Points { get; }
        public string NextSelectedPointId { get; }
    }

    public static class WofQuestDevRules
    {
        public const int MaximumProgramCount = 2048;
        public const int MaximumScriptPointCount = 24;
        public const int MaximumNpcIdLength = 96;
        public const int MaximumTownIdLength = 64;
        public const int MaximumHutIdLength = 96;
        public const int MaximumNameLength = 42;
        public const int MaximumThemeLength = 48;
        public const int MaximumTitleLength = 48;
        public const int MaximumDialogLength = 900;
        public const int MaximumEventScriptLength = 900;

        private static readonly WofQuestEventPreset[] EventPresetsInternal =
        {
            new("RANDOM SPELL", "unlockRandomLockedSpell"),
            new("BLINK", "unlockSpell blink"),
            new("START QUEST", "startQuest town_01_quest"),
            new("COMPLETE", "completeQuest town_01_quest"),
            new("FLAG", "setFlag town_01_quests=1"),
            new("DARREL GROVE", "teleportQuestRealm darrel"),
            new("MESSAGE", "message Good work, wizard.")
        };

        public static IReadOnlyList<WofQuestEventPreset> EventPresets => EventPresetsInternal;

        public static string MakeScriptPointId()
        {
            return $"point-{Guid.NewGuid():N}".Substring(0, 14);
        }

        public static WofQuestNpcProgram CreateDefaultProgram(
            WofQuestNpcEditorTarget target,
            Func<string> idFactory = null,
            long now = 0)
        {
            idFactory ??= MakeScriptPointId;
            now = now > 0 ? now : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (IsDarrelIdentity(target.NpcId) || IsDarrelIdentity(target.DefaultName))
            {
                return new WofQuestNpcProgram
                {
                    npcId = target.NpcId,
                    townId = target.TownId,
                    hutId = target.HutId,
                    displayName = "Darrel",
                    role = WofQuestNpcRole.QuestGiver,
                    theme = target.Theme,
                    hasPosition = true,
                    position = target.Position,
                    greeting = "Who are you what do you want!",
                    scriptPoints = new[]
                    {
                        Point(idFactory(), "Opening", "Darrel: Who are you what do you want!\n\nPlayer choices:\n- None of your business.\n- What kind of wizard has only 2 spells?", "message Darrel opens with a hard stare."),
                        Point(idFactory(), "Jerk Response", "Player: None of your business.\n\nDarrel: Then my business is none of yours. Try again when you remember how doors work.", "message Darrel did not appreciate that."),
                        Point(idFactory(), "Scripted Response", "Player: What kind of wizard has only 2 spells?\n\nDarrel: A pitiful one. Fine. I have a job that might make you slightly less embarrassing.", $"setFlag {WofSpellQuestRules.DarrelAcceptedFlag}=true\nstartQuest spellquest:{WofSpellQuestRules.DarrelRewardSpell}\nmessage Darrel offers the Sacred Garden job."),
                        Point(idFactory(), "Job Brief", "Darrel: Travel to the sacred garden in an alternate dimension. Supposedly there is a spirit dragon there. Bring back healing crystals.\n\nFirst you need a garden draught. Go out into the fields, gather 1 leaves, 1 berries, and 1 roots, then brew it at any brewing station.", $"setFlag {WofSpellQuestRules.DarrelLeavesFlag}=needed\nsetFlag {WofSpellQuestRules.DarrelBerriesFlag}=needed\nsetFlag {WofSpellQuestRules.DarrelRootsFlag}=needed\nsetFlag {WofSpellQuestRules.DarrelPotionFlag}=needed\nmessage Search the fields for leaves, berries, and roots."),
                        Point(idFactory(), "Potion Drink", "The garden draught tastes like a wet lawn and a dare. Reality folds toward the sacred garden.", $"setFlag {WofSpellQuestRules.DarrelPotionFlag}=drunk\nteleportDarrelQuest darrel-grove"),
                        Point(idFactory(), "Sleeping Dragon Encounter", "Spirit Dragon: Hm? State your business, little wizard.\n\nPlayer choices:\n- Fight and take the Healing Crystals by force.\n- Peacefully ask for crystals for Darrel and yourself.", "setFlag darrel:dragon:woken=true\nmessage The Spirit Dragon wakes inside the garden house.")
                    },
                    updatedAt = now
                };
            }

            return new WofQuestNpcProgram
            {
                npcId = target.NpcId,
                townId = target.TownId,
                hutId = target.HutId,
                displayName = target.DefaultName,
                role = WofQuestNpcRole.Villager,
                theme = target.Theme,
                hasPosition = true,
                position = target.Position,
                greeting = "The villager watches you carefully, waiting for the next line of the quest.",
                scriptPoints = new[]
                {
                    Point(idFactory(), "Greeting", "Need something, wizard?", "message Quest scriptpoint reached")
                },
                updatedAt = now
            };
        }

        public static WofQuestNpcProgram CloneProgram(WofQuestNpcProgram program)
        {
            if (program == null) return null;
            var clone = JsonUtility.FromJson<WofQuestNpcProgram>(JsonUtility.ToJson(program));
            clone.scriptPoints ??= Array.Empty<WofQuestScriptPoint>();
            return clone;
        }

        public static WofQuestScriptPoint GetSelectedPoint(WofQuestNpcProgram draft, string selectedPointId)
        {
            if (draft?.scriptPoints == null || draft.scriptPoints.Length == 0) return null;
            for (var index = 0; index < draft.scriptPoints.Length; index++)
            {
                var point = draft.scriptPoints[index];
                if (point != null && string.Equals(point.id, selectedPointId, StringComparison.Ordinal)) return point;
            }
            return draft.scriptPoints[0];
        }

        public static int GetPointIndex(WofQuestNpcProgram draft, WofQuestScriptPoint point)
        {
            return draft?.scriptPoints == null || point == null ? -1 : Array.IndexOf(draft.scriptPoints, point);
        }

        public static int CountEvents(WofQuestScriptPoint point)
        {
            if (point == null || string.IsNullOrEmpty(point.eventScript)) return 0;
            var count = 0;
            var lineHasText = false;
            for (var index = 0; index < point.eventScript.Length; index++)
            {
                var character = point.eventScript[index];
                if (character == '\n' || character == '\r')
                {
                    if (lineHasText) count++;
                    lineHasText = false;
                    if (character == '\r' && index + 1 < point.eventScript.Length && point.eventScript[index + 1] == '\n') index++;
                    continue;
                }
                if (!char.IsWhiteSpace(character)) lineHasText = true;
            }
            return lineHasText ? count + 1 : count;
        }

        public static WofQuestScriptPoint CreatePoint(int pointCount, Func<string> idFactory = null)
        {
            return Point((idFactory ?? MakeScriptPointId)(), $"Point {pointCount + 1}", string.Empty, string.Empty);
        }

        public static WofQuestScriptPoint DuplicatePoint(WofQuestScriptPoint point, Func<string> idFactory = null)
        {
            if (point == null) return null;
            return Point(
                (idFactory ?? MakeScriptPointId)(),
                Truncate($"{(string.IsNullOrEmpty(point.title) ? "Point" : point.title)} Copy", MaximumTitleLength),
                point.dialog,
                point.eventScript);
        }

        public static WofQuestScriptPoint[] InsertPointAfter(
            WofQuestScriptPoint[] points,
            string selectedPointId,
            WofQuestScriptPoint nextPoint)
        {
            points ??= Array.Empty<WofQuestScriptPoint>();
            var selectedIndex = FindPointIndex(points, selectedPointId);
            var insertIndex = selectedIndex < 0 ? points.Length : selectedIndex + 1;
            var next = new WofQuestScriptPoint[Math.Min(MaximumScriptPointCount, points.Length + 1)];
            if (next.Length == points.Length) return points;
            for (int source = 0, target = 0; target < next.Length; target++)
            {
                if (target == insertIndex) next[target] = nextPoint;
                else next[target] = points[source++];
            }
            return next;
        }

        public static bool MovePoint(WofQuestScriptPoint[] points, string selectedPointId, int direction)
        {
            if (points == null) return false;
            var currentIndex = FindPointIndex(points, selectedPointId);
            var targetIndex = currentIndex + Math.Sign(direction);
            if (currentIndex < 0 || targetIndex < 0 || targetIndex >= points.Length) return false;
            (points[currentIndex], points[targetIndex]) = (points[targetIndex], points[currentIndex]);
            return true;
        }

        public static WofQuestPointRemoval? RemovePoint(
            WofQuestScriptPoint[] points,
            string selectedPointId,
            int selectedIndex)
        {
            if (points == null || points.Length <= 1) return null;
            var removeIndex = FindPointIndex(points, selectedPointId);
            if (removeIndex < 0) return null;
            var next = new WofQuestScriptPoint[points.Length - 1];
            for (int source = 0, target = 0; source < points.Length; source++)
            {
                if (source == removeIndex) continue;
                next[target++] = points[source];
            }
            var nextIndex = Mathf.Clamp(selectedIndex - 1, 0, next.Length - 1);
            return new WofQuestPointRemoval(next, next[nextIndex]?.id);
        }

        public static string AppendEventLine(string currentScript, string line)
        {
            var cleaned = (line ?? string.Empty).Trim();
            if (cleaned.Length == 0) return currentScript ?? string.Empty;
            var existing = (currentScript ?? string.Empty).TrimEnd();
            return existing.Length == 0 ? cleaned : $"{existing}\n{cleaned}";
        }

        public static string BuildEventLine(
            WofQuestEventBuilderKind kind,
            string message,
            string questId,
            string flag)
        {
            return kind switch
            {
                WofQuestEventBuilderKind.Message => $"message {Fallback(message, "Good work, wizard.")}",
                WofQuestEventBuilderKind.SetFlag => $"setFlag {Fallback(flag, "town_01_quests=1")}",
                WofQuestEventBuilderKind.CompleteQuest => $"completeQuest {Fallback(questId, "town_01_quest")}",
                _ => $"startQuest {Fallback(questId, "town_01_quest")}"
            };
        }

        public static WofQuestNpcProgram SanitizeProgram(
            WofQuestNpcProgram value,
            WofQuestNpcEditorTarget? fallbackTarget = null,
            Func<string> idFactory = null,
            long now = 0)
        {
            if (value == null) return null;
            idFactory ??= MakeScriptPointId;
            var npcId = Truncate((value.npcId ?? string.Empty).Trim(), MaximumNpcIdLength);
            if (npcId.Length == 0) return null;
            var target = fallbackTarget;
            var points = value.scriptPoints ?? Array.Empty<WofQuestScriptPoint>();
            var pointCount = Math.Min(MaximumScriptPointCount, Math.Max(1, points.Length));
            var cleanPoints = new WofQuestScriptPoint[pointCount];
            for (var index = 0; index < pointCount; index++)
            {
                var point = index < points.Length ? points[index] : null;
                cleanPoints[index] = new WofQuestScriptPoint
                {
                    id = string.IsNullOrWhiteSpace(point?.id) ? idFactory() : Truncate(point.id.Trim(), MaximumNpcIdLength),
                    title = string.IsNullOrWhiteSpace(point?.title) ? $"Point {index + 1}" : Truncate(point.title.Trim(), MaximumTitleLength),
                    dialog = Truncate(point?.dialog ?? string.Empty, MaximumDialogLength),
                    eventScript = Truncate(point?.eventScript ?? string.Empty, MaximumEventScriptLength)
                };
            }
            return new WofQuestNpcProgram
            {
                npcId = npcId,
                townId = Fallback(Truncate((value.townId ?? string.Empty).Trim(), MaximumTownIdLength), target?.TownId ?? "unknown-town"),
                hutId = Truncate(Fallback((value.hutId ?? string.Empty).Trim(), target?.HutId ?? string.Empty), MaximumHutIdLength),
                displayName = Truncate(Fallback((value.displayName ?? string.Empty).Trim(), target?.DefaultName ?? "Quest NPC"), MaximumNameLength),
                role = Enum.IsDefined(typeof(WofQuestNpcRole), value.role) ? value.role : WofQuestNpcRole.Villager,
                theme = Truncate(Fallback((value.theme ?? string.Empty).Trim(), target?.Theme ?? "village"), MaximumThemeLength),
                hasPosition = value.hasPosition || target.HasValue,
                position = value.hasPosition ? value.position : target?.Position ?? Vector3.zero,
                greeting = Truncate(value.greeting ?? string.Empty, MaximumDialogLength),
                scriptPoints = cleanPoints,
                updatedAt = now > 0 ? now : value.updatedAt > 0 ? value.updatedAt : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public static bool IsDarrelIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = NormalizeLookup(value);
            return normalized == "darrel" || normalized == "darrell" ||
                   normalized.Contains("darrel", StringComparison.Ordinal) ||
                   normalized.Contains("darrell", StringComparison.Ordinal);
        }

        public static string RoleLabel(WofQuestNpcRole role)
        {
            return role switch
            {
                WofQuestNpcRole.QuestGiver => "QUEST-GIVER",
                WofQuestNpcRole.TownLeader => "TOWN-LEADER",
                _ => "VILLAGER"
            };
        }

        public static string GetDialogPreview(WofQuestScriptPoint point, string greeting)
        {
            return !string.IsNullOrEmpty(point?.dialog)
                ? point.dialog
                : !string.IsNullOrEmpty(greeting)
                    ? greeting
                    : "Need something, wizard?";
        }

        public static string FormatEventSummary(WofQuestScriptPoint point, int selectedIndex)
        {
            var count = CountEvents(point);
            var title = !string.IsNullOrEmpty(point?.title) ? point.title : $"Point {selectedIndex + 1}";
            return $"{title} - {count} event{(count == 1 ? string.Empty : "s")}";
        }

        internal static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        internal static string NormalizeLookup(string value)
        {
            var builder = new StringBuilder(value?.Length ?? 0);
            foreach (var character in value ?? string.Empty)
            {
                if (char.IsLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString();
        }

        private static WofQuestScriptPoint Point(string id, string title, string dialog, string eventScript)
        {
            return new WofQuestScriptPoint { id = id, title = title, dialog = dialog, eventScript = eventScript };
        }

        private static int FindPointIndex(WofQuestScriptPoint[] points, string id)
        {
            for (var index = 0; index < points.Length; index++)
            {
                if (points[index] != null && string.Equals(points[index].id, id, StringComparison.Ordinal)) return index;
            }
            return -1;
        }

        private static string Fallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string Truncate(string value, int maximumLength)
        {
            value ??= string.Empty;
            return value.Length <= maximumLength ? value : value.Substring(0, maximumLength);
        }
    }
}
