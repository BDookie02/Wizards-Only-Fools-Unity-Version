using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    public enum WofEnginePlaceableCategory
    {
        Huts,
        Village,
        Props,
        Nature,
        Training,
        Magic
    }

    public enum WofEnginePlaceableYawMode
    {
        Player,
        Random,
        Fixed
    }

    public sealed class WofEnginePlaceableDefinition
    {
        internal WofEnginePlaceableDefinition(
            string id,
            string name,
            WofEnginePlaceableCategory category,
            string description,
            string baseColor,
            string accentColor,
            string highlightColor,
            float footprintRadius,
            float maxSlopeDelta,
            float heightOffset,
            WofEnginePlaceableYawMode yawMode,
            params string[] tags)
        {
            Id = id;
            Name = name;
            Category = category;
            Description = description;
            BaseColor = ParseColor(baseColor);
            AccentColor = ParseColor(accentColor);
            HighlightColor = ParseColor(highlightColor);
            FootprintRadius = footprintRadius;
            MaxSlopeDelta = maxSlopeDelta;
            HeightOffset = heightOffset;
            YawMode = yawMode;
            Tags = tags ?? Array.Empty<string>();
            SearchText = $"{id} {name} {description} {string.Join(" ", Tags)}".ToLowerInvariant();
        }

        public string Id { get; }
        public string Name { get; }
        public WofEnginePlaceableCategory Category { get; }
        public string Description { get; }
        public Color BaseColor { get; }
        public Color AccentColor { get; }
        public Color HighlightColor { get; }
        public float FootprintRadius { get; }
        public float MaxSlopeDelta { get; }
        public float HeightOffset { get; }
        public WofEnginePlaceableYawMode YawMode { get; }
        public IReadOnlyList<string> Tags { get; }
        internal string SearchText { get; }

        private static Color ParseColor(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.white;
        }
    }

    public static class WofEnginePlaceableCatalog
    {
        public const int MaximumPlacedObjects = 64;
        public const int MaximumSaveSlots = 6;

        private static readonly WofEnginePlaceableDefinition[] Definitions =
        {
            new("hut-mushroom-red", "Red Cap Mushroom Hut", WofEnginePlaceableCategory.Huts,
                "Rounded village hut with a red cap roof.", "#f3efe2", "#db0f27", "#ffffff",
                8f, 1.8f, 0f, WofEnginePlaceableYawMode.Player, "hut", "mushroom", "village", "home"),
            new("hut-mushroom-lavender", "Lavender Mushroom Hut", WofEnginePlaceableCategory.Huts,
                "Soft purple mushroom hut variation.", "#f3efe2", "#b57edc", "#ffffff",
                8f, 1.8f, 0f, WofEnginePlaceableYawMode.Player, "hut", "mushroom", "village", "home"),
            new("hut-grass-mound", "Grass Mound Hut", WofEnginePlaceableCategory.Huts,
                "Low rounded hut built into a grass mound.", "#5c4033", "#5f8f3d", "#b88a4a",
                10f, 1.4f, 0f, WofEnginePlaceableYawMode.Player, "hut", "grass", "earth", "home"),
            new("hut-log-cabin", "Log Cabin Hut", WofEnginePlaceableCategory.Huts,
                "Simple squared log hut with readable doorway.", "#6b4423", "#8a5a2c", "#3a2517",
                10f, 1.25f, 0f, WofEnginePlaceableYawMode.Player, "hut", "cabin", "wood", "home"),
            new("hut-dirt-grass-roof", "Dirt Hut Grass Roof", WofEnginePlaceableCategory.Huts,
                "Dirt-wall hut with a flat grassy roof.", "#5a3b22", "#6fa24f", "#2c1b12",
                10f, 1.25f, 0f, WofEnginePlaceableYawMode.Player, "hut", "dirt", "grass", "home"),
            new("mountain-cabin", "Mountain Cabin", WofEnginePlaceableCategory.Village,
                "Steep-roof cabin sized for highland villages.", "#7a4d2d", "#c0894f", "#d9d7c5",
                11f, 2.2f, 0f, WofEnginePlaceableYawMode.Player, "mountain", "cabin", "village"),
            new("swamp-treehouse-platform", "Swamp Treehouse Platform", WofEnginePlaceableCategory.Village,
                "Raised platform for swamp village staging.", "#4f3a23", "#7f5f36", "#315c35",
                13f, 2f, 2.2f, WofEnginePlaceableYawMode.Player, "swamp", "treehouse", "platform", "village"),
            new("campfire-small", "Small Campfire", WofEnginePlaceableCategory.Props,
                "Compact campfire prop for village staging.", "#3a2515", "#ff8a1d", "#ffd166",
                3f, 1.2f, 0f, WofEnginePlaceableYawMode.Random, "campfire", "prop", "light"),
            new("bush-round", "Round Bush", WofEnginePlaceableCategory.Nature,
                "Low round bush for readable scene dressing.", "#244a24", "#4f8730", "#78b94f",
                3f, 1.8f, 0f, WofEnginePlaceableYawMode.Random, "bush", "nature", "foliage"),
            new("training-spell-dummy", "Spell Dummy", WofEnginePlaceableCategory.Training,
                "Combat test target for spell damage and aim checks.", "#7f1d1d", "#facc15", "#111827",
                4f, 1.4f, 0f, WofEnginePlaceableYawMode.Player, "dummy", "combat", "qa", "training"),
            new("magic-portal-marker", "Portal Marker", WofEnginePlaceableCategory.Magic,
                "Readable magical placement marker using the portal sprite.", "#172033", "#38bdf8", "#c084fc",
                5f, 1.5f, 0f, WofEnginePlaceableYawMode.Fixed, "portal", "magic", "marker"),
            new("spellbook-pedestal", "Spellbook Pedestal", WofEnginePlaceableCategory.Magic,
                "Pedestal marker for spell and quest menu staging.", "#2e1a47", "#facc15", "#93c5fd",
                4f, 1.2f, 0f, WofEnginePlaceableYawMode.Player, "spellbook", "magic", "pedestal")
        };

        private static readonly WofEnginePlaceableCategory[] Categories =
        {
            WofEnginePlaceableCategory.Huts,
            WofEnginePlaceableCategory.Village,
            WofEnginePlaceableCategory.Props,
            WofEnginePlaceableCategory.Nature,
            WofEnginePlaceableCategory.Training,
            WofEnginePlaceableCategory.Magic
        };

        public static IReadOnlyList<WofEnginePlaceableDefinition> All => Definitions;
        public static IReadOnlyList<WofEnginePlaceableCategory> OrderedCategories => Categories;

        public static WofEnginePlaceableDefinition Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (var index = 0; index < Definitions.Length; index++)
            {
                if (Definitions[index].Id.Equals(id, StringComparison.Ordinal)) return Definitions[index];
            }
            return null;
        }

        public static string GetCategoryLabel(WofEnginePlaceableCategory category)
        {
            return category switch
            {
                WofEnginePlaceableCategory.Huts => "Huts",
                WofEnginePlaceableCategory.Village => "Village",
                WofEnginePlaceableCategory.Props => "Props",
                WofEnginePlaceableCategory.Nature => "Nature",
                WofEnginePlaceableCategory.Training => "Training",
                WofEnginePlaceableCategory.Magic => "Magic",
                _ => category.ToString()
            };
        }

        public static int GetDefaultGridSize(WofEnginePlaceableDefinition definition)
        {
            return definition.Category is WofEnginePlaceableCategory.Props or
                WofEnginePlaceableCategory.Nature or WofEnginePlaceableCategory.Magic ? 1 : 2;
        }

        public static void GetBuildingMetrics(
            WofEnginePlaceableDefinition definition,
            out float bodyWidth,
            out float bodyDepth,
            out float bodyHeight,
            out float roofHeight,
            out int roofSegments)
        {
            var mushroom = definition.Id.Contains("mushroom", StringComparison.Ordinal);
            bodyWidth = definition.Category == WofEnginePlaceableCategory.Village ? 8.8f : 7.2f;
            bodyDepth = definition.Category == WofEnginePlaceableCategory.Village ? 9.8f : 7.6f;
            bodyHeight = definition.Id.Contains("grass-roof", StringComparison.Ordinal) ? 5.8f : 6.8f;
            roofHeight = mushroom ? 3.4f : 2.6f;
            roofSegments = mushroom ? 20 : 4;
        }
    }
}
