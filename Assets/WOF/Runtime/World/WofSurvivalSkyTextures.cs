using UnityEngine;

namespace WOF
{
    internal static class WofSurvivalSkyTextures
    {
        private static readonly Vector3[] MoonCraters =
        {
            new(-8f, -7f, 5f), new(7f, 4f, 4f), new(-2f, 11f, 3f), new(9f, -11f, 3f)
        };

        private static readonly Vector4[] CloudEllipses =
        {
            new(44f, 50f, 50f, 23f), new(76f, 37f, 62f, 31f), new(121f, 34f, 78f, 36f),
            new(165f, 41f, 66f, 29f), new(206f, 55f, 44f, 18f), new(124f, 62f, 134f, 16f)
        };

        public static Texture2D CreateSun()
        {
            const int size = 160;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(80f, 80f));
                var glow = SampleStops(distance / 78f,
                    new Color(1f, 1f, 0.961f, 1f),
                    new Color(1f, 0.882f, 0.4f, 0.96f),
                    new Color(1f, 0.561f, 0.188f, 0.5f));
                if (distance <= 34f) glow = AlphaOver(glow, new Color32(255, 247, 180, 255));
                if (distance >= 38f && distance <= 46f)
                    glow = AlphaOver(glow, new Color(1f, 0.702f, 0.255f, 0.68f));
                pixels[y * size + x] = glow;
            }
            return MakeTexture("ReactSurvivalSun", size, size, pixels, TextureWrapMode.Clamp);
        }

        public static Texture2D[] CreateMoonPhases()
        {
            var phases = new Texture2D[8];
            for (var phase = 0; phase < phases.Length; phase++) phases[phase] = CreateMoonPhase(phase);
            return phases;
        }

        private static Texture2D CreateMoonPhase(int phase)
        {
            const int size = 96;
            const float radius = 24f;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var px = x + 0.5f - 48f;
                var py = y + 0.5f - 48f;
                var distance = Mathf.Sqrt(px * px + py * py);
                var glowT = Mathf.InverseLerp(46f, 16f, distance);
                var color = new Color(0.596f, 0.698f, 0.922f, Mathf.Lerp(0f, 0.3f, glowT));
                if (distance <= 16f) color.a = Mathf.Lerp(0.3f, 0.86f, Mathf.InverseLerp(16f, 0f, distance));
                if (distance <= radius)
                {
                    var lit = IsMoonPixelLit(phase, px, py, radius);
                    var disk = lit ? (Color)new Color32(220, 228, 243, 255) : new Color(0.133f, 0.169f, 0.294f, 0.88f);
                    color = AlphaOver(color, disk);
                    if (phase != 4)
                    {
                        foreach (var crater in MoonCraters)
                        {
                            var dx = px - crater.x;
                            var dy = py + crater.y;
                            if (dx * dx + dy * dy <= crater.z * crater.z)
                                color = AlphaOver(color, new Color(0.353f, 0.412f, 0.569f, 0.22f));
                        }
                    }
                }
                pixels[y * size + x] = color;
            }
            return MakeTexture($"ReactSurvivalMoon{phase}", size, size, pixels, TextureWrapMode.Clamp);
        }

        private static bool IsMoonPixelLit(int phase, float x, float y, float radius)
        {
            if (phase == 0) return true;
            if (phase == 2) return x >= 0f;
            if (phase == 4) return false;
            if (phase == 6) return x <= 0f;
            var shadowOffset = phase switch
            {
                1 => -radius * 1.04f,
                3 => -radius * 0.28f,
                5 => radius * 0.28f,
                _ => radius * 1.04f
            };
            var sx = x - shadowOffset;
            return sx * sx + y * y > (radius + 0.6f) * (radius + 0.6f);
        }

        public static Texture2D CreateCloud()
        {
            const int width = 256;
            const int height = 96;
            var pixels = new Color[width * height];
            foreach (var ellipse in CloudEllipses)
            {
                var minX = Mathf.Max(0, Mathf.FloorToInt(ellipse.x - ellipse.z));
                var maxX = Mathf.Min(width - 1, Mathf.CeilToInt(ellipse.x + ellipse.z));
                var minY = Mathf.Max(0, Mathf.FloorToInt(ellipse.y - ellipse.w));
                var maxY = Mathf.Min(height - 1, Mathf.CeilToInt(ellipse.y + ellipse.w));
                for (var y = minY; y <= maxY; y++)
                for (var x = minX; x <= maxX; x++)
                {
                    var dx = (x + 0.5f - ellipse.x) / ellipse.z;
                    var dy = (y + 0.5f - ellipse.y) / ellipse.w;
                    if (dx * dx + dy * dy > 1f) continue;
                    var vertical = Mathf.InverseLerp(18f, 78f, y);
                    var cloud = vertical <= 0.62f
                        ? Color.Lerp(new Color(1f, 1f, 1f, 0.82f), new Color(1f, 1f, 1f, 0.72f), vertical / 0.62f)
                        : Color.Lerp(new Color(1f, 1f, 1f, 0.72f), new Color(0.722f, 0.859f, 0.922f, 0.2f), (vertical - 0.62f) / 0.38f);
                    pixels[y * width + x] = AlphaOver(pixels[y * width + x], cloud);
                }
            }

            DrawEllipse(pixels, width, height, 130f, 67f, 102f, 11f, new Color(0.576f, 0.773f, 0.839f, 0.12f));
            return MakeTexture("ReactSurvivalCloud", width, height, pixels, TextureWrapMode.Clamp);
        }

        public static Texture2D CreateStars()
        {
            const int width = 768;
            const int height = 512;
            const int count = 520;
            var pixels = new Color[width * height];
            for (var index = 0; index < count; index++)
            {
                var x = Mathf.FloorToInt(Hash01(index, count, 4330) * width);
                var y = Mathf.FloorToInt(Mathf.Pow(Hash01(index, count, 4370), 0.78f) * height);
                var brightness = 0.18f + Hash01(index, count, 4410) * 0.42f;
                var tint = Hash01(index, count, 4490);
                Color color = tint > 0.88f
                    ? new Color32(255, 218, 166, 255)
                    : tint > 0.7f ? new Color32(188, 205, 255, 255) : new Color32(255, 249, 232, 255);
                color.a = brightness;
                if (x >= 0 && x < width && y >= 0 && y < height) pixels[y * width + x] = color;
            }

            for (var cluster = 0; cluster < 34; cluster++)
            {
                var cx = Hash01(cluster, 9, 4530) * width;
                var cy = Hash01(cluster, 11, 4570) * height * 0.72f;
                var radius = 18f + Hash01(cluster, 13, 4610) * 22f;
                var minX = Mathf.Max(0, Mathf.FloorToInt(cx - 44f));
                var maxX = Mathf.Min(width - 1, Mathf.CeilToInt(cx + 44f));
                var minY = Mathf.Max(0, Mathf.FloorToInt(cy - 44f));
                var maxY = Mathf.Min(height - 1, Mathf.CeilToInt(cy + 44f));
                for (var y = minY; y <= maxY; y++)
                for (var x = minX; x <= maxX; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (distance > radius) continue;
                    var glow = new Color(0.706f, 0.8f, 1f, (1f - distance / radius) * 0.12f);
                    pixels[y * width + x] = AlphaOver(pixels[y * width + x], glow);
                }
            }
            return MakeTexture("ReactSurvivalStars", width, height, pixels, TextureWrapMode.Repeat);
        }

        public static float Hash01(float x, float z, float salt = 0f)
        {
            var value = Mathf.Sin(x * 127.1f + z * 311.7f + salt * 74.7f) * 43758.5453123f;
            return value - Mathf.Floor(value);
        }

        private static Color SampleStops(float t, Color center, Color middle, Color outer)
        {
            if (t >= 1f) return new Color(outer.r, outer.g, outer.b, 0f);
            if (t <= 0.32f) return Color.Lerp(center, middle, t / 0.32f);
            if (t <= 0.62f) return Color.Lerp(middle, outer, (t - 0.32f) / 0.3f);
            return Color.Lerp(outer, new Color(outer.r, outer.g, outer.b, 0f), (t - 0.62f) / 0.38f);
        }

        private static void DrawEllipse(Color[] pixels, int width, int height, float cx, float cy, float rx, float ry, Color color)
        {
            for (var y = Mathf.Max(0, Mathf.FloorToInt(cy - ry)); y <= Mathf.Min(height - 1, Mathf.CeilToInt(cy + ry)); y++)
            for (var x = Mathf.Max(0, Mathf.FloorToInt(cx - rx)); x <= Mathf.Min(width - 1, Mathf.CeilToInt(cx + rx)); x++)
            {
                var dx = (x + 0.5f - cx) / rx;
                var dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy <= 1f) pixels[y * width + x] = AlphaOver(pixels[y * width + x], color);
            }
        }

        private static Color AlphaOver(Color bottom, Color top)
        {
            var alpha = top.a + bottom.a * (1f - top.a);
            if (alpha <= 0.00001f) return Color.clear;
            return new Color(
                (top.r * top.a + bottom.r * bottom.a * (1f - top.a)) / alpha,
                (top.g * top.a + bottom.g * bottom.a * (1f - top.a)) / alpha,
                (top.b * top.a + bottom.b * bottom.a * (1f - top.a)) / alpha,
                alpha);
        }

        private static Texture2D MakeTexture(string name, int width, int height, Color[] pixels, TextureWrapMode wrap)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = wrap,
                anisoLevel = 0
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
