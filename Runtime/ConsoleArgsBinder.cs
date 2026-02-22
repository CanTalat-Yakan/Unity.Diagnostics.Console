using System;
using System.Globalization;
using System.Reflection;

namespace UnityEssentials
{
    internal static class ConsoleArgsBinder
    {
        public readonly struct BindResult
        {
            public readonly bool Ok;
            public readonly object[] Values;
            public readonly string Error;

            public BindResult(bool ok, object[] values, string error)
            {
                Ok = ok;
                Values = values;
                Error = error;
            }

            public static BindResult Success(object[] values) => new(true, values, string.Empty);
            public static BindResult Fail(string error) => new(false, null, error ?? "Invalid arguments");
        }

        public static bool CanBindParameters(ParameterInfo[] parameters)
        {
            if (parameters == null) return true;

            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.ParameterType == typeof(string))
                    continue;

                if (TryGetNullableUnderlyingType(p.ParameterType, out var inner))
                {
                    if (!IsSupportedNonNullableType(inner))
                        return false;
                    continue;
                }

                if (!IsSupportedNonNullableType(p.ParameterType))
                    return false;
            }

            return true;
        }

        public static BindResult Bind(string rawArgs, ParameterInfo[] parameters)
        {
            parameters ??= Array.Empty<ParameterInfo>();
            var tokens = ConsoleArgsTokenizer.Tokenize(rawArgs);

            if (parameters.Length == 0)
            {
                if (tokens.Count != 0)
                    return BindResult.Fail("This command takes no arguments");
                return BindResult.Success(Array.Empty<object>());
            }

            var values = new object[parameters.Length];
            var tokenIndex = 0;

            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var pType = p.ParameterType;
                var isLast = i == parameters.Length - 1;

                var hasToken = tokenIndex < tokens.Count;

                // If last parameter is string, let it consume the remainder for ergonomics.
                if (pType == typeof(string) && isLast)
                {
                    var remainder = ConsoleArgsTokenizer.GetRemainder(rawArgs ?? string.Empty, tokens, tokenIndex);
                    values[i] = remainder;
                    tokenIndex = tokens.Count;
                    continue;
                }

                if (!hasToken)
                {
                    if (p.HasDefaultValue)
                    {
                        values[i] = p.DefaultValue;
                        continue;
                    }

                    if (TryGetNullableUnderlyingType(pType, out _))
                    {
                        values[i] = null;
                        continue;
                    }

                    return BindResult.Fail($"Missing argument: {p.Name}");
                }

                var token = tokens[tokenIndex++];

                if (TryConvert(token, pType, out var val, out var err))
                {
                    values[i] = val;
                    continue;
                }

                return BindResult.Fail($"Invalid '{p.Name}': {err}");
            }

            if (tokenIndex < tokens.Count)
                return BindResult.Fail("Too many arguments");

            return BindResult.Success(values);
        }

        private static bool TryConvert(string token, Type targetType, out object value, out string error)
        {
            error = string.Empty;
            value = null;

            if (targetType == typeof(string))
            {
                value = token ?? string.Empty;
                return true;
            }

            if (TryGetNullableUnderlyingType(targetType, out var inner))
            {
                if (string.IsNullOrEmpty(token))
                {
                    value = null;
                    return true;
                }

                if (TryConvert(token, inner, out var innerValue, out error))
                {
                    value = innerValue;
                    return true;
                }

                value = null;
                return false;
            }

            if (targetType.IsEnum)
            {
                if (Enum.TryParse(targetType, token, ignoreCase: true, out var enumValue))
                {
                    value = enumValue;
                    return true;
                }

                error = $"expected {targetType.Name}";
                return false;
            }

            var ci = CultureInfo.InvariantCulture;
            var style = NumberStyles.Float | NumberStyles.AllowThousands;

            if (targetType == typeof(int))
            {
                if (int.TryParse(token, NumberStyles.Integer, ci, out var v)) { value = v; return true; }
                error = "expected int";
                return false;
            }

            if (targetType == typeof(float))
            {
                if (float.TryParse(token, style, ci, out var v)) { value = v; return true; }
                error = "expected float";
                return false;
            }

            if (targetType == typeof(double))
            {
                if (double.TryParse(token, style, ci, out var v)) { value = v; return true; }
                error = "expected double";
                return false;
            }

            if (targetType == typeof(long))
            {
                if (long.TryParse(token, NumberStyles.Integer, ci, out var v)) { value = v; return true; }
                error = "expected long";
                return false;
            }

            if (targetType == typeof(short))
            {
                if (short.TryParse(token, NumberStyles.Integer, ci, out var v)) { value = v; return true; }
                error = "expected short";
                return false;
            }

            if (targetType == typeof(byte))
            {
                if (byte.TryParse(token, NumberStyles.Integer, ci, out var v)) { value = v; return true; }
                error = "expected byte";
                return false;
            }

            if (targetType == typeof(bool))
            {
                if (TryParseBool(token, out var b)) { value = b; return true; }
                error = "expected bool (true/false/on/off/1/0)";
                return false;
            }

            if (targetType == typeof(uint))
            {
                if (uint.TryParse(token, NumberStyles.Integer, ci, out var v)) { value = v; return true; }
                error = "expected uint";
                return false;
            }

            if (targetType == typeof(ulong))
            {
                if (ulong.TryParse(token, NumberStyles.Integer, ci, out var v)) { value = v; return true; }
                error = "expected ulong";
                return false;
            }

            if (targetType == typeof(ushort))
            {
                if (ushort.TryParse(token, NumberStyles.Integer, ci, out var v)) { value = v; return true; }
                error = "expected ushort";
                return false;
            }

            if (targetType == typeof(sbyte))
            {
                if (sbyte.TryParse(token, NumberStyles.Integer, ci, out var v)) { value = v; return true; }
                error = "expected sbyte";
                return false;
            }

            if (targetType == typeof(decimal))
            {
                if (decimal.TryParse(token, style, ci, out var v)) { value = v; return true; }
                error = "expected decimal";
                return false;
            }

            error = $"unsupported type {targetType.Name}";
            return false;
        }

        private static bool IsSupportedNonNullableType(Type t)
        {
            if (t == typeof(string)) return true;
            if (t == typeof(int) || t == typeof(float) || t == typeof(double) || t == typeof(long) || t == typeof(short) || t == typeof(byte)) return true;
            if (t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte)) return true;
            if (t == typeof(bool) || t == typeof(decimal)) return true;
            if (t.IsEnum) return true;
            return false;
        }

        private static bool TryGetNullableUnderlyingType(Type t, out Type inner)
        {
            inner = Nullable.GetUnderlyingType(t);
            return inner != null;
        }

        private static bool TryParseBool(string token, out bool value)
        {
            if (bool.TryParse(token, out value))
                return true;

            switch ((token ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "1":
                case "on":
                case "yes":
                case "y":
                case "true":
                    value = true;
                    return true;
                case "0":
                case "off":
                case "no":
                case "n":
                case "false":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }
    }
}

