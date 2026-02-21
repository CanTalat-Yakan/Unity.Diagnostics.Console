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

            public Command(string name, string description, MethodInfo method, bool takesArgs)
            {
                Name = name;
                Description = description;
                Method = method;
                TakesArgs = takesArgs;
            }
        }

        private readonly Dictionary<string, Command> _commands = new(StringComparer.OrdinalIgnoreCase);

        // Sorted list for UI/autocomplete (rebuilt only when the registry changes).
        private readonly List<Command> _sortedCommands = new(128);

        public IEnumerable<Command> AllCommands => _commands.Values;

        public IReadOnlyList<Command> SortedCommands => _sortedCommands;

        public void Clear()
        {
            _commands.Clear();
            _sortedCommands.Clear();
        }

        public void RegisterFromLoadedAssemblies()
        {
            Clear();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var a = 0; a < assemblies.Length; a++)
            {
                Type[] types;
                try { types = assemblies[a].GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                catch { continue; }

                if (types == null) continue;

                for (var t = 0; t < types.Length; t++)
                {
                    var type = types[t];
                    if (type == null) continue;

                    const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                    var methods = type.GetMethods(flags);
                    for (var m = 0; m < methods.Length; m++)
                    {
                        var method = methods[m];
                        if (method.IsGenericMethodDefinition) continue;

                        var attrs = method.GetCustomAttributes<ConsoleAttribute>(inherit: false);
                        foreach (var attr in attrs)
                        {
                            if (!TryCreateCommand(method, attr, out var cmd))
                                continue;

                            // Collision policy: first one wins.
                            if (!_commands.ContainsKey(cmd.Name))
                                _commands[cmd.Name] = cmd;
                        }
                    }
                }
            }

            // Build the sorted view.
            _sortedCommands.AddRange(_commands.Values);
            _sortedCommands.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        }

        public bool TryGet(string name, out Command cmd) =>
            _commands.TryGetValue(name, out cmd);

        private static bool TryCreateCommand(MethodInfo method, ConsoleAttribute attr, out Command cmd)
        {
            cmd = default;

            if (!method.IsStatic)
                return false;

            var parameters = method.GetParameters();

            bool takesArgs;
            if (parameters.Length == 0)
            {
                takesArgs = false;
            }
            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
            {
                takesArgs = true;
            }
            else
            {
                return false;
            }

            // Allow void or string return types.
            if (method.ReturnType != typeof(void) && method.ReturnType != typeof(string))
                return false;

            var name = string.IsNullOrWhiteSpace(attr?.Name) ? method.Name : attr.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var desc = attr?.Description ?? string.Empty;
            cmd = new Command(name, desc, method, takesArgs);
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
                if (cmd.TakesArgs)
                    ret = cmd.Method.Invoke(null, new object[] { args ?? string.Empty });
                else
                    ret = cmd.Method.Invoke(null, null);

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
    }
}