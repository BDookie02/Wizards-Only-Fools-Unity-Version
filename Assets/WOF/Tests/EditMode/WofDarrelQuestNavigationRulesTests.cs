using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofDarrelQuestNavigationRulesTests
    {
        [Test]
        public void AcceptedQuestStartsAtExactFieldMarkerWithOrderedIngredients()
        {
            var profile = AcceptedProfile();
            var target = WofDarrelQuestNavigationRules.Resolve(profile);

            Assert.That(target.Id, Is.EqualTo("-64--48:spellquest:healingcrystals:fields"));
            Assert.That(target.Label, Is.EqualTo("The Fields"));
            Assert.That(target.Detail, Is.EqualTo("Gather leaves, berries, roots for Darrel's garden draught."));
            Assert.That(target.Tone, Is.EqualTo(WofQuestNavigationTone.Field));
            Assert.That(target.Position, Is.EqualTo(new Vector3(0f, 4f, 360f)));
        }

        [Test]
        public void GatheredIngredientsThenBrewedPotionAdvanceExactMarkers()
        {
            var profile = AcceptedProfile();
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelLeavesFlag, "gathered");
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelBerriesFlag, "gathered");
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelRootsFlag, "gathered");

            var brew = WofDarrelQuestNavigationRules.Resolve(profile);
            Assert.That(brew.Id, Does.EndWith(":brew"));
            Assert.That(brew.Label, Is.EqualTo("Brew Garden Draught"));
            Assert.That(brew.Position, Is.EqualTo(new Vector3(-42f, 2.6f, -26f)));
            Assert.That(brew.Tone, Is.EqualTo(WofQuestNavigationTone.Brew));

            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelPotionFlag, "brewed");
            var drink = WofDarrelQuestNavigationRules.Resolve(profile);
            Assert.That(drink.Id, Does.EndWith(":drink"));
            Assert.That(drink.Label, Is.EqualTo("Drink Garden Draught"));
            Assert.That(drink.Position, Is.EqualTo(new Vector3(-64f, 7.45f, -49.25f)));
            Assert.That(drink.Tone, Is.EqualTo(WofQuestNavigationTone.Realm));
        }

        [Test]
        public void DrunkPotionTargetsExactReactSpiritDragonMarker()
        {
            var profile = AcceptedProfile();
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelPotionFlag, "drunk");

            var target = WofDarrelQuestNavigationRules.Resolve(profile);

            Assert.That(target.Id, Does.EndWith(":spirit-dragon"));
            Assert.That(target.Label, Is.EqualTo("Spirit Dragon"));
            Assert.That(target.Detail, Is.EqualTo("Find the sleeping dragon inside the garden house."));
            Assert.That(target.Position, Is.EqualTo(new Vector3(6154f, 43.25f, -6138f)));
            Assert.That(target.Tone, Is.EqualTo(WofQuestNavigationTone.Realm));
        }

        [Test]
        public void ReadyFlagTakesPriorityAndTargetsDarrelTurnIn()
        {
            var profile = AcceptedProfile();
            WofSpellQuestRules.SetFlag(profile, WofSpellQuestRules.DarrelPotionFlag, "drunk");
            WofSpellQuestRules.SetFlag(profile, "spellquest:healingcrystals:ready", "true");

            var target = WofDarrelQuestNavigationRules.Resolve(profile);

            Assert.That(target.Id, Does.EndWith(":turn-in"));
            Assert.That(target.Label, Is.EqualTo("Return to Darrel"));
            Assert.That(target.Detail, Is.EqualTo("Bring the Healing Crystals back to Darrel."));
            Assert.That(target.Position, Is.EqualTo(new Vector3(-64f, 7.45f, -49.25f)));
            Assert.That(target.Tone, Is.EqualTo(WofQuestNavigationTone.TurnIn));
        }

        [Test]
        public void CompletedOrUnlockedQuestHasNoActiveBeacon()
        {
            var profile = AcceptedProfile();
            WofSpellQuestRules.FindAssignment(profile, WofQuestDialogRules.DarrelNpcId).status =
                WofQuestDialogRules.QuestStatusCompleted;
            Assert.That(WofDarrelQuestNavigationRules.Resolve(profile), Is.Null);

            profile = AcceptedProfile();
            WofSpellQuestRules.AddUnlockedSpell(profile, WofSpellQuestRules.DarrelRewardSpell);
            Assert.That(WofDarrelQuestNavigationRules.Resolve(profile), Is.Null);
        }

        private static WofSurvivalProfile AcceptedProfile()
        {
            var profile = new WofSurvivalProfile
            {
                version = 1,
                playerName = "Marker QA"
            };
            WofSpellQuestRules.AcceptDarrelQuest(profile, 1000L);
            return profile;
        }
    }
}
