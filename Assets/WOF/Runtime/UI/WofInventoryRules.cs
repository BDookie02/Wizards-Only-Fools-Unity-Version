using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofInventoryItemEntry
    {
        public string itemId = string.Empty;
        public int quantity;
        public long acquiredAt;
    }

    public sealed class WofInventoryItemDefinition
    {
        public WofInventoryItemDefinition(string id, string name, string description, string category, int maxStack)
        {
            Id = id;
            Name = name;
            Description = description;
            Category = category;
            MaxStack = maxStack;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string Category { get; }
        public int MaxStack { get; }
    }

    public readonly struct WofInventoryDisplayEntry
    {
        public WofInventoryDisplayEntry(WofInventoryItemDefinition definition, int quantity)
        {
            Definition = definition;
            Quantity = quantity;
        }

        public WofInventoryItemDefinition Definition { get; }
        public int Quantity { get; }
    }

    public readonly struct WofInventoryQuestEntry
    {
        public WofInventoryQuestEntry(WofSpellQuestAssignment assignment, WofSpellQuestDefinition definition)
        {
            Assignment = assignment;
            Definition = definition;
        }

        public WofSpellQuestAssignment Assignment { get; }
        public WofSpellQuestDefinition Definition { get; }
    }

    public readonly struct WofInventoryQuestProgressRow
    {
        public WofInventoryQuestProgressRow(string label, bool done)
        {
            Label = label;
            Done = done;
        }

        public string Label { get; }
        public bool Done { get; }
    }

    public enum WofInventoryKeyboardAction
    {
        None,
        ToggleQuestJournal,
        MoveQuestNext,
        MoveQuestPrevious,
        OpenQuestJournal,
        CloseQuestJournal,
        CloseInventory
    }

    public enum WofInventoryShortcutAction
    {
        None,
        OpenInventory
    }

    public struct WofInventoryControllerHoldState
    {
        public float StartedAt;
        public bool HasStarted;
        public bool TapEligible;
        public bool IgnoreUntilRelease;
    }

    public struct WofInventoryControllerRepeatState
    {
        public bool WasHeld;
        public float NextRepeatAt;
    }

    public static class WofInventoryRules
    {
        public const int BackpackSlotCount = 27;
        public const int QuickSlotCount = 9;
        public const float ControllerInventoryHoldSeconds = 3f;
        public const float ControllerNavigationThreshold = 0.6f;
        public const float ControllerFirstRepeatSeconds = 0.26f;
        public const float ControllerRepeatSeconds = 0.17f;

        private static readonly WofInventoryItemDefinition[] ItemDefinitionsInternal =
        {
            new("darrel-leaves", "Leaves", "Field leaves for Darrel's garden draught.", "material", 9),
            new("darrel-berries", "Berries", "Bright field berries for Darrel's garden draught.", "material", 9),
            new("darrel-roots", "Roots", "Fresh roots dug up in the fields.", "material", 9),
            new("garden-draught", "Garden Draught", "A rough potion that folds the world toward the sacred garden.", "consumable", 3),
            new("healing-crystals", "Healing Crystals", "Crystals carried back from the spirit dragon.", "quest", 9)
        };

        public static IReadOnlyList<WofInventoryItemDefinition> ItemDefinitions => ItemDefinitionsInternal;

        public static void NormalizeProfile(WofSurvivalProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            profile.inventory ??= Array.Empty<WofInventoryItemEntry>();
            var sanitized = new Dictionary<string, WofInventoryItemEntry>(StringComparer.Ordinal);
            for (var index = 0; index < profile.inventory.Length; index++)
            {
                var entry = profile.inventory[index];
                var definition = FindItemDefinition(entry?.itemId);
                if (entry == null || definition == null)
                {
                    continue;
                }

                var quantity = Mathf.Clamp(entry.quantity, 0, definition.MaxStack);
                if (quantity <= 0)
                {
                    continue;
                }

                sanitized[definition.Id] = new WofInventoryItemEntry
                {
                    itemId = definition.Id,
                    quantity = quantity,
                    acquiredAt = Math.Max(0L, entry.acquiredAt)
                };
            }

            var normalized = new List<WofInventoryItemEntry>(ItemDefinitionsInternal.Length);
            for (var index = 0; index < ItemDefinitionsInternal.Length; index++)
            {
                if (sanitized.TryGetValue(ItemDefinitionsInternal[index].Id, out var entry))
                {
                    normalized.Add(entry);
                }
            }
            profile.inventory = normalized.ToArray();
        }

        public static WofInventoryDisplayEntry[] GetInventoryEntries(WofSurvivalProfile profile)
        {
            if (profile == null)
            {
                return Array.Empty<WofInventoryDisplayEntry>();
            }

            NormalizeProfile(profile);
            var entries = new List<WofInventoryDisplayEntry>(profile.inventory.Length);
            for (var definitionIndex = 0; definitionIndex < ItemDefinitionsInternal.Length; definitionIndex++)
            {
                var definition = ItemDefinitionsInternal[definitionIndex];
                for (var entryIndex = 0; entryIndex < profile.inventory.Length; entryIndex++)
                {
                    var entry = profile.inventory[entryIndex];
                    if (string.Equals(entry.itemId, definition.Id, StringComparison.Ordinal) && entry.quantity > 0)
                    {
                        entries.Add(new WofInventoryDisplayEntry(definition, entry.quantity));
                        break;
                    }
                }
            }
            return entries.ToArray();
        }

        public static int GetQuantity(WofSurvivalProfile profile, string itemId)
        {
            if (profile == null)
            {
                return 0;
            }
            NormalizeProfile(profile);
            for (var index = 0; index < profile.inventory.Length; index++)
            {
                if (string.Equals(profile.inventory[index].itemId, itemId, StringComparison.Ordinal))
                {
                    return profile.inventory[index].quantity;
                }
            }
            return 0;
        }

        public static bool AddQuantity(WofSurvivalProfile profile, string itemId, int quantity, long now)
        {
            var definition = FindItemDefinition(itemId);
            if (profile == null || definition == null || quantity <= 0)
            {
                return false;
            }

            NormalizeProfile(profile);
            var entries = new List<WofInventoryItemEntry>(profile.inventory);
            for (var index = 0; index < entries.Count; index++)
            {
                if (!string.Equals(entries[index].itemId, definition.Id, StringComparison.Ordinal))
                {
                    continue;
                }
                var next = Mathf.Clamp(entries[index].quantity + quantity, 0, definition.MaxStack);
                var changed = next != entries[index].quantity;
                entries[index].quantity = next;
                profile.inventory = entries.ToArray();
                NormalizeProfile(profile);
                return changed;
            }

            entries.Add(new WofInventoryItemEntry
            {
                itemId = definition.Id,
                quantity = Mathf.Clamp(quantity, 0, definition.MaxStack),
                acquiredAt = Math.Max(0L, now)
            });
            profile.inventory = entries.ToArray();
            NormalizeProfile(profile);
            return true;
        }

        public static bool RemoveQuantity(WofSurvivalProfile profile, string itemId, int quantity)
        {
            if (profile == null || quantity <= 0)
            {
                return false;
            }

            NormalizeProfile(profile);
            var entries = new List<WofInventoryItemEntry>(profile.inventory);
            for (var index = 0; index < entries.Count; index++)
            {
                if (!string.Equals(entries[index].itemId, itemId, StringComparison.Ordinal) ||
                    entries[index].quantity < quantity)
                {
                    continue;
                }
                entries[index].quantity -= quantity;
                if (entries[index].quantity <= 0)
                {
                    entries.RemoveAt(index);
                }
                profile.inventory = entries.ToArray();
                NormalizeProfile(profile);
                return true;
            }
            return false;
        }

        public static WofInventoryQuestEntry[] GetActiveQuestEntries(WofSurvivalProfile profile)
        {
            if (profile == null)
            {
                return Array.Empty<WofInventoryQuestEntry>();
            }

            WofSpellQuestRules.NormalizeProfile(profile);
            var entries = new List<WofInventoryQuestEntry>(profile.spellQuestAssignments.Length);
            for (var index = 0; index < profile.spellQuestAssignments.Length; index++)
            {
                var assignment = profile.spellQuestAssignments[index];
                var definition = ResolveQuestDefinition(assignment);
                if (assignment == null || definition == null ||
                    string.Equals(assignment.status, WofQuestDialogRules.QuestStatusCompleted, StringComparison.Ordinal) ||
                    Contains(profile.questUnlockedSpells, assignment.spell))
                {
                    continue;
                }
                entries.Add(new WofInventoryQuestEntry(assignment, definition));
            }
            return entries.ToArray();
        }

        public static WofSpellQuestDefinition ResolveQuestDefinition(WofSpellQuestAssignment assignment)
        {
            if (assignment == null)
            {
                return null;
            }

            WofSpellQuestDefinition questIdMatch = null;
            WofSpellQuestDefinition spellMatch = null;
            var questIdIndex = int.MaxValue;
            var spellIndex = int.MaxValue;
            for (var index = 0; index < WofSpellQuestRules.Definitions.Count; index++)
            {
                var definition = WofSpellQuestRules.Definitions[index];
                if (questIdMatch == null && string.Equals(definition.Id, assignment.questId, StringComparison.Ordinal))
                {
                    questIdMatch = definition;
                    questIdIndex = index;
                }
                if (spellMatch == null && string.Equals(definition.Spell, assignment.spell, StringComparison.Ordinal))
                {
                    spellMatch = definition;
                    spellIndex = index;
                }
            }

            if (questIdMatch == null)
            {
                return spellMatch;
            }
            if (spellMatch == null)
            {
                return questIdMatch;
            }
            return questIdIndex <= spellIndex ? questIdMatch : spellMatch;
        }

        public static int ClampQuestIndex(int index, int count)
        {
            return Mathf.Min(index, Mathf.Max(0, count - 1));
        }

        public static int GetNextQuestIndex(int index, int direction, int count)
        {
            return count > 0 ? ((index + Math.Sign(direction)) % count + count) % count : 0;
        }

        public static string GetQuestStatus(WofInventoryQuestEntry? entry, WofSurvivalProfile profile)
        {
            if (!entry.HasValue || profile == null)
            {
                return "No active quest selected.";
            }

            var value = entry.Value;
            var requiredReady = IsTruthy(WofSpellQuestRules.GetFlag(profile, value.Definition.RequiredFlag));
            var questState = WofSpellQuestRules.GetFlag(profile, $"quest:{value.Definition.Id}");
            if (string.Equals(questState, "completed", StringComparison.Ordinal))
            {
                return "Complete";
            }
            if (requiredReady || string.Equals(questState, "ready", StringComparison.Ordinal))
            {
                return "Ready to turn in";
            }
            if (string.Equals(questState, "started", StringComparison.Ordinal) ||
                string.Equals(value.Assignment.status, WofQuestDialogRules.QuestStatusAccepted, StringComparison.Ordinal))
            {
                return "In progress";
            }
            return "Discovered";
        }

        public static WofInventoryQuestProgressRow[] GetDarrelProgressRows(WofSurvivalProfile profile)
        {
            return new[]
            {
                new WofInventoryQuestProgressRow("Leaves", IsDarrelStepDone(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelLeavesFlag))),
                new WofInventoryQuestProgressRow("Berries", IsDarrelStepDone(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelBerriesFlag))),
                new WofInventoryQuestProgressRow("Roots", IsDarrelStepDone(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelRootsFlag))),
                new WofInventoryQuestProgressRow("Garden Draught", IsDarrelStepDone(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelPotionFlag)))
            };
        }

        public static WofInventoryKeyboardAction GetKeyboardAction(string code, bool questJournalOpen)
        {
            if (string.Equals(code, "KeyJ", StringComparison.Ordinal))
            {
                return WofInventoryKeyboardAction.ToggleQuestJournal;
            }
            if (questJournalOpen && string.Equals(code, "ArrowDown", StringComparison.Ordinal))
            {
                return WofInventoryKeyboardAction.MoveQuestNext;
            }
            if (questJournalOpen && string.Equals(code, "ArrowUp", StringComparison.Ordinal))
            {
                return WofInventoryKeyboardAction.MoveQuestPrevious;
            }
            if (!questJournalOpen && string.Equals(code, "Enter", StringComparison.Ordinal))
            {
                return WofInventoryKeyboardAction.OpenQuestJournal;
            }
            if (!string.Equals(code, "Escape", StringComparison.Ordinal) &&
                !string.Equals(code, "KeyI", StringComparison.Ordinal))
            {
                return WofInventoryKeyboardAction.None;
            }
            return questJournalOpen
                ? WofInventoryKeyboardAction.CloseQuestJournal
                : WofInventoryKeyboardAction.CloseInventory;
        }

        public static WofInventoryShortcutAction UpdateControllerInventoryHold(
            ref WofInventoryControllerHoldState state,
            float now,
            bool inventoryHeld,
            bool standingStill,
            bool canUseShortcut,
            float holdSeconds = ControllerInventoryHoldSeconds)
        {
            if (state.IgnoreUntilRelease)
            {
                state.HasStarted = false;
                state.TapEligible = false;
                if (!inventoryHeld)
                {
                    state.IgnoreUntilRelease = false;
                }
                return WofInventoryShortcutAction.None;
            }

            if (inventoryHeld)
            {
                if (standingStill && canUseShortcut)
                {
                    if (!state.HasStarted)
                    {
                        state.StartedAt = now;
                        state.HasStarted = true;
                        state.TapEligible = true;
                    }
                    else if (now - state.StartedAt >= holdSeconds)
                    {
                        state.TapEligible = false;
                    }
                }
                else
                {
                    state.TapEligible = false;
                }
                return WofInventoryShortcutAction.None;
            }

            if (!state.HasStarted)
            {
                return WofInventoryShortcutAction.None;
            }

            var duration = now - state.StartedAt;
            var shouldOpen = state.TapEligible && duration < holdSeconds && standingStill && canUseShortcut;
            state.HasStarted = false;
            state.TapEligible = false;
            return shouldOpen ? WofInventoryShortcutAction.OpenInventory : WofInventoryShortcutAction.None;
        }

        public static void MarkControllerInventoryIgnoreUntilRelease(ref WofInventoryControllerHoldState state)
        {
            state.IgnoreUntilRelease = true;
            state.HasStarted = false;
            state.TapEligible = false;
        }

        public static bool ConsumeControllerRepeat(
            ref WofInventoryControllerRepeatState state,
            bool held,
            float now,
            float firstDelay = ControllerFirstRepeatSeconds,
            float repeatDelay = ControllerRepeatSeconds)
        {
            var wasHeld = state.WasHeld;
            state.WasHeld = held;
            if (!held)
            {
                state.NextRepeatAt = 0f;
                return false;
            }
            if (!wasHeld)
            {
                state.NextRepeatAt = now + firstDelay;
                return true;
            }
            if (now < state.NextRepeatAt)
            {
                return false;
            }
            state.NextRepeatAt = now + repeatDelay;
            return true;
        }

        public static bool IsStandingStillForControllerInventory(
            bool controllerGameplayActive,
            bool moving,
            bool sprinting,
            bool sliding,
            bool crouching,
            Vector2 movementAxis)
        {
            return controllerGameplayActive && !moving && !sprinting && !sliding && !crouching &&
                   movementAxis.x == 0f && movementAxis.y == 0f;
        }

        public static WofInventoryItemDefinition FindItemDefinition(string itemId)
        {
            for (var index = 0; index < ItemDefinitionsInternal.Length; index++)
            {
                if (string.Equals(ItemDefinitionsInternal[index].Id, itemId, StringComparison.Ordinal))
                {
                    return ItemDefinitionsInternal[index];
                }
            }
            return null;
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "true", StringComparison.Ordinal) ||
                   string.Equals(value, "completed", StringComparison.Ordinal) ||
                   string.Equals(value, "ready", StringComparison.Ordinal) ||
                   string.Equals(value, "1", StringComparison.Ordinal);
        }

        private static bool IsDarrelStepDone(string value)
        {
            return string.Equals(value, "gathered", StringComparison.Ordinal) ||
                   string.Equals(value, "brewed", StringComparison.Ordinal) ||
                   string.Equals(value, "drunk", StringComparison.Ordinal) ||
                   IsTruthy(value);
        }

        private static bool Contains(string[] values, string value)
        {
            if (values == null)
            {
                return false;
            }
            for (var index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
