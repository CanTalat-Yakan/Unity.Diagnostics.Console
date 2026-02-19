using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace UnityEssentials
{
    public sealed class ConsoleHost : GlobalSingleton<ConsoleHost>
    {
        public static bool Enabled { get; set; } = true;

        internal static readonly ConsoleData Data = new();
        internal static readonly ConsoleCommandRegistry Commands = new();

        private static readonly ConcurrentQueue<(string Condition, string StackTrace, LogType Type)> s_logQueue = new();
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            _ = Instance;

            Commands.RegisterFromLoadedAssemblies();

            HookLogs();
        }

        private static void HookLogs()
        {
            if (s_hooked)
                return;

            s_hooked = true;
            Application.logMessageReceivedThreaded += OnLogMessageReceivedThreaded;
        }

        private static void UnhookLogs()
        {
            if (!s_hooked)
                return;

            s_hooked = false;
            Application.logMessageReceivedThreaded -= OnLogMessageReceivedThreaded;
        }

        private static void OnLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            s_logQueue.Enqueue((condition, stackTrace, type));
        }

        private void OnEnable() =>
            HookLogs();

        private void OnDisable() =>
            UnhookLogs();

        protected override void OnDestroy()
        {
            UnhookLogs();
            base.OnDestroy();
        }

        private void Update()
        {
            if (!Enabled)
                return;

            DrainLogsIntoBuffer();

            ConsoleImGui.DrawImGui();
        }

        private static void DrainLogsIntoBuffer()
        {
            // Resize if user changed max entries.
            Data.Resize(Data.Config.MaxEntries);

            while (s_logQueue.TryDequeue(out var msg))
            {
                var sev = ConsoleData.ToSeverity(msg.Type);

                var captureTrace = msg.Type == LogType.Error
                                   || msg.Type == LogType.Exception
                                   || msg.Type == LogType.Assert
                                   || (msg.Type == LogType.Warning && Data.Config.CaptureStackTracesForWarnings)
                                   || (msg.Type == LogType.Log && Data.Config.CaptureStackTracesForLogs);

                Data.Add(sev, msg.Condition, captureTrace ? msg.StackTrace : string.Empty);
            }
        }

        public static void Clear() =>
            Data.Clear();

        public static void Print(string message) =>
            Data.Add(ConsoleSeverity.Log, message ?? string.Empty, string.Empty);

        public static void PrintWarning(string message) =>
            Data.Add(ConsoleSeverity.Warning, message ?? string.Empty, string.Empty);

        public static void PrintError(string message) =>
            Data.Add(ConsoleSeverity.Error, message ?? string.Empty, string.Empty);

        public static bool TryExecuteLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            var trimmed = line.Trim();
            var space = trimmed.IndexOf(' ');
            var cmdName = space < 0 ? trimmed : trimmed.Substring(0, space);
            var args = space < 0 ? string.Empty : trimmed.Substring(space + 1);

            var ok = Commands.TryExecute(cmdName, args, out var result);

            // Echo command.
            Print($"> {trimmed}");

            if (!string.IsNullOrWhiteSpace(result))
                Print(result);
            else if (!ok)
                PrintError($"Unknown command: {cmdName}");

            return ok;
        }
    }
}