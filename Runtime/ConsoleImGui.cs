using System;
using System.Collections.Generic;
using ImGuiNET;
using UnityEngine;

namespace UnityEssentials
{
    public static class ConsoleImGui
    {
        private static readonly List<string> s_history = new(32);

        private static bool s_autoScroll = true;
        private static bool s_collapse = true;

        private static readonly List<ConsoleCommandRegistry.Command> s_suggestions = new(16);
        private static int s_suggestionIndex;
        private static bool s_requestFocusInput;

        // Consolidate input related state to reduce hidden coupling.
        private sealed class ConsoleInputState
        {
            public string Input = string.Empty;
            public bool UserEdited;
            public int HistoryIndex = -1;
            public string LastQuery = string.Empty;
        }

        private static readonly ConsoleInputState s_inputState = new();

        // Cached UTF8 encoder + reusable buffer to avoid transient allocations in native callbacks.
        private static readonly System.Text.UTF8Encoding s_utf8 = new(false);
        private static byte[] s_utf8Buffer = new byte[2048];

        public static void DrawImGui()
        {
            if (!ConsoleHost.Enabled)
                return;

            using var scope = ImGuiScope.TryEnter();
            if (!scope.Active)
                return;

            var data = ConsoleHost.Data;
            data.Config.CollapseDuplicates = s_collapse;

            ImGui.SetNextWindowSize(new System.Numerics.Vector2(900, 500), ImGuiCond.FirstUseEver);

            var open = true;
            if (!ImGui.Begin("Console", ref open, ImGuiWindowFlags.NoCollapse))
            {
                ImGui.End();
                if (!open)
                    ConsoleHost.Enabled = false;
                return;
            }

            if (!open)
            {
                ImGui.End();
                ConsoleHost.Enabled = false;
                return;
            }

            DrawLogBodyWithFixedInput(data);

            ImGui.End();
        }

        private static void DrawLogBodyWithFixedInput(ConsoleData data)
        {
            // Space reserved for the input row.
            var footerHeight = ImGui.GetFrameHeightWithSpacing() + 6f;

            // Scrollback.
            var avail = ImGui.GetContentRegionAvail();
            var bodySize = new System.Numerics.Vector2(avail.X, Mathf.Max(50, avail.Y - footerHeight));
            DrawLogList(data, bodySize);

            // Input row.
            ImGui.Separator();
            DrawInputBar();

            // Suggestions (positioned relative to the input).
            DrawSuggestionsFlyout();
        }

        private static unsafe void DrawLogList(ConsoleData data, System.Numerics.Vector2 bodySize)
        {
            ImGui.BeginChild("##console_body", bodySize, ImGuiChildFlags.None, ImGuiWindowFlags.None);

            ImGui.PushTextWrapPos(0);

            // Drawing all log lines every frame scales poorly; clip to visible range.
            // We keep the UI order newest-first, so we clip over filtered indices.
            var visibleIndices = GetVisibleIndices(data);

            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            clipper.Begin(visibleIndices.Count);

            while (clipper.Step())
            {
                for (var row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
                {
                    var idx = visibleIndices[row];
                    var entry = data.GetNewest(idx);

                    var color = GetColor(entry.Severity);
                    if (color.HasValue)
                        ImGui.PushStyleColor(ImGuiCol.Text, (System.Numerics.Vector4)color.Value);
                    
                    // Show the message, adding a count suffix only when collapsing and there are multiple entries
                    ImGui.TextUnformatted(entry.Message + (s_collapse && entry.Count > 1 ? $" (x{entry.Count})" : string.Empty));

                    if (color.HasValue)
                        ImGui.PopStyleColor();

                    if (!string.IsNullOrEmpty(entry.StackTrace))
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.65f, 0.65f, 0.65f, 1f));
                        ImGui.TextUnformatted(entry.StackTrace);
                        ImGui.PopStyleColor();
                    }
                }
            }

            clipper.End();
            clipper.Destroy();

            ImGui.PopTextWrapPos();

            // Only autoscroll if we were already at the bottom.
            var atBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - ImGui.GetTextLineHeight();
            if (s_autoScroll && atBottom)
                ImGui.SetScrollHereY(1f);

            ImGui.EndChild();
        }

        private static List<int> GetVisibleIndices(ConsoleData data)
        {
            // Cache list instance? Kept simple for now; still avoids per-line ImGui work when clipped.
            // Note: data.Count is bounded by config (default max) so this allocation is acceptable,
            // but can be optimized later by persisting a list.
            var indices = new List<int>(data.Count);
            for (var i = data.Count - 1; i >= 0; i--)
            {
                var entry = data.GetNewest(i);
                if (!data.PassesFilters(entry))
                    continue;

                indices.Add(i);
            }

            return indices;
        }

        private static unsafe void DrawInputBar()
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

            var flags = ImGuiInputTextFlags.EnterReturnsTrue
                        | ImGuiInputTextFlags.CallbackHistory
                        | ImGuiInputTextFlags.CallbackCompletion
                        | ImGuiInputTextFlags.CallbackEdit;

            // Rendering must not mutate logic state; autocomplete updates happen in callbacks.
            if (ImGui.InputText("##console_input", ref s_inputState.Input, 2048, flags, InputCallback))
            {
                var line = s_inputState.Input;
                s_inputState.Input = string.Empty;
                s_inputState.LastQuery = string.Empty;
                s_inputState.UserEdited = false;
                s_inputState.HistoryIndex = -1;

                s_suggestions.Clear();
                s_suggestionIndex = -1;

                if (!string.IsNullOrWhiteSpace(line))
                {
                    PushHistory(line);
                    ConsoleHost.TryExecuteLine(line);
                }

                ImGui.SetKeyboardFocusHere(-1);
            }

            // Re-focus input after applying a suggestion.
            if (s_requestFocusInput)
            {
                ImGui.SetKeyboardFocusHere(-1);
                s_requestFocusInput = false;
            }

            ImGui.SameLine();
        }

        private static void DrawSuggestionsFlyout()
        {
            if (!s_inputState.UserEdited)
                return;

            if (s_suggestions.Count == 0)
                return;

            if (string.IsNullOrWhiteSpace(GetCommandQuery(s_inputState.Input)))
                return;

            // Anchor to the input rect (DrawInputBar drew the previous item).
            var inputMin = ImGui.GetItemRectMin();
            var inputMax = ImGui.GetItemRectMax();

            const int maxVisible = 6;
            var style = ImGui.GetStyle();
            var visibleCount = Mathf.Min(maxVisible, s_suggestions.Count);

            var rowHeight = ImGui.GetFrameHeight();
            var contentHeight = visibleCount * rowHeight;

            var height = contentHeight + style.WindowPadding.Y;
            var width = Mathf.Max(220f, inputMax.X - inputMin.X);

            ImGui.SetNextWindowPos(new System.Numerics.Vector2(inputMin.X, inputMin.Y - height - 2f), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(width, height), ImGuiCond.Always);

            // Tooltip renders above the console window.
            var flags = ImGuiWindowFlags.NoTitleBar
                        | ImGuiWindowFlags.NoResize
                        | ImGuiWindowFlags.NoMove
                        | ImGuiWindowFlags.NoSavedSettings
                        | ImGuiWindowFlags.NoFocusOnAppearing
                        | ImGuiWindowFlags.Tooltip;

            if (s_suggestions.Count <= maxVisible)
                flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

            if (!ImGui.Begin("##console_suggestions_flyout", flags))
            {
                ImGui.End();
                return;
            }

            // Clamp selection if the list shrank.
            if (s_suggestionIndex >= s_suggestions.Count)
                s_suggestionIndex = s_suggestions.Count - 1;

            // Default to the first entry once there's a token.
            if (s_suggestionIndex < 0 && !string.IsNullOrWhiteSpace(GetCommandQuery(s_inputState.Input)))
                s_suggestionIndex = 0;

            for (var i = 0; i < s_suggestions.Count; i++)
            {
                var cmd = s_suggestions[i];

                if (i == s_suggestionIndex)
                    ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.7f, 0.9f, 1f, 1f));

                var label = string.IsNullOrWhiteSpace(cmd.Description)
                    ? cmd.Name
                    : $"{cmd.Name}  -  {cmd.Description}";

                if (ImGui.Selectable(label, i == s_suggestionIndex))
                {
                    // Only change the highlighted suggestion.
                    s_suggestionIndex = i;
                    s_requestFocusInput = true;
                }

                if (i == s_suggestionIndex)
                    ImGui.PopStyleColor();
            }

            ImGui.End();
        }

        private enum NavigationMode
        {
            None,
            Suggestions,
            History
        }

        private static void UpdateSuggestions(string input, bool force)
        {
            if (!force && !s_inputState.UserEdited)
            {
                s_inputState.LastQuery = GetCommandQuery(input);
                s_suggestions.Clear();
                s_suggestionIndex = -1;
                return;
            }

            var query = GetCommandQuery(input);
            if (!force && string.Equals(query, s_inputState.LastQuery, StringComparison.Ordinal))
                return;

            s_inputState.LastQuery = query;

            // Preserve current highlighted selection when possible.
            var previousSelectedName = (s_suggestionIndex >= 0 && s_suggestionIndex < s_suggestions.Count)
                ? s_suggestions[s_suggestionIndex].Name
                : null;

            s_suggestions.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                s_suggestionIndex = -1;
                return;
            }

            // UI must be O(n): iterate already-sorted commands.
            var cmds = ConsoleHost.Commands.SortedCommands;
            for (var i = 0; i < cmds.Count; i++)
            {
                var cmd = cmds[i];

                // If the user already typed a full command name, don't suggest the exact same command.
                if (string.Equals(cmd.Name, query, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (cmd.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                    || cmd.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    s_suggestions.Add(cmd);
                    if (s_suggestions.Count >= 10)
                        break;
                }
            }

            if (s_suggestions.Count == 0)
            {
                s_suggestionIndex = -1;
                return;
            }

            // If we had a previous selection, try to keep it.
            if (!string.IsNullOrEmpty(previousSelectedName))
                for (var i = 0; i < s_suggestions.Count; i++)
                    if (string.Equals(s_suggestions[i].Name, previousSelectedName, StringComparison.Ordinal))
                    {
                        s_suggestionIndex = i;
                        return;
                    }

            // Otherwise clamp the existing index into range (or default to first).
            s_suggestionIndex = Mathf.Clamp(s_suggestionIndex, 0, s_suggestions.Count - 1);
        }

        private static NavigationMode ResolveNavigationMode(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return NavigationMode.History;

            // Up/Down navigates suggestions only after a selection is active.
            if (s_suggestions.Count > 0 && s_suggestionIndex >= 0)
                return NavigationMode.Suggestions;

            return NavigationMode.History;
        }

        private static unsafe int InputCallback(ImGuiInputTextCallbackData* data)
        {
            if (data->EventFlag == ImGuiInputTextFlags.CallbackEdit)
            {
                s_inputState.UserEdited = true;

                // Pull current buffer into managed input so everything reads a single source of truth.
                var current = ReadFromImGuiInputBuffer(data);
                s_inputState.Input = current;

                UpdateSuggestions(current, force: false);

                return 0;
            }

            if (data->EventFlag == ImGuiInputTextFlags.CallbackHistory)
            {
                var currentLine = ReadFromImGuiInputBuffer(data);
                s_inputState.Input = currentLine;

                UpdateSuggestions(currentLine, force: false);

                var query = GetCommandQuery(currentLine);
                var mode = ResolveNavigationMode(query);

                if (mode == NavigationMode.Suggestions)
                {
                    if (data->EventKey == ImGuiKey.UpArrow)
                        s_suggestionIndex = Mathf.Max(0, s_suggestionIndex - 1);

                    if (data->EventKey == ImGuiKey.DownArrow)
                        s_suggestionIndex = Mathf.Min(s_suggestions.Count - 1, s_suggestionIndex + 1);

                    return 0;
                }

                // History navigation.
                s_suggestionIndex = -1;

                if (s_history.Count == 0)
                    return 0;

                if (data->EventKey == ImGuiKey.UpArrow)
                {
                    if (s_inputState.HistoryIndex < 0)
                        s_inputState.HistoryIndex = s_history.Count - 1;
                    else
                        s_inputState.HistoryIndex = Mathf.Max(0, s_inputState.HistoryIndex - 1);

                    if (s_inputState.HistoryIndex >= 0 && s_inputState.HistoryIndex < s_history.Count)
                        s_inputState.Input = s_history[s_inputState.HistoryIndex];
                }
                else if (data->EventKey == ImGuiKey.DownArrow)
                {
                    if (s_inputState.HistoryIndex < 0)
                        return 0;

                    s_inputState.HistoryIndex++;
                    if (s_inputState.HistoryIndex >= s_history.Count)
                    {
                        s_inputState.HistoryIndex = -1;
                        s_inputState.Input = string.Empty;

                        WriteToImGuiInputBuffer(data, s_inputState.Input);
                        s_inputState.UserEdited = false;
                        s_suggestions.Clear();
                        s_inputState.LastQuery = GetCommandQuery(s_inputState.Input);

                        return 0;
                    }

                    s_inputState.Input = s_history[s_inputState.HistoryIndex];
                }

                // History changes are programmatic.
                s_inputState.UserEdited = false;

                WriteToImGuiInputBuffer(data, s_inputState.Input);

                s_suggestions.Clear();
                s_suggestionIndex = -1;
                s_inputState.LastQuery = GetCommandQuery(s_inputState.Input);

                return 0;
            }

            if (data->EventFlag == ImGuiInputTextFlags.CallbackCompletion)
            {
                // Tab completion.
                var currentLine = ReadFromImGuiInputBuffer(data);
                s_inputState.Input = currentLine;

                UpdateSuggestions(currentLine, force: true);

                if (s_suggestions.Count == 0)
                    return 0;

                if (s_suggestionIndex < 0)
                    s_suggestionIndex = 0;

                var completed = ReplaceCommandToken(currentLine, s_suggestions[s_suggestionIndex].Name);
                WriteToImGuiInputBuffer(data, completed);

                s_inputState.Input = completed;

                // Completion is programmatic.
                s_inputState.UserEdited = false;

                s_suggestions.Clear();
                s_suggestionIndex = -1;
                s_inputState.LastQuery = GetCommandQuery(s_inputState.Input);

                s_requestFocusInput = true;

                return 0;
            }

            return 0;
        }

        private static unsafe string ReadFromImGuiInputBuffer(ImGuiInputTextCallbackData* data)
        {
            if (data->BufTextLen <= 0)
                return string.Empty;

            // Ensure scratch buffer capacity.
            if (s_utf8Buffer.Length < data->BufTextLen)
                s_utf8Buffer = new byte[Mathf.NextPowerOfTwo(data->BufTextLen)];

            for (var i = 0; i < data->BufTextLen; i++)
                s_utf8Buffer[i] = data->Buf[i];

            return s_utf8.GetString(s_utf8Buffer, 0, data->BufTextLen);
        }

        private static unsafe void WriteToImGuiInputBuffer(ImGuiInputTextCallbackData* data, string value)
        {
            value ??= string.Empty;

            // Encode into reusable buffer.
            var maxBytes = data->BufSize > 0 ? data->BufSize - 1 : 0;
            var byteCount = s_utf8.GetByteCount(value);

            if (s_utf8Buffer.Length < byteCount)
                s_utf8Buffer = new byte[Mathf.NextPowerOfTwo(byteCount)];

            var bytesWritten = s_utf8.GetBytes(value, 0, value.Length, s_utf8Buffer, 0);
            var writeLen = Math.Min(bytesWritten, maxBytes);

            fixed (byte* src = s_utf8Buffer)
            {
                Buffer.MemoryCopy(src, data->Buf, data->BufSize, writeLen);
            }

            if (data->BufSize > 0)
                data->Buf[writeLen] = 0;

            data->BufTextLen = writeLen;
            data->CursorPos = writeLen;
            data->SelectionStart = writeLen;
            data->SelectionEnd = writeLen;
            data->BufDirty = 1;
        }

        private static void PushHistory(string line)
        {
            if (s_history.Count == 0 || !string.Equals(s_history[^1], line, StringComparison.Ordinal))
                s_history.Add(line);

            if (s_history.Count > 50)
                s_history.RemoveAt(0);

            s_inputState.HistoryIndex = -1;
        }

        private static string GetCommandQuery(string currentLine)
        {
            if (string.IsNullOrWhiteSpace(currentLine))
                return string.Empty;

            var trimmed = currentLine.TrimStart();
            var space = trimmed.IndexOf(' ');
            return (space < 0 ? trimmed : trimmed.Substring(0, space)).Trim();
        }

        private static string ReplaceCommandToken(string input, string command)
        {
            input ??= string.Empty;

            var leading = input.Length - input.TrimStart().Length;
            var trimmed = input.TrimStart();

            var space = trimmed.IndexOf(' ');
            var args = space < 0 ? string.Empty : trimmed.Substring(space);

            return new string(' ', leading) + command + args;
        }

        private static System.Numerics.Vector4? GetColor(ConsoleSeverity sev)
        {
            return sev switch
            {
                ConsoleSeverity.Warning => new System.Numerics.Vector4(1f, 0.85f, 0.3f, 1f),
                ConsoleSeverity.Error => new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f),
                ConsoleSeverity.Exception => new System.Numerics.Vector4(1f, 0.3f, 1f, 1f),
                ConsoleSeverity.Assert => new System.Numerics.Vector4(1f, 0.6f, 0.6f, 1f),
                _ => null
            };
        }
    }
}

