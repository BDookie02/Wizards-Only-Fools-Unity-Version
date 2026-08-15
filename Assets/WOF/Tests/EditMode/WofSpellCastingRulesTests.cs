using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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

        [Test]
        public void EveryPlayableSpellHasAStableEquippedHandVisual()
        {
            Assert.That(WofSpellLoadout.PlayableSpells, Has.Length.EqualTo(26));
            foreach (var spell in WofSpellLoadout.PlayableSpells)
            {
                var spec = WofHeldSpellPresentationRules.Get(spell);
                Assert.That(WofHeldSpellPresentationRules.GetSpriteIndex(spell), Is.EqualTo((int)spell), spell.ToString());
                if (spell == WofSpellId.ArcaneBeam)
                {
                    Assert.That(spec.Kind, Is.EqualTo(WofHeldSpellVisualKind.HandPoseOnly));
                    Assert.That(spec.ResolveSizePixels(720f), Is.Zero);
                }
                else
                {
                    Assert.That(spec.ResolveSizePixels(720f), Is.GreaterThan(0f), spell.ToString());
                }
            }

            Assert.That(
                WofHeldSpellPresentationRules.Get(WofSpellId.Fireball).Kind,
                Is.EqualTo(WofHeldSpellVisualKind.AnimatedFireball));
            Assert.That(
                WofHeldSpellPresentationRules.Get(WofSpellId.Flamethrower).Kind,
                Is.EqualTo(WofHeldSpellVisualKind.AnimatedFireball));
            Assert.That(
                WofHeldSpellPresentationRules.Get(WofSpellId.MagicGlassOrb).Kind,
                Is.EqualTo(WofHeldSpellVisualKind.MagicGlassOrb));
        }

        [Test]
        public void HeldSpellLayerIsAlwaysBehindItsHand()
        {
            var root = new GameObject("HeldSpellOcclusionRoot", typeof(RectTransform));
            try
            {
                var handObject = new GameObject("Hand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                handObject.transform.SetParent(root.transform, false);
                var spellObject = new GameObject("Spell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                spellObject.transform.SetParent(root.transform, false);
                Assert.That(spellObject.transform.GetSiblingIndex(), Is.GreaterThan(handObject.transform.GetSiblingIndex()));

                WofHud.EnsureHandOccludesHeldSpell(
                    spellObject.GetComponent<Image>(),
                    handObject.GetComponent<Image>());

                Assert.That(spellObject.transform.GetSiblingIndex(), Is.LessThan(handObject.transform.GetSiblingIndex()));
            }
            finally
            {
                Object.DestroyImmediate(root);
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
