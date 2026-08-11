using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofSpellCastingRulesTests
    {
        [TestCase(WofSpellId.IceShard)]
        [TestCase(WofSpellId.ArcaneBeam)]
        [TestCase(WofSpellId.Grab)]
        [TestCase(WofSpellId.MagicArmor)]
        [TestCase(WofSpellId.JumpBoost)]
        [TestCase(WofSpellId.SpeedBoost)]
        [TestCase(WofSpellId.MagicGlassOrb)]
        public void ReactImmediateSpellsExecuteOnCastStart(WofSpellId spell)
        {
            Assert.That(WofSpellCastingRules.GetStartMode(spell), Is.EqualTo(WofSpellCastStartMode.Immediate));
            Assert.That(WofSpellCastingRules.ShouldConsumeCooldownOnStart(spell), Is.True);
        }

        [TestCase(WofSpellId.Heal)]
        [TestCase(WofSpellId.Flamethrower)]
        public void ReactChannelSpellsRemainActiveAndSuppressRelease(WofSpellId spell)
        {
            Assert.That(WofSpellCastingRules.GetStartMode(spell), Is.EqualTo(WofSpellCastStartMode.Channel));
            Assert.That(WofSpellCastingRules.KeepsHandActiveAfterStart(spell), Is.True);
            Assert.That(WofSpellCastingRules.SuppressesReleaseEffect(spell), Is.True);
            Assert.That(WofSpellCastingRules.ShouldConsumeCooldownOnStart(spell), Is.False);
            Assert.That(WofSpellCastingRules.ShouldConsumeCooldownOnRelease(spell), Is.False);
        }

        [Test]
        public void EveryPlayableSpellHasExactlyOneReactCastPhasePlan()
        {
            Assert.That(WofSpellLoadout.PlayableSpells, Has.Length.EqualTo(26));
            foreach (var spell in WofSpellLoadout.PlayableSpells)
            {
                var startMode = WofSpellCastingRules.GetStartMode(spell);
                var consumesOnStart = WofSpellCastingRules.ShouldConsumeCooldownOnStart(spell);
                var consumesOnRelease = WofSpellCastingRules.ShouldConsumeCooldownOnRelease(spell);

                Assert.That(System.Enum.IsDefined(typeof(WofSpellCastStartMode), startMode), Is.True, spell.ToString());
                Assert.That(consumesOnStart && consumesOnRelease, Is.False, spell.ToString());
                if (startMode == WofSpellCastStartMode.ChargeForRelease)
                {
                    Assert.That(consumesOnRelease, Is.True, spell.ToString());
                }
            }
        }

        [TestCase(WofSpellId.MagicArmor)]
        [TestCase(WofSpellId.JumpBoost)]
        [TestCase(WofSpellId.SpeedBoost)]
        [TestCase(WofSpellId.MagicGlassOrb)]
        public void ReactSelfBuffsPulseWithoutLeavingAHandActive(WofSpellId spell)
        {
            Assert.That(WofSpellCastingRules.KeepsHandActiveAfterStart(spell), Is.False);
            Assert.That(WofSpellCastingRules.SuppressesReleaseEffect(spell), Is.False);
        }

        [TestCase(WofSpellId.IceShard)]
        [TestCase(WofSpellId.ArcaneBeam)]
        [TestCase(WofSpellId.Grab)]
        public void ReactImmediateAttackHandsStayActiveUntilRelease(WofSpellId spell)
        {
            Assert.That(WofSpellCastingRules.KeepsHandActiveAfterStart(spell), Is.True);
            Assert.That(WofSpellCastingRules.SuppressesReleaseEffect(spell), Is.True);
        }

        [Test]
        public void HealUsesReactContinuousTwoHealthPerSecondRate()
        {
            Assert.That(
                WofSpellCastingRules.GetHealAmount(0.25f, WofSpellRuntimeTuning.HealSpellHealPerSecond),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(WofSpellCastingRules.GetHealAmount(float.NaN, 2f), Is.Zero);
            Assert.That(WofSpellCastingRules.GetHealAmount(1f, -2f), Is.Zero);
        }

        [Test]
        public void FlamethrowerUsesReactStrictFiftyMillisecondFrameTimer()
        {
            Assert.That(WofSpellCastingRules.AdvanceFlamethrowerTimer(0f, 0.02f, out var timer), Is.False);
            Assert.That(timer, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(WofSpellCastingRules.AdvanceFlamethrowerTimer(timer, 0.03f, out timer), Is.False);
            Assert.That(timer, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(WofSpellCastingRules.AdvanceFlamethrowerTimer(timer, 0.01f, out timer), Is.True);
            Assert.That(timer, Is.Zero);
        }
    }
}
