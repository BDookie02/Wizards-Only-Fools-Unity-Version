namespace WOF
{
    using UnityEngine;

    public enum WofSpellCastStartMode
    {
        ChargeForRelease,
        Immediate,
        Channel
    }

    /// <summary>
    /// Cast-phase ownership ported from React's playerHandCastingRuntime and
    /// PlayerController. This is intentionally separate from projectile mode:
    /// a self-targeted spell such as Blink still charges and executes on release.
    /// </summary>
    public static class WofSpellCastingRules
    {
        public const float FlamethrowerIntervalSeconds = 0.05f;

        public static WofSpellCastStartMode GetStartMode(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.IceShard or WofSpellId.ArcaneBeam or WofSpellId.Grab or
                    WofSpellId.MagicArmor or WofSpellId.JumpBoost or WofSpellId.SpeedBoost or
                    WofSpellId.MagicGlassOrb => WofSpellCastStartMode.Immediate,
                WofSpellId.Heal or WofSpellId.Flamethrower => WofSpellCastStartMode.Channel,
                _ => WofSpellCastStartMode.ChargeForRelease
            };
        }

        public static bool KeepsHandActiveAfterStart(WofSpellId spell)
        {
            return spell is not (WofSpellId.MagicArmor or WofSpellId.JumpBoost or
                WofSpellId.SpeedBoost or WofSpellId.MagicGlassOrb);
        }

        public static bool SuppressesReleaseEffect(WofSpellId spell)
        {
            return spell is WofSpellId.IceShard or WofSpellId.ArcaneBeam or
                WofSpellId.Heal or WofSpellId.Flamethrower or WofSpellId.Grab;
        }

        public static bool IsChannelSpell(WofSpellId spell)
        {
            return GetStartMode(spell) == WofSpellCastStartMode.Channel;
        }

        public static bool ShouldConsumeCooldownOnStart(WofSpellId spell)
        {
            return GetStartMode(spell) == WofSpellCastStartMode.Immediate;
        }

        public static bool ShouldConsumeCooldownOnRelease(WofSpellId spell)
        {
            return !SuppressesReleaseEffect(spell) &&
                   GetStartMode(spell) == WofSpellCastStartMode.ChargeForRelease;
        }

        public static float GetHealAmount(float deltaSeconds, float healPerSecond)
        {
            if (!float.IsFinite(deltaSeconds) || !float.IsFinite(healPerSecond) ||
                deltaSeconds <= 0f || healPerSecond <= 0f)
            {
                return 0f;
            }
            return deltaSeconds * healPerSecond;
        }

        public static bool AdvanceFlamethrowerTimer(float current, float deltaSeconds, out float next)
        {
            if (!float.IsFinite(current) || !float.IsFinite(deltaSeconds) || deltaSeconds <= 0f)
            {
                next = Mathf.Max(0f, float.IsFinite(current) ? current : 0f);
                return false;
            }
            next = Mathf.Max(0f, current) + deltaSeconds;
            if (next <= FlamethrowerIntervalSeconds) return false;
            next = 0f;
            return true;
        }
    }
}
