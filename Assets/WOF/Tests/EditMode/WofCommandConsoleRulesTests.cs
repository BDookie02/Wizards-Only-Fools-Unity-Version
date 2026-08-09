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
        }

        [Test]
        public void Constants_MatchReactInputAndSuggestionLimits()
        {
            Assert.That(WofCommandConsoleRules.MaximumInputLength, Is.EqualTo(90));
            Assert.That(WofCommandConsoleRules.MaximumVisibleSuggestions, Is.EqualTo(6));
            Assert.That(WofCommandConsoleRules.Suggestions.Count, Is.EqualTo(11));
        }
    }
}
