using System;
using System.Text;
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
                : "Commands:\n" + string.Join("\n", list);
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
        private static void ClearConsole()
        {
            ConsoleHost.Clear();

#if UNITY_EDITOR
            // Also clear the Editor Console.
            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            var clearMethod = logEntriesType?.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            clearMethod?.Invoke(null, null);
#endif
        }

        [Console("copy", "Copies the full console log to clipboard.")]
        private static string CopyConsoleToClipboard()
        {
            var data = ConsoleHost.Data;
            if (data == null || data.Count == 0)
                return "Console is empty";

            var sb = new StringBuilder(16 * 1024);

            // Oldest -> newest
            for (var i = data.Count - 1; i >= 0; i--)
            {
                var e = data.GetNewest(i);

                sb.Append('[').Append(e.Severity).Append("] ");
                if (e.Count > 1)
                    sb.Append("(x").Append(e.Count).Append(") ");

                sb.Append(e.Message ?? string.Empty);

                if (!string.IsNullOrWhiteSpace(e.StackTrace))
                {
                    sb.AppendLine();
                    sb.AppendLine(e.StackTrace.TrimEnd());
                }

                sb.AppendLine();
            }

            var text = sb.ToString().TrimEnd();
            GUIUtility.systemCopyBuffer = text;

            return $"Copied {data.Count} entries to clipboard";
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

        [Console("quit", "Quits the application")]
        private static void Quit()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        [Console("imgui.demo", "Toggles ImGui demo window.")]
        private static void ToggleImGuiDemoWindow() =>
            ConsoleHost.DemoWindow = !ConsoleHost.DemoWindow;

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

        [Console("application.targetframerate", "Gets/sets Application.targetFrameRate")]
        private static string TargetFrameRate(int? v)
        {
            if (v == null)
                return $"Application.targetFrameRate = {Application.targetFrameRate}";

            Application.targetFrameRate = v.Value;
            return $"Application.targetFrameRate = {Application.targetFrameRate}";
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

        [Console("time.timeScale", "Gets/sets Time.timeScale.")]
        private static string TimeScale(float? v)
        {
            if (v == null)
                return $"Time.timeScale = {Time.timeScale}";

            Time.timeScale = MathF.Max(0f, v.Value);
            return $"Time.timeScale = {Time.timeScale}";
        }

        [Console("time.fixedDeltaTime", "Gets/sets Time.fixedDeltaTime.")]
        private static string FixedDeltaTime(float? v)
        {
            if (v == null)
                return $"Time.fixedDeltaTime = {Time.fixedDeltaTime}";

            Time.fixedDeltaTime = MathF.Max(0f, v.Value);
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

        [Console("scene.load", "Loads a scene by build index or name.")]
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
        
        [Console("scene.world", "Prints loaded scenes and full hierarchy into the console log.")]
        private static string SceneWorldInfo()
        {
            var maxNodes = 5000;
            var maxDepth = 32;

            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine($"Scenes loaded: {SceneManager.sceneCount}");

            var printedNodes = 0;
            var truncated = false;

            static void AppendIndent(StringBuilder b, int depth)
            {
                // 2 spaces per level
                for (var i = 0; i < depth; i++)
                    b.Append("  ");
            }

            void AppendTransformTree(Transform t, int depth)
            {
                if (t == null || truncated)
                    return;

                if (depth > maxDepth)
                {
                    AppendIndent(sb, depth);
                    sb.AppendLine("<maxDepth reached>");
                    return;
                }

                printedNodes++;
                if (printedNodes > maxNodes)
                {
                    truncated = true;
                    return;
                }

                AppendIndent(sb, depth);
                var go = t.gameObject;
                sb.Append(go != null ? go.name : "<null>");

                if (go != null && !go.activeSelf)
                    sb.Append(" [inactive]");

                sb.AppendLine();

                // Children
                var childCount = t.childCount;
                for (var i = 0; i < childCount; i++)
                    AppendTransformTree(t.GetChild(i), depth + 1);
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.IsValid())
                    continue;

                var rootCount = 0;
                try { rootCount = s.rootCount; }
                catch { }

                sb.AppendLine();
                sb.AppendLine($"[{i}] {s.name}");
                sb.AppendLine($"  buildIndex: {s.buildIndex}");
                sb.AppendLine($"  loaded: {s.isLoaded} active: {s == SceneManager.GetActiveScene()}");
                sb.AppendLine($"  roots: {rootCount}");

                GameObject[] roots = Array.Empty<GameObject>();
                try { roots = s.GetRootGameObjects(); }
                catch { }

                for (var r = 0; r < roots.Length; r++)
                {
                    if (truncated)
                        break;

                    var go = roots[r];
                    if (go == null)
                        continue;

                    AppendTransformTree(go.transform, depth: 0);
                }

                if (truncated)
                {
                    sb.AppendLine();
                    sb.AppendLine($"<output truncated after {maxNodes} nodes>");
                    break;
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}