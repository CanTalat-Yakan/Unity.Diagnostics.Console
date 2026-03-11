using System;
using System.Collections.Generic;

namespace UnityEssentials
{
    public enum ConsoleInputNavigationMode
    {
        Suggestions,
        History
    }

    public sealed class ConsoleInputState
    {
        public string Input = string.Empty;
        public string CurrentSuggestion = string.Empty;
        public readonly List<ConsoleCommandRegistry.Command> Suggestions = new(16);
        public int SuggestionIndex = -1;
        public readonly List<string> History = new(32);
        public int HistoryIndex = -1;

        // Runtime ImGui uses these to avoid rebuilding suggestions too often.
        public bool UserEdited;
        public string LastQuery = string.Empty;
    }

    public static class ConsoleInputShared
    {
        public const int DefaultMaxHistoryEntries = 50;

        public static void PushHistory(List<string> history, string line, int maxEntries = DefaultMaxHistoryEntries)
        {
            if (history == null)
                throw new ArgumentNullException(nameof(history));

            if (string.IsNullOrWhiteSpace(line))
                return;

            if (history.Count == 0 || !string.Equals(history[^1], line, StringComparison.Ordinal))
                history.Add(line);

            while (history.Count > maxEntries)
                history.RemoveAt(0);
        }

        public static ConsoleInputNavigationMode ResolveNavigationMode(string query, int suggestionCount, int suggestionIndex)
        {
            if (string.IsNullOrWhiteSpace(query))
                return ConsoleInputNavigationMode.History;

            if (suggestionCount > 0 && suggestionIndex >= 0)
                return ConsoleInputNavigationMode.Suggestions;

            return ConsoleInputNavigationMode.History;
        }

        public static bool ShouldShowAllSuggestions(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return false;

            var first = query[0];
            var last = query[query.Length - 1];
            return ConsoleUtilities.IsCommandTokenSeparator(first)
                   || ConsoleUtilities.IsCommandTokenSeparator(last);
        }

        public static void RebuildSuggestions(IReadOnlyList<ConsoleCommandRegistry.Command> commands, string query,
            List<ConsoleCommandRegistry.Command> suggestions)
        {
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));
            if (suggestions == null)
                throw new ArgumentNullException(nameof(suggestions));

            suggestions.Clear();

            if (string.IsNullOrWhiteSpace(query))
                return;

            if (ShouldShowAllSuggestions(query))
            {
                for (var i = 0; i < commands.Count; i++)
                    suggestions.Add(commands[i]);

                return;
            }

            for (var i = 0; i < commands.Count; i++)
            {
                var cmd = commands[i];
                if (string.Equals(cmd.Name, query, StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = ConsoleUtilities.MatchCommandQuery(cmd.Name, query);
                if (match.IsPrefixMatch)
                    suggestions.Add(cmd);
            }

            for (var i = 0; i < commands.Count; i++)
            {
                var cmd = commands[i];
                if (string.Equals(cmd.Name, query, StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = ConsoleUtilities.MatchCommandQuery(cmd.Name, query);
                if (match.IsTokenMatch && !match.IsPrefixMatch)
                    suggestions.Add(cmd);
            }
        }
    }
}
