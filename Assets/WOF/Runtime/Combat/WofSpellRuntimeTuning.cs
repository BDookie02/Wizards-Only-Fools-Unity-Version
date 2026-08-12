using UnityEngine;

namespace WOF
{
    public enum WofSpellRuntimeMode
    {
        Projectile,
        Hitscan,
        GroundArea,
        Self,
        Portal
    }

    /// <summary>
    /// Live values ported from the React projectile components. Values here
    /// intentionally follow the executed components instead of unused exports.
    /// </summary>
    public static class WofSpellRuntimeTuning
    {
        public const float HitscanRange = 150f;
        public const float HitscanRadius = 2.5f;
        public const float LightningRadius = 12f;
        public const float IceSpellFlashbangRadius = 40f;
        public const float IceSpellLocalOpacity = 0.4f;
        public const float IceSpellRemoteOpacity = 1f;
        public const float IceSpellFadeRatePerSecond = 1.5f;
        public const float DirectStatusRange = 48f;
        public const float DirectStatusRadius = 1.85f;
        public const float GrabRange = 40f;
        public const float GrabRadius = 1.8f;
        public const float TornadoRadius = 17f;
        public const float TornadoPullInwardSpeed = 16f;
        public const float TornadoPullSpinSpeed = 3.8f;
        public const float TornadoPullVerticalSpeed = 2.6f;
        public const int ExternalPullFrames = 15;
        public const float MeteorRadius = 15f;
        public const int MeteorCount = 5;
        public const float MeteorDelayStepSeconds = 0.24f;
        public const float MeteorDelayRandomSeconds = 0.32f;
        public const float MeteorFallDurationMinimumSeconds = 0.9f;
        public const float MeteorFallDurationRandomSeconds = 0.35f;
        public const float MeteorImpactRadiusMinimum = 3.2f;
        public const float MeteorImpactRadiusRandom = 0.5f;
        public const float MeteorTargetHeightOffset = 0.12f;
        public const float HealingCrystalRadius = 3f;
        public const float HealingCrystalHealPerSecond = 10f;
        public const float HealSpellHealPerSecond = 2f;
        public const float ToxicDamagePerSecond = 5f;
        public const float TungstonSlowMultiplier = 0.35f;
        public const float TornadoSummonDistance = 22f;
        public const float MeteorSummonDistance = 32f;
        public const float BlinkMinimumDistance = 20f;
        public const float BlinkMaximumDistance = 60f;
        public const float BlinkUpwardOffset = 10f;
        public const float GrabMinimumDistance = 4f;
        public const float GrabMaximumDistance = 36f;
        public const float GrabFollowSpeed = 18f;
        public const float GrabThrowSpeed = 42f;
        public const float GrabMinimumThrowVerticalSpeed = -18f;
        public const float GrabMaximumThrowVerticalSpeed = 26f;
        public const float GrabMaximumDurationSeconds = 6f;
        public const float KunaiPullSpeed = 60f;
        public const float KunaiPullVerticalBoost = 5f;
        public const int PortalMaximumEndpoints = 2;
        public const float PortalLifetimeSeconds = 12f;
        public const float PortalTeleportCooldownSeconds = 1f;
        public const float PortalHalfWidth = 1.6f;
        public const float PortalHalfHeight = 2.4f;
        public const float PortalHalfDepth = 1.6f;
        public const float MagicGlassOrbLockAngleRadians = 0.12f;

        public static WofSpellRuntimeMode GetMode(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.ArcaneBeam or WofSpellId.Grab or WofSpellId.TungstonBallsack =>
                    WofSpellRuntimeMode.Hitscan,
                WofSpellId.IceSpell or WofSpellId.Tornado or WofSpellId.MeteorShower or WofSpellId.Lightning or
                    WofSpellId.HealingCrystals or WofSpellId.DiscShield or WofSpellId.OrbShield =>
                    WofSpellRuntimeMode.GroundArea,
                WofSpellId.Heal or WofSpellId.Blink or WofSpellId.MagicArmor or
                    WofSpellId.JumpBoost or WofSpellId.SpeedBoost or WofSpellId.MagicGlassOrb =>
                    WofSpellRuntimeMode.Self,
                WofSpellId.Portal => WofSpellRuntimeMode.Portal,
                _ => WofSpellRuntimeMode.Projectile
            };
        }

        public static float GetSpeed(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.Fireball => 80f,
                WofSpellId.IceShard => 70f,
                WofSpellId.IceSpell => 60f,
                WofSpellId.RingsOfPower => 40f,
                WofSpellId.SmokeBomb => 45f,
                WofSpellId.Portal => 50f,
                WofSpellId.Flamethrower => 90f,
                WofSpellId.Kunai => 120f,
                WofSpellId.Sleep or WofSpellId.Poison or WofSpellId.Acid => 56f,
                _ => 0f
            };
        }

        public static float GetLifetimeSeconds(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.Fireball or WofSpellId.IceShard => 5f,
                WofSpellId.ArcaneBeam => 0.4f,
                WofSpellId.IceSpell => 0.6f,
                WofSpellId.RingsOfPower => 5f,
                WofSpellId.Lightning => 2f,
                WofSpellId.SmokeBomb => 16f,
                WofSpellId.Portal => 12f,
                WofSpellId.Blink => 0.2f,
                WofSpellId.Tornado => 8f,
                WofSpellId.MeteorShower => 7.4f,
                WofSpellId.Flamethrower => 0.4f,
                WofSpellId.DiscShield or WofSpellId.OrbShield => 10f,
                WofSpellId.Kunai => 2f,
                WofSpellId.HealingCrystals => 10f,
                WofSpellId.Sleep or WofSpellId.Poison or WofSpellId.Acid => 4.5f,
                _ => 1f
            };
        }

        public static float GetPlayerDamage(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.Fireball => 20f,
                WofSpellId.IceShard => 10f,
                WofSpellId.ArcaneBeam => 35f,
                WofSpellId.RingsOfPower => 20f,
                WofSpellId.Flamethrower => 5f,
                WofSpellId.Kunai => 15f,
                WofSpellId.MeteorShower => 18f,
                WofSpellId.Sleep => 5f,
                WofSpellId.Poison => 7f,
                WofSpellId.Acid => 9f,
                _ => 0f
            };
        }

        public static float GetStatusDurationSeconds(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.TungstonBallsack or WofSpellId.Sleep => 8f,
                WofSpellId.Poison or WofSpellId.Acid => 10f,
                _ => 0f
            };
        }

        public static float GetCastCooldownSeconds(WofSpellId spell)
        {
            return spell == WofSpellId.IceShard ? 0.4f : 1f;
        }

        public static Color GetColor(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.Fireball or WofSpellId.Flamethrower or WofSpellId.MeteorShower => new Color32(255, 82, 24, 255),
                WofSpellId.IceShard or WofSpellId.IceSpell or WofSpellId.Lightning => new Color32(125, 224, 255, 255),
                WofSpellId.ArcaneBeam or WofSpellId.RingsOfPower or WofSpellId.Portal => new Color32(177, 64, 255, 255),
                WofSpellId.Heal or WofSpellId.HealingCrystals => new Color32(88, 255, 154, 255),
                WofSpellId.SmokeBomb => new Color32(126, 132, 145, 255),
                WofSpellId.Blink or WofSpellId.MagicGlassOrb => new Color32(64, 255, 255, 255),
                WofSpellId.Grab or WofSpellId.DiscShield or WofSpellId.OrbShield => new Color32(244, 114, 255, 255),
                WofSpellId.Tornado => new Color32(209, 213, 219, 255),
                WofSpellId.Kunai => new Color32(220, 220, 220, 255),
                WofSpellId.MagicArmor => new Color32(125, 211, 252, 255),
                WofSpellId.JumpBoost => new Color32(190, 242, 100, 255),
                WofSpellId.SpeedBoost => new Color32(253, 224, 71, 255),
                WofSpellId.TungstonBallsack => new Color32(148, 163, 184, 255),
                WofSpellId.Sleep => new Color32(125, 211, 252, 255),
                WofSpellId.Poison => new Color32(168, 85, 247, 255),
                WofSpellId.Acid => new Color32(34, 197, 94, 255),
                _ => Color.white
            };
        }

        public static float GetVisualScale(WofSpellId spell)
        {
            return spell switch
            {
                WofSpellId.RingsOfPower => 4f,
                WofSpellId.Lightning => 15f,
                WofSpellId.Tornado => 12f,
                WofSpellId.MeteorShower => 15f,
                WofSpellId.DiscShield => 4.5f,
                WofSpellId.OrbShield => 5f,
                WofSpellId.HealingCrystals => 2.5f,
                WofSpellId.SmokeBomb => 3.5f,
                WofSpellId.Portal => 4.5f,
                WofSpellId.Flamethrower => 1.5f,
                WofSpellId.Kunai => 1.4f,
                _ => 3f
            };
        }
    }
}
