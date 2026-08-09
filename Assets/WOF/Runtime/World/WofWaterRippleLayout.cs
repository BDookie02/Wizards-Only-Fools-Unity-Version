using UnityEngine;

namespace WOF
{
    public static class WofWaterRippleLayout
    {
        public const float WaterPlaneY = -0.8f;
        public const float BaseVillageRippleY = -0.79f;
        public const float RippleLifetimeSeconds = 0.5f;
        public const float DesktopSpawnIntervalSeconds = 0.4f;
        public const float MobileSpawnIntervalSeconds = 0.7f;
        public const int DesktopSegments = 32;
        public const int MobileSegments = 18;
        public const float InnerRadius = 0.8f;
        public const float OuterRadius = 1f;

        public static bool IsBaseVillageWaterRippleSpot(Vector3 position)
        {
            var radiusSquared = position.x * position.x + position.z * position.z;
            var inMoatOrOuterWater =
                (radiusSquared > 42f * 42f && radiusSquared < 58f * 58f) ||
                (radiusSquared > 125f * 125f && radiusSquared < 145f * 145f);
            return inMoatOrOuterWater && position.y < 1.15f;
        }

        public static float ResolveScale(float ageSeconds)
        {
            var age = ageSeconds / RippleLifetimeSeconds;
            return 0.5f + age * 2f;
        }

        public static float ResolveOpacity(float ageSeconds)
        {
            return Mathf.Max(0f, 1f - ageSeconds / RippleLifetimeSeconds);
        }
    }
}
