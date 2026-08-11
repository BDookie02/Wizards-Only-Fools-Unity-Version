using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace WOF
{
    public enum WofQuestScriptTeleport
    {
        None = 0,
        LilyCoil = 1,
        DarrelGrove = 2
    }

    public readonly struct WofQuestScriptResult
    {
        public WofQuestScriptResult(string[] messages, bool profileChanged, WofQuestScriptTeleport teleport)
        {
            Messages = messages ?? Array.Empty<string>();
            ProfileChanged = profileChanged;
            Teleport = teleport;
        }

        public string[] Messages { get; }
        public bool ProfileChanged { get; }
        public WofQuestScriptTeleport Teleport { get; }
    }

    public static class WofQuestScriptRuntime
    {
        private static readonly Regex EventPattern = new(
            @"^([a-zA-Z][a-zA-Z0-9_-]*)(?:\s*[:=]\s*|\s+)?(.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static WofQuestScriptResult Execute(
            WofSurvivalProfile profile,
            WofQuestNpcProgram program,
            WofQuestScriptPoint point,
            double randomUnit,
            long now)
        {
            if (profile == null || program == null || point == null)
            {
                return new WofQuestScriptResult(new[] { "Quest scriptpoint not found" }, false, WofQuestScriptTeleport.None);
            }
            WofSpellQuestRules.NormalizeProfile(profile);
            WofInventoryRules.NormalizeProfile(profile);
            var events = ParseEvents(point.eventScript);
            if (events.Count == 0)
            {
                return new WofQuestScriptResult(
                    new[] { $"{program.displayName}: no events on {point.title}" },
                    false,
                    WofQuestScriptTeleport.None);
            }

            var messages = new List<string>();
            var changed = false;
            var teleport = WofQuestScriptTeleport.None;
            for (var index = 0; index < events.Count; index++)
            {
                var questEvent = events[index];
                var command = questEvent.Command;
                var value = questEvent.Value;
                if (command is "unlockspell" or "spellunlock")
                {
                    var definition = FindSpell(value);
                    if (definition == null)
                    {
                        messages.Add($"Unknown spell: {(string.IsNullOrWhiteSpace(value) ? "blank" : value)}");
                        continue;
                    }
                    if (Contains(profile.questUnlockedSpells, definition.Spell))
                    {
                        messages.Add($"{definition.Spell} already unlocked");
                    }
                    else
                    {
                        WofSpellQuestRules.AddUnlockedSpell(profile, definition.Spell);
                        messages.Add($"Unlocked {definition.Spell}");
                        changed = true;
                    }
                    changed |= CompleteAssignmentsForSpell(profile, definition.Spell, now);
                    if (string.Equals(definition.Spell, WofSpellQuestRules.DarrelRewardSpell, StringComparison.Ordinal))
                    {
                        WofInventoryRules.AddQuantity(profile, "healing-crystals", 1, now);
                        changed = true;
                    }
                    continue;
                }

                if (command is "unlockrandomlockedspell" or "randomlockedspell" or "gambleunlock" or "gamblespell")
                {
                    var locked = new List<WofSpellQuestDefinition>();
                    for (var definitionIndex = 0; definitionIndex < WofSpellQuestRules.Definitions.Count; definitionIndex++)
                    {
                        var definition = WofSpellQuestRules.Definitions[definitionIndex];
                        if (!Contains(profile.questUnlockedSpells, definition.Spell)) locked.Add(definition);
                    }
                    if (locked.Count == 0)
                    {
                        messages.Add("No locked spells remain");
                        continue;
                    }
                    var normalized = double.IsFinite(randomUnit) ? Math.Max(0d, Math.Min(0.999999999999d, randomUnit)) : 0d;
                    var selected = locked[Math.Min(locked.Count - 1, (int)Math.Floor(normalized * locked.Count))];
                    WofSpellQuestRules.AddUnlockedSpell(profile, selected.Spell);
                    CompleteAssignmentsForSpell(profile, selected.Spell, now);
                    messages.Add($"Gamble unlocked {selected.Spell}");
                    changed = true;
                    continue;
                }

                if (command is "setspellquestready" or "spellquestready" or "readyassignedspellquest")
                {
                    var targetNpcId = string.IsNullOrWhiteSpace(value) ? program.npcId : value;
                    var assignment = WofSpellQuestRules.FindAssignment(profile, targetNpcId);
                    if (assignment == null)
                    {
                        messages.Add($"No spell quest assigned to {targetNpcId}");
                        continue;
                    }
                    var definition = FindSpell(assignment.spell);
                    if (definition == null)
                    {
                        messages.Add($"Missing quest definition for {assignment.spell}");
                        continue;
                    }
                    WofSpellQuestRules.SetFlag(profile, definition.RequiredFlag, "true");
                    WofSpellQuestRules.SetFlag(profile, $"quest:{definition.Id}", "ready");
                    messages.Add($"{definition.DisplayName} quest ready");
                    changed = true;
                    continue;
                }

                if (command is "completeassignedspellquest" or "completespellquest")
                {
                    var targetNpcId = string.IsNullOrWhiteSpace(value) ? program.npcId : value;
                    var assignment = WofSpellQuestRules.FindAssignment(profile, targetNpcId);
                    if (assignment == null)
                    {
                        messages.Add($"No spell quest assigned to {targetNpcId}");
                        continue;
                    }
                    var definition = FindSpell(assignment.spell);
                    if (definition == null)
                    {
                        messages.Add($"Missing quest definition for {assignment.spell}");
                        continue;
                    }
                    WofSpellQuestRules.SetFlag(profile, definition.RequiredFlag, "true");
                    WofSpellQuestRules.SetFlag(profile, $"quest:{definition.Id}", "completed");
                    WofSpellQuestRules.AddUnlockedSpell(profile, assignment.spell);
                    assignment.status = WofQuestDialogRules.QuestStatusCompleted;
                    assignment.completedAt = now;
                    if (string.Equals(assignment.spell, WofSpellQuestRules.DarrelRewardSpell, StringComparison.Ordinal))
                    {
                        WofInventoryRules.AddQuantity(profile, "healing-crystals", 1, now);
                    }
                    messages.Add($"{assignment.displayName}: {definition.ReadyLine}");
                    changed = true;
                    continue;
                }

                if (command is "teleportquestrealm" or "teleportquestworld" or "teleportdarrelquest" or "darrelquest")
                {
                    var destination = ResolveTeleport(value);
                    if (destination == WofQuestScriptTeleport.None)
                    {
                        messages.Add($"Unknown quest realm: {(string.IsNullOrWhiteSpace(value) ? "blank" : value)}");
                        continue;
                    }
                    teleport = destination;
                    if (destination == WofQuestScriptTeleport.DarrelGrove)
                    {
                        changed |= EnsureDarrelAssignment(profile, program, now, messages);
                    }
                    messages.Add(destination == WofQuestScriptTeleport.DarrelGrove
                        ? "Transported to Darrel's Grove"
                        : "Transported to Lily Coil");
                    continue;
                }

                if (command == "startquest")
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        messages.Add("startQuest needs a quest id");
                        continue;
                    }
                    WofSpellQuestRules.SetFlag(profile, $"quest:{value}", "started");
                    messages.Add($"Started quest {value}");
                    changed = true;
                    continue;
                }

                if (command == "completequest")
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        messages.Add("completeQuest needs a quest id");
                        continue;
                    }
                    WofSpellQuestRules.SetFlag(profile, $"quest:{value}", "completed");
                    messages.Add($"Completed quest {value}");
                    changed = true;
                    continue;
                }

                if (command == "setflag")
                {
                    ParseFlag(value, out var key, out var flagValue);
                    if (key.Length == 0)
                    {
                        messages.Add("setFlag needs key=value");
                        continue;
                    }
                    WofSpellQuestRules.SetFlag(profile, key, flagValue);
                    messages.Add($"Set {key}");
                    changed = true;
                    continue;
                }

                if (command is "message" or "say")
                {
                    if (!string.IsNullOrWhiteSpace(value)) messages.Add(value);
                    continue;
                }

                messages.Add($"Unknown event: {command}");
            }
            return new WofQuestScriptResult(messages.ToArray(), changed, teleport);
        }

        internal static List<(string Command, string Value)> ParseEvents(string script)
        {
            var events = new List<(string Command, string Value)>();
            var lines = (script ?? string.Empty).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            for (var index = 0; index < lines.Length; index++)
            {
                var trimmed = lines[index].Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal) ||
                    trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                var match = EventPattern.Match(trimmed);
                if (!match.Success) continue;
                events.Add((NormalizeCommand(match.Groups[1].Value), match.Groups[2].Value.Trim()));
            }
            return events;
        }

        private static string NormalizeCommand(string value)
        {
            var builder = new StringBuilder(value?.Length ?? 0);
            foreach (var character in value ?? string.Empty)
            {
                if (char.IsLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString();
        }

        private static WofSpellQuestDefinition FindSpell(string value)
        {
            var normalized = WofQuestDevRules.NormalizeLookup(value);
            for (var index = 0; index < WofSpellQuestRules.Definitions.Count; index++)
            {
                var definition = WofSpellQuestRules.Definitions[index];
                if (WofQuestDevRules.NormalizeLookup(definition.Spell) == normalized ||
                    WofQuestDevRules.NormalizeLookup(definition.DisplayName) == normalized ||
                    WofQuestDevRules.NormalizeLookup(definition.Id) == normalized)
                {
                    return definition;
                }
            }
            return null;
        }

        private static WofQuestScriptTeleport ResolveTeleport(string value)
        {
            var normalized = WofQuestDevRules.NormalizeLookup(value);
            if (normalized.Length == 0 || normalized is "lilycoil" or "coil" or "springcoil" or "purplecoil")
            {
                return WofQuestScriptTeleport.LilyCoil;
            }
            return normalized is "darrel" or "darrelgrove" or "grove"
                ? WofQuestScriptTeleport.DarrelGrove
                : WofQuestScriptTeleport.None;
        }

        private static void ParseFlag(string value, out string key, out string flagValue)
        {
            value ??= string.Empty;
            var equalsIndex = value.IndexOf('=');
            if (equalsIndex >= 0)
            {
                key = value.Substring(0, equalsIndex).Trim();
                flagValue = value.Substring(equalsIndex + 1).Trim();
            }
            else
            {
                var parts = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                key = parts.Length == 0 ? string.Empty : parts[0].Trim();
                flagValue = parts.Length <= 1 ? string.Empty : string.Join(" ", parts, 1, parts.Length - 1).Trim();
            }
            if (flagValue.Length == 0) flagValue = "true";
        }

        private static bool EnsureDarrelAssignment(
            WofSurvivalProfile profile,
            WofQuestNpcProgram program,
            long now,
            List<string> messages)
        {
            var existing = WofSpellQuestRules.FindAssignment(profile, program.npcId);
            if (existing != null && string.Equals(existing.spell, WofSpellQuestRules.DarrelRewardSpell, StringComparison.Ordinal))
            {
                WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelAcceptedFlag, "true");
                return true;
            }
            var definition = FindSpell(WofSpellQuestRules.DarrelRewardSpell);
            var assignment = new WofSpellQuestAssignment
            {
                npcId = program.npcId,
                townId = program.townId,
                displayName = program.displayName,
                questId = definition?.Id ?? $"spellquest:{WofSpellQuestRules.DarrelRewardSpell}",
                spell = WofSpellQuestRules.DarrelRewardSpell,
                status = WofQuestDialogRules.QuestStatusAccepted,
                assignedAt = now
            };
            var assignments = new WofSpellQuestAssignment[profile.spellQuestAssignments.Length + 1];
            Array.Copy(profile.spellQuestAssignments, assignments, profile.spellQuestAssignments.Length);
            assignments[^1] = assignment;
            profile.spellQuestAssignments = assignments;
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelAcceptedFlag, "true");
            WofSpellQuestRules.SetFlag(profile, $"quest:{assignment.questId}", "started");
            messages.Add($"{program.displayName}: grove trial opened - {definition?.Title ?? "The Sacred Garden Draught"}.");
            return true;
        }

        private static bool CompleteAssignmentsForSpell(WofSurvivalProfile profile, string spell, long now)
        {
            var changed = false;
            for (var index = 0; index < profile.spellQuestAssignments.Length; index++)
            {
                var assignment = profile.spellQuestAssignments[index];
                if (assignment == null || !string.Equals(assignment.spell, spell, StringComparison.Ordinal)) continue;
                if (!string.Equals(assignment.status, WofQuestDialogRules.QuestStatusCompleted, StringComparison.Ordinal) ||
                    assignment.completedAt <= 0)
                {
                    assignment.status = WofQuestDialogRules.QuestStatusCompleted;
                    assignment.completedAt = now;
                    changed = true;
                }
            }
            return changed;
        }

        private static bool Contains(string[] values, string value)
        {
            if (values == null) return false;
            for (var index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index], value, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
