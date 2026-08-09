using System;
using System.Collections.Generic;

namespace WOF
{
    public readonly struct WofDarrelProgressionResult
    {
        public WofDarrelProgressionResult(
            string[] messages,
            bool profileChanged,
            bool shouldTeleport,
            bool shouldKillPlayer,
            bool closed,
            WofQuestDialogSession session)
        {
            Messages = messages ?? Array.Empty<string>();
            ProfileChanged = profileChanged;
            ShouldTeleport = shouldTeleport;
            ShouldKillPlayer = shouldKillPlayer;
            Closed = closed;
            Session = session;
        }

        public string[] Messages { get; }
        public bool ProfileChanged { get; }
        public bool ShouldTeleport { get; }
        public bool ShouldKillPlayer { get; }
        public bool Closed { get; }
        public WofQuestDialogSession Session { get; }
    }

    public static class WofDarrelProgressionRules
    {
        public const string DragonNpcId = "darrel-spirit-dragon";
        public const string DragonTownId = "darrel-grove";
        public const string DragonWokenFlag = "darrel:dragon:woken";
        public const string DragonPeacefulFlag = "darrel:dragon:peaceful";
        public const string DragonFoughtFlag = "darrel:dragon:fought";
        public const string GroveQuestFlag = "quest:darrel-grove";

        public const string DragonOpeningLine = "Spirit Dragon: Hm? State your business, little wizard.";
        public const string DragonPotionRequiredLine = "Spirit Dragon: You do not smell like Darrel's garden draught. If Darrel sent you, drink the potion first and come back properly.";
        public const string DragonCompletedLine = "Spirit Dragon: Back already? I still have lemonade, but I am guarding the tacos from myself.";
        public const string DragonFightLine = "Player: I will fight you and take the Healing Crystals by force, killing you and taking control of this realm for myself.\n\nSpirit Dragon: That was a whole villain speech for someone standing in my living room. Adorable. Take a nap.";
        public const string DragonPeaceLine = "Player: I need Healing Crystals for a friend in need, and for myself.\n\nSpirit Dragon: Oh good. I thought you had come to slay me and steal my Healing Crystals, but you are a good man and I would be glad to call you my friend.\n\nSpirit Dragon: Here, have some soft tacos and lemonade. I grabbed you enough crystals for Darrel and for your own spellwork.";

        public static WofDarrelProgressionResult CollectIngredient(
            WofSurvivalProfile profile,
            string ingredient,
            bool survivalMode,
            long now)
        {
            if (!survivalMode)
            {
                return Message("Darrel ingredients can only be gathered in survival mode.");
            }
            if (profile == null || !HasDarrelQuest(profile))
            {
                return Message("Darrel has not offered the garden draught job yet.");
            }

            var itemId = ingredient switch
            {
                "leaves" => "darrel-leaves",
                "berries" => "darrel-berries",
                "roots" => "darrel-roots",
                _ => null
            };
            var definition = WofInventoryRules.FindItemDefinition(itemId);
            if (definition == null)
            {
                return new WofDarrelProgressionResult(Array.Empty<string>(), false, false, false, false, null);
            }
            if (WofInventoryRules.GetQuantity(profile, itemId) >= 1)
            {
                return Message($"{definition.Name} already gathered.");
            }

            WofInventoryRules.AddQuantity(profile, itemId, 1, now);
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelAcceptedFlag, "true");
            WofSpellQuestRules.SetFlag(profile, ResolveIngredientFlag(ingredient), "gathered");
            WofSpellQuestRules.SetFlag(profile, $"quest:{ResolveHealingQuest().Id}", "started");
            return Changed($"Gathered {definition.Name} from the fields.");
        }

        public static WofDarrelProgressionResult BrewGardenDraught(WofSurvivalProfile profile, long now)
        {
            if (profile == null || !HasDarrelQuest(profile))
            {
                return Message("Darrel has not offered the garden draught job yet.");
            }

            var ingredientIds = new[] { "darrel-leaves", "darrel-berries", "darrel-roots" };
            var ingredientNames = new[] { "leaves", "berries", "roots" };
            var missing = new List<string>(3);
            for (var index = 0; index < ingredientIds.Length; index++)
            {
                if (WofInventoryRules.GetQuantity(profile, ingredientIds[index]) < 1)
                {
                    missing.Add(ingredientNames[index]);
                }
            }
            if (missing.Count > 0)
            {
                return Message($"Missing {string.Join(", ", missing)} for the garden draught.");
            }

            for (var index = 0; index < ingredientIds.Length; index++)
            {
                WofInventoryRules.RemoveQuantity(profile, ingredientIds[index], 1);
            }
            WofInventoryRules.AddQuantity(profile, "garden-draught", 1, now);
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelLeavesFlag, "brewed");
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelBerriesFlag, "brewed");
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelRootsFlag, "brewed");
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelPotionFlag, "brewed");
            WofSpellQuestRules.SetFlag(profile, $"quest:{ResolveHealingQuest().Id}", "started");
            return Changed("Brewed the garden draught.");
        }

        public static WofDarrelProgressionResult DrinkGardenDraught(WofSurvivalProfile profile)
        {
            if (profile == null || WofInventoryRules.GetQuantity(profile, "garden-draught") < 1)
            {
                return Message("No garden draught in inventory.");
            }
            if (!WofInventoryRules.RemoveQuantity(profile, "garden-draught", 1))
            {
                return new WofDarrelProgressionResult(Array.Empty<string>(), false, false, false, false, null);
            }

            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelPotionFlag, "drunk");
            WofSpellQuestRules.SetFlag(profile, $"quest:{ResolveHealingQuest().Id}", "started");
            return new WofDarrelProgressionResult(
                new[] { "The garden draught pulls you toward the sacred garden." },
                true,
                true,
                false,
                false,
                null);
        }

        public static WofDarrelProgressionResult OpenDragonDialog(WofSurvivalProfile profile)
        {
            if (profile == null)
            {
                return new WofDarrelProgressionResult(Array.Empty<string>(), false, false, false, false, null);
            }

            var quest = ResolveHealingQuest();
            var hasDrunkPotion = string.Equals(
                                     WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelPotionFlag),
                                     "drunk",
                                     StringComparison.Ordinal) ||
                                 string.Equals(WofSpellQuestRules.GetFlag(profile, GroveQuestFlag), "started", StringComparison.Ordinal);
            var alreadyCompleted = Contains(profile.questUnlockedSpells, WofSpellQuestRules.DarrelRewardSpell) ||
                                   string.Equals(WofSpellQuestRules.GetFlag(profile, $"quest:{quest.Id}"), "completed", StringComparison.Ordinal);

            WofSpellQuestRules.SetFlag(profile, DragonWokenFlag, "true");
            if (hasDrunkPotion || alreadyCompleted)
            {
                WofSpellQuestRules.SetFlag(
                    profile,
                    GroveQuestFlag,
                    alreadyCompleted || string.Equals(WofSpellQuestRules.GetFlag(profile, GroveQuestFlag), "completed", StringComparison.Ordinal)
                        ? "completed"
                        : "started");
            }

            var line = alreadyCompleted
                ? DragonCompletedLine
                : hasDrunkPotion
                    ? DragonOpeningLine
                    : DragonPotionRequiredLine;
            var session = alreadyCompleted || !hasDrunkPotion
                ? CreateDragonSession(line, new WofQuestDialogChoice("darrel-close", "Leave the dragon in peace"))
                : CreateDragonSession(
                    line,
                    new WofQuestDialogChoice("darrel-dragon-fight", "Fight and take the Healing Crystals by force."),
                    new WofQuestDialogChoice("darrel-dragon-peace", "Peacefully ask for crystals for Darrel and yourself."));
            return new WofDarrelProgressionResult(new[] { line }, true, false, false, false, session);
        }

        public static WofDarrelProgressionResult ChooseDragon(
            WofSurvivalProfile profile,
            WofQuestDialogSession current,
            string choiceId,
            long now)
        {
            if (profile == null || current == null ||
                !string.Equals(current.NpcId, DragonNpcId, StringComparison.Ordinal))
            {
                return new WofDarrelProgressionResult(Array.Empty<string>(), false, false, false, false, current);
            }

            if (string.Equals(choiceId, "darrel-close", StringComparison.Ordinal))
            {
                return new WofDarrelProgressionResult(Array.Empty<string>(), false, false, false, true, null);
            }
            if (string.Equals(choiceId, "darrel-dragon-fight", StringComparison.Ordinal))
            {
                WofSpellQuestRules.SetFlag(profile, DragonWokenFlag, "true");
                WofSpellQuestRules.SetFlag(profile, DragonFoughtFlag, "true");
                return new WofDarrelProgressionResult(
                    new[] { "Spirit Dragon: villain speech detected. Nap administered." },
                    true,
                    false,
                    true,
                    false,
                    CreateDragonSession(DragonFightLine, new WofQuestDialogChoice("darrel-close", "Respawn and reconsider")));
            }
            if (!string.Equals(choiceId, "darrel-dragon-peace", StringComparison.Ordinal))
            {
                return new WofDarrelProgressionResult(Array.Empty<string>(), false, false, false, false, current);
            }

            var quest = ResolveHealingQuest();
            var assignment = FindHealingAssignment(profile) ?? new WofSpellQuestAssignment
            {
                npcId = WofQuestDialogRules.DarrelNpcId,
                townId = WofQuestDialogRules.DarrelTownId,
                displayName = "Darrel",
                questId = quest.Id,
                spell = quest.Spell,
                status = WofQuestDialogRules.QuestStatusAccepted,
                assignedAt = now
            };
            assignment.displayName = "Darrel";
            assignment.status = WofQuestDialogRules.QuestStatusCompleted;
            assignment.completedAt = now;
            ReplaceAssignment(profile, assignment);
            WofSpellQuestRules.AddUnlockedSpell(profile, WofSpellQuestRules.DarrelRewardSpell);
            WofSpellQuestRules.SetFlag(profile, DragonWokenFlag, "true");
            WofSpellQuestRules.SetFlag(profile, DragonPeacefulFlag, "true");
            WofSpellQuestRules.SetFlag(profile, quest.RequiredFlag, "true");
            WofSpellQuestRules.SetFlag(profile, $"quest:{quest.Id}", "completed");
            WofSpellQuestRules.SetFlag(profile, GroveQuestFlag, "completed");
            WofInventoryRules.AddQuantity(profile, "healing-crystals", 2, now);
            profile.darrelHealingCrystalsQuestStatus = WofQuestDialogRules.QuestStatusCompleted;
            profile.darrelHealingCrystalsCompletedAt = now;
            return new WofDarrelProgressionResult(
                new[] { "Healing Crystals unlocked. The Spirit Dragon packed tacos and lemonade." },
                true,
                false,
                false,
                false,
                CreateDragonSession(DragonPeaceLine, new WofQuestDialogChoice("darrel-close", "Thank the dragon")));
        }

        public static WofDarrelProgressionResult CompleteGroveReturn(WofSurvivalProfile profile, long now)
        {
            if (profile == null)
            {
                return new WofDarrelProgressionResult(Array.Empty<string>(), false, false, false, false, null);
            }

            var quest = ResolveHealingQuest();
            var assignment = FindHealingAssignment(profile) ?? new WofSpellQuestAssignment
            {
                npcId = WofQuestDialogRules.DarrelNpcId,
                townId = WofQuestDialogRules.DarrelTownId,
                displayName = "Darrel",
                questId = quest.Id,
                spell = quest.Spell,
                status = WofQuestDialogRules.QuestStatusAccepted,
                assignedAt = now
            };
            assignment.displayName = "Darrel";
            assignment.townId = string.IsNullOrWhiteSpace(assignment.townId)
                ? WofDarrelProgressionRules.DragonTownId
                : assignment.townId;

            if (string.Equals(assignment.status, WofQuestDialogRules.QuestStatusCompleted, StringComparison.Ordinal) ||
                Contains(profile.questUnlockedSpells, assignment.spell))
            {
                return Message("Darrel: Healing Crystals is already yours.");
            }

            assignment.status = WofQuestDialogRules.QuestStatusCompleted;
            assignment.completedAt = now;
            ReplaceAssignment(profile, assignment);
            WofSpellQuestRules.AddUnlockedSpell(profile, WofSpellQuestRules.DarrelRewardSpell);
            WofSpellQuestRules.SetFlag(profile, quest.RequiredFlag, "true");
            WofSpellQuestRules.SetFlag(profile, $"quest:{quest.Id}", "completed");
            WofSpellQuestRules.SetFlag(profile, GroveQuestFlag, "completed");
            WofInventoryRules.AddQuantity(profile, "healing-crystals", 1, now);
            profile.darrelHealingCrystalsQuestStatus = WofQuestDialogRules.QuestStatusCompleted;
            profile.darrelHealingCrystalsCompletedAt = now;
            return Changed($"Darrel: {quest.ReadyLine}");
        }

        public static bool HasDarrelQuest(WofSurvivalProfile profile)
        {
            if (profile == null)
            {
                return false;
            }
            if (IsTruthy(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelAcceptedFlag)))
            {
                return true;
            }
            var assignment = FindHealingAssignment(profile);
            return assignment != null &&
                   string.Equals(assignment.status, WofQuestDialogRules.QuestStatusAccepted, StringComparison.Ordinal);
        }

        private static WofSpellQuestDefinition ResolveHealingQuest()
        {
            for (var index = 0; index < WofSpellQuestRules.Definitions.Count; index++)
            {
                if (string.Equals(WofSpellQuestRules.Definitions[index].Spell, WofSpellQuestRules.DarrelRewardSpell, StringComparison.Ordinal))
                {
                    return WofSpellQuestRules.Definitions[index];
                }
            }
            throw new InvalidOperationException("The canonical Healing Crystals quest definition is missing.");
        }

        private static string ResolveIngredientFlag(string ingredient)
        {
            return ingredient switch
            {
                "leaves" => WofSpellQuestRules.DarrelLeavesFlag,
                "berries" => WofSpellQuestRules.DarrelBerriesFlag,
                _ => WofSpellQuestRules.DarrelRootsFlag
            };
        }

        private static WofSpellQuestAssignment FindHealingAssignment(WofSurvivalProfile profile)
        {
            WofSpellQuestRules.NormalizeProfile(profile);
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

        private static void ReplaceAssignment(WofSurvivalProfile profile, WofSpellQuestAssignment assignment)
        {
            var assignments = new List<WofSpellQuestAssignment>(profile.spellQuestAssignments ?? Array.Empty<WofSpellQuestAssignment>());
            for (var index = 0; index < assignments.Count; index++)
            {
                if (string.Equals(assignments[index]?.npcId, assignment.npcId, StringComparison.Ordinal) ||
                    string.Equals(assignments[index]?.spell, assignment.spell, StringComparison.Ordinal))
                {
                    assignments[index] = assignment;
                    profile.spellQuestAssignments = assignments.ToArray();
                    return;
                }
            }
            assignments.Add(assignment);
            profile.spellQuestAssignments = assignments.ToArray();
        }

        private static WofQuestDialogSession CreateDragonSession(string line, params WofQuestDialogChoice[] choices)
        {
            return new WofQuestDialogSession(DragonNpcId, DragonTownId, "Spirit Dragon", line, choices);
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "true", StringComparison.Ordinal) ||
                   string.Equals(value, "completed", StringComparison.Ordinal) ||
                   string.Equals(value, "ready", StringComparison.Ordinal) ||
                   string.Equals(value, "1", StringComparison.Ordinal);
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

        private static WofDarrelProgressionResult Message(string message)
        {
            return new WofDarrelProgressionResult(new[] { message }, false, false, false, false, null);
        }

        private static WofDarrelProgressionResult Changed(string message)
        {
            return new WofDarrelProgressionResult(new[] { message }, true, false, false, false, null);
        }
    }
}
