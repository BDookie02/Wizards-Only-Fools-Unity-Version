using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofLaunchRulesTests
    {
        [Test]
        public void SanitizePlayerNameMatchesReactLaunchRules()
        {
            Assert.That(
                WofLaunchRules.SanitizePlayerName("  Wiz\u0001ard!!   Foo "),
                Is.EqualTo("Wizard Foo"));
            Assert.That(
                WofLaunchRules.SanitizePlayerName("Mage_One - Two"),
                Is.EqualTo("Mage_One - Two"));
        }

        [Test]
        public void SanitizePlayerNameStopsAtEighteenCharacters()
        {
            var result = WofLaunchRules.SanitizePlayerName("1234567890123456789");

            Assert.That(result, Is.EqualTo("123456789012345678"));
            Assert.That(result.Length, Is.EqualTo(18));
        }

        [TestCase(-1, 5, 4)]
        [TestCase(5, 5, 0)]
        [TestCase(11, 5, 1)]
        [TestCase(4, 0, 0)]
        public void WrapOptionIndexCyclesBothDirections(int index, int count, int expected)
        {
            Assert.That(WofLaunchRules.WrapOptionIndex(index, count), Is.EqualTo(expected));
        }

        [TestCase("floppy-wizard", "Floppy Wizard")]
        [TestCase("hair_color", "Hair Color")]
        [TestCase("shortHair", "Short Hair")]
        public void FormatOptionCreatesLaunchLabels(string input, string expected)
        {
            Assert.That(WofLaunchRules.FormatOption(input), Is.EqualTo(expected));
        }
    }
}
