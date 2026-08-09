using System;
using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofSpellQuestRulesTests
    {
        [Test]
        public void CanonicalReactCatalogContainsEveryNonReservedSpellInOrder()
        {
            Assert.That(WofSpellQuestRules.Definitions.Count, Is.EqualTo(25));
            Assert.That(WofSpellQuestRules.Definitions[0].Spell, Is.EqualTo("fireball"));
            Assert.That(WofSpellQuestRules.Definitions[0].Title, Is.EqualTo("Coal for the Cold Hearth"));
            Assert.That(WofSpellQuestRules.Definitions[16].Spell, Is.EqualTo("healingcrystals"));
            Assert.That(WofSpellQuestRules.Definitions[16].Title, Is.EqualTo("The Sacred Garden Draught"));
            Assert.That(WofSpellQuestRules.Definitions[24].Spell, Is.EqualTo("magicglassorb"));
            Assert.That(WofSpellQuestRules.Definitions[24].IncompleteLine,
                Is.EqualTo("The orb is still cloudy. Polish it again when the marker is ready."));
        }

        [Test]
        public void FirstGenericInteractionAssignsMysteryQuestAndExactThreeMessages()
        {
            var profile = MakeProfile();

            var result = WofSpellQuestRules.InteractWithGenericVillager(
                profile,
                "12--20",
                "village-town",
                "Town Villager 7",
                0d,
                1234L);

            Assert.That(result.ProfileChanged, Is.True);
            Assert.That(result.Assignment.spell, Is.EqualTo("fireball"));
            Assert.That(result.Assignment.status, Is.EqualTo("assigned"));
            Assert.That(result.Assignment.assignedAt, Is.EqualTo(1234L));
            Assert.That(result.Messages, Is.EqualTo(new[]
            {
                "Town Villager 7: mystery box opened - Coal for the Cold Hearth.",
                "Bring proof that you lit a cold town hearth without burning the rafters.",
                "The hearth still needs a spark. Try again after the town marks the hearth lit."
            }));
            Assert.That(WofSpellQuestRules.GetFlag(profile, "quest:spellquest:fireball"), Is.EqualTo("started"));
        }

        [Test]
        public void ActiveSpellIsExcludedFromNextVillagersRandomPool()
        {
            var profile = MakeProfile();
            WofSpellQuestRules.InteractWithGenericVillager(
                profile,
                "npc-a",
                "village-town",
                "Town Villager 1",
                0d,
                100L);

            var second = WofSpellQuestRules.InteractWithGenericVillager(
                profile,
                "npc-b",
                "village-town",
                "Town Villager 2",
                0d,
                200L);

            Assert.That(second.Assignment.spell, Is.EqualTo("iceshard"));
        }

        [Test]
        public void RepeatInteractionUsesExactTitleAndIncompleteCopy()
        {
            var profile = MakeProfile();
            WofSpellQuestRules.InteractWithGenericVillager(
                profile,
                "npc-a",
                "village-town",
                "Town Villager 1",
                0d,
                100L);

            var repeat = WofSpellQuestRules.InteractWithGenericVillager(
                profile,
                "npc-a",
                "village-town",
                "Town Villager 1",
                0.9d,
                200L);

            Assert.That(repeat.ProfileChanged, Is.False);
            Assert.That(repeat.Messages, Is.EqualTo(new[]
            {
                "Town Villager 1: Coal for the Cold Hearth.",
                "The hearth still needs a spark. Try again after the town marks the hearth lit."
            }));
        }

        [Test]
        public void ReadyFlagCompletesAssignmentAndUnlocksSpell()
        {
            var profile = MakeProfile();
            var first = WofSpellQuestRules.InteractWithGenericVillager(
                profile,
                "npc-a",
                "village-town",
                "Town Villager 1",
                0d,
                100L);
            profile.questFlags = new[]
            {
                new WofQuestFlagEntry { key = "spellquest:fireball:ready", value = "ready" }
            };

            var completed = WofSpellQuestRules.InteractWithGenericVillager(
                profile,
                "npc-a",
                "village-town",
                "Town Villager 1",
                0d,
                200L);

            Assert.That(completed.ProfileChanged, Is.True);
            Assert.That(first.Assignment.status, Is.EqualTo("completed"));
            Assert.That(first.Assignment.completedAt, Is.EqualTo(200L));
            Assert.That(profile.questUnlockedSpells, Does.Contain("fireball"));
            Assert.That(completed.Messages, Is.EqualTo(new[]
            {
                "Town Villager 1: The hearth is breathing again. Fireball is yours."
            }));
            Assert.That(WofSpellQuestRules.GetFlag(profile, "quest:spellquest:fireball"), Is.EqualTo("completed"));
        }

        [Test]
        public void DarrelAcceptanceCreatesCanonicalAssignmentAndNeededFlags()
        {
            var profile = MakeProfile();

            WofSpellQuestRules.AcceptDarrelQuest(profile, 999L);

            var assignment = WofSpellQuestRules.FindAssignment(profile, WofQuestDialogRules.DarrelNpcId);
            Assert.That(assignment, Is.Not.Null);
            Assert.That(assignment.spell, Is.EqualTo("healingcrystals"));
            Assert.That(assignment.townId, Is.EqualTo("base-village"));
            Assert.That(assignment.displayName, Is.EqualTo("Darrel"));
            Assert.That(assignment.assignedAt, Is.EqualTo(999L));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelAcceptedFlag), Is.EqualTo("true"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelLeavesFlag), Is.EqualTo("needed"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelBerriesFlag), Is.EqualTo("needed"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelRootsFlag), Is.EqualTo("needed"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelPotionFlag), Is.EqualTo("needed"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, "quest:spellquest:healingcrystals"), Is.EqualTo("started"));
        }

        [Test]
        public void FullyUnlockedProfileGetsExactExhaustedLine()
        {
            var profile = MakeProfile();
            profile.questUnlockedSpells = new string[WofSpellQuestRules.Definitions.Count + 1];
            profile.questUnlockedSpells[0] = WofSpellQuestRules.DefaultUnlockedSpell;
            for (var index = 0; index < WofSpellQuestRules.Definitions.Count; index++)
            {
                profile.questUnlockedSpells[index + 1] = WofSpellQuestRules.Definitions[index].Spell;
            }

            var result = WofSpellQuestRules.InteractWithGenericVillager(
                profile,
                "npc-a",
                "village-town",
                "Town Villager 1",
                0d,
                100L);

            Assert.That(result.ProfileChanged, Is.False);
            Assert.That(result.Assignment, Is.Null);
            Assert.That(result.Messages, Is.EqualTo(new[]
            {
                "Town Villager 1: You already know every spell quest I can offer."
            }));
        }

        private static WofSurvivalProfile MakeProfile()
        {
            return new WofSurvivalProfile
            {
                playerName = "Tester",
                questUnlockedSpells = new[] { WofSpellQuestRules.DefaultUnlockedSpell },
                spellQuestAssignments = Array.Empty<WofSpellQuestAssignment>(),
                questFlags = Array.Empty<WofQuestFlagEntry>()
            };
        }
    }
}
