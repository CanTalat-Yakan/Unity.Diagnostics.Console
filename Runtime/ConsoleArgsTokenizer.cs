using System;
using System.Collections.Generic;
using System.Text;

namespace UnityEssentials
{
    internal static class ConsoleArgsTokenizer
    {
        public static List<string> Tokenize(string args)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(args))
                return tokens;

            var s = args;
            var sb = new StringBuilder(s.Length);
            var inQuotes = false;
            var hadAny = false;

            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (hadAny)
                    {
                        tokens.Add(sb.ToString());
                        sb.Length = 0;
                        hadAny = false;
                    }
                    continue;
                }

                hadAny = true;

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == '\\' && i + 1 < s.Length)
                {
                    var n = s[i + 1];
                    if (n == '"' || n == '\\')
                    {
                        sb.Append(n);
                        i++;
                        continue;
                    }
                }

                sb.Append(c);
            }

            if (hadAny)
                tokens.Add(sb.ToString());

            return tokens;
        }

        public static string GetRemainder(string args, IReadOnlyList<string> tokens, int startTokenIndex)
        {
            if (startTokenIndex <= 0)
                return args ?? string.Empty;
            if (tokens == null || startTokenIndex >= tokens.Count)
                return string.Empty;

            // Reconstruct remainder from tokens to keep behavior deterministic.
            // This means quotes are not preserved; we return the parsed string value.
            var sb = new StringBuilder();
            for (var i = startTokenIndex; i < tokens.Count; i++)
            {
                if (i > startTokenIndex)
                    sb.Append(' ');
                sb.Append(tokens[i]);
            }
            return sb.ToString();
        }
    }
}

