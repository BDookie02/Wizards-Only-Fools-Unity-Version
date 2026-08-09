using System;

namespace WOF
{
    public enum WofSpellId
    {
        Fireball = 0,
        SpeedBoost = 1,
        JumpBoost = 2
    }

    public static class WofSpellLoadout
    {
        public const WofSpellId ReactDefaultLeft = WofSpellId.SpeedBoost;
        public const WofSpellId ReactDefaultRight = WofSpellId.JumpBoost;
        public const float SelfBuffDurationSeconds = 12f;
        public const float SelfBuffHandChargeSeconds = 0.18f;
        public const float SpeedBoostMultiplier = 2f;
        public const float JumpBoostMultiplier = 2f;

        public static readonly WofSpellId[] PlayableSpells =
        {
            WofSpellId.Fireball,
            WofSpellId.SpeedBoost,
            WofSpellId.JumpBoost
        };

        public static string GetDisplayName(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.Fireball => "Fireball",
                WofSpellId.SpeedBoost => "Speed Boost",
                WofSpellId.JumpBoost => "Up and Over!",
                _ => throw new ArgumentOutOfRangeException(nameof(spell), spell, null)
            };
        }

        public static string GetFamilyName(WofSpellId spell)
        {
            return spell == WofSpellId.Fireball ? "DAMAGE" : "MOVEMENT";
        }

        public static bool IsValid(int value)
        {
            return value >= (int)WofSpellId.Fireball && value <= (int)WofSpellId.JumpBoost;
        }
    }
}
