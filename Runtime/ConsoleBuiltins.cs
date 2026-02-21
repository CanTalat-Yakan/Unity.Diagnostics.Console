using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [Console("console.enable", "Toggles the console.")]
        private static void ToggleConsole() => 
            ConsoleHost.Enabled = !ConsoleHost.Enabled;
        
        [Console("console.collapse", "Toggles log collapsing.")]
        private static void CollapseConsole() => 
            ConsoleImGui.Collapse = !ConsoleImGui.Collapse;

        [Console("console.header", "Toggles console header.")]
        private static void ToggleConsoleHeader() => 
            ConsoleImGui.Header = !ConsoleImGui.Header;

        [Console("console.body", "Toggles console body.")]
        private static void ToggleConsoleBody() => 
            ConsoleImGui.Body = !ConsoleImGui.Body;
        
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

        [Console("gc", "Forces a GC.Collect")]
        private static void Gc()
        {
            GC.Collect();
            ConsoleHost.Print("GC.Collect() called");
        }

        [Console("application.info", "Prints basic Application info.")]
        private static string ApplicationInfo()
        {
            return string.Join("\n", new[]
            {
                $"productName: {Application.productName}",
                $"version: {Application.version}",
                $"unityVersion: {Application.unityVersion}",
                $"platform: {Application.platform}",
                $"identifier: {Application.identifier}",
                $"dataPath: {Application.dataPath}",
                $"persistentDataPath: {Application.persistentDataPath}",
                $"isEditor: {Application.isEditor}",
                $"isPlaying: {Application.isPlaying}",
                $"runInBackground: {Application.runInBackground}",
                $"targetFrameRate: {Application.targetFrameRate}",
            });
        }

        [Console("application.targetframerate", "Gets/sets Application.targetFrameRate. Usage: application.targetframerate or application.targetframerate <int>")]
        private static string TargetFrameRate(string args)
        {
            args = (args ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(args))
                return $"Application.targetFrameRate = {Application.targetFrameRate}";

            if (!int.TryParse(args, out var v))
                return "Invalid integer";

            Application.targetFrameRate = v;
            return $"Application.targetFrameRate = {Application.targetFrameRate}";
        }

        [Console("application.quit", "Quits the application (no-op in Editor). Usage: application.quit or application.quit <exitCode>")]
        private static void Quit(string args)
        {
            args = (args ?? string.Empty).Trim();
            var exitCode = 0;
            if (!string.IsNullOrEmpty(args) && !int.TryParse(args, out exitCode))
            {
                ConsoleHost.Print("Invalid exit code; using 0");
                exitCode = 0;
            }

#if UNITY_EDITOR
            // Keep it a no-op in the editor to avoid accidentally stopping play mode.
            ConsoleHost.Print("Application.Quit is ignored in the Unity Editor");
#else
            Application.Quit(exitCode);
#endif
        }

        [Console("time.info", "Prints common Time values.")]
        private static string TimeInfo()
        {
            return string.Join("\n", new[]
            {
                $"timeScale: {Time.timeScale}",
                $"deltaTime: {Time.deltaTime}",
                $"unscaledDeltaTime: {Time.unscaledDeltaTime}",
                $"time: {Time.time}",
                $"unscaledTime: {Time.unscaledTime}",
                $"realtimeSinceStartup: {Time.realtimeSinceStartup}",
                $"frameCount: {Time.frameCount}",
                $"fixedDeltaTime: {Time.fixedDeltaTime}",
                $"smoothDeltaTime: {Time.smoothDeltaTime}",
            });
        }

        [Console("time.timeScale", "Gets/sets Time.timeScale. Usage: timescale or timescale <float>")]
        private static string TimeScale(string args)
        {
            args = (args ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(args))
                return $"Time.timeScale = {Time.timeScale}";

            if (!float.TryParse(args, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return "Invalid number";

            Time.timeScale = MathF.Max(0f, v);
            return $"Time.timeScale = {Time.timeScale}";
        }

        [Console("time.fixedDeltaTime", "Gets/sets Time.fixedDeltaTime. Usage: time.fixedDeltaTime or time.fixedDeltaTime <float>")]
        private static string FixedDeltaTime(string args)
        {
            args = (args ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(args))
                return $"Time.fixedDeltaTime = {Time.fixedDeltaTime}";

            if (!float.TryParse(args, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return "Invalid number";

            Time.fixedDeltaTime = MathF.Max(0f, v);
            return $"Time.fixedDeltaTime = {Time.fixedDeltaTime}";
        }

        [Console("scene.list", "Lists loaded scenes and their build indices.")]
        private static string SceneList()
        {
            if (SceneManager.sceneCount == 0)
                return "No scenes loaded";

            var lines = new string[SceneManager.sceneCount];
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                lines[i] = $"[{i}] name={s.name} buildIndex={s.buildIndex} path={s.path} loaded={s.isLoaded} active={(s == SceneManager.GetActiveScene())}";
            }

            return string.Join("\n", lines);
        }

        [Console("scene.active", "Prints the active scene.")]
        private static string SceneActive()
        {
            var s = SceneManager.GetActiveScene();
            return $"activeScene: name={s.name} buildIndex={s.buildIndex} path={s.path} loaded={s.isLoaded}";
        }

        [Console("scene.load", "Loads a scene by build index or name. Usage: scene.load <index|name>")]
        private static string SceneLoad(string args)
        {
            args = (args ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(args))
                return "Usage: scene.load <index|name>";

            if (int.TryParse(args, out var buildIndex))
            {
                SceneManager.LoadScene(buildIndex);
                return $"Loading scene index {buildIndex}...";
            }

            SceneManager.LoadScene(args);
            return $"Loading scene '{args}'...";
        }

        [Console("scene.reload", "Reloads the active scene.")]
        private static string SceneReload()
        {
            var s = SceneManager.GetActiveScene();
            SceneManager.LoadScene(s.buildIndex);
            return $"Reloading active scene (buildIndex={s.buildIndex}, name={s.name})...";
        }
    }
}