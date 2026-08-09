using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    public enum WofQuestNavigationTone
    {
        Default,
        Field,
        Brew,
        Realm,
        TurnIn
    }

    public sealed class WofQuestNavigationTarget
    {
        public WofQuestNavigationTarget(
            string id,
            string questId,
            string npcId,
            string label,
            string detail,
            WofQuestNavigationTone tone,
            Vector3 position)
        {
            Id = id;
            QuestId = questId;
            NpcId = npcId;
            Label = label;
            Detail = detail;
            Tone = tone;
            Position = position;
        }

        public string Id { get; }
        public string QuestId { get; }
        public string NpcId { get; }
        public string Label { get; }
        public string Detail { get; }
        public WofQuestNavigationTone Tone { get; }
        public Vector3 Position { get; }
    }

    public static class WofDarrelQuestNavigationRules
    {
        public static readonly Vector3 FieldSearchPosition = new(0f, 4f, 360f);
        public static readonly Vector3 BrewingStationHintPosition = new(-42f, 2.6f, -26f);
        public static readonly Vector3 DarrelTurnInPosition = new(-64f, 7.45f, -49.25f);

        public static WofQuestNavigationTarget Resolve(WofSurvivalProfile profile)
        {
            if (profile == null)
            {
                return null;
            }
            WofSpellQuestRules.NormalizeProfile(profile);
            var assignment = FindDarrelAssignment(profile);
            var quest = FindDarrelDefinition();
            if (assignment == null || quest == null ||
                string.Equals(assignment.status, WofQuestDialogRules.QuestStatusCompleted, StringComparison.Ordinal) ||
                Contains(profile.questUnlockedSpells, assignment.spell))
            {
                return null;
            }

            var idPrefix = $"{assignment.npcId}:{quest.Id}";
            if (IsTruthy(WofSpellQuestRules.GetFlag(profile, quest.RequiredFlag)) ||
                string.Equals(WofSpellQuestRules.GetFlag(profile, $"quest:{quest.Id}"), "completed", StringComparison.Ordinal))
            {
                return new WofQuestNavigationTarget(
                    $"{idPrefix}:turn-in",
                    quest.Id,
                    assignment.npcId,
                    "Return to Darrel",
                    "Bring the Healing Crystals back to Darrel.",
                    WofQuestNavigationTone.TurnIn,
                    DarrelTurnInPosition);
            }

            var potionState = WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelPotionFlag);
            if (string.Equals(potionState, "drunk", StringComparison.Ordinal) ||
                string.Equals(WofSpellQuestRules.GetFlag(profile, WofDarrelProgressionRules.GroveQuestFlag), "started", StringComparison.Ordinal))
            {
                return new WofQuestNavigationTarget(
                    $"{idPrefix}:spirit-dragon",
                    quest.Id,
                    assignment.npcId,
                    "Spirit Dragon",
                    "Find the sleeping dragon inside the garden house.",
                    WofQuestNavigationTone.Realm,
                    WofDarrelGroveLayout.DragonQuestMarkerWorldPosition);
            }

            var missingIngredients = new List<string>(3);
            AddMissing(profile, WofSpellQuestRules.DarrelLeavesFlag, "leaves", missingIngredients);
            AddMissing(profile, WofSpellQuestRules.DarrelBerriesFlag, "berries", missingIngredients);
            AddMissing(profile, WofSpellQuestRules.DarrelRootsFlag, "roots", missingIngredients);
            if (missingIngredients.Count > 0)
            {
                return new WofQuestNavigationTarget(
                    $"{idPrefix}:fields",
                    quest.Id,
                    assignment.npcId,
                    "The Fields",
                    $"Gather {string.Join(", ", missingIngredients)} for Darrel's garden draught.",
                    WofQuestNavigationTone.Field,
                    FieldSearchPosition);
            }

            if (!string.Equals(potionState, "brewed", StringComparison.Ordinal))
            {
                return new WofQuestNavigationTarget(
                    $"{idPrefix}:brew",
                    quest.Id,
                    assignment.npcId,
                    "Brew Garden Draught",
                    "Use a brewing station to make Darrel's garden draught.",
                    WofQuestNavigationTone.Brew,
                    BrewingStationHintPosition);
            }

            return new WofQuestNavigationTarget(
                $"{idPrefix}:drink",
                quest.Id,
                assignment.npcId,
                "Drink Garden Draught",
                "Drink the garden draught from your inventory to enter the sacred garden.",
                WofQuestNavigationTone.Realm,
                DarrelTurnInPosition);
        }

        public static Color ResolveColor(WofQuestNavigationTone tone)
        {
            return tone switch
            {
                WofQuestNavigationTone.Field => Parse("#a7f3d0"),
                WofQuestNavigationTone.Brew => Parse("#fcd34d"),
                WofQuestNavigationTone.Realm => Parse("#c084fc"),
                WofQuestNavigationTone.TurnIn => Parse("#f9a8d4"),
                _ => Parse("#67e8f9")
            };
        }

        private static void AddMissing(
            WofSurvivalProfile profile,
            string flag,
            string ingredient,
            List<string> missing)
        {
            var value = WofSpellQuestRules.GetFlag(profile, flag);
            if (!string.Equals(value, "gathered", StringComparison.Ordinal) &&
                !string.Equals(value, "brewed", StringComparison.Ordinal) &&
                !string.Equals(value, "drunk", StringComparison.Ordinal) &&
                !IsTruthy(value))
            {
                missing.Add(ingredient);
            }
        }

        private static WofSpellQuestAssignment FindDarrelAssignment(WofSurvivalProfile profile)
        {
            for (var index = 0; index < profile.spellQuestAssignments.Length; index++)
            {
                var assignment = profile.spellQuestAssignments[index];
                if (assignment != null &&
                    string.Equals(assignment.spell, WofSpellQuestRules.DarrelRewardSpell, StringComparison.Ordinal))
                {
                    return assignment;
                }
            }
            return null;
        }

        private static WofSpellQuestDefinition FindDarrelDefinition()
        {
            for (var index = 0; index < WofSpellQuestRules.Definitions.Count; index++)
            {
                var definition = WofSpellQuestRules.Definitions[index];
                if (string.Equals(definition.Spell, WofSpellQuestRules.DarrelRewardSpell, StringComparison.Ordinal))
                {
                    return definition;
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

        private static Color Parse(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.white;
        }
    }
}
