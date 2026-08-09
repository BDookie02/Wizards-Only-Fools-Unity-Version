using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofGameplayHudTests
    {
        [TestCase(100f, 100, "100/100")]
        [TestCase(49.6f, 100, "50/100")]
        [TestCase(-3f, 100, "0/100")]
        [TestCase(120f, 100, "100/100")]
        public void VitalLabelsMatchReactValueOverMaximumFormat(float value, int maximum, string expected)
        {
            Assert.That(WofHud.FormatVitalValue(value, maximum), Is.EqualTo(expected));
        }

        [TestCase("Fireball", "FIREBALL")]
        [TestCase("  healing crystals  ", "HEALING CRYSTALS")]
        [TestCase("", "NO MANA")]
        public void SpellLabelsUseCanonicalHudCapitalization(string value, string expected)
        {
            Assert.That(WofHud.NormalizeSpellLabel(value), Is.EqualTo(expected));
        }
    }
}
