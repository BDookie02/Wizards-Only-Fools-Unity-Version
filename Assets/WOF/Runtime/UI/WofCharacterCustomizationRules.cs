using System;
using System.Text.RegularExpressions;

namespace WOF
{
    public static class WofCharacterCustomizationRules
    {
        private static readonly Regex HexColor = new("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant);

        public static readonly string[] ColorPresets =
        {
            "#d6cf91", "#8d5524", "#c68642", "#f1c27d", "#ffdbac",
            "#f472b6", "#60a5fa", "#22c55e", "#facc15", "#f8fafc"
        };

        public static readonly string[] TopStyles = { "simple", "robe", "vest", "tunic" };
        public static readonly string[] PantsStyles = { "pants", "shorts", "skirt", "robe" };
        public static readonly string[] ShoesStyles = { "boots", "shoes", "sandals", "barefoot" };
        public static readonly string[] HatStyles = { "none", "wizard", "floppy-wizard", "cap", "hood", "pharaoh" };
        public static readonly string[] HairStyles = { "none", "short", "bob", "spikes", "long" };
        public static readonly string[] FacialHairStyles = { "none", "mustache", "goatee", "beard" };
        public static readonly string[] EyeStyles =
        {
            "calm", "angry", "content", "dull", "sus", "sus-shadow", "terrified",
            "sad", "hard-shut", "done", "happy", "nervous", "nervous-teary"
        };
        public static readonly string[] MouthStyles = { "neutral", "smile", "frown", "open" };

        public static void Normalize(WofSurvivalProfile profile)
        {
            if (profile == null) return;
            profile.skinColor = NormalizeColor(profile.skinColor, "#d6cf91");
            profile.topColor = NormalizeColor(profile.topColor, "#7c3aed");
            profile.pantsColor = NormalizeColor(profile.pantsColor, "#334155");
            profile.shoesColor = NormalizeColor(profile.shoesColor, "#1f2937");
            profile.hatColor = NormalizeColor(profile.hatColor, profile.topColor);
            profile.hairColor = NormalizeColor(profile.hairColor, "#3f2a1d");
            profile.facialHairColor = NormalizeColor(profile.facialHairColor, profile.hairColor);
            profile.topStyle = NormalizeOption(profile.topStyle, TopStyles, "simple");
            profile.pantsStyle = NormalizeOption(profile.pantsStyle, PantsStyles, "pants");
            profile.shoesStyle = NormalizeOption(profile.shoesStyle, ShoesStyles, "boots");
            profile.hatStyle = NormalizeOption(profile.hatStyle, HatStyles, "floppy-wizard");
            profile.hairStyle = NormalizeOption(profile.hairStyle, HairStyles, "none");
            profile.facialHairStyle = NormalizeOption(profile.facialHairStyle, FacialHairStyles, "none");
            profile.eyeStyle = NormalizeOption(profile.eyeStyle, EyeStyles, "calm");
            profile.mouthStyle = NormalizeOption(profile.mouthStyle, MouthStyles, "neutral");
        }

        public static string Next(string[] options, string current, int direction)
        {
            if (options == null || options.Length == 0) return current ?? string.Empty;
            var index = Array.IndexOf(options, current);
            if (index < 0) index = 0;
            return options[(index + direction + options.Length) % options.Length];
        }

        private static string NormalizeColor(string value, string fallback)
        {
            return !string.IsNullOrWhiteSpace(value) && HexColor.IsMatch(value) ? value.ToLowerInvariant() : fallback;
        }

        private static string NormalizeOption(string value, string[] options, string fallback)
        {
            return Array.IndexOf(options, value) >= 0 ? value : fallback;
        }
    }
}
