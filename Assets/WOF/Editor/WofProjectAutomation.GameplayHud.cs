using UnityEngine;
using UnityEngine.UI;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private static WofGameplayHudReferences CreateReactGameplayHud(Transform parent, Font font)
        {
            var statusBar = CreatePanel(
                "ReactGameplayStatusBar",
                parent,
                Vector2.zero,
                new Vector2(1f, 96f / 720f),
                HexColor("#3b2a45"));
            statusBar.GetComponent<Image>().raycastTarget = false;
            AddHudBevel(
                statusBar.transform,
                HexColor("#5d466e"),
                HexColor("#1c1421"),
                4f / 1280f,
                6f / 96f,
                4f / 96f);

            var grid = CreatePanel(
                "StatusGrid",
                statusBar.transform,
                new Vector2((1280f - 1180f) / (2f * 1280f), 6f / 96f),
                new Vector2(1f - (1280f - 1180f) / (2f * 1280f), 1f - 6f / 96f),
                Color.clear);
            grid.GetComponent<Image>().raycastTarget = false;

            const float padding = 6f / 1180f;
            const float gap = 10f / 1180f;
            const float column = (1f - padding * 2f - gap * 3f) / 4f;
            var panels = new GameObject[4];
            for (var index = 0; index < panels.Length; index++)
            {
                var left = padding + index * (column + gap);
                panels[index] = CreateHudInset(
                    index switch
                    {
                        0 => "GrimoirePanel",
                        1 => "SpellsPanel",
                        2 => "VitalityPanel",
                        _ => "AetherPanel"
                    },
                    grid.transform,
                    new Vector2(left, 0f),
                    new Vector2(left + column, 1f));
            }

            CreateHudText(
                "GrimoireTitle",
                panels[0].transform,
                font,
                "GRIMOIRE",
                14,
                new Vector2(0.05f, 0.70f),
                new Vector2(0.95f, 0.93f),
                HexColor("#a8a8a8"));
            CreateHudText(
                "LeftHotkeys",
                panels[0].transform,
                font,
                "<color=#FDE047>L</color>   <color=#FACC15>1</color> <color=#555555>2 3 4 5 6 7 8 9 0</color>",
                14,
                new Vector2(0.055f, 0.39f),
                new Vector2(0.945f, 0.64f),
                Color.white,
                TextAnchor.MiddleLeft);
            CreateHudText(
                "RightHotkeys",
                panels[0].transform,
                font,
                "<color=#555555>R</color>   <color=#F0ABFC>1</color> <color=#555555>2 3 4 5 6 7 8 9 0</color>",
                14,
                new Vector2(0.055f, 0.10f),
                new Vector2(0.945f, 0.35f),
                Color.white,
                TextAnchor.MiddleLeft);

            CreateHudText(
                "SpellsTitle",
                panels[1].transform,
                font,
                "SPELLS",
                14,
                new Vector2(0.05f, 0.70f),
                new Vector2(0.95f, 0.93f),
                HexColor("#a8a8a8"));
            var leftSpellText = CreateHudText(
                "LeftSpell",
                panels[1].transform,
                font,
                "L FIREBALL",
                20,
                new Vector2(0.04f, 0.39f),
                new Vector2(0.96f, 0.64f),
                HexColor("#f97316"));
            var rightSpellText = CreateHudText(
                "RightSpell",
                panels[1].transform,
                font,
                "R FIREBALL",
                20,
                new Vector2(0.04f, 0.10f),
                new Vector2(0.96f, 0.35f),
                HexColor("#f97316"));

            CreateHudText(
                "VitalityTitle",
                panels[2].transform,
                font,
                "VITALITY",
                14,
                new Vector2(0.05f, 0.74f),
                new Vector2(0.95f, 0.95f),
                HexColor("#a8a8a8"));
            var healthBar = CreateHudMeter(
                "Health",
                panels[2].transform,
                font,
                "HEALTH",
                "100/100",
                new Vector2(0.07f, 0.42f),
                new Vector2(0.93f, 0.69f),
                HexColor("#ef4444"),
                HexColor("#fecaca"));
            var armorBar = CreateHudMeter(
                "Armor",
                panels[2].transform,
                font,
                "ARMOR",
                "0/100",
                new Vector2(0.07f, 0.10f),
                new Vector2(0.93f, 0.37f),
                HexColor("#38bdf8"),
                HexColor("#e0f2fe"));

            CreateHudText(
                "AetherTitle",
                panels[3].transform,
                font,
                "AETHER",
                14,
                new Vector2(0.05f, 0.67f),
                new Vector2(0.95f, 0.92f),
                HexColor("#a8a8a8"));
            var aether = CreatePanel(
                "AetherBackground",
                panels[3].transform,
                new Vector2(0.07f, 0.22f),
                new Vector2(0.93f, 0.55f),
                Color.black);
            AddHudBevel(aether.transform, HexColor("#4a3359"), HexColor("#120c16"), 0.012f, 0.10f, 0.10f);
            var aetherFill = CreateHudFill("AetherFill", aether.transform, HexColor("#3b82f6"));

            var manaRoot = CreatePanel(
                "ManaMeter",
                parent,
                new Vector2((1280f - 400f) / (2f * 1280f), 106f / 720f),
                new Vector2(1f - (1280f - 400f) / (2f * 1280f), 143f / 720f),
                Color.clear);
            manaRoot.GetComponent<Image>().raycastTarget = false;
            CreateHudText(
                "ManaTitle",
                manaRoot.transform,
                font,
                "MANA",
                18,
                new Vector2(0f, 0.57f),
                Vector2.one,
                HexColor("#a8a8a8"));
            var manaBackground = CreatePanel(
                "ManaBackground",
                manaRoot.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.57f),
                Color.black);
            manaBackground.GetComponent<Image>().raycastTarget = false;
            var leftManaFill = CreateHudFill(
                "LeftManaFill",
                manaBackground.transform,
                HexColor("#a855f7"),
                new Vector2(0f, 0f),
                new Vector2(0.4975f, 1f));
            var rightManaFill = CreateHudFill(
                "RightManaFill",
                manaBackground.transform,
                HexColor("#a855f7"),
                new Vector2(0.5025f, 0f),
                Vector2.one);

            return new WofGameplayHudReferences
            {
                HealthFill = healthBar.Fill,
                ArmorFill = armorBar.Fill,
                HealthText = healthBar.Value,
                ArmorText = armorBar.Value,
                AetherFill = aetherFill,
                LeftManaFill = leftManaFill,
                RightManaFill = rightManaFill,
                LeftSpellText = leftSpellText,
                RightSpellText = rightSpellText
            };
        }

        private static GameObject CreateHudInset(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var panel = CreatePanel(name, parent, min, max, HexColor("#211627"));
            panel.GetComponent<Image>().raycastTarget = false;
            AddHudBevel(panel.transform, HexColor("#120c16"), HexColor("#4a3359"), 0.014f, 0.048f, 0.048f);
            return panel;
        }

        private static HudMeterReferences CreateHudMeter(
            string name,
            Transform parent,
            Font font,
            string label,
            string value,
            Vector2 min,
            Vector2 max,
            Color fillColor,
            Color textColor)
        {
            var background = CreatePanel(name + "Background", parent, min, max, Color.black);
            background.GetComponent<Image>().raycastTarget = false;
            var fill = CreateHudFill(name + "Fill", background.transform, fillColor);
            var labelText = CreateHudText(
                name + "Label",
                background.transform,
                font,
                label,
                13,
                new Vector2(0.04f, 0f),
                new Vector2(0.57f, 1f),
                textColor,
                TextAnchor.MiddleLeft);
            var valueText = CreateHudText(
                name + "Value",
                background.transform,
                font,
                value,
                13,
                new Vector2(0.57f, 0f),
                new Vector2(0.96f, 1f),
                textColor,
                TextAnchor.MiddleRight);
            return new HudMeterReferences(fill, labelText, valueText);
        }

        private static Image CreateHudFill(
            string name,
            Transform parent,
            Color color,
            Vector2? min = null,
            Vector2? max = null)
        {
            var fill = CreateImage(name, parent, null, color);
            SetRect(fill.rectTransform, min ?? Vector2.zero, max ?? Vector2.one);
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            return fill;
        }

        private static Text CreateHudText(
            string name,
            Transform parent,
            Font font,
            string value,
            int size,
            Vector2 min,
            Vector2 max,
            Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var text = CreateText(name, parent, font, value, size, anchor, color);
            SetRect(text.rectTransform, min, max);
            text.supportRichText = true;
            text.resizeTextMinSize = Mathf.Max(6, size / 2);
            return text;
        }

        private static void AddHudBevel(
            Transform parent,
            Color topLeft,
            Color bottomRight,
            float horizontalFraction,
            float topFraction,
            float bottomFraction)
        {
            var left = CreatePanel("LeftBevel", parent, Vector2.zero, new Vector2(horizontalFraction, 1f), topLeft);
            var top = CreatePanel("TopBevel", parent, new Vector2(0f, 1f - topFraction), Vector2.one, topLeft);
            var right = CreatePanel("RightBevel", parent, new Vector2(1f - horizontalFraction, 0f), Vector2.one, bottomRight);
            var bottom = CreatePanel("BottomBevel", parent, Vector2.zero, new Vector2(1f, bottomFraction), bottomRight);
            left.GetComponent<Image>().raycastTarget = false;
            top.GetComponent<Image>().raycastTarget = false;
            right.GetComponent<Image>().raycastTarget = false;
            bottom.GetComponent<Image>().raycastTarget = false;
        }

        private static Color HexColor(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out var color))
            {
                throw new System.InvalidOperationException($"Invalid HUD color {value}.");
            }
            return color;
        }

        private sealed class WofGameplayHudReferences
        {
            public Image HealthFill;
            public Image ArmorFill;
            public Text HealthText;
            public Text ArmorText;
            public Image AetherFill;
            public Image LeftManaFill;
            public Image RightManaFill;
            public Text LeftSpellText;
            public Text RightSpellText;
        }

        private readonly struct HudMeterReferences
        {
            public HudMeterReferences(Image fill, Text label, Text value)
            {
                Fill = fill;
                Label = label;
                Value = value;
            }

            public Image Fill { get; }
            public Text Label { get; }
            public Text Value { get; }
        }
    }
}
