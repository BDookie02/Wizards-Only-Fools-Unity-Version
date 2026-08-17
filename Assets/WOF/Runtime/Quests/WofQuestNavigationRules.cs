using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    public static class WofQuestNavigationRules
    {
        private const float QuestNpcBeaconHeight = 5.5f;

        private readonly struct OrderedAssignment
        {
            public OrderedAssignment(WofSpellQuestAssignment assignment, int originalIndex)
            {
                Assignment = assignment;
                OriginalIndex = originalIndex;
            }

            public WofSpellQuestAssignment Assignment { get; }
            public int OriginalIndex { get; }
        }

        public static IReadOnlyList<WofQuestNavigationTarget> ResolveAll(
            WofSurvivalProfile profile,
            IReadOnlyList<WofQuestNpcProgram> programs = null)
        {
            if (profile == null)
            {
                return Array.Empty<WofQuestNavigationTarget>();
            }

            WofSpellQuestRules.NormalizeProfile(profile);
            var ordered = new List<OrderedAssignment>(profile.spellQuestAssignments.Length);
            for (var index = 0; index < profile.spellQuestAssignments.Length; index++)
            {
                var assignment = profile.spellQuestAssignments[index];
                if (assignment == null ||
                    string.Equals(assignment.status, WofQuestDialogRules.QuestStatusCompleted, StringComparison.Ordinal) ||
                    Contains(profile.questUnlockedSpells, assignment.spell))
                {
                    continue;
                }

                ordered.Add(new OrderedAssignment(assignment, index));
            }

            ordered.Sort((left, right) =>
            {
                var byAssignedAt = left.Assignment.assignedAt.CompareTo(right.Assignment.assignedAt);
                return byAssignedAt != 0 ? byAssignedAt : left.OriginalIndex.CompareTo(right.OriginalIndex);
            });

            var targets = new List<WofQuestNavigationTarget>(ordered.Count);
            for (var index = 0; index < ordered.Count; index++)
            {
                var assignment = ordered[index].Assignment;
                var definition = FindDefinition(assignment.questId) ?? FindDefinition(assignment.spell);
                if (definition == null)
                {
                    continue;
                }

                var program = FindProgram(programs, assignment.npcId);
                if (IsAcceptedDarrelAssignment(profile, assignment))
                {
                    var darrelTarget = WofDarrelQuestNavigationRules.Resolve(profile);
                    if (darrelTarget != null)
                    {
                        targets.Add(ApplyQuestNpcPosition(darrelTarget, program));
                    }
                    continue;
                }

                if (program == null || !program.hasPosition)
                {
                    continue;
                }

                var ready = IsReady(profile, definition);
                var position = program.position + Vector3.up * QuestNpcBeaconHeight;
                targets.Add(new WofQuestNavigationTarget(
                    $"{assignment.npcId}:{definition.Id}:{(ready ? "turn-in" : "quest-giver")}",
                    definition.Id,
                    assignment.npcId,
                    ready ? $"Turn in {definition.DisplayName}" : definition.Title,
                    ready ? $"Return to {assignment.displayName}." : definition.Objective,
                    ready ? WofQuestNavigationTone.TurnIn : WofQuestNavigationTone.Npc,
                    position));
            }

            return targets;
        }

        private static WofQuestNavigationTarget ApplyQuestNpcPosition(
            WofQuestNavigationTarget target,
            WofQuestNpcProgram program)
        {
            if (program == null || !program.hasPosition ||
                (!target.Id.EndsWith(":turn-in", StringComparison.Ordinal) &&
                 !target.Id.EndsWith(":drink", StringComparison.Ordinal)))
            {
                return target;
            }

            return new WofQuestNavigationTarget(
                target.Id,
                target.QuestId,
                target.NpcId,
                target.Label,
                target.Detail,
                target.Tone,
                program.position + Vector3.up * QuestNpcBeaconHeight);
        }

        private static WofQuestNpcProgram FindProgram(
            IReadOnlyList<WofQuestNpcProgram> programs,
            string npcId)
        {
            if (programs == null || string.IsNullOrWhiteSpace(npcId))
            {
                return null;
            }

            for (var index = 0; index < programs.Count; index++)
            {
                var program = programs[index];
                if (program != null && string.Equals(program.npcId, npcId, StringComparison.Ordinal))
                {
                    return program;
                }
            }

            return null;
        }

        private static WofSpellQuestDefinition FindDefinition(string spellOrQuestId)
        {
            for (var index = 0; index < WofSpellQuestRules.Definitions.Count; index++)
            {
                var definition = WofSpellQuestRules.Definitions[index];
                if (string.Equals(definition.Spell, spellOrQuestId, StringComparison.Ordinal) ||
                    string.Equals(definition.Id, spellOrQuestId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private static bool IsReady(WofSurvivalProfile profile, WofSpellQuestDefinition definition)
        {
            return IsTruthy(WofSpellQuestRules.GetFlag(profile, definition.RequiredFlag)) ||
                   string.Equals(
                       WofSpellQuestRules.GetFlag(profile, $"quest:{definition.Id}"),
                       "completed",
                       StringComparison.Ordinal);
        }

        private static bool IsAcceptedDarrelAssignment(
            WofSurvivalProfile profile,
            WofSpellQuestAssignment assignment)
        {
            return string.Equals(assignment.spell, WofSpellQuestRules.DarrelRewardSpell, StringComparison.Ordinal) &&
                   (LooksLikeDarrel(assignment.npcId) ||
                    LooksLikeDarrel(assignment.displayName) ||
                    IsTruthy(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelAcceptedFlag)));
        }

        private static bool LooksLikeDarrel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            Span<char> normalized = stackalloc char[value.Length];
            var length = 0;
            for (var index = 0; index < value.Length; index++)
            {
                var character = char.ToLowerInvariant(value[index]);
                if (character >= 'a' && character <= 'z' || character >= '0' && character <= '9')
                {
                    normalized[length++] = character;
                }
            }

            var clean = normalized.Slice(0, length);
            return clean.SequenceEqual("darrel".AsSpan()) ||
                   clean.SequenceEqual("darrell".AsSpan()) ||
                   clean.IndexOf("darrel".AsSpan()) >= 0 ||
                   clean.IndexOf("darrell".AsSpan()) >= 0;
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "completed", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "ready", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string[] values, string expected)
        {
            if (values == null)
            {
                return false;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index], expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
