using System;
using System.Linq;
using UnityEngine;

namespace UnityEssentials
{
    internal static class ConsoleBuiltins
    {
        [Console("help", "Lists commands. Usage: help or help <command>")]
        private static string Help(string args)
        {
            args = (args ?? string.Empty).Trim();

            if (!string.IsNullOrEmpty(args))
            {
                if (ConsoleHost.Commands.TryGet(args, out var cmd))
                {
                    var desc = string.IsNullOrWhiteSpace(cmd.Description) ? "(no description)" : cmd.Description;
                    return $"{cmd.Name} - {desc}";
                }

                return $"Unknown command: {args}";
            }

            var list = ConsoleHost.Commands.AllCommands
                .Select(c => c.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return list.Length == 0
                ? "No commands loaded"
                : "Commands: " + string.Join(", ", list);
        }

        [Console("header", "Toggles console header. Usage: header <on/off>")]
        private static void ShowConsoleHeader(bool header) => 
            ConsoleImGui.Header = header;
        
        [Console("body", "Toggles console body. Usage: body <on/off>")]
        private static void ShowConsoleBody(bool body) => 
            ConsoleImGui.Body = body;
        
        [Console("collapse", "Toggles log collapsing. Usage: collapse <on/off>")]
        private static void CollapseConsole(bool collapse) => 
            ConsoleImGui.Collapse = collapse;
        
        [Console("clear", "Clears the console log")]
        private static void Clear()
        {
            ConsoleHost.Clear();

#if UNITY_EDITOR
            // Also clear the Editor Console.
            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            var clearMethod = logEntriesType?.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            clearMethod?.Invoke(null, null);
#endif
        }

        [Console("echo", "Echoes arguments back. Usage: echo <text>")]
        private static string Echo(string args) =>
            args ?? string.Empty;
        
        [Console("log", "Logs message. Usage: log <text>")]
        private static void Log(string args) =>
            Debug.Log(args ?? string.Empty);

        [Console("timescale", "Gets/sets Time.timeScale. Usage: timescale or timescale <float>")]
        private static string TimeScale(string args)
        {
            args = (args ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(args))
                return $"Time.timeScale = {Time.timeScale}";

            if (!float.TryParse(args, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return "Invalid number";

            Time.timeScale = Mathf.Max(0f, v);
            return $"Time.timeScale = {Time.timeScale}";
        }

        [Console("gc", "Forces a GC.Collect")]
        private static void Gc()
        {
            GC.Collect();
            ConsoleHost.Print("GC.Collect() called");
        }
    }
}
