using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using UnityEngine;

namespace UnityEssentials
{
    public static class ConsoleImGui
    {
        private static readonly List<string> s_history = new(32);
        private static int s_historyIndex = -1;

        private static string s_input = string.Empty;
        private static bool s_autoScroll = true;

        private static readonly List<ConsoleCommandRegistry.Command> s_suggestions = new(16);
        private static int s_suggestionIndex;
        private static string s_lastSuggestionQuery = string.Empty;
        private static bool s_requestFocusInput;

        public static void DrawImGui()
        {
            if (!ConsoleHost.Enabled)
                return;

            using var scope = ImGuiScope.TryEnter();
            if (!scope.Active)
                return;

            var data = ConsoleHost.Data;

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
            // Reserve exact space for the input line (and a tiny separator).
            var footerHeight = ImGui.GetFrameHeightWithSpacing() + 6f;

            // Body (scrollback) first.
            var avail = ImGui.GetContentRegionAvail();
            var bodySize = new System.Numerics.Vector2(avail.X, Mathf.Max(50, avail.Y - footerHeight));
            DrawLogList(data, bodySize);

            // Fixed input at the bottom.
            ImGui.Separator();
            DrawInputBar();

            // Suggestions are not part of the body; draw them as a separate flyout.
            DrawSuggestionsFlyout();
        }

        private static void DrawLogList(ConsoleData data, System.Numerics.Vector2 bodySize)
        {
            ImGui.BeginChild("##console_body", bodySize, ImGuiChildFlags.None, ImGuiWindowFlags.None);

            // Wrap at the right edge of the child.
            ImGui.PushTextWrapPos(0);

            for (var i = data.Count - 1; i >= 0; i--)
            {
                var entry = data.GetNewest(i);
                if (!data.PassesFilters(entry))
                    continue;

                var color = GetColor(entry.Severity);
                if (color.HasValue)
                    ImGui.PushStyleColor(ImGuiCol.Text, color.Value);

                ImGui.TextUnformatted(entry.Message);

                if (color.HasValue)
                    ImGui.PopStyleColor();

                if (!string.IsNullOrEmpty(entry.StackTrace))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.65f, 0.65f, 0.65f, 1f));
                    ImGui.TextUnformatted(entry.StackTrace);
                    ImGui.PopStyleColor();
                }
            }

            ImGui.PopTextWrapPos();

            if (s_autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f)
                ImGui.SetScrollHereY(1f);

            ImGui.EndChild();
        }

        private static unsafe void DrawInputBar()
        {
            // Layout: [input.................................] [Auto]
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var fullWidth = ImGui.GetContentRegionAvail().X;
            var inputWidth = Mathf.Max(50, fullWidth - spacing);

            ImGui.SetNextItemWidth(inputWidth);

            var flags = ImGuiInputTextFlags.EnterReturnsTrue
                        | ImGuiInputTextFlags.CallbackHistory
                        | ImGuiInputTextFlags.CallbackCompletion;

            // Keep suggestions fresh.
            UpdateSuggestionsIfNeeded();

            if (ImGui.InputText("##console_input", ref s_input, 2048, flags, InputCallback))
            {
                var line = s_input;
                s_input = string.Empty;
                s_lastSuggestionQuery = string.Empty;

                if (!string.IsNullOrWhiteSpace(line))
                {
                    PushHistory(line);
                    ConsoleHost.TryExecuteLine(line);
                }

                ImGui.SetKeyboardFocusHere(-1);
            }

            // If a suggestion click happened (or completion), re-focus the input.
            if (s_requestFocusInput)
            {
                ImGui.SetKeyboardFocusHere(-1);
                s_requestFocusInput = false;
            }

            ImGui.SameLine();
        }

        private static void DrawSuggestionsFlyout()
        {
            if (s_suggestions.Count == 0)
                return;

            // Only show suggestions while user is typing a command token.
            if (string.IsNullOrWhiteSpace(GetCommandQuery(s_input)))
                return;

            // Anchor to the input item rect (DrawInputBar drew the last item).
            var inputMin = ImGui.GetItemRectMin();
            var inputMax = ImGui.GetItemRectMax();

            const int maxVisible = 6;
            var style = ImGui.GetStyle();
            var visibleCount = Mathf.Min(maxVisible, s_suggestions.Count);

            // Use frame height with spacing since we render Selectables (framed items).
            var rowHeight = ImGui.GetFrameHeight();
            var contentHeight = visibleCount * rowHeight;

            // Exact-ish sizing: avoid being a hair too small (which triggers a scrollbar).
            var height = contentHeight + style.WindowPadding.Y ;
            var width = Mathf.Max(220f, inputMax.X - inputMin.X);

            ImGui.SetNextWindowPos(new System.Numerics.Vector2(inputMin.X, inputMin.Y - height - 2f), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(width, height), ImGuiCond.Always);

            // Tooltip windows render on top.
            var flags = ImGuiWindowFlags.NoTitleBar
                        | ImGuiWindowFlags.NoResize
                        | ImGuiWindowFlags.NoMove
                        | ImGuiWindowFlags.NoSavedSettings
                        | ImGuiWindowFlags.NoFocusOnAppearing
                        | ImGuiWindowFlags.Tooltip;

            // If everything fits, disable scrollbars entirely.
            if (s_suggestions.Count <= maxVisible)
                flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

            if (!ImGui.Begin("##console_suggestions_flyout", flags))
            {
                ImGui.End();
                return;
            }

            // Clamp selection defensively (query changes can shrink the list between frames).
            if (s_suggestionIndex < 0)
                s_suggestionIndex = 0;
            if (s_suggestionIndex >= s_suggestions.Count)
                s_suggestionIndex = s_suggestions.Count - 1;

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
                    s_suggestionIndex = i;
                    ApplySuggestion(i);
                    s_requestFocusInput = true;
                }

                if (i == s_suggestionIndex)
                    ImGui.PopStyleColor();
            }

            ImGui.End();
        }

        private static void UpdateSuggestionsIfNeeded()
        {
            var query = GetCommandQuery(s_input);
            if (string.Equals(query, s_lastSuggestionQuery, StringComparison.Ordinal))
                return;

            s_lastSuggestionQuery = query;
            s_suggestions.Clear();
            s_suggestionIndex = 0;

            if (string.IsNullOrWhiteSpace(query))
                return;

            foreach (var cmd in ConsoleHost.Commands.AllCommands
                         .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (cmd.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                    || cmd.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    s_suggestions.Add(cmd);
                    if (s_suggestions.Count >= 10)
                        break;
                }
            }

            // Keep selection valid after refresh.
            if (s_suggestionIndex >= s_suggestions.Count)
                s_suggestionIndex = Mathf.Max(0, s_suggestions.Count - 1);
        }

        private static unsafe int InputCallback(ImGuiInputTextCallbackData* data)
        {
            if (data->EventFlag == ImGuiInputTextFlags.CallbackHistory)
            {
                // Prefer suggestion navigation when suggestions are visible; otherwise fall back to command history.
                UpdateSuggestionsIfNeeded();
                var hasSuggestionNav = s_suggestions.Count > 0 && !string.IsNullOrWhiteSpace(GetCommandQuery(s_input));

                if (hasSuggestionNav)
                {
                    if (data->EventKey == ImGuiKey.UpArrow)
                    {
                        s_suggestionIndex = Mathf.Max(0, s_suggestionIndex - 1);
                        return 0;
                    }

                    if (data->EventKey == ImGuiKey.DownArrow)
                    {
                        s_suggestionIndex = Mathf.Min(s_suggestions.Count - 1, s_suggestionIndex + 1);
                        return 0;
                    }
                }

                if (s_history.Count == 0)
                    return 0;

                if (data->EventKey == ImGuiKey.UpArrow)
                {
                    if (s_historyIndex < 0)
                        s_historyIndex = s_history.Count - 1;
                    else
                        s_historyIndex = Mathf.Max(0, s_historyIndex - 1);

                    if (s_historyIndex >= 0 && s_historyIndex < s_history.Count)
                        s_input = s_history[s_historyIndex];
                }
                else if (data->EventKey == ImGuiKey.DownArrow)
                {
                    if (s_historyIndex < 0)
                        return 0;

                    s_historyIndex++;
                    if (s_historyIndex >= s_history.Count)
                    {
                        s_historyIndex = -1;
                        s_input = string.Empty;
                        return 0;
                    }

                    s_input = s_history[s_historyIndex];
                }

                return 0;
            }

            if (data->EventFlag == ImGuiInputTextFlags.CallbackCompletion)
            {
                // Tab accepts current suggestion.
                UpdateSuggestionsIfNeeded();

                if (s_suggestions.Count == 0)
                    return 0;

                // Build the completed line: replace first token only, keep args.
                var current = s_input ?? string.Empty;
                var leadingSpaces = current.Length - current.TrimStart().Length;
                var trimmed = current.TrimStart();
                var space = trimmed.IndexOf(' ');
                var args = space < 0 ? string.Empty : trimmed.Substring(space);

                if (s_suggestionIndex < 0)
                    s_suggestionIndex = 0;
                if (s_suggestionIndex >= s_suggestions.Count)
                    s_suggestionIndex = s_suggestions.Count - 1;

                var completed = new string(' ', leadingSpaces) + s_suggestions[s_suggestionIndex].Name + args;

                // IMPORTANT: this binding uses a UTF-8 byte buffer.
                // Overwrite Buf (clamped to BufSize-1 to reserve null terminator).
                var maxBytes = data->BufSize > 0 ? data->BufSize - 1 : 0;
                var utf8 = System.Text.Encoding.UTF8.GetBytes(completed);
                var writeLen = Math.Min(utf8.Length, maxBytes);

                for (var i = 0; i < writeLen; i++)
                    data->Buf[i] = utf8[i];

                if (data->BufSize > 0)
                    data->Buf[writeLen] = 0;

                data->BufTextLen = writeLen;
                data->CursorPos = writeLen;
                data->SelectionStart = writeLen;
                data->SelectionEnd = writeLen;
                data->BufDirty = 1;

                // Keep our managed mirror in sync so suggestion rendering uses the new value.
                s_input = completed;
                s_requestFocusInput = true;

                return 0;
            }

            return 0;
        }

        private static void PushHistory(string line)
        {
            if (s_history.Count == 0 || !string.Equals(s_history[^1], line, StringComparison.Ordinal))
                s_history.Add(line);

            if (s_history.Count > 50)
                s_history.RemoveAt(0);

            s_historyIndex = -1;
        }

        private static string GetCommandQuery(string currentLine)
        {
            if (string.IsNullOrWhiteSpace(currentLine))
                return string.Empty;

            var trimmed = currentLine.TrimStart();
            var space = trimmed.IndexOf(' ');
            return (space < 0 ? trimmed : trimmed.Substring(0, space)).Trim();
        }

        private static void ApplySuggestion(int index)
        {
            if (index < 0 || index >= s_suggestions.Count)
                return;

            var cmd = s_suggestions[index].Name;

            // Replace first token only, preserve args.
            var existing = s_input ?? string.Empty;
            var leadingSpaces = existing.Length - existing.TrimStart().Length;
            var trimmed = existing.TrimStart();
            var space = trimmed.IndexOf(' ');
            var args = space < 0 ? string.Empty : trimmed.Substring(space);

            s_input = new string(' ', leadingSpaces) + cmd + args;
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
