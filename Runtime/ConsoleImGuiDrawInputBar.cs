using System;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace UnityEssentials
{
    internal static class ConsoleImGuiDrawInputBar
    {
        internal const string InputTextId = "##console_input";

        internal sealed class InputState
        {
            public string Input = string.Empty;
            public bool UserEdited;
            public int HistoryIndex = -1;
            public string LastQuery = string.Empty;
        }

        private enum NavigationMode
        {
            Suggestions,
            History
        }

        internal static unsafe void Draw(ConsoleImGuiContext ctx, Func<string, bool> executeLine)
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

            var flags = ImGuiInputTextFlags.EnterReturnsTrue
                        | ImGuiInputTextFlags.CallbackHistory
                        | ImGuiInputTextFlags.CallbackCompletion
                        | ImGuiInputTextFlags.CallbackEdit;

            var callbackContext = new CallbackContext(ctx)
            {
                ExecuteLine = executeLine
            };

            // Pin the context object for the duration of this InputText call.
            var handle = GCHandle.Alloc(callbackContext, GCHandleType.Normal);
            try
            {
                if (ImGui.InputText(InputTextId, ref ctx.Input.Input, 2048, flags, InputCallback,
                        (IntPtr)GCHandle.ToIntPtr(handle)))
                {
                    var line = ctx.Input.Input;
                    ctx.Input.Input = string.Empty;
                    ctx.Input.LastQuery = string.Empty;
                    ctx.Input.UserEdited = false;
                    ctx.Input.HistoryIndex = -1;

                    ctx.Suggestions.Clear();
                    ctx.SuggestionIndex = -1;

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        ConsoleImGui.PushHistory(ctx, line);
                        executeLine?.Invoke(line);
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
        
        private static NavigationMode ResolveNavigationMode(ConsoleImGuiContext ctx, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return NavigationMode.History;

            if (ctx.Suggestions.Count > 0 && ctx.SuggestionIndex >= 0)
                return NavigationMode.Suggestions;

            return NavigationMode.History;
        }

        private sealed class CallbackContext
        {
            public readonly ConsoleImGuiContext Ctx;
            public Func<string, bool> ExecuteLine;

            public CallbackContext(ConsoleImGuiContext ctx)
            {
                Ctx = ctx;
            }
        }

        private static unsafe int InputCallback(ImGuiInputTextCallbackData* data)
        {
            var handle = GCHandle.FromIntPtr((IntPtr)data->UserData);
            var ctx = (CallbackContext)handle.Target;
            var state = ctx.Ctx;

            if (data->EventFlag == ImGuiInputTextFlags.CallbackEdit)
            {
                state.Input.UserEdited = true;

                var current = ImGuiUtf8InputBuffer.Read(data);
                state.Input.Input = current;

                ConsoleImGui.UpdateSuggestions(state, current, false);

                return 0;
            }

            if (data->EventFlag == ImGuiInputTextFlags.CallbackHistory)
            {
                var currentLine = ImGuiUtf8InputBuffer.Read(data);
                state.Input.Input = currentLine;

                ConsoleImGui.UpdateSuggestions(state, currentLine, false);

                var query = ConsoleImGuiUtilities.GetCommandQuery(currentLine);
                var mode = ResolveNavigationMode(state, query);

                if (mode == NavigationMode.Suggestions)
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
                    if (state.Input.HistoryIndex < 0)
                        state.Input.HistoryIndex = state.History.Count - 1;
                    else
                        state.Input.HistoryIndex = Math.Max(0, state.Input.HistoryIndex - 1);

                    if (state.Input.HistoryIndex >= 0 && state.Input.HistoryIndex < state.History.Count)
                        state.Input.Input = state.History[state.Input.HistoryIndex];
                }
                else if (data->EventKey == ImGuiKey.DownArrow)
                {
                    if (state.Input.HistoryIndex < 0)
                        return 0;

                    state.Input.HistoryIndex++;
                    if (state.Input.HistoryIndex >= state.History.Count)
                    {
                        state.Input.HistoryIndex = -1;
                        state.Input.Input = string.Empty;

                        ImGuiUtf8InputBuffer.Write(data, state.Input.Input);
                        state.Input.UserEdited = false;
                        state.Suggestions.Clear();
                        state.Input.LastQuery = ConsoleImGuiUtilities.GetCommandQuery(state.Input.Input);

                        return 0;
                    }

                    state.Input.Input = state.History[state.Input.HistoryIndex];
                }

                state.Input.UserEdited = false;

                ImGuiUtf8InputBuffer.Write(data, state.Input.Input);

                state.Suggestions.Clear();
                state.SuggestionIndex = -1;
                state.Input.LastQuery = ConsoleImGuiUtilities.GetCommandQuery(state.Input.Input);

                return 0;
            }

            if (data->EventFlag == ImGuiInputTextFlags.CallbackCompletion)
            {
                var currentLine = ImGuiUtf8InputBuffer.Read(data);
                state.Input.Input = currentLine;

                ConsoleImGui.UpdateSuggestions(state, currentLine, true);

                if (state.Suggestions.Count == 0)
                    return 0;

                if (state.SuggestionIndex < 0)
                    state.SuggestionIndex = 0;

                var completed = ConsoleImGuiUtilities.ReplaceCommandToken(currentLine, state.Suggestions[state.SuggestionIndex].Name);
                ImGuiUtf8InputBuffer.Write(data, completed);

                state.Input.Input = completed;

                state.Input.UserEdited = false;

                state.Suggestions.Clear();
                state.SuggestionIndex = -1;
                state.Input.LastQuery = ConsoleImGuiUtilities.GetCommandQuery(state.Input.Input);

                state.RequestFocusInput = true;

                return 0;
            }

            return 0;
        }
    }
}
