using System;
using System.Collections.Generic;

namespace WOF
{
    public enum WofCommandConsoleAction
    {
        None,
        OpenInventory,
        ForageLeaves,
        ForageBerries,
        ForageRoots,
        BrewGardenDraught,
        DrinkGardenDraught
    }

    public readonly struct WofCommandSuggestion
    {
        public WofCommandSuggestion(string command, string sample, string label, params string[] aliases)
        {
            Command = command;
            Sample = sample;
            Label = label;
            Aliases = aliases ?? Array.Empty<string>();
        }

        public string Command { get; }
        public string Sample { get; }
        public string Label { get; }
        public string[] Aliases { get; }
    }

    public readonly struct WofCommandConsoleSubmission
    {
        public WofCommandConsoleSubmission(WofCommandConsoleAction action, string message)
        {
            Action = action;
            Message = message ?? string.Empty;
        }

        public WofCommandConsoleAction Action { get; }
        public string Message { get; }
    }

    public static class WofCommandConsoleRules
    {
        public const int MaximumInputLength = 90;
        public const int MaximumVisibleSuggestions = 6;

        private static readonly WofCommandSuggestion[] s_Suggestions =
        {
            new("engine", "/engine", "Engine menu", "devmenu", "placemenu", "place"),
            new("place", "/place hut-log-cabin", "Place object", "hut", "spawn", "object"),
            new("inventory", "/inventory", "Inventory", "inv", "bag", "items"),
            new("questdev", "/questdev on", "Quest dev", "npcdev", "devquests", "quest"),
            new("vclip", "/vclip on", "VCLIP", "noclip", "clip"),
            new("day", "/day", "Force day", "sun", "morning"),
            new("night", "/night", "Force night", "dark", "moon"),
            new("navrecord", "/navrecord start", "Nav record", "nav", "record", "path"),
            new("forage", "/forage leaves", "Forage", "leaves", "berries", "roots"),
            new("brew", "/brew", "Brew potion", "drink", "draught", "potion"),
            new("darrelspawnhere", "/darrelspawnhere", "Darrel spawn", "darrel", "setdarrelquestspawn")
        };

        public static IReadOnlyList<WofCommandSuggestion> Suggestions => s_Suggestions;

        public static WofCommandSuggestion[] GetSuggestions(string value, int maximumCount = MaximumVisibleSuggestions)
        {
            var normalized = (value ?? string.Empty).TrimStart('/').Trim().ToLowerInvariant();
            var tokenEnd = normalized.Length;
            for (var index = 0; index < normalized.Length; index++)
            {
                if (char.IsWhiteSpace(normalized[index]))
                {
                    tokenEnd = index;
                    break;
                }
            }
            var firstToken = tokenEnd == normalized.Length ? normalized : normalized.Substring(0, tokenEnd);
            var suggestions = new List<WofCommandSuggestion>(Math.Max(0, maximumCount));
            for (var index = 0; index < s_Suggestions.Length && suggestions.Count < maximumCount; index++)
            {
                var suggestion = s_Suggestions[index];
                if (firstToken.Length == 0 ||
                    suggestion.Command.StartsWith(firstToken, StringComparison.Ordinal) ||
                    suggestion.Sample.ToLowerInvariant().Contains(firstToken) ||
                    suggestion.Label.ToLowerInvariant().Contains(firstToken) ||
                    AliasStartsWith(suggestion.Aliases, firstToken))
                {
                    suggestions.Add(suggestion);
                }
            }
            return suggestions.ToArray();
        }

        public static WofCommandConsoleSubmission Evaluate(string commandValue)
        {
            var rawCommand = (commandValue ?? string.Empty).Trim();
            var commandParts = ParseParts(rawCommand);
            var commandName = commandParts.Length > 0 ? commandParts[0] : string.Empty;
            var normalizedCommand = commandName.ToLowerInvariant();
            var normalizedValue = commandParts.Length > 1
                ? string.Join(" ", commandParts, 1, commandParts.Length - 1).ToLowerInvariant()
                : string.Empty;

            if (!rawCommand.StartsWith("/", StringComparison.Ordinal) || normalizedCommand.Length == 0)
            {
                return new WofCommandConsoleSubmission(WofCommandConsoleAction.None, "Commands must start with /");
            }
            if (normalizedCommand == "inventory" || normalizedCommand == "inv")
            {
                return new WofCommandConsoleSubmission(WofCommandConsoleAction.OpenInventory, string.Empty);
            }
            if (normalizedCommand == "forage")
            {
                var ingredient = normalizedValue == "leaf" ? "leaves" : normalizedValue;
                return ingredient switch
                {
                    "leaves" => new WofCommandConsoleSubmission(WofCommandConsoleAction.ForageLeaves, string.Empty),
                    "berries" => new WofCommandConsoleSubmission(WofCommandConsoleAction.ForageBerries, string.Empty),
                    "roots" => new WofCommandConsoleSubmission(WofCommandConsoleAction.ForageRoots, string.Empty),
                    _ => new WofCommandConsoleSubmission(
                        WofCommandConsoleAction.None,
                        "Usage: /forage leaves, /forage berries, or /forage roots")
                };
            }
            if (normalizedCommand == "brew")
            {
                return new WofCommandConsoleSubmission(WofCommandConsoleAction.BrewGardenDraught, string.Empty);
            }
            if (normalizedCommand == "drinkpotion" || normalizedCommand == "drinkdraught" || normalizedCommand == "drink")
            {
                return new WofCommandConsoleSubmission(WofCommandConsoleAction.DrinkGardenDraught, string.Empty);
            }
            return new WofCommandConsoleSubmission(
                WofCommandConsoleAction.None,
                $"Unknown command: /{normalizedCommand}");
        }

        private static string[] ParseParts(string rawCommand)
        {
            var normalized = (rawCommand ?? string.Empty).TrimStart('/').Trim();
            return normalized.Length == 0
                ? Array.Empty<string>()
                : normalized.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool AliasStartsWith(string[] aliases, string token)
        {
            for (var index = 0; index < aliases.Length; index++)
            {
                if (aliases[index].StartsWith(token, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
