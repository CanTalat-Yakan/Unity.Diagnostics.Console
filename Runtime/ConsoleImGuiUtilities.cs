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
    }
}
