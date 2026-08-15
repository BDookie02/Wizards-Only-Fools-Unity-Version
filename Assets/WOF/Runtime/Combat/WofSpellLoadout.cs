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

    public enum WofHeldSpellVisualKind
    {
        HandPoseOnly = 0,
        ReactSprite = 1,
        AnimatedFireball = 2,
        MagicGlassOrb = 3
    }

    public readonly struct WofHeldSpellVisualSpec
    {
        public WofHeldSpellVisualSpec(
            WofHeldSpellVisualKind kind,
            float maximumSizePixels,
            float viewportHeightRatio,
            float minimumSizePixels,
            float rotationDegrees = 0f)
        {
            Kind = kind;
            MaximumSizePixels = maximumSizePixels;
            ViewportHeightRatio = viewportHeightRatio;
            MinimumSizePixels = minimumSizePixels;
            RotationDegrees = rotationDegrees;
        }

        public WofHeldSpellVisualKind Kind { get; }
        public float MaximumSizePixels { get; }
        public float ViewportHeightRatio { get; }
        public float MinimumSizePixels { get; }
        public float RotationDegrees { get; }

        public float ResolveSizePixels(float viewportHeight)
        {
            return Math.Clamp(
                viewportHeight * ViewportHeightRatio,
                MinimumSizePixels,
                MaximumSizePixels);
        }
    }

    /// <summary>
    /// The equipped-hand visual contract from the React magic-hand renderers. The
    /// React size helper multiplies its base values by 1.72 before clamping; keeping
    /// that conversion here makes every spell share the same palm anchor and scale.
    /// </summary>
    public static class WofHeldSpellPresentationRules
    {
        private const float ReactHeldSpriteScale = 1.72f;

        public static WofHeldSpellVisualSpec Get(WofSpellId spell)
        {
            if (!WofSpellLoadout.IsValid((int)spell))
            {
                throw new ArgumentOutOfRangeException(nameof(spell), spell, null);
            }

            return spell switch
            {
                // React's "Hands" spell intentionally uses the magic-hand pose itself and
                // suppresses a second held overlay (magicHandsPoseRuntime.ts).
                WofSpellId.ArcaneBeam => new WofHeldSpellVisualSpec(
                    WofHeldSpellVisualKind.HandPoseOnly, 0f, 0f, 0f),
                WofSpellId.Fireball or WofSpellId.Flamethrower =>
                    SpriteSpec(WofHeldSpellVisualKind.AnimatedFireball, 160f, 0.24f, 92f),
                WofSpellId.MagicGlassOrb =>
                    SpriteSpec(WofHeldSpellVisualKind.MagicGlassOrb, 184f, 0.27f, 98f),
                WofSpellId.Tornado => SpriteSpec(WofHeldSpellVisualKind.ReactSprite, 178f, 0.26f, 102f),
                WofSpellId.MeteorShower => SpriteSpec(WofHeldSpellVisualKind.ReactSprite, 170f, 0.25f, 98f),
                WofSpellId.DiscShield or WofSpellId.OrbShield =>
                    SpriteSpec(WofHeldSpellVisualKind.ReactSprite, 192f, 0.28f, 108f),
                WofSpellId.Grab => SpriteSpec(WofHeldSpellVisualKind.ReactSprite, 190f, 0.28f, 108f, -2f),
                WofSpellId.MagicArmor or WofSpellId.JumpBoost or WofSpellId.SpeedBoost or
                    WofSpellId.TungstonBallsack or WofSpellId.Sleep or WofSpellId.Poison or WofSpellId.Acid =>
                    SpriteSpec(WofHeldSpellVisualKind.ReactSprite, 165f, 0.25f, 96f),
                _ => SpriteSpec(WofHeldSpellVisualKind.ReactSprite, 160f, 0.24f, 92f)
            };
        }

        public static int GetSpriteIndex(WofSpellId spell)
        {
            return WofSpellLoadout.IsValid((int)spell) ? (int)spell : -1;
        }

        private static WofHeldSpellVisualSpec SpriteSpec(
            WofHeldSpellVisualKind kind,
            float baseSize,
            float heightRatio,
            float minimumSize,
            float rotationDegrees = 0f)
        {
            return new WofHeldSpellVisualSpec(
                kind,
                baseSize * ReactHeldSpriteScale,
                heightRatio * ReactHeldSpriteScale,
                minimumSize * ReactHeldSpriteScale,
                rotationDegrees);
        }
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
