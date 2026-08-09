using System;

namespace WOF
{
    public readonly struct WofDamageResult
    {
        public WofDamageResult(float health, float armor, float appliedToHealth, float absorbedByArmor)
        {
            Health = health;
            Armor = armor;
            AppliedToHealth = appliedToHealth;
            AbsorbedByArmor = absorbedByArmor;
        }

        public float Health { get; }
        public float Armor { get; }
        public float AppliedToHealth { get; }
        public float AbsorbedByArmor { get; }
        public bool IsDead => Health <= 0;
    }

    public static class WofDamageMath
    {
        public static WofDamageResult Apply(float health, float armor, float incomingDamage, bool bypassArmor = false)
        {
            var safeHealth = Math.Clamp(health, 0f, WofGameConstants.MaxHealth);
            var safeArmor = Math.Clamp(armor, 0f, WofGameConstants.MaxArmor);
            var damage = Math.Max(0f, incomingDamage);
            var absorbed = bypassArmor ? 0f : Math.Min(safeArmor, damage);
            var healthDamage = damage - absorbed;

            return new WofDamageResult(
                Math.Max(0f, safeHealth - healthDamage),
                Math.Max(0f, safeArmor - absorbed),
                healthDamage,
                absorbed);
        }
    }
}
