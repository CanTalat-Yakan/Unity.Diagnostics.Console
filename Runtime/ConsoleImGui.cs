using System;
using ImGuiNET;

namespace UnityEssentials
{
    public static class ConsoleImGui
    {
        public static bool Header = false;
        public static bool Body = true;

        public static bool Collapse = true;

        // Shared UI state (history, suggestions, selection, input rect, input buffer).
        private static readonly ConsoleImGuiContext s_ctx = new()
        {
            Header = Header,
            Body = Body,
            Collapse = Collapse
        };

        public static void DrawImGui()
        {
            if (!ConsoleHost.Enabled)
                return;

            using var scope = ImGuiScope.TryEnter();
            if (!scope.Active)
                return;

            var data = ConsoleHost.Data;
            data.Config.CollapseDuplicates = Collapse;

            // Copy the public toggles into the context.
            s_ctx.Header = Header;
            s_ctx.Body = Body;
            s_ctx.Collapse = Collapse;

            // No-header mode: always keep the input selected so you can type commands back-to-back.
            // (Header mode keeps the existing "focus only when requested" behavior.)
            if (!Header)
                s_ctx.RequestFocusInput = true;

            s_ctx.BeginFrame();

            var layout = ConsoleImGuiLayout.CreateLayoutContext(Header, Body);

            ConsoleImGuiLayout.ConfigureLayout(Header, layout);
            var windowFlags = ConsoleImGuiLayout.BuildWindowFlags(Header, layout.InputOnly);

            var open = true;
            if (!ImGui.Begin("Console", ref open, windowFlags))
            {
                ImGui.End();
                HandleClose(open);
                return;
            }

            if (HandleClose(open))
            {
                ImGui.End();
                return;
            }

            // No-header mode: force the layout after Begin so saved/user state can’t win.
            if (!Header)
            {
                ConsoleImGuiLayout.ForceOverlayWindowToLayout(layout.InputOnly, layout.WorkPos, layout.WorkSize);
                // Clamp again once the final size is known.
                ConsoleImGuiLayout.ClampToWorkArea(layout.WorkPos, layout.WorkSize);
            }

            ConsoleImGuiDrawBody.DrawImGui(data, s_ctx);

            ImGui.End();
        }

        private static bool HandleClose(bool open)
        {
            if (Header && !open)
            {
                ConsoleHost.Enabled = false;
                return true;
            }

            return false;
        }

        internal static void UpdateSuggestions(ConsoleImGuiContext ctx, string input, bool force)
        {
            var state = ctx.State;

            if (!force && !state.UserEdited)
            {
                state.LastQuery = ConsoleUtilities.GetCommandQuery(input);
                state.Suggestions.Clear();
                state.SuggestionIndex = -1;
                return;
            }

            var query = ConsoleUtilities.GetCommandQuery(input);
            if (!force && string.Equals(query, state.LastQuery, StringComparison.Ordinal))
                return;

            state.LastQuery = query;

            // Try to keep the currently selected entry.
            var previousSelectedName = (state.SuggestionIndex >= 0 && state.SuggestionIndex < state.Suggestions.Count)
                ? state.Suggestions[state.SuggestionIndex].Name
                : null;

            ConsoleInputShared.RebuildSuggestions(ConsoleHost.Commands.SortedCommands, query, state.Suggestions);

            if (state.Suggestions.Count == 0)
            {
                state.SuggestionIndex = -1;
                return;
            }

            // If we had a previous selection, try to keep it.
            if (!string.IsNullOrEmpty(previousSelectedName))
                for (var i = 0; i < state.Suggestions.Count; i++)
                    if (string.Equals(state.Suggestions[i].Name, previousSelectedName, StringComparison.Ordinal))
                    {
                        state.SuggestionIndex = i;
                        return;
                    }

            // Otherwise, pick a reasonable default.
            state.SuggestionIndex = Math.Clamp(state.SuggestionIndex, 0, state.Suggestions.Count - 1);
        }
    }
}