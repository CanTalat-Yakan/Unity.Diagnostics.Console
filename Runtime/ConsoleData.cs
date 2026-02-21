using System;
using UnityEngine;

namespace UnityEssentials
{
    internal enum ConsoleSeverity
    {
        Log,
        Warning,
        Error,
        Exception,
        Assert,
    }

    internal readonly struct ConsoleEntry
    {
        public readonly int Index;
        public readonly float Time;
        public readonly int Frame;
        public readonly ConsoleSeverity Severity;
        public readonly string Message;
        public readonly string StackTrace;
        public readonly int Count;

        public ConsoleEntry(int index, float time, int frame, ConsoleSeverity severity, string message, string stackTrace, int count = 1)
        {
            Index = index;
            Time = time;
            Frame = frame;
            Severity = severity;
            Message = message;
            StackTrace = stackTrace;
            Count = count;
        }
    }

    internal sealed class ConsoleData
    {
        internal sealed class Settings
        {
            public int MaxEntries = 2000;
            public bool CaptureStackTracesForWarnings = false;
            public bool CaptureStackTracesForLogs = false;
            public bool CollapseDuplicates = true;
        }

        internal sealed class Filters
        {
            public bool ShowLog = true;
            public bool ShowWarning = true;
            public bool ShowError = true;
            public bool ShowException = true;
            public bool ShowAssert = true;

            public string Search = string.Empty;
        }

        public readonly Settings Config = new();
        public readonly Filters Filter = new();

        private ConsoleEntry[] _buffer = Array.Empty<ConsoleEntry>();
        private int _nextWrite;
        private int _count;
        private int _nextIndex;

        public int Count => _count;

        public ConsoleData() =>
            Resize(Config.MaxEntries);

        public void Resize(int maxEntries)
        {
            maxEntries = Math.Clamp(maxEntries, 32, 20000);

            if (_buffer.Length == maxEntries)
                return;

            // Keep the newest entries.
            var newBuf = new ConsoleEntry[maxEntries];
            var keep = Math.Min(_count, maxEntries);
            for (var i = 0; i < keep; i++)
            {
                var entry = GetEntryFromNewestOffset(keep - 1 - i);
                newBuf[i] = entry;
            }

            _buffer = newBuf;
            _count = keep;
            _nextWrite = keep % _buffer.Length;
        }

        public void Clear()
        {
            _nextWrite = 0;
            _count = 0;
        }

        public void Add(ConsoleSeverity severity, string message, string stackTrace) =>
            Add(severity, message, stackTrace, Config.CollapseDuplicates);

        public void Add(ConsoleSeverity severity, string message, string stackTrace, bool collapseDuplicates)
        {
            if (_buffer.Length == 0)
                Resize(Config.MaxEntries);

            if (_buffer.Length == 0)
                return;

            message ??= string.Empty;
            stackTrace ??= string.Empty;

            // If enabled, coalesce duplicates at the newest end.
            if (collapseDuplicates && _count > 0)
            {
                var newest = GetEntryFromNewestOffset(0);
                if (newest.Severity == severity
                    && string.Equals(newest.Message, message, StringComparison.Ordinal)
                    && string.Equals(newest.StackTrace, stackTrace, StringComparison.Ordinal))
                {
                    var idx = (_nextWrite - 1);
                    while (idx < 0) idx += _buffer.Length;

                    _buffer[idx] = new ConsoleEntry(
                        index: newest.Index,
                        time: newest.Time,
                        frame: newest.Frame,
                        severity: newest.Severity,
                        message: newest.Message,
                        stackTrace: newest.StackTrace,
                        count: newest.Count + 1);
                    return;
                }
            }

            var entry = new ConsoleEntry(
                index: _nextIndex++,
                time: Time.unscaledTime,
                frame: Time.frameCount,
                severity: severity,
                message: message,
                stackTrace: stackTrace,
                count: 1);

            _buffer[_nextWrite] = entry;
            _nextWrite = (_nextWrite + 1) % _buffer.Length;
            _count = Math.Min(_count + 1, _buffer.Length);
        }

        public ConsoleEntry GetNewest(int newestOffset)
        {
            if (newestOffset < 0 || newestOffset >= _count)
                throw new ArgumentOutOfRangeException(nameof(newestOffset));

            return GetEntryFromNewestOffset(newestOffset);
        }

        private ConsoleEntry GetEntryFromNewestOffset(int newestOffset)
        {
            // newestOffset: 0 = newest
            var idx = (_nextWrite - 1 - newestOffset);
            while (idx < 0) idx += _buffer.Length;
            return _buffer[idx];
        }

        public bool PassesFilters(in ConsoleEntry entry)
        {
            if (!IsSeverityEnabled(entry.Severity))
                return false;

            var search = Filter.Search;
            if (string.IsNullOrWhiteSpace(search))
                return true;

            // Case-insensitive substring search.
            return entry.Message?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                   || entry.StackTrace?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsSeverityEnabled(ConsoleSeverity s) =>
            s switch
            {
                ConsoleSeverity.Log => Filter.ShowLog,
                ConsoleSeverity.Warning => Filter.ShowWarning,
                ConsoleSeverity.Error => Filter.ShowError,
                ConsoleSeverity.Exception => Filter.ShowException,
                ConsoleSeverity.Assert => Filter.ShowAssert,
                _ => true
            };

        public static ConsoleSeverity ToSeverity(LogType type) =>
            type switch
            {
                LogType.Assert => ConsoleSeverity.Assert,
                LogType.Error => ConsoleSeverity.Error,
                LogType.Exception => ConsoleSeverity.Exception,
                LogType.Warning => ConsoleSeverity.Warning,
                _ => ConsoleSeverity.Log
            };
    }
}
