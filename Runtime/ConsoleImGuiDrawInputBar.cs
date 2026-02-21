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

        internal static unsafe void DrawImGui(ConsoleImGuiContext ctx)
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

            var flags = ImGuiInputTextFlags.EnterReturnsTrue
                        | ImGuiInputTextFlags.CallbackHistory
                        | ImGuiInputTextFlags.CallbackCompletion
                        | ImGuiInputTextFlags.CallbackEdit;

            // Pin the context object for the duration of this InputText call.
            var handle = GCHandle.Alloc(ctx, GCHandleType.Normal);
            try
            {
                if (ImGui.InputText(InputTextId, ref ctx.InputState.Input, 2048, flags, InputCallback,
                        GCHandle.ToIntPtr(handle)))
                {
                    var line = ctx.InputState.Input;
                    ctx.InputState.Input = string.Empty;
                    ctx.InputState.LastQuery = string.Empty;
                    ctx.InputState.UserEdited = false;
                    ctx.InputState.HistoryIndex = -1;

                    ctx.Suggestions.Clear();
                    ctx.SuggestionIndex = -1;

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        ConsoleImGui.PushHistory(ctx, line);
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

        private static NavigationMode ResolveNavigationMode(ConsoleImGuiContext ctx, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return NavigationMode.History;

            if (ctx.Suggestions.Count > 0 && ctx.SuggestionIndex >= 0)
                return NavigationMode.Suggestions;

            return NavigationMode.History;
        }

        private static unsafe int InputCallback(ImGuiInputTextCallbackData* data)
        {
            var handle = GCHandle.FromIntPtr((IntPtr)data->UserData);
            var ctx = (ConsoleImGuiContext)handle.Target;

            if (data->EventFlag == ImGuiInputTextFlags.CallbackEdit)
            {
                ctx.InputState.UserEdited = true;

                var current = ImGuiUtf8InputBuffer.Read(data);
                ctx.InputState.Input = current;

                ConsoleImGui.UpdateSuggestions(ctx, current, false);

                return 0;
            }

            if (data->EventFlag == ImGuiInputTextFlags.CallbackHistory)
            {
                var currentLine = ImGuiUtf8InputBuffer.Read(data);
                ctx.InputState.Input = currentLine;

                ConsoleImGui.UpdateSuggestions(ctx, currentLine, false);

                var query = ConsoleImGuiUtilities.GetCommandQuery(currentLine);
                var mode = ResolveNavigationMode(ctx, query);

                if (mode == NavigationMode.Suggestions)
                {
                    if (data->EventKey == ImGuiKey.UpArrow)
                        ctx.SuggestionIndex = Math.Max(0, ctx.SuggestionIndex - 1);

                    if (data->EventKey == ImGuiKey.DownArrow)
                        ctx.SuggestionIndex = Math.Min(ctx.Suggestions.Count - 1, ctx.SuggestionIndex + 1);

                    return 0;
                }

                // History navigation.
                ctx.SuggestionIndex = -1;

                if (ctx.History.Count == 0)
                    return 0;

                if (data->EventKey == ImGuiKey.UpArrow)
                {
                    if (ctx.InputState.HistoryIndex < 0)
                        ctx.InputState.HistoryIndex = ctx.History.Count - 1;
                    else
                        ctx.InputState.HistoryIndex = Math.Max(0, ctx.InputState.HistoryIndex - 1);

                    if (ctx.InputState.HistoryIndex >= 0 && ctx.InputState.HistoryIndex < ctx.History.Count)
                        ctx.InputState.Input = ctx.History[ctx.InputState.HistoryIndex];
                }
                else if (data->EventKey == ImGuiKey.DownArrow)
                {
                    if (ctx.InputState.HistoryIndex < 0)
                        return 0;

                    ctx.InputState.HistoryIndex++;
                    if (ctx.InputState.HistoryIndex >= ctx.History.Count)
                    {
                        ctx.InputState.HistoryIndex = -1;
                        ctx.InputState.Input = string.Empty;

                        ImGuiUtf8InputBuffer.Write(data, ctx.InputState.Input);
                        ctx.InputState.UserEdited = false;
                        ctx.Suggestions.Clear();
                        ctx.InputState.LastQuery = ConsoleImGuiUtilities.GetCommandQuery(ctx.InputState.Input);

                        return 0;
                    }

                    ctx.InputState.Input = ctx.History[ctx.InputState.HistoryIndex];
                }

                ctx.InputState.UserEdited = false;

                ImGuiUtf8InputBuffer.Write(data, ctx.InputState.Input);

                ctx.Suggestions.Clear();
                ctx.SuggestionIndex = -1;
                ctx.InputState.LastQuery = ConsoleImGuiUtilities.GetCommandQuery(ctx.InputState.Input);

                return 0;
            }

            if (data->EventFlag == ImGuiInputTextFlags.CallbackCompletion)
            {
                var currentLine = ImGuiUtf8InputBuffer.Read(data);
                ctx.InputState.Input = currentLine;

                ConsoleImGui.UpdateSuggestions(ctx, currentLine, true);

                if (ctx.Suggestions.Count == 0)
                    return 0;

                if (ctx.SuggestionIndex < 0)
                    ctx.SuggestionIndex = 0;

                var completed =
                    ConsoleImGuiUtilities.ReplaceCommandToken(currentLine,
                        ctx.Suggestions[ctx.SuggestionIndex].Name);
                ImGuiUtf8InputBuffer.Write(data, completed);

                ctx.InputState.Input = completed;

                ctx.InputState.UserEdited = false;

                ctx.Suggestions.Clear();
                ctx.SuggestionIndex = -1;
                ctx.InputState.LastQuery = ConsoleImGuiUtilities.GetCommandQuery(ctx.InputState.Input);

                ctx.RequestFocusInput = true;

                return 0;
            }

            return 0;
        }
    }
}