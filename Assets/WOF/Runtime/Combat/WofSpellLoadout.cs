using System;

namespace WOF
{
    // Numeric order is the canonical React ALL_SPELLS order. It is also the
    // multiplayer wire value, so additions must remain append-only.
    public enum WofSpellId
    {
        Fireball = 0,
        IceShard = 1,
        ArcaneBeam = 2,
        Heal = 3,
        IceSpell = 4,
        RingsOfPower = 5,
        Lightning = 6,
        SmokeBomb = 7,
        Portal = 8,
        Blink = 9,
        Grab = 10,
        Tornado = 11,
        MeteorShower = 12,
        Flamethrower = 13,
        DiscShield = 14,
        OrbShield = 15,
        Kunai = 16,
        HealingCrystals = 17,
        MagicArmor = 18,
        JumpBoost = 19,
        SpeedBoost = 20,
        TungstonBallsack = 21,
        Sleep = 22,
        Poison = 23,
        Acid = 24,
        MagicGlassOrb = 25
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
            WofSpellId.IceShard,
            WofSpellId.ArcaneBeam,
            WofSpellId.Heal,
            WofSpellId.IceSpell,
            WofSpellId.RingsOfPower,
            WofSpellId.Lightning,
            WofSpellId.SmokeBomb,
            WofSpellId.Portal,
            WofSpellId.Blink,
            WofSpellId.Grab,
            WofSpellId.Tornado,
            WofSpellId.MeteorShower,
            WofSpellId.Flamethrower,
            WofSpellId.DiscShield,
            WofSpellId.OrbShield,
            WofSpellId.Kunai,
            WofSpellId.HealingCrystals,
            WofSpellId.MagicArmor,
            WofSpellId.JumpBoost,
            WofSpellId.SpeedBoost,
            WofSpellId.TungstonBallsack,
            WofSpellId.Sleep,
            WofSpellId.Poison,
            WofSpellId.Acid,
            WofSpellId.MagicGlassOrb
        };

        public static string GetDisplayName(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.Fireball => "Fireball",
                WofSpellId.IceShard => "Biden Blast",
                WofSpellId.ArcaneBeam => "Hands",
                WofSpellId.Heal => "Heal",
                WofSpellId.IceSpell => "Plasma Flash",
                WofSpellId.RingsOfPower => "Rings of Power",
                WofSpellId.Lightning => "Chidori",
                WofSpellId.SmokeBomb => "Smoke Bomb",
                WofSpellId.Portal => "Portal",
                WofSpellId.Blink => "Blink",
                WofSpellId.Grab => "Grab",
                WofSpellId.Tornado => "Tornado",
                WofSpellId.MeteorShower => "Meteor Shower",
                WofSpellId.Flamethrower => "Flamethrower",
                WofSpellId.DiscShield => "Disc Shield",
                WofSpellId.OrbShield => "Orb Shield",
                WofSpellId.Kunai => "Kunai",
                WofSpellId.HealingCrystals => "Healing Crystals",
                WofSpellId.MagicArmor => "Magic Armor",
                WofSpellId.JumpBoost => "Up and Over!",
                WofSpellId.SpeedBoost => "Speed Boost",
                WofSpellId.TungstonBallsack => "Tungston Ballsack",
                WofSpellId.Sleep => "Sleep",
                WofSpellId.Poison => "Poison",
                WofSpellId.Acid => "Acid",
                WofSpellId.MagicGlassOrb => "Magic Glass Orb",
                _ => throw new ArgumentOutOfRangeException(nameof(spell), spell, null)
            };
        }

        public static string GetFamilyName(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.Fireball or WofSpellId.IceShard or WofSpellId.IceSpell or
                    WofSpellId.RingsOfPower or WofSpellId.Lightning or WofSpellId.Tornado or
                    WofSpellId.MeteorShower or WofSpellId.Flamethrower or WofSpellId.Kunai => "DAMAGE",
                WofSpellId.Portal or WofSpellId.Blink or WofSpellId.JumpBoost or WofSpellId.SpeedBoost => "MOVEMENT",
                WofSpellId.DiscShield or WofSpellId.OrbShield or WofSpellId.MagicArmor => "DEFENSE",
                WofSpellId.TungstonBallsack or WofSpellId.Sleep or WofSpellId.Poison or WofSpellId.Acid => "STATUS",
                WofSpellId.HealingCrystals => "QUEST",
                _ => "UTILITY"
            };
        }

        public static string GetReactId(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.IceShard => "iceshard",
                WofSpellId.ArcaneBeam => "arcanebeam",
                WofSpellId.Heal => "healspell",
                WofSpellId.IceSpell => "icespell",
                WofSpellId.RingsOfPower => "ringsofpower",
                WofSpellId.SmokeBomb => "smokebomb",
                WofSpellId.MeteorShower => "meteorshower",
                WofSpellId.DiscShield => "discshield",
                WofSpellId.OrbShield => "orbshield",
                WofSpellId.HealingCrystals => "healingcrystals",
                WofSpellId.MagicArmor => "magicarmor",
                WofSpellId.JumpBoost => "jumpboost",
                WofSpellId.SpeedBoost => "speedboost",
                WofSpellId.TungstonBallsack => "tungstonballsack",
                WofSpellId.MagicGlassOrb => "magicglassorb",
                _ => spell.ToString().ToLowerInvariant()
            };
        }

        public static bool UsesHeldVisual(WofSpellId spell)
        {
            return spell is WofSpellId.Fireball or WofSpellId.IceShard or WofSpellId.Heal or
                WofSpellId.IceSpell or WofSpellId.RingsOfPower or WofSpellId.Lightning or
                WofSpellId.SmokeBomb or WofSpellId.Portal or WofSpellId.Blink or WofSpellId.Kunai or
                WofSpellId.HealingCrystals or WofSpellId.DiscShield or WofSpellId.OrbShield;
        }

        public static bool IsValid(int value)
        {
            return value >= (int)WofSpellId.Fireball && value <= (int)WofSpellId.MagicGlassOrb;
        }
    }
}
