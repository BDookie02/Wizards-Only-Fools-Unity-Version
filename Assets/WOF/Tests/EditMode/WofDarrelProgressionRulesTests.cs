using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofDarrelProgressionRulesTests
    {
        [Test]
        public void IngredientCollectionRequiresSurvivalAndAcceptedDarrelQuest()
        {
            var profile = AcceptedProfile();
            var wrongMode = WofDarrelProgressionRules.CollectIngredient(profile, "leaves", false, 1000);
            Assert.That(wrongMode.Messages[0], Is.EqualTo("Darrel ingredients can only be gathered in survival mode."));
            Assert.That(wrongMode.ProfileChanged, Is.False);

            var unaccepted = WofDarrelProgressionRules.CollectIngredient(new WofSurvivalProfile(), "leaves", true, 1000);
            Assert.That(unaccepted.Messages[0], Is.EqualTo("Darrel has not offered the garden draught job yet."));
        }

        [Test]
        public void IngredientCollectionAddsCanonicalItemAndFlagsExactlyOnce()
        {
            var profile = AcceptedProfile();

            var result = WofDarrelProgressionRules.CollectIngredient(profile, "berries", true, 1234);

            Assert.That(result.ProfileChanged, Is.True);
            Assert.That(result.Messages[0], Is.EqualTo("Gathered Berries from the fields."));
            Assert.That(WofInventoryRules.GetQuantity(profile, "darrel-berries"), Is.EqualTo(1));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelBerriesFlag), Is.EqualTo("gathered"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, "quest:spellquest:healingcrystals"), Is.EqualTo("started"));

            var duplicate = WofDarrelProgressionRules.CollectIngredient(profile, "berries", true, 1300);
            Assert.That(duplicate.ProfileChanged, Is.False);
            Assert.That(duplicate.Messages[0], Is.EqualTo("Berries already gathered."));
        }

        [Test]
        public void BrewingReportsMissingIngredientsInReactOrder()
        {
            var profile = AcceptedProfile();
            WofInventoryRules.AddQuantity(profile, "darrel-berries", 1, 1000);

            var result = WofDarrelProgressionRules.BrewGardenDraught(profile, 1200);

            Assert.That(result.ProfileChanged, Is.False);
            Assert.That(result.Messages[0], Is.EqualTo("Missing leaves, roots for the garden draught."));
        }

        [Test]
        public void BrewingConsumesOneOfEachAndCreatesDraught()
        {
            var profile = AcceptedProfile();
            WofInventoryRules.AddQuantity(profile, "darrel-leaves", 1, 1000);
            WofInventoryRules.AddQuantity(profile, "darrel-berries", 1, 1000);
            WofInventoryRules.AddQuantity(profile, "darrel-roots", 1, 1000);

            var result = WofDarrelProgressionRules.BrewGardenDraught(profile, 1200);

            Assert.That(result.Messages[0], Is.EqualTo("Brewed the garden draught."));
            Assert.That(WofInventoryRules.GetQuantity(profile, "darrel-leaves"), Is.Zero);
            Assert.That(WofInventoryRules.GetQuantity(profile, "darrel-berries"), Is.Zero);
            Assert.That(WofInventoryRules.GetQuantity(profile, "darrel-roots"), Is.Zero);
            Assert.That(WofInventoryRules.GetQuantity(profile, "garden-draught"), Is.EqualTo(1));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelLeavesFlag), Is.EqualTo("brewed"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelPotionFlag), Is.EqualTo("brewed"));
        }

        [Test]
        public void DrinkingRequiresAndConsumesDraughtThenRequestsTeleport()
        {
            var profile = AcceptedProfile();
            var missing = WofDarrelProgressionRules.DrinkGardenDraught(profile);
            Assert.That(missing.Messages[0], Is.EqualTo("No garden draught in inventory."));
            Assert.That(missing.ShouldTeleport, Is.False);

            WofInventoryRules.AddQuantity(profile, "garden-draught", 1, 1000);
            var result = WofDarrelProgressionRules.DrinkGardenDraught(profile);
            Assert.That(result.ShouldTeleport, Is.True);
            Assert.That(result.Messages[0], Is.EqualTo("The garden draught pulls you toward the sacred garden."));
            Assert.That(WofInventoryRules.GetQuantity(profile, "garden-draught"), Is.Zero);
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofSpellQuestRules.DarrelPotionFlag), Is.EqualTo("drunk"));
        }

        [Test]
        public void GroveReturnCompletesAcceptedAssignmentAndAddsOneReactCrystal()
        {
            var profile = AcceptedProfile();
            var result = WofDarrelProgressionRules.CompleteGroveReturn(profile, 4321L);

            Assert.That(result.ProfileChanged, Is.True);
            Assert.That(result.Messages, Is.EqualTo(new[]
            {
                "Darrel: The spirit dragon sends you back with lemonade breath, lunch packed for the road, and Healing Crystals. Healing Crystals is unlocked."
            }));
            Assert.That(profile.questUnlockedSpells, Does.Contain(WofSpellQuestRules.DarrelRewardSpell));
            Assert.That(WofInventoryRules.GetQuantity(profile, "healing-crystals"), Is.EqualTo(1));
            Assert.That(WofSpellQuestRules.GetFlag(profile, "spellquest:healingcrystals:ready"), Is.EqualTo("true"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, "quest:spellquest:healingcrystals"), Is.EqualTo("completed"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofDarrelProgressionRules.GroveQuestFlag), Is.EqualTo("completed"));
            Assert.That(WofSpellQuestRules.FindAssignment(profile, WofQuestDialogRules.DarrelNpcId).completedAt, Is.EqualTo(4321L));
        }

        [Test]
        public void GroveReturnAfterPeaceDoesNotDuplicateReward()
        {
            var profile = AcceptedProfile();
            WofInventoryRules.AddQuantity(profile, "healing-crystals", 2, 1000L);
            WofSpellQuestRules.AddUnlockedSpell(profile, WofSpellQuestRules.DarrelRewardSpell);
            var assignment = WofSpellQuestRules.FindAssignment(profile, WofQuestDialogRules.DarrelNpcId);
            assignment.status = WofQuestDialogRules.QuestStatusCompleted;

            var result = WofDarrelProgressionRules.CompleteGroveReturn(profile, 4321L);

            Assert.That(result.ProfileChanged, Is.False);
            Assert.That(result.Messages, Is.EqualTo(new[] { "Darrel: Healing Crystals is already yours." }));
            Assert.That(WofInventoryRules.GetQuantity(profile, "healing-crystals"), Is.EqualTo(2));
        }

        [Test]
        public void DragonRejectsWizardWhoDidNotDrinkPotion()
        {
            var profile = AcceptedProfile();

            var result = WofDarrelProgressionRules.OpenDragonDialog(profile);

            Assert.That(result.Session.Line, Is.EqualTo(WofDarrelProgressionRules.DragonPotionRequiredLine));
            Assert.That(result.Session.Choices, Has.Length.EqualTo(1));
            Assert.That(result.Session.Choices[0].Id, Is.EqualTo("darrel-close"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofDarrelProgressionRules.DragonWokenFlag), Is.EqualTo("true"));
        }

        [Test]
        public void DrunkWizardGetsExactFightAndPeaceChoices()
        {
            var profile = AcceptedProfile();
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelPotionFlag, "drunk");

            var result = WofDarrelProgressionRules.OpenDragonDialog(profile);

            Assert.That(result.Session.Line, Is.EqualTo(WofDarrelProgressionRules.DragonOpeningLine));
            Assert.That(result.Session.Choices, Has.Length.EqualTo(2));
            Assert.That(result.Session.Choices[0].Id, Is.EqualTo("darrel-dragon-fight"));
            Assert.That(result.Session.Choices[1].Id, Is.EqualTo("darrel-dragon-peace"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofDarrelProgressionRules.GroveQuestFlag), Is.EqualTo("started"));
        }

        [Test]
        public void FightChoiceSetsFlagsAndRequestsServerAuthoritativeDeath()
        {
            var profile = AcceptedProfile();
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelPotionFlag, "drunk");
            var session = WofDarrelProgressionRules.OpenDragonDialog(profile).Session;

            var result = WofDarrelProgressionRules.ChooseDragon(profile, session, "darrel-dragon-fight", 2000);

            Assert.That(result.ShouldKillPlayer, Is.True);
            Assert.That(result.Session.Line, Is.EqualTo(WofDarrelProgressionRules.DragonFightLine));
            Assert.That(result.Messages[0], Is.EqualTo("Spirit Dragon: villain speech detected. Nap administered."));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofDarrelProgressionRules.DragonFoughtFlag), Is.EqualTo("true"));
        }

        [Test]
        public void PeaceChoiceCompletesQuestAddsTwoCrystalsAndUnlocksSpell()
        {
            var profile = AcceptedProfile();
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelPotionFlag, "drunk");
            var session = WofDarrelProgressionRules.OpenDragonDialog(profile).Session;

            var result = WofDarrelProgressionRules.ChooseDragon(profile, session, "darrel-dragon-peace", 2500);

            Assert.That(result.ShouldKillPlayer, Is.False);
            Assert.That(result.Session.Line, Is.EqualTo(WofDarrelProgressionRules.DragonPeaceLine));
            Assert.That(result.Messages[0], Is.EqualTo("Healing Crystals unlocked. The Spirit Dragon packed tacos and lemonade."));
            Assert.That(profile.questUnlockedSpells, Does.Contain("healingcrystals"));
            Assert.That(WofInventoryRules.GetQuantity(profile, "healing-crystals"), Is.EqualTo(2));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofDarrelProgressionRules.DragonPeacefulFlag), Is.EqualTo("true"));
            Assert.That(WofSpellQuestRules.GetFlag(profile, "quest:spellquest:healingcrystals"), Is.EqualTo("completed"));
            Assert.That(profile.spellQuestAssignments[0].status, Is.EqualTo("completed"));
            Assert.That(profile.darrelHealingCrystalsQuestStatus, Is.EqualTo("completed"));
        }

        [Test]
        public void CompletedWizardGetsLemonadeReturnLineOnly()
        {
            var profile = AcceptedProfile();
            WofSpellQuestRules.AddUnlockedSpell(profile, "healingcrystals");

            var result = WofDarrelProgressionRules.OpenDragonDialog(profile);

            Assert.That(result.Session.Line, Is.EqualTo(WofDarrelProgressionRules.DragonCompletedLine));
            Assert.That(result.Session.Choices, Has.Length.EqualTo(1));
            Assert.That(WofSpellQuestRules.GetFlag(profile, WofDarrelProgressionRules.GroveQuestFlag), Is.EqualTo("completed"));
        }

        [Test]
        public void CloseDragonChoiceClosesWithoutChangingProfile()
        {
            var profile = AcceptedProfile();
            var session = WofDarrelProgressionRules.OpenDragonDialog(profile).Session;

            var result = WofDarrelProgressionRules.ChooseDragon(profile, session, "darrel-close", 3000);

            Assert.That(result.Closed, Is.True);
            Assert.That(result.ProfileChanged, Is.False);
            Assert.That(result.Session, Is.Null);
        }

        private static WofSurvivalProfile AcceptedProfile()
        {
            var profile = new WofSurvivalProfile
            {
                playerName = "Quest QA",
                questUnlockedSpells = new[] { "blink" },
                spellQuestAssignments = new[]
                {
                    new WofSpellQuestAssignment
                    {
                        npcId = WofQuestDialogRules.DarrelNpcId,
                        townId = WofQuestDialogRules.DarrelTownId,
                        displayName = "Darrel",
                        questId = "spellquest:healingcrystals",
                        spell = "healingcrystals",
                        status = "assigned",
                        assignedAt = 900
                    }
                }
            };
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelAcceptedFlag, "true");
            return profile;
        }
    }
}
