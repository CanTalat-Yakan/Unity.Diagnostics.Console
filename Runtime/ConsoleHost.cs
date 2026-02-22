using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityEssentials
{
    public sealed class ConsoleHost : GlobalSingleton<ConsoleHost>
    {
        public static bool Enabled { get; set; } = false;
        public Key ToggleKey = Key.F1;

        public static bool DemoWindow = false;

        internal static readonly ConsoleData Data = new();
        internal static readonly ConsoleCommandRegistry Commands = new();

        private static readonly ConcurrentQueue<(string Condition, string StackTrace, LogType Type)> s_logQueue = new();

        private bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize() =>
            Commands.RegisterFromLoadedAssemblies();

        private void OnEnable() =>
            EnsureHooked();

        private void OnDisable() =>
            Unhook();

        protected override void OnDestroy()
        {
            Unhook();
            base.OnDestroy();
        }

        private void Update()
        {
            using var scope = ImGuiScope.TryEnter();
            if (scope.Active && DemoWindow)
                ImGuiNET.ImGui.ShowDemoWindow();
            
            if(Keyboard.current != null)
                if(ToggleKey != Key.None && Keyboard.current[ToggleKey].wasPressedThisFrame)
                    Enabled = !Enabled;

            if (!Enabled)
                return;

            DrainLogsIntoBuffer();

            ConsoleImGui.DrawImGui();
        }

        private void EnsureHooked()
        {
            if (_hooked)
                return;

            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked)
                return;

            Application.logMessageReceivedThreaded -= OnLog;
            _hooked = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type) =>
            s_logQueue.Enqueue((condition, stackTrace, type));

        private static void DrainLogsIntoBuffer()
        {
            // Resize if MaxEntries changed.
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

            // Echo the command.
            Print($"> {trimmed}");

            if (!string.IsNullOrWhiteSpace(result))
                Print(result);
            else if (!ok)
                PrintError($"Unknown command: {cmdName}");

            return ok;
        }
    }
}