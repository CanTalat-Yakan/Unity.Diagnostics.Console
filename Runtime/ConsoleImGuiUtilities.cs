using System;
using System.Collections.Generic;
using System.Numerics;

namespace UnityEssentials
{
    internal static class ConsoleImGuiUtilities
    {
        internal static List<int> GetVisibleIndices(ConsoleData data)
        {
            // Allocates a small list each frame; data.Count is capped and clipping keeps draw cost down.
            var indices = new List<int>(data.Count);
            for (var i = data.Count - 1; i >= 0; i--)
            {
                var entry = data.GetNewest(i);
                if (!data.PassesFilters(entry))
                    continue;

                indices.Add(i);
            }

            return indices;
        }

        internal static string GetCommandQuery(string currentLine)
        {
            if (string.IsNullOrWhiteSpace(currentLine))
                return string.Empty;

            var trimmed = currentLine.TrimStart();
            var space = trimmed.IndexOf(' ');
            return (space < 0 ? trimmed : trimmed.Substring(0, space)).Trim();
        }

        internal static string ReplaceCommandToken(string input, string command)
        {
            input ??= string.Empty;

            var leading = input.Length - input.TrimStart().Length;
            var trimmed = input.TrimStart();

            var space = trimmed.IndexOf(' ');
            var args = space < 0 ? string.Empty : trimmed.Substring(space);

            return new string(' ', leading) + command + args;
        }

        internal static Vector4? GetColor(ConsoleSeverity sev)
        {
            return sev switch
            {
                ConsoleSeverity.Warning => new Vector4(1f, 0.85f, 0.3f, 1f),
                ConsoleSeverity.Error => new Vector4(1f, 0.3f, 0.3f, 1f),
                ConsoleSeverity.Exception => new Vector4(1f, 0.3f, 1f, 1f),
                ConsoleSeverity.Assert => new Vector4(1f, 0.6f, 0.6f, 1f),
                _ => null
            };
        }

        private static bool IsCommandTokenSeparator(char c)
        {
            // Common separators used in command naming schemes.
            return c == '.'
                   || c == '_'
                   || c == '-'
                   || c == '/'
                   || c == '\\'
                   || c == ':';
        }

        internal readonly struct CommandMatch
        {
            public readonly bool IsMatch;
            public readonly bool IsPrefixMatch;
            public readonly bool IsTokenMatch;

            public CommandMatch(bool isMatch, bool isPrefixMatch, bool isTokenMatch)
            {
                IsMatch = isMatch;
                IsPrefixMatch = isPrefixMatch;
                IsTokenMatch = isTokenMatch;
            }

            public static CommandMatch None => new(false, false, false);
        }

        internal static CommandMatch MatchCommandQuery(string commandName, string query)
        {
            if (string.IsNullOrWhiteSpace(commandName) || string.IsNullOrWhiteSpace(query))
                return CommandMatch.None;

            query = query.Trim();

            // If the user starts with a token separator, show all commands.
            if (query.Length > 0 && IsCommandTokenSeparator(query[0]))
                return new CommandMatch(true, isPrefixMatch: true, isTokenMatch: false);

            // 1) Regular prefix match (completes the command from the start).
            if (commandName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return new CommandMatch(true, isPrefixMatch: true, isTokenMatch: false);

            // 2) Token-boundary match: allow matching the start of a token after separators.
            for (var j = 0; j < commandName.Length - 1; j++)
            {
                if (!IsCommandTokenSeparator(commandName[j]))
                    continue;

                var start = j + 1;
                if (start < commandName.Length
                    && commandName.AsSpan(start).StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    return new CommandMatch(true, isPrefixMatch: false, isTokenMatch: true);
            }

            return CommandMatch.None;
        }

        internal static bool MatchesCommandQuery(string commandName, string query) =>
            MatchCommandQuery(commandName, query).IsMatch;
    }
}