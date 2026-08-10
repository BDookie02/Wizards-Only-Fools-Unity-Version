using UnityEngine;

namespace WOF
{
    public readonly struct WofManaRechargeResult
    {
        public WofManaRechargeResult(float left, float right, bool changed, WofHandSide rechargedHand)
        {
            Left = left;
            Right = right;
            Changed = changed;
            RechargedHand = rechargedHand;
        }

        public float Left { get; }
        public float Right { get; }
        public bool Changed { get; }
        public WofHandSide RechargedHand { get; }
    }

    public static class WofManaRules
    {
        public const float MaximumPower = 60f;
        public const float DecayPerSecond = 1f;
        public const double FlowerRespawnSeconds = 142d;

        public static WofManaRechargeResult RechargeMostEmpty(float left, float right)
        {
            left = Mathf.Clamp(left, 0f, MaximumPower);
            right = Mathf.Clamp(right, 0f, MaximumPower);
            if (left >= MaximumPower && right >= MaximumPower)
                return new WofManaRechargeResult(left, right, false, WofHandSide.Left);
            if (MaximumPower - left >= MaximumPower - right)
                return new WofManaRechargeResult(MaximumPower, right, true, WofHandSide.Left);
            return new WofManaRechargeResult(left, MaximumPower, true, WofHandSide.Right);
        }

        public static float Decay(float power, int elapsedWholeSeconds)
        {
            return Mathf.Max(0f, power - Mathf.Max(0, elapsedWholeSeconds) * DecayPerSecond);
        }
    }
}
