using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityEssentials
{
    internal sealed class ConsoleCommandRegistry
    {
        internal readonly struct Command
        {
            public readonly string Name;
            public readonly string Description;
            public readonly MethodInfo Method;
            public readonly bool TakesArgs;
            public readonly ParameterInfo[] Parameters;
            public readonly ConsoleTarget Target;

            public Command(string name, string description, MethodInfo method, bool takesArgs,
                ParameterInfo[] parameters, ConsoleTarget target)
            {
                Name = name;
                Description = description;
                Method = method;
                TakesArgs = takesArgs;
                Parameters = parameters;
                Target = target;
            }
        }

        private readonly Dictionary<string, Command> _commands = new(StringComparer.OrdinalIgnoreCase);

        // Sorted list for UI autocomplete; rebuilt only when the registry changes.
        private readonly List<Command> _sortedCommands = new(128);

        public IEnumerable<Command> AllCommands => _commands.Values;

        public IReadOnlyList<Command> SortedCommands => _sortedCommands;

        public void Clear()
        {
            _commands.Clear();
            _sortedCommands.Clear();
        }

        public bool TryGet(string name, out Command cmd) =>
            _commands.TryGetValue(name, out cmd);

        public void RegisterFromLoadedAssemblies()
        {
            Clear();

            foreach (var (method, attr) in PredefinedAssemblies.EnumerateRuntimeMethodsWithAttributes<ConsoleAttribute>())
            {
                if (method == null) continue;
                if (method.IsGenericMethodDefinition) continue;
                if (!TryCreateCommand(method, attr, out var cmd)) continue;
                if (!_commands.ContainsKey(cmd.Name))
                    _commands[cmd.Name] = cmd;
            }

            _sortedCommands.AddRange(_commands.Values);
            _sortedCommands.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        }

        private static bool TryCreateCommand(MethodInfo method, ConsoleAttribute attr, out Command cmd)
        {
            cmd = default;

            // Targeting applies only to instance methods. Static methods are invoked directly.
            var target = attr?.Target ?? ConsoleTarget.SceneSingle;

            var parameters = method.GetParameters();

            bool takesArgs;
            if (parameters.Length == 0)
                takesArgs = false;
            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                // Legacy mode: a single `string` parameter receives the unparsed remainder of the input line.
                takesArgs = true;
            else
            {
                // Token mode: parse arguments and bind typed parameters.
                if (!ConsoleArgsBinder.CanBindParameters(parameters))
                    return false;

                takesArgs = true;
            }

            // Return type must be `void` or `string`.
            if (method.ReturnType != typeof(void) && method.ReturnType != typeof(string))
                return false;

            var name = string.IsNullOrWhiteSpace(attr?.Name) ? method.Name : attr.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var desc = attr?.Description ?? string.Empty;
            cmd = new Command(name, desc, method, takesArgs, parameters, target);
            return true;
        }

        public bool TryExecute(string commandName, string args, out string result)
        {
            result = string.Empty;

            if (!TryGet(commandName, out var cmd))
            {
                result = $"Unknown command: {commandName}";
                return false;
            }

            try
            {
                object ret;

                var parameters = cmd.Parameters ?? Array.Empty<ParameterInfo>();

                object[] values;
                if (parameters.Length == 0)
                    values = null;
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    // Legacy mode: pass the raw argument string.
                    values = new object[] { args ?? string.Empty };
                else
                {
                    var bind = ConsoleArgsBinder.Bind(args ?? string.Empty, parameters);
                    if (!bind.Ok)
                    {
                        result = bind.Error;
                        return false;
                    }

                    values = bind.Values;
                }

                // Static command: invoke without resolving a target instance.
                if (cmd.Method.IsStatic)
                {
                    ret = cmd.Method.Invoke(null, values);
                    result = ret as string ?? string.Empty;
                    return true;
                }

                // Resolve the target instance at invocation time.
                if (!TryResolveTargets(cmd, out var targets, out var resolveError))
                {
                    result = resolveError;
                    return false;
                }

                if (cmd.Target == ConsoleTarget.SceneAll)
                {
                    for (var i = 0; i < targets.Length; i++)
                        cmd.Method.Invoke(targets[i], values);

                    // For fan-out execution, ignore return values to keep output consistent.
                    result = string.Empty;
                    return true;
                }

                ret = cmd.Method.Invoke(targets[0], values);
                result = ret as string ?? string.Empty;
                return true;
            }
            catch (TargetInvocationException e)
            {
                Debug.LogException(e.InnerException ?? e);
                result = (e.InnerException ?? e).Message;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = e.Message;
                return false;
            }
        }

        private static bool TryResolveTargets(Command cmd, out object[] targets, out string error)
        {
            targets = Array.Empty<object>();
            error = string.Empty;

            var t = cmd.Method.DeclaringType;
            if (t == null)
            {
                error = "Invalid command target type";
                return false;
            }

            if (cmd.Target == ConsoleTarget.Singleton)
            {
                var prop = t.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
                if (prop == null)
                {
                    error = $"{t.Name} has no public static Instance property (required for Singleton target)";
                    return false;
                }

                var inst = prop.GetValue(null);
                if (inst == null)
                {
                    error = $"{t.Name}.Instance is null";
                    return false;
                }

                if (!t.IsInstanceOfType(inst))
                {
                    error = $"{t.Name}.Instance is not an instance of {t.Name}";
                    return false;
                }

                targets = new[] { inst };
                return true;
            }

            // Scene lookup requires a `UnityEngine.Object`-derived type.
            if (!typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                error = $"{t.Name} is not a UnityEngine.Object, cannot resolve via scene lookup";
                return false;
            }

            var found = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Exclude);
            if (found == null || found.Length == 0)
            {
                error = $"No instance of {t.Name} found in loaded scenes";
                return false;
            }

            if (cmd.Target == ConsoleTarget.SceneSingle)
            {
                if (found.Length != 1)
                {
                    error = $"Expected exactly one {t.Name} instance, found {found.Length}";
                    return false;
                }

                targets = new object[] { found[0] };
                return true;
            }

            // `SceneAll`: return all matching instances.
            targets = new object[found.Length];
            for (var i = 0; i < found.Length; i++)
                targets[i] = found[i];

            return true;
        }
    }
}