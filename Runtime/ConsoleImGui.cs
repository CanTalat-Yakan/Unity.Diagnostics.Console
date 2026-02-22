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
            if (!force && !ctx.InputState.UserEdited)
            {
                ctx.InputState.LastQuery = ConsoleImGuiUtilities.GetCommandQuery(input);
                ctx.Suggestions.Clear();
                ctx.SuggestionIndex = -1;
                return;
            }

            var query = ConsoleImGuiUtilities.GetCommandQuery(input);
            if (!force && string.Equals(query, ctx.InputState.LastQuery, StringComparison.Ordinal))
                return;

            ctx.InputState.LastQuery = query;

            // Try to keep the currently selected entry.
            var previousSelectedName = (ctx.SuggestionIndex >= 0 && ctx.SuggestionIndex < ctx.Suggestions.Count)
                ? ctx.Suggestions[ctx.SuggestionIndex].Name
                : null;

            ctx.Suggestions.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                ctx.SuggestionIndex = -1;
                return;
            }

            // Iterate the already-sorted command list.
            var cmds = ConsoleHost.Commands.SortedCommands;
            for (var i = 0; i < cmds.Count; i++)
            {
                var cmd = cmds[i];

                // Don't suggest the exact command name if it's already fully typed.
                if (string.Equals(cmd.Name, query, StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = cmd.Name;
                var match = ConsoleImGuiUtilities.MatchCommandQuery(name, query);
                if (!match.IsMatch)
                    continue;

                // Prefer direct prefix matches before token (separator) matches.
                if (match.IsPrefixMatch)
                    ctx.Suggestions.Insert(0, cmd);
                else
                    ctx.Suggestions.Add(cmd);

                if (ctx.Suggestions.Count >= 10)
                    break;
            }

            // Re-stabilize: keep prefix matches first, then everything else, both groups alphabetical.
            if (ctx.Suggestions.Count > 1)
            {
                ctx.Suggestions.Sort((a, b) =>
                {
                    var ma = ConsoleImGuiUtilities.MatchCommandQuery(a.Name, query);
                    var mb = ConsoleImGuiUtilities.MatchCommandQuery(b.Name, query);

                    var pa = ma.IsPrefixMatch ? 0 : 1;
                    var pb = mb.IsPrefixMatch ? 0 : 1;
                    var p = pa.CompareTo(pb);
                    return p != 0 ? p : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
                });
            }

            if (ctx.Suggestions.Count == 0)
            {
                ctx.SuggestionIndex = -1;
                return;
            }

            // If we had a previous selection, try to keep it.
            if (!string.IsNullOrEmpty(previousSelectedName))
                for (var i = 0; i < ctx.Suggestions.Count; i++)
                    if (string.Equals(ctx.Suggestions[i].Name, previousSelectedName, StringComparison.Ordinal))
                    {
                        ctx.SuggestionIndex = i;
                        return;
                    }

            // Otherwise, pick a reasonable default.
            ctx.SuggestionIndex = Math.Clamp(ctx.SuggestionIndex, 0, ctx.Suggestions.Count - 1);
        }

        internal static void PushHistory(ConsoleImGuiContext ctx, string line)
        {
            if (ctx.History.Count == 0 || !string.Equals(ctx.History[^1], line, StringComparison.Ordinal))
                ctx.History.Add(line);

            if (ctx.History.Count > 50)
                ctx.History.RemoveAt(0);

            ctx.InputState.HistoryIndex = -1;
        }
    }
}