using System;
using System.Collections.Generic;

namespace WOF
{
    [Serializable]
    public sealed class WofSpellQuestAssignment
    {
        public string npcId = string.Empty;
        public string townId = string.Empty;
        public string displayName = string.Empty;
        public string questId = string.Empty;
        public string spell = string.Empty;
        public string status = WofQuestDialogRules.QuestStatusAccepted;
        public long assignedAt;
        public long completedAt;
    }

    [Serializable]
    public sealed class WofQuestFlagEntry
    {
        public string key = string.Empty;
        public string value = string.Empty;
    }

    public sealed class WofSpellQuestDefinition
    {
        public WofSpellQuestDefinition(
            string spell,
            string displayName,
            string title,
            string objective,
            string readyLine,
            string incompleteLine)
        {
            Spell = spell;
            DisplayName = displayName;
            Id = $"spellquest:{spell}";
            Title = title;
            Objective = objective;
            ReadyLine = readyLine;
            IncompleteLine = incompleteLine;
            RequiredFlag = $"spellquest:{spell}:ready";
        }

        public string Id { get; }
        public string Spell { get; }
        public string DisplayName { get; }
        public string Title { get; }
        public string Objective { get; }
        public string ReadyLine { get; }
        public string IncompleteLine { get; }
        public string RequiredFlag { get; }
    }

    public readonly struct WofSpellQuestInteractionResult
    {
        public WofSpellQuestInteractionResult(
            string[] messages,
            WofSpellQuestAssignment assignment,
            bool profileChanged)
        {
            Messages = messages ?? Array.Empty<string>();
            Assignment = assignment;
            ProfileChanged = profileChanged;
        }

        public string[] Messages { get; }
        public WofSpellQuestAssignment Assignment { get; }
        public bool ProfileChanged { get; }
    }

    public static class WofSpellQuestRules
    {
        public const string DefaultUnlockedSpell = "blink";
        public const string DarrelRewardSpell = "healingcrystals";
        public const string DarrelAcceptedFlag = "darrel:healingcrystals:accepted";
        public const string DarrelLeavesFlag = "darrel:ingredient:leaves";
        public const string DarrelBerriesFlag = "darrel:ingredient:berries";
        public const string DarrelRootsFlag = "darrel:ingredient:roots";
        public const string DarrelPotionFlag = "darrel:garden-draught";

        private static readonly WofSpellQuestDefinition[] DefinitionsInternal =
        {
            Define("fireball", "Fireball", "Coal for the Cold Hearth", "Bring proof that you lit a cold town hearth without burning the rafters.", "The hearth is breathing again. Fireball is yours.", "The hearth still needs a spark. Try again after the town marks the hearth lit."),
            Define("iceshard", "Biden Blast", "Frost in the Well", "Freeze the old well bucket so the water comes up clean.", "The well is clear and cold. Biden Blast is unlocked.", "The well water is still murky. Come back once the frost marker is set."),
            Define("arcanebeam", "Hands", "Hands of the Clocktower", "Realign the clocktower hands before nightfall.", "The tower is keeping time again. Hands is unlocked.", "The clocktower is still crooked. Give it another try."),
            Define("healspell", "Heal", "Bandages for the Road", "Help a wounded traveler recover near the village path.", "The traveler can stand again. Heal is unlocked.", "The traveler still needs help. Return when the recovery marker is set."),
            Define("icespell", "Plasma Snowball", "Snow in the Furnace", "Cool the furnace core without cracking the stone.", "The furnace is quiet. Plasma Snowball is unlocked.", "The furnace is still roaring. Try again once it is cooled."),
            Define("ringsofpower", "Rings of Power", "Three Lost Rings", "Recover the town rings from the edge of the graveyard.", "The rings are back on the shrine. Rings of Power is unlocked.", "The shrine still has empty grooves. Keep searching."),
            Define("lightning", "Chidori", "Storm Rod", "Charge the copper storm rod on the chapel roof.", "The rod hums with stormlight. Chidori is unlocked.", "The storm rod is still dull. It needs a charge first."),
            Define("smokebomb", "Smoke Bomb", "Vanishing Flour", "Recover the baker's black flour from the cellar.", "The baker grins through the dust. Smoke Bomb is unlocked.", "No black flour yet. The cellar job is still open."),
            Define("portal", "Portal", "Two Doorways", "Link the broken blue door to its twin outside town.", "Both doors blink at once. Portal is unlocked.", "Only one doorway is awake. Finish the link and return."),
            Define("grab", "Grab", "Bell Rope Rescue", "Pull the jammed chapel bell rope free.", "The bell rings clean. Grab is unlocked.", "The bell rope is still stuck. Try again when it loosens."),
            Define("tornado", "Tornado", "Millwind", "Restart the windmill with a controlled spiral.", "The mill turns again. Tornado is unlocked.", "The windmill is still still. The spiral was not enough yet."),
            Define("meteorshower", "Meteor Shower", "Sky Stones", "Collect three warm sky stones from the fields.", "The stones glow in a circle. Meteor Shower is unlocked.", "The circle is missing sky stones. Keep looking."),
            Define("flamethrower", "Fire Breath", "Ash Path", "Clear the bramble path without scorching the marker stones.", "The ash path is open. Fire Breath is unlocked.", "The brambles still block the path. Try another careful burn."),
            Define("discshield", "Disc Shield", "Slate Disc", "Repair the cracked slate disc above the schoolhouse door.", "The slate disc holds firm. Disc Shield is unlocked.", "The slate disc is still cracked. It needs more work."),
            Define("orbshield", "Orb Shield", "Glass Orchard", "Protect the glass fruit during the next village ambush.", "Not a single fruit broke. Orb Shield is unlocked.", "The orchard is not secure yet. Come back after it survives."),
            Define("kunai", "Kunai", "Needle Throw", "Win the old target board challenge behind the smithy.", "Every target is pinned. Kunai is unlocked.", "The target board is still laughing at you. Try again."),
            Define("healingcrystals", "Healing Crystals", "The Sacred Garden Draught", "Gather 1 leaves, 1 berries, and 1 roots from the fields, brew the garden draught at a brewing station, then drink it to reach the sacred garden and bring back Healing Crystals.", "The spirit dragon sends you back with lemonade breath, lunch packed for the road, and Healing Crystals. Healing Crystals is unlocked.", "The garden draught is not finished yet: gather leaves, berries, and roots from the fields, brew it, and drink it when you are ready."),
            Define("magicarmor", "Magic Armor", "Knight's Dent", "Straighten the dented armor on the town statue.", "The statue stands proud. Magic Armor is unlocked.", "The statue is still bent. Give it another try."),
            Define("jumpboost", "Jump Boost", "Roofline Errand", "Fetch the weather vane from the tallest roof.", "The weather vane spins again. Jump Boost is unlocked.", "The weather vane is still up there. Find a way onto the roof."),
            Define("speedboost", "Speed Boost", "Courier Trial", "Carry a sealed letter across town before the candle burns down.", "The wax is still warm. Speed Boost is unlocked.", "The candle burned too low. Run it again."),
            Define("tungstonballsack", "Tungston", "Heavy Favor", "Move the stubborn tungsten weight off the market road.", "The market road is clear. Tungston is unlocked.", "That weight has not moved. Bring a better trick."),
            Define("sleep", "Sleep", "Quiet Bell", "Silence the midnight bell so the town can rest.", "The town finally sleeps. Sleep is unlocked.", "The bell is still waking everyone. Try again at the tower."),
            Define("poison", "Poison", "Bitter Roots", "Gather bitter roots from the swamp edge without touching the blue ones.", "The roots are sorted safely. Poison is unlocked.", "The root bundle is wrong. Sort it again."),
            Define("acid", "Acid", "Locked Rust", "Melt the rust from the cemetery gate hinges.", "The gate opens without a scream. Acid is unlocked.", "The hinges are still rusted shut. Keep at it."),
            Define("magicglassorb", "Magic Glass Orb", "Glass Eye", "Polish the scrying orb until it reflects the moon.", "The moon sits inside the glass. Magic Glass Orb is unlocked.", "The orb is still cloudy. Polish it again when the marker is ready.")
        };

        public static IReadOnlyList<WofSpellQuestDefinition> Definitions => DefinitionsInternal;

        public static void NormalizeProfile(WofSurvivalProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            profile.questUnlockedSpells ??= new[] { DefaultUnlockedSpell };
            profile.spellQuestAssignments ??= Array.Empty<WofSpellQuestAssignment>();
            profile.questFlags ??= Array.Empty<WofQuestFlagEntry>();
        }

        public static WofSpellQuestInteractionResult InteractWithGenericVillager(
            WofSurvivalProfile profile,
            string npcId,
            string townId,
            string displayName,
            double randomUnit,
            long now)
        {
            if (profile == null || string.IsNullOrWhiteSpace(npcId))
            {
                return new WofSpellQuestInteractionResult(Array.Empty<string>(), null, false);
            }

            NormalizeProfile(profile);
            var assignment = FindAssignment(profile, npcId);
            if (assignment == null)
            {
                var quest = PickAvailableQuest(profile, randomUnit);
                if (quest == null)
                {
                    return Result($"{displayName}: You already know every spell quest I can offer.");
                }

                assignment = new WofSpellQuestAssignment
                {
                    npcId = npcId,
                    townId = townId,
                    displayName = displayName,
                    questId = quest.Id,
                    spell = quest.Spell,
                    status = WofQuestDialogRules.QuestStatusAccepted,
                    assignedAt = now
                };
                AppendAssignment(profile, assignment);
                SetFlag(profile, $"quest:{quest.Id}", "started");
                return new WofSpellQuestInteractionResult(
                    new[]
                    {
                        $"{displayName}: mystery box opened - {quest.Title}.",
                        quest.Objective,
                        quest.IncompleteLine
                    },
                    assignment,
                    true);
            }

            var definition = FindDefinition(assignment.spell) ?? FindDefinition(assignment.questId);
            if (definition == null)
            {
                return Result($"{displayName}: this quest points at a missing spell.", assignment);
            }

            if (string.Equals(assignment.status, WofQuestDialogRules.QuestStatusCompleted, StringComparison.OrdinalIgnoreCase) ||
                Contains(profile.questUnlockedSpells, assignment.spell))
            {
                var changed = !string.Equals(assignment.status, WofQuestDialogRules.QuestStatusCompleted, StringComparison.OrdinalIgnoreCase) ||
                              !string.Equals(assignment.displayName, displayName, StringComparison.Ordinal) ||
                              !string.Equals(assignment.townId, townId, StringComparison.Ordinal);
                assignment.displayName = displayName;
                assignment.townId = townId;
                assignment.status = WofQuestDialogRules.QuestStatusCompleted;
                if (assignment.completedAt <= 0)
                {
                    assignment.completedAt = now;
                    changed = true;
                }
                return new WofSpellQuestInteractionResult(
                    new[] { $"{displayName}: {definition.DisplayName} is already yours." },
                    assignment,
                    changed);
            }

            if (IsReady(profile, definition))
            {
                AddUnlockedSpell(profile, assignment.spell);
                SetFlag(profile, definition.RequiredFlag, "true");
                SetFlag(profile, $"quest:{definition.Id}", "completed");
                assignment.displayName = displayName;
                assignment.townId = townId;
                assignment.status = WofQuestDialogRules.QuestStatusCompleted;
                assignment.completedAt = now;
                return new WofSpellQuestInteractionResult(
                    new[] { $"{displayName}: {definition.ReadyLine}" },
                    assignment,
                    true);
            }

            return new WofSpellQuestInteractionResult(
                new[] { $"{displayName}: {definition.Title}.", definition.IncompleteLine },
                assignment,
                false);
        }

        public static void AcceptDarrelQuest(WofSurvivalProfile profile, long now)
        {
            if (profile == null)
            {
                return;
            }

            NormalizeProfile(profile);
            var quest = FindDefinition(DarrelRewardSpell);
            var assignment = FindAssignment(profile, WofQuestDialogRules.DarrelNpcId);
            if (assignment == null || !string.Equals(assignment.spell, DarrelRewardSpell, StringComparison.Ordinal))
            {
                assignment = new WofSpellQuestAssignment
                {
                    npcId = WofQuestDialogRules.DarrelNpcId,
                    townId = WofQuestDialogRules.DarrelTownId,
                    displayName = "Darrel",
                    questId = quest?.Id ?? $"spellquest:{DarrelRewardSpell}",
                    spell = DarrelRewardSpell,
                    status = WofQuestDialogRules.QuestStatusAccepted,
                    assignedAt = now
                };
                ReplaceAssignment(profile, assignment);
            }
            else
            {
                assignment.displayName = "Darrel";
                assignment.townId = WofQuestDialogRules.DarrelTownId;
            }

            SetFlag(profile, DarrelAcceptedFlag, "true");
            SetFlagIfMissing(profile, DarrelLeavesFlag, "needed");
            SetFlagIfMissing(profile, DarrelBerriesFlag, "needed");
            SetFlagIfMissing(profile, DarrelRootsFlag, "needed");
            SetFlagIfMissing(profile, DarrelPotionFlag, "needed");
            var questStateKey = $"quest:{assignment.questId}";
            if (!string.Equals(GetFlag(profile, questStateKey), "completed", StringComparison.Ordinal))
            {
                SetFlag(profile, questStateKey, "started");
            }
        }

        public static WofSpellQuestAssignment FindAssignment(WofSurvivalProfile profile, string npcId)
        {
            if (profile?.spellQuestAssignments == null)
            {
                return null;
            }
            for (var index = 0; index < profile.spellQuestAssignments.Length; index++)
            {
                var assignment = profile.spellQuestAssignments[index];
                if (assignment != null && string.Equals(assignment.npcId, npcId, StringComparison.Ordinal))
                {
                    return assignment;
                }
            }
            return null;
        }

        public static string GetFlag(WofSurvivalProfile profile, string key)
        {
            if (profile?.questFlags == null)
            {
                return null;
            }
            for (var index = 0; index < profile.questFlags.Length; index++)
            {
                var flag = profile.questFlags[index];
                if (flag != null && string.Equals(flag.key, key, StringComparison.Ordinal))
                {
                    return flag.value;
                }
            }
            return null;
        }

        private static WofSpellQuestDefinition PickAvailableQuest(WofSurvivalProfile profile, double randomUnit)
        {
            var locked = new List<WofSpellQuestDefinition>(DefinitionsInternal.Length);
            var unassigned = new List<WofSpellQuestDefinition>(DefinitionsInternal.Length);
            for (var definitionIndex = 0; definitionIndex < DefinitionsInternal.Length; definitionIndex++)
            {
                var definition = DefinitionsInternal[definitionIndex];
                if (Contains(profile.questUnlockedSpells, definition.Spell))
                {
                    continue;
                }
                locked.Add(definition);
                if (!HasActiveAssignmentForSpell(profile, definition.Spell))
                {
                    unassigned.Add(definition);
                }
            }

            var pool = unassigned.Count > 0 ? unassigned : locked;
            if (pool.Count == 0)
            {
                return null;
            }
            var normalized = double.IsNaN(randomUnit) || double.IsInfinity(randomUnit)
                ? 0d
                : Math.Max(0d, Math.Min(0.999999999999d, randomUnit));
            var index = Math.Min(pool.Count - 1, (int)Math.Floor(normalized * pool.Count));
            return pool[index];
        }

        private static bool HasActiveAssignmentForSpell(WofSurvivalProfile profile, string spell)
        {
            for (var index = 0; index < profile.spellQuestAssignments.Length; index++)
            {
                var assignment = profile.spellQuestAssignments[index];
                if (assignment != null &&
                    string.Equals(assignment.spell, spell, StringComparison.Ordinal) &&
                    !string.Equals(assignment.status, WofQuestDialogRules.QuestStatusCompleted, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsReady(WofSurvivalProfile profile, WofSpellQuestDefinition definition)
        {
            return IsTruthy(GetFlag(profile, definition.RequiredFlag)) ||
                   string.Equals(GetFlag(profile, $"quest:{definition.Id}"), "completed", StringComparison.Ordinal);
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "completed", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "ready", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }

        public static void AddUnlockedSpell(WofSurvivalProfile profile, string spell)
        {
            if (Contains(profile.questUnlockedSpells, spell))
            {
                return;
            }
            var next = new string[profile.questUnlockedSpells.Length + 1];
            Array.Copy(profile.questUnlockedSpells, next, profile.questUnlockedSpells.Length);
            next[next.Length - 1] = spell;
            profile.questUnlockedSpells = next;
        }

        private static void AppendAssignment(WofSurvivalProfile profile, WofSpellQuestAssignment assignment)
        {
            var next = new WofSpellQuestAssignment[profile.spellQuestAssignments.Length + 1];
            Array.Copy(profile.spellQuestAssignments, next, profile.spellQuestAssignments.Length);
            next[next.Length - 1] = assignment;
            profile.spellQuestAssignments = next;
        }

        private static void ReplaceAssignment(WofSurvivalProfile profile, WofSpellQuestAssignment assignment)
        {
            for (var index = 0; index < profile.spellQuestAssignments.Length; index++)
            {
                if (profile.spellQuestAssignments[index] != null &&
                    string.Equals(profile.spellQuestAssignments[index].npcId, assignment.npcId, StringComparison.Ordinal))
                {
                    profile.spellQuestAssignments[index] = assignment;
                    return;
                }
            }
            AppendAssignment(profile, assignment);
        }

        private static void SetFlagIfMissing(WofSurvivalProfile profile, string key, string value)
        {
            if (GetFlag(profile, key) == null)
            {
                SetFlag(profile, key, value);
            }
        }

        public static void SetFlag(WofSurvivalProfile profile, string key, string value)
        {
            for (var index = 0; index < profile.questFlags.Length; index++)
            {
                var flag = profile.questFlags[index];
                if (flag != null && string.Equals(flag.key, key, StringComparison.Ordinal))
                {
                    flag.value = value;
                    return;
                }
            }
            var next = new WofQuestFlagEntry[profile.questFlags.Length + 1];
            Array.Copy(profile.questFlags, next, profile.questFlags.Length);
            next[next.Length - 1] = new WofQuestFlagEntry { key = key, value = value };
            profile.questFlags = next;
        }

        private static WofSpellQuestDefinition FindDefinition(string spellOrQuestId)
        {
            for (var index = 0; index < DefinitionsInternal.Length; index++)
            {
                var definition = DefinitionsInternal[index];
                if (string.Equals(definition.Spell, spellOrQuestId, StringComparison.Ordinal) ||
                    string.Equals(definition.Id, spellOrQuestId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }
            return null;
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

        private static WofSpellQuestInteractionResult Result(
            string message,
            WofSpellQuestAssignment assignment = null)
        {
            return new WofSpellQuestInteractionResult(new[] { message }, assignment, false);
        }

        private static WofSpellQuestDefinition Define(
            string spell,
            string displayName,
            string title,
            string objective,
            string readyLine,
            string incompleteLine)
        {
            return new WofSpellQuestDefinition(spell, displayName, title, objective, readyLine, incompleteLine);
        }
    }
}
