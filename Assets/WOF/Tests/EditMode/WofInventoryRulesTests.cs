using System;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofInventoryRulesTests
    {
        [Test]
        public void CanonicalInventoryMatchesReactIdsOrderCopyAndStackLimits()
        {
            Assert.That(WofInventoryRules.ItemDefinitions.Count, Is.EqualTo(5));
            Assert.That(WofInventoryRules.ItemDefinitions[0].Id, Is.EqualTo("darrel-leaves"));
            Assert.That(WofInventoryRules.ItemDefinitions[1].Description, Is.EqualTo("Bright field berries for Darrel's garden draught."));
            Assert.That(WofInventoryRules.ItemDefinitions[2].MaxStack, Is.EqualTo(9));
            Assert.That(WofInventoryRules.ItemDefinitions[3].Description, Is.EqualTo("A rough potion that folds the world toward the sacred garden."));
            Assert.That(WofInventoryRules.ItemDefinitions[3].MaxStack, Is.EqualTo(3));
            Assert.That(WofInventoryRules.ItemDefinitions[4].Category, Is.EqualTo("quest"));
        }

        [Test]
        public void NormalizeInventoryRejectsUnknownAndEmptyEntriesAndClampsStacks()
        {
            var profile = new WofSurvivalProfile
            {
                inventory = new[]
                {
                    new WofInventoryItemEntry { itemId = "unknown", quantity = 5 },
                    null,
                    new WofInventoryItemEntry { itemId = "garden-draught", quantity = 99, acquiredAt = -4 },
                    new WofInventoryItemEntry { itemId = "darrel-leaves", quantity = 0 }
                }
            };

            WofInventoryRules.NormalizeProfile(profile);

            Assert.That(profile.inventory, Has.Length.EqualTo(1));
            Assert.That(profile.inventory[0].itemId, Is.EqualTo("garden-draught"));
            Assert.That(profile.inventory[0].quantity, Is.EqualTo(3));
            Assert.That(profile.inventory[0].acquiredAt, Is.EqualTo(0));
        }

        [Test]
        public void DisplayEntriesUseReactCanonicalOrder()
        {
            var profile = new WofSurvivalProfile
            {
                inventory = new[]
                {
                    new WofInventoryItemEntry { itemId = "healing-crystals", quantity = 1 },
                    new WofInventoryItemEntry { itemId = "darrel-berries", quantity = 2 }
                }
            };

            var entries = WofInventoryRules.GetInventoryEntries(profile);

            Assert.That(entries, Has.Length.EqualTo(2));
            Assert.That(entries[0].Definition.Id, Is.EqualTo("darrel-berries"));
            Assert.That(entries[1].Definition.Id, Is.EqualTo("healing-crystals"));
        }

        [Test]
        public void ActiveQuestsExcludeCompletedAndUnlockedSpells()
        {
            var profile = new WofSurvivalProfile
            {
                questUnlockedSpells = new[] { "blink", "iceshard" },
                spellQuestAssignments = new[]
                {
                    Assignment("a", "fireball", "assigned"),
                    Assignment("b", "iceshard", "assigned"),
                    Assignment("c", "healspell", "completed")
                }
            };

            var entries = WofInventoryRules.GetActiveQuestEntries(profile);

            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].Definition.Title, Is.EqualTo("Coal for the Cold Hearth"));
        }

        [Test]
        public void QuestDefinitionFallsBackFromMissingQuestIdToSpell()
        {
            var assignment = Assignment("a", "magicglassorb", "assigned");
            assignment.questId = "missing";

            var definition = WofInventoryRules.ResolveQuestDefinition(assignment);

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Title, Is.EqualTo("Glass Eye"));
        }

        [Test]
        public void QuestSelectionClampsAndWrapsLikeReact()
        {
            Assert.That(WofInventoryRules.ClampQuestIndex(9, 3), Is.EqualTo(2));
            Assert.That(WofInventoryRules.ClampQuestIndex(9, 0), Is.EqualTo(0));
            Assert.That(WofInventoryRules.GetNextQuestIndex(2, 1, 3), Is.EqualTo(0));
            Assert.That(WofInventoryRules.GetNextQuestIndex(0, -1, 3), Is.EqualTo(2));
        }

        [Test]
        public void QuestStatusMatchesReactProgressionLabels()
        {
            var profile = new WofSurvivalProfile
            {
                spellQuestAssignments = new[] { Assignment("a", "fireball", "assigned") }
            };
            var entry = WofInventoryRules.GetActiveQuestEntries(profile)[0];
            Assert.That(WofInventoryRules.GetQuestStatus(entry, profile), Is.EqualTo("In progress"));

            profile.questFlags = new[] { new WofQuestFlagEntry { key = entry.Definition.RequiredFlag, value = "true" } };
            Assert.That(WofInventoryRules.GetQuestStatus(entry, profile), Is.EqualTo("Ready to turn in"));

            profile.questFlags = new[] { new WofQuestFlagEntry { key = $"quest:{entry.Definition.Id}", value = "completed" } };
            Assert.That(WofInventoryRules.GetQuestStatus(entry, profile), Is.EqualTo("Complete"));
            Assert.That(WofInventoryRules.GetQuestStatus(null, profile), Is.EqualTo("No active quest selected."));
        }

        [Test]
        public void DarrelProgressRecognizesGatheredBrewedDrunkAndTruthyValues()
        {
            var profile = new WofSurvivalProfile
            {
                questFlags = new[]
                {
                    new WofQuestFlagEntry { key = WofSpellQuestRules.DarrelLeavesFlag, value = "gathered" },
                    new WofQuestFlagEntry { key = WofSpellQuestRules.DarrelBerriesFlag, value = "true" },
                    new WofQuestFlagEntry { key = WofSpellQuestRules.DarrelRootsFlag, value = "needed" },
                    new WofQuestFlagEntry { key = WofSpellQuestRules.DarrelPotionFlag, value = "drunk" }
                }
            };

            var rows = WofInventoryRules.GetDarrelProgressRows(profile);

            Assert.That(rows[0].Done, Is.True);
            Assert.That(rows[1].Done, Is.True);
            Assert.That(rows[2].Done, Is.False);
            Assert.That(rows[3].Done, Is.True);
        }

        [Test]
        public void KeyboardActionsPreserveOriginalInventoryScheme()
        {
            Assert.That(WofInventoryRules.GetKeyboardAction("KeyJ", false), Is.EqualTo(WofInventoryKeyboardAction.ToggleQuestJournal));
            Assert.That(WofInventoryRules.GetKeyboardAction("Enter", false), Is.EqualTo(WofInventoryKeyboardAction.OpenQuestJournal));
            Assert.That(WofInventoryRules.GetKeyboardAction("ArrowDown", true), Is.EqualTo(WofInventoryKeyboardAction.MoveQuestNext));
            Assert.That(WofInventoryRules.GetKeyboardAction("ArrowUp", true), Is.EqualTo(WofInventoryKeyboardAction.MoveQuestPrevious));
            Assert.That(WofInventoryRules.GetKeyboardAction("KeyI", true), Is.EqualTo(WofInventoryKeyboardAction.CloseQuestJournal));
            Assert.That(WofInventoryRules.GetKeyboardAction("Escape", false), Is.EqualTo(WofInventoryKeyboardAction.CloseInventory));
        }

        [Test]
        public void ControllerDpadRightShortTapOpensOnlyOnRelease()
        {
            var state = new WofInventoryControllerHoldState();
            Assert.That(WofInventoryRules.UpdateControllerInventoryHold(ref state, 10f, true, true, true), Is.EqualTo(WofInventoryShortcutAction.None));
            Assert.That(WofInventoryRules.UpdateControllerInventoryHold(ref state, 10.2f, false, true, true), Is.EqualTo(WofInventoryShortcutAction.OpenInventory));
        }

        [Test]
        public void ControllerDpadRightLongHoldDoesNotOpen()
        {
            var state = new WofInventoryControllerHoldState();
            WofInventoryRules.UpdateControllerInventoryHold(ref state, 1f, true, true, true);
            WofInventoryRules.UpdateControllerInventoryHold(ref state, 4f, true, true, true);
            Assert.That(WofInventoryRules.UpdateControllerInventoryHold(ref state, 4.1f, false, true, true), Is.EqualTo(WofInventoryShortcutAction.None));
        }

        [Test]
        public void ControllerShortcutCancelsWhenPlayerMoves()
        {
            var state = new WofInventoryControllerHoldState();
            WofInventoryRules.UpdateControllerInventoryHold(ref state, 1f, true, true, true);
            WofInventoryRules.UpdateControllerInventoryHold(ref state, 1.1f, true, false, true);
            Assert.That(WofInventoryRules.UpdateControllerInventoryHold(ref state, 1.2f, false, true, true), Is.EqualTo(WofInventoryShortcutAction.None));
        }

        [Test]
        public void IgnoreUntilReleasePreventsImmediateControllerReopen()
        {
            var state = new WofInventoryControllerHoldState();
            WofInventoryRules.MarkControllerInventoryIgnoreUntilRelease(ref state);
            Assert.That(WofInventoryRules.UpdateControllerInventoryHold(ref state, 1f, true, true, true), Is.EqualTo(WofInventoryShortcutAction.None));
            Assert.That(state.IgnoreUntilRelease, Is.True);
            WofInventoryRules.UpdateControllerInventoryHold(ref state, 1.1f, false, true, true);
            Assert.That(state.IgnoreUntilRelease, Is.False);
        }

        [Test]
        public void ControllerNavigationRepeatUsesReact260And170MillisecondCadence()
        {
            var state = new WofInventoryControllerRepeatState();
            Assert.That(WofInventoryRules.ConsumeControllerRepeat(ref state, true, 1f), Is.True);
            Assert.That(WofInventoryRules.ConsumeControllerRepeat(ref state, true, 1.25f), Is.False);
            Assert.That(WofInventoryRules.ConsumeControllerRepeat(ref state, true, 1.26f), Is.True);
            Assert.That(WofInventoryRules.ConsumeControllerRepeat(ref state, true, 1.42f), Is.False);
            Assert.That(WofInventoryRules.ConsumeControllerRepeat(ref state, true, 1.43f), Is.True);
        }

        [Test]
        public void ControllerInventoryRequiresExactReactStandstillGate()
        {
            Assert.That(WofInventoryRules.IsStandingStillForControllerInventory(true, false, false, false, false, Vector2.zero), Is.True);
            Assert.That(WofInventoryRules.IsStandingStillForControllerInventory(false, false, false, false, false, Vector2.zero), Is.False);
            Assert.That(WofInventoryRules.IsStandingStillForControllerInventory(true, true, false, false, false, Vector2.zero), Is.False);
            Assert.That(WofInventoryRules.IsStandingStillForControllerInventory(true, false, false, false, false, new Vector2(0.01f, 0f)), Is.False);
        }

        [Test]
        public void InventorySlotCountsRemainTwentySevenPlusNine()
        {
            Assert.That(WofInventoryRules.BackpackSlotCount, Is.EqualTo(27));
            Assert.That(WofInventoryRules.QuickSlotCount, Is.EqualTo(9));
        }

        private static WofSpellQuestAssignment Assignment(string npcId, string spell, string status)
        {
            return new WofSpellQuestAssignment
            {
                npcId = npcId,
                townId = "village-town",
                displayName = "Villager",
                questId = $"spellquest:{spell}",
                spell = spell,
                status = status,
                assignedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
    }
}
