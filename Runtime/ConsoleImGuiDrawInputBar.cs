using System;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace UnityEssentials
{
    internal static class ConsoleImGuiDrawInputBar
    {
        internal const string InputTextId = "##console_input";

        internal static unsafe void DrawImGui(ConsoleImGuiContext ctx)
        {
            var state = ctx.State;

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

            var flags = ImGuiInputTextFlags.EnterReturnsTrue
                        | ImGuiInputTextFlags.CallbackHistory
                        | ImGuiInputTextFlags.CallbackCompletion
                        | ImGuiInputTextFlags.CallbackEdit;

            // Pin the context object for the duration of this InputText call.
            var handle = GCHandle.Alloc(ctx, GCHandleType.Normal);
            try
            {
                if (ImGui.InputText(InputTextId, ref state.Input, 2048, flags, InputCallback,
                        GCHandle.ToIntPtr(handle)))
                {
                    var line = state.Input;
                    state.Input = string.Empty;
                    state.LastQuery = string.Empty;
                    state.UserEdited = false;
                    state.HistoryIndex = -1;

                    state.Suggestions.Clear();
                    state.SuggestionIndex = -1;

                    if (!string.IsNullOrWhiteSpace(line))
                    {

                        ConsoleInputShared.PushHistory(state.History, line);
                        state.HistoryIndex = -1;
                        ConsoleHost.TryExecuteLine(line);
                    }

                    ImGui.SetKeyboardFocusHere(-1);
                }
            }
            finally
            {
                handle.Free();
            }

            ctx.InputRectMin = ImGui.GetItemRectMin();
            ctx.InputRectMax = ImGui.GetItemRectMax();
            ctx.HasInputRect = ctx.InputRectMax.X > ctx.InputRectMin.X && ctx.InputRectMax.Y > ctx.InputRectMin.Y;

            // Refocus after tab completion / click completion.
            if (ctx.RequestFocusInput)
            {
                ImGui.SetKeyboardFocusHere(-1);
                ctx.RequestFocusInput = false;
            }

            ImGui.SameLine();
        }

        private static unsafe int InputCallback(ImGuiInputTextCallbackData* data)
        {
            var handle = GCHandle.FromIntPtr((IntPtr)data->UserData);
            var ctx = (ConsoleImGuiContext)handle.Target;

            if (data->EventFlag == ImGuiInputTextFlags.CallbackEdit)
            {
                ctx.State.UserEdited = true;

                var current = ImGuiUtf8InputBuffer.Read(data);
                ctx.State.Input = current;

                ConsoleImGui.UpdateSuggestions(ctx, current, false);

                return 0;
            }

            if (data->EventFlag == ImGuiInputTextFlags.CallbackHistory)
            {
                var state = ctx.State;
                var currentLine = ImGuiUtf8InputBuffer.Read(data);
                state.Input = currentLine;

                ConsoleImGui.UpdateSuggestions(ctx, currentLine, false);

                var query = ConsoleUtilities.GetCommandQuery(currentLine);
                var mode = ConsoleInputShared.ResolveNavigationMode(query, state.Suggestions.Count, state.SuggestionIndex);

                if (mode == ConsoleInputNavigationMode.Suggestions)
                {
                    if (data->EventKey == ImGuiKey.UpArrow)
                        state.SuggestionIndex = Math.Max(0, state.SuggestionIndex - 1);

                    if (data->EventKey == ImGuiKey.DownArrow)
                        state.SuggestionIndex = Math.Min(state.Suggestions.Count - 1, state.SuggestionIndex + 1);

                    return 0;
                }

                // History navigation.
                state.SuggestionIndex = -1;

                if (state.History.Count == 0)
                    return 0;

                if (data->EventKey == ImGuiKey.UpArrow)
                {
                    if (state.HistoryIndex < 0)
                        state.HistoryIndex = state.History.Count - 1;
                    else
                        state.HistoryIndex = Math.Max(0, state.HistoryIndex - 1);

                    if (state.HistoryIndex >= 0 && state.HistoryIndex < state.History.Count)
                        state.Input = state.History[state.HistoryIndex];
                }
                else if (data->EventKey == ImGuiKey.DownArrow)
                {
                    if (state.HistoryIndex < 0)
                        return 0;

                    state.HistoryIndex++;
                    if (state.HistoryIndex >= state.History.Count)
                    {
                        state.HistoryIndex = -1;
                        state.Input = string.Empty;

                        ImGuiUtf8InputBuffer.Write(data, state.Input);
                        state.UserEdited = false;
                        state.Suggestions.Clear();
                        state.LastQuery = ConsoleUtilities.GetCommandQuery(state.Input);

                        return 0;
                    }

                    state.Input = state.History[state.HistoryIndex];
                }

                state.UserEdited = false;

                ImGuiUtf8InputBuffer.Write(data, state.Input);

                state.Suggestions.Clear();
                state.SuggestionIndex = -1;
                state.LastQuery = ConsoleUtilities.GetCommandQuery(state.Input);

                return 0;
            }

            if (data->EventFlag == ImGuiInputTextFlags.CallbackCompletion || data->EventKey == ImGuiKey.Tab)
            {
                var state = ctx.State;
                var currentLine = ImGuiUtf8InputBuffer.Read(data);
                state.Input = currentLine;

                ConsoleImGui.UpdateSuggestions(ctx, currentLine, true);

                if (state.Suggestions.Count == 0)
                    return 0;

                if (state.SuggestionIndex < 0)
                    state.SuggestionIndex = 0;

                var completed = ConsoleUtilities.ReplaceCommandToken(currentLine, state.Suggestions[state.SuggestionIndex].Name);
                ImGuiUtf8InputBuffer.Write(data, completed);

                state.Input = completed;

                state.UserEdited = false;

                state.Suggestions.Clear();
                state.SuggestionIndex = -1;
                state.LastQuery = ConsoleUtilities.GetCommandQuery(state.Input);

                ctx.RequestFocusInput = true;

                return 0;
            }

            return 0;
        }
    }
}