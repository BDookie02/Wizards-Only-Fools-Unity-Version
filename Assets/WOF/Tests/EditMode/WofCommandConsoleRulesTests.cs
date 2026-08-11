using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofCommandConsoleRulesTests
    {
        [Test]
        public void Suggestions_PreserveReactOrderAndSixItemLimit()
        {
            var suggestions = WofCommandConsoleRules.GetSuggestions("/");

            Assert.That(suggestions, Has.Length.EqualTo(6));
            Assert.That(suggestions[0].Sample, Is.EqualTo("/engine"));
            Assert.That(suggestions[1].Sample, Is.EqualTo("/place hut-log-cabin"));
            Assert.That(suggestions[2].Sample, Is.EqualTo("/inventory"));
            Assert.That(suggestions[5].Sample, Is.EqualTo("/day"));
        }

        [TestCase("/f", "/day")]
        [TestCase("/berries", "/forage leaves")]
        [TestCase("/potion", "/brew")]
        [TestCase("/noclip", "/vclip on")]
        public void Suggestions_FilterCommandsSamplesLabelsAndAliases(string value, string expectedSample)
        {
            var suggestions = WofCommandConsoleRules.GetSuggestions(value);

            Assert.That(suggestions, Is.Not.Empty);
            Assert.That(suggestions[0].Sample, Is.EqualTo(expectedSample));
        }

        [TestCase("/inventory", WofCommandConsoleAction.OpenInventory)]
        [TestCase("/inv", WofCommandConsoleAction.OpenInventory)]
        [TestCase("/forage leaf", WofCommandConsoleAction.ForageLeaves)]
        [TestCase("//FoRaGe BERRIES", WofCommandConsoleAction.ForageBerries)]
        [TestCase("/forage roots", WofCommandConsoleAction.ForageRoots)]
        [TestCase("/brew anything", WofCommandConsoleAction.BrewGardenDraught)]
        [TestCase("/drink", WofCommandConsoleAction.DrinkGardenDraught)]
        [TestCase("/drinkdraught", WofCommandConsoleAction.DrinkGardenDraught)]
        [TestCase("/drinkpotion", WofCommandConsoleAction.DrinkGardenDraught)]
        public void Evaluate_ParsesPortedReactCommands(string value, WofCommandConsoleAction expectedAction)
        {
            Assert.That(WofCommandConsoleRules.Evaluate(value).Action, Is.EqualTo(expectedAction));
        }

        [Test]
        public void Evaluate_PreservesExactReactValidationMessages()
        {
            Assert.That(WofCommandConsoleRules.Evaluate("hello").Message, Is.EqualTo("Commands must start with /"));
            Assert.That(
                WofCommandConsoleRules.Evaluate("/forage mushrooms").Message,
                Is.EqualTo("Usage: /forage leaves, /forage berries, or /forage roots"));
            Assert.That(WofCommandConsoleRules.Evaluate("/wat").Message, Is.EqualTo("Unknown command: /wat"));
            Assert.That(
                WofCommandConsoleRules.Evaluate("/vclip sideways").Message,
                Is.EqualTo("Usage: /vclip on or /vclip off"));
            Assert.That(
                WofCommandConsoleRules.Evaluate("/navrecord maybe").Message,
                Is.EqualTo("Usage: /navrecord start, stop, export, status, or clear"));
        }

        [Test]
        public void Evaluate_PreservesReactVClipToggleAndExplicitValues()
        {
            var enabled = WofCommandConsoleRules.Evaluate("/vclip", false);
            var disabled = WofCommandConsoleRules.Evaluate("/vclip off", true);

            Assert.That(enabled.Action, Is.EqualTo(WofCommandConsoleAction.SetVClipEnabled));
            Assert.That(enabled.Enabled, Is.True);
            Assert.That(enabled.Message, Is.EqualTo("VCLIP ENABLED"));
            Assert.That(disabled.Enabled, Is.False);
            Assert.That(disabled.Message, Is.EqualTo("VCLIP DISABLED"));
        }

        [TestCase("/day", WofCommandConsoleAction.ForceDay, "DAY FORCED")]
        [TestCase("/night yes", WofCommandConsoleAction.ForceNight, "NIGHT FORCED")]
        [TestCase("/day cycle", WofCommandConsoleAction.ResumeDayNightCycle, "DAY/NIGHT CYCLE RESUMED")]
        [TestCase("/night day", WofCommandConsoleAction.ResumeDayNightCycle, "DAY/NIGHT CYCLE RESUMED")]
        public void Evaluate_PreservesReactSkyOverrideActions(
            string value,
            WofCommandConsoleAction expectedAction,
            string expectedMessage)
        {
            var submission = WofCommandConsoleRules.Evaluate(value);

            Assert.That(submission.Action, Is.EqualTo(expectedAction));
            Assert.That(submission.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void Evaluate_PreservesReactNavigationAliasesAndLabel()
        {
            var start = WofCommandConsoleRules.Evaluate("/nav begin mountain route");
            var status = WofCommandConsoleRules.Evaluate("/nav");

            Assert.That(start.Action, Is.EqualTo(WofCommandConsoleAction.StartNavigationRecording));
            Assert.That(start.Value, Is.EqualTo("mountain route"));
            Assert.That(status.Action, Is.EqualTo(WofCommandConsoleAction.ShowNavigationRecordingStatus));
            Assert.That(WofCommandConsoleRules.Evaluate("/nav save").Action,
                Is.EqualTo(WofCommandConsoleAction.ExportNavigationRecording));
        }

        [TestCase("/engine")]
        [TestCase("/devmenu")]
        [TestCase("/placemenu")]
        public void Evaluate_OpensReactEngineMenuAliases(string command)
        {
            Assert.That(WofCommandConsoleRules.Evaluate(command).Action,
                Is.EqualTo(WofCommandConsoleAction.OpenEngineMenu));
        }

        [Test]
        public void Evaluate_PreservesReactPlaceCommandAndUsage()
        {
            var place = WofCommandConsoleRules.Evaluate("/place HUT-LOG-CABIN ignored");

            Assert.That(place.Action, Is.EqualTo(WofCommandConsoleAction.PlaceEngineObject));
            Assert.That(place.Value, Is.EqualTo("hut-log-cabin"));
            Assert.That(WofCommandConsoleRules.Evaluate("/place").Message,
                Is.EqualTo("Usage: /place hut-log-cabin"));
        }

        [Test]
        public void Constants_MatchReactInputAndSuggestionLimits()
        {
            Assert.That(WofCommandConsoleRules.MaximumInputLength, Is.EqualTo(90));
            Assert.That(WofCommandConsoleRules.MaximumVisibleSuggestions, Is.EqualTo(6));
            Assert.That(WofCommandConsoleRules.Suggestions.Count, Is.EqualTo(11));
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        public void CloseFocusGate_WaitsForNextFrameAndKeyboardRelease(
            bool closingFrameElapsed,
            bool keyboardInputActive,
            bool expected)
        {
            Assert.That(
                WofCommandConsoleRules.CanRestoreGameplayAfterClose(closingFrameElapsed, keyboardInputActive),
                Is.EqualTo(expected));
        }
    }
}
