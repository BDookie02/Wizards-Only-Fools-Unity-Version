using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofQuestNavigationRulesTests
    {
        [Test]
        public void GenericAssignedQuestUsesAuthoredNpcPositionAndExactReactCopy()
        {
            var profile = ProfileWithAssignment(
                "npc:clockmaker",
                "Clockmaker",
                "arcanebeam",
                1200L);
            var programs = new[]
            {
                Program("npc:clockmaker", new Vector3(14f, 2f, -9f))
            };

            var targets = WofQuestNavigationRules.ResolveAll(profile, programs);

            Assert.That(targets, Has.Count.EqualTo(1));
            Assert.That(targets[0].Id, Is.EqualTo("npc:clockmaker:spellquest:arcanebeam:quest-giver"));
            Assert.That(targets[0].QuestId, Is.EqualTo("spellquest:arcanebeam"));
            Assert.That(targets[0].Label, Is.EqualTo("Hands of the Clocktower"));
            Assert.That(targets[0].Detail, Is.EqualTo("Realign the clocktower hands before nightfall."));
            Assert.That(targets[0].Tone, Is.EqualTo(WofQuestNavigationTone.Npc));
            Assert.That(targets[0].Position, Is.EqualTo(new Vector3(14f, 7.5f, -9f)));
        }

        [Test]
        public void ReadyGenericQuestTargetsTurnInWithSpellAndNpcNames()
        {
            var profile = ProfileWithAssignment("npc:healer", "Nora", "healspell", 1200L);
            WofSpellQuestRules.SetFlag(profile, "spellquest:healspell:ready", "true");

            var targets = WofQuestNavigationRules.ResolveAll(
                profile,
                new[] { Program("npc:healer", new Vector3(-7f, 3f, 22f)) });

            Assert.That(targets, Has.Count.EqualTo(1));
            Assert.That(targets[0].Id, Is.EqualTo("npc:healer:spellquest:healspell:turn-in"));
            Assert.That(targets[0].Label, Is.EqualTo("Turn in Heal"));
            Assert.That(targets[0].Detail, Is.EqualTo("Return to Nora."));
            Assert.That(targets[0].Tone, Is.EqualTo(WofQuestNavigationTone.TurnIn));
            Assert.That(targets[0].Position, Is.EqualTo(new Vector3(-7f, 8.5f, 22f)));
        }

        [Test]
        public void MultipleTargetsStayOrderedByAssignedAtThenProfileOrder()
        {
            var profile = new WofSurvivalProfile
            {
                spellQuestAssignments = new[]
                {
                    Assignment("npc:later", "Later", "iceshard", 200L),
                    Assignment("npc:first-a", "First A", "arcanebeam", 100L),
                    Assignment("npc:first-b", "First B", "healspell", 100L)
                }
            };
            var programs = new[]
            {
                Program("npc:later", new Vector3(1f, 0f, 0f)),
                Program("npc:first-a", new Vector3(2f, 0f, 0f)),
                Program("npc:first-b", new Vector3(3f, 0f, 0f))
            };

            var targets = WofQuestNavigationRules.ResolveAll(profile, programs);

            Assert.That(targets, Has.Count.EqualTo(3));
            Assert.That(targets[0].NpcId, Is.EqualTo("npc:first-a"));
            Assert.That(targets[1].NpcId, Is.EqualTo("npc:first-b"));
            Assert.That(targets[2].NpcId, Is.EqualTo("npc:later"));
        }

        [Test]
        public void MissingProgramCompletedAndUnlockedAssignmentsDoNotCreateBeacons()
        {
            var missingProgram = Assignment("npc:missing", "Missing", "iceshard", 100L);
            var completed = Assignment("npc:done", "Done", "arcanebeam", 200L);
            completed.status = WofQuestDialogRules.QuestStatusCompleted;
            var unlocked = Assignment("npc:known", "Known", "healspell", 300L);
            var profile = new WofSurvivalProfile
            {
                questUnlockedSpells = new[] { WofSpellQuestRules.DefaultUnlockedSpell, "healspell" },
                spellQuestAssignments = new[] { missingProgram, completed, unlocked }
            };

            var targets = WofQuestNavigationRules.ResolveAll(
                profile,
                new[]
                {
                    Program("npc:done", Vector3.zero),
                    Program("npc:known", Vector3.one)
                });

            Assert.That(targets, Is.Empty);
        }

        [Test]
        public void ClaimedDarrelProgramMovesTurnInAndDrinkButNotWorldObjectiveMarkers()
        {
            var profile = new WofSurvivalProfile();
            WofSpellQuestRules.AcceptDarrelQuest(profile, 1000L);
            var assignment = WofSpellQuestRules.FindAssignment(profile, WofQuestDialogRules.DarrelNpcId);
            var customPosition = new Vector3(90f, 12f, -30f);
            var programs = new[] { Program(assignment.npcId, customPosition) };

            var field = WofQuestNavigationRules.ResolveAll(profile, programs);
            Assert.That(field[0].Position, Is.EqualTo(WofDarrelQuestNavigationRules.FieldSearchPosition));

            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelLeavesFlag, "gathered");
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelBerriesFlag, "gathered");
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelRootsFlag, "gathered");
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelPotionFlag, "brewed");
            var drink = WofQuestNavigationRules.ResolveAll(profile, programs);
            Assert.That(drink[0].Position, Is.EqualTo(customPosition + Vector3.up * 5.5f));

            WofSpellQuestRules.SetFlag(profile, "spellquest:healingcrystals:ready", "true");
            var turnIn = WofQuestNavigationRules.ResolveAll(profile, programs);
            Assert.That(turnIn[0].Position, Is.EqualTo(customPosition + Vector3.up * 5.5f));
        }

        [Test]
        public void AcceptedFlagRecognizesClaimedDarrelWithoutNameHeuristics()
        {
            var profile = new WofSurvivalProfile
            {
                spellQuestAssignments = new[]
                {
                    Assignment("custom:npc:42", "Moon Gardener", WofSpellQuestRules.DarrelRewardSpell, 100L)
                }
            };
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelAcceptedFlag, "true");

            var targets = WofQuestNavigationRules.ResolveAll(
                profile,
                new[] { Program("custom:npc:42", new Vector3(4f, 2f, 8f)) });

            Assert.That(targets, Has.Count.EqualTo(1));
            Assert.That(targets[0].NpcId, Is.EqualTo("custom:npc:42"));
            Assert.That(targets[0].Id, Does.EndWith(":fields"));
        }

        private static WofSurvivalProfile ProfileWithAssignment(
            string npcId,
            string displayName,
            string spell,
            long assignedAt)
        {
            return new WofSurvivalProfile
            {
                spellQuestAssignments = new[] { Assignment(npcId, displayName, spell, assignedAt) }
            };
        }

        private static WofSpellQuestAssignment Assignment(
            string npcId,
            string displayName,
            string spell,
            long assignedAt)
        {
            return new WofSpellQuestAssignment
            {
                npcId = npcId,
                townId = "test-town",
                displayName = displayName,
                questId = $"spellquest:{spell}",
                spell = spell,
                status = WofQuestDialogRules.QuestStatusAccepted,
                assignedAt = assignedAt
            };
        }

        private static WofQuestNpcProgram Program(string npcId, Vector3 position)
        {
            return new WofQuestNpcProgram
            {
                npcId = npcId,
                displayName = npcId,
                hasPosition = true,
                position = position
            };
        }
    }
}
