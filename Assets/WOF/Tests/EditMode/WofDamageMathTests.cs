using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofDamageMathTests
    {
        [Test]
        public void ArmorAbsorbsDamageBeforeHealth()
        {
            var result = WofDamageMath.Apply(100, 15, 20);

            Assert.That(result.Armor, Is.EqualTo(0));
            Assert.That(result.Health, Is.EqualTo(95));
            Assert.That(result.AbsorbedByArmor, Is.EqualTo(15));
            Assert.That(result.AppliedToHealth, Is.EqualTo(5));
        }

        [Test]
        public void ToxicDamageCanBypassArmor()
        {
            var result = WofDamageMath.Apply(100, 50, 5, bypassArmor: true);

            Assert.That(result.Armor, Is.EqualTo(50));
            Assert.That(result.Health, Is.EqualTo(95));
        }

        [Test]
        public void DamageCannotDriveHealthBelowZero()
        {
            var result = WofDamageMath.Apply(10, 0, 250);

            Assert.That(result.Health, Is.EqualTo(0));
            Assert.That(result.IsDead, Is.True);
        }

        [Test]
        public void NegativeDamageDoesNothing()
        {
            var result = WofDamageMath.Apply(80, 12, -10);

            Assert.That(result.Health, Is.EqualTo(80));
            Assert.That(result.Armor, Is.EqualTo(12));
        }

        [Test]
        public void FractionalCampfireTickIsAppliedToArmorFirst()
        {
            var result = WofDamageMath.Apply(100f, 0.1f, WofBaseVillageLayout.CampfireDamagePerTick);

            Assert.That(result.Armor, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(result.Health, Is.EqualTo(99.9f).Within(0.00001f));
            Assert.That(result.AbsorbedByArmor, Is.EqualTo(0.1f).Within(0.00001f));
            Assert.That(result.AppliedToHealth, Is.EqualTo(0.1f).Within(0.00001f));
        }
    }
}
