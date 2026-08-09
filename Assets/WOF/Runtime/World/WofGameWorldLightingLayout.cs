using UnityEngine;

namespace WOF
{
    /// <summary>
    /// Scene-independent port of the classic GameWorld.tsx light configuration.
    /// React's DirectionalLight points from [50,20,50] toward the world origin.
    /// </summary>
    public static class WofGameWorldLightingLayout
    {
        public static readonly Vector3 DirectionalLightPosition = new(50f, 20f, 50f);
        public const float ClassicAmbientIntensity = 0.4f;
        public const float ClassicDirectionalIntensity = 1.5f;
        public const float MobileAmbientIntensity = 1.25f;
        public const float MobileDirectionalIntensity = 2.55f;
        public const float MobileHemisphereIntensity = 1.18f;
        public static readonly Color MobileHemisphereSkyColor = Color.white;
        public static readonly Color MobileHemisphereGroundColor = new Color32(163, 125, 82, 255);

        public static Quaternion GetDirectionalLightRotation()
        {
            return Quaternion.LookRotation(-DirectionalLightPosition.normalized, Vector3.up);
        }

        public static Color GetClassicAmbientColor()
        {
            return Color.white * ClassicAmbientIntensity;
        }

        public static Color GetMobileAmbientSkyColor()
        {
            return AddRgb(
                ScaleRgb(Color.white, MobileAmbientIntensity),
                ScaleRgb(MobileHemisphereSkyColor, MobileHemisphereIntensity));
        }

        public static Color GetMobileAmbientGroundColor()
        {
            return AddRgb(
                ScaleRgb(Color.white, MobileAmbientIntensity),
                ScaleRgb(MobileHemisphereGroundColor, MobileHemisphereIntensity));
        }

        public static Color GetMobileAmbientEquatorColor()
        {
            var sky = GetMobileAmbientSkyColor();
            var ground = GetMobileAmbientGroundColor();
            return new Color(
                (sky.r + ground.r) * 0.5f,
                (sky.g + ground.g) * 0.5f,
                (sky.b + ground.b) * 0.5f,
                1f);
        }

        private static Color ScaleRgb(Color color, float scale)
        {
            return new Color(color.r * scale, color.g * scale, color.b * scale, 1f);
        }

        private static Color AddRgb(Color left, Color right)
        {
            return new Color(left.r + right.r, left.g + right.g, left.b + right.b, 1f);
        }
    }

}
