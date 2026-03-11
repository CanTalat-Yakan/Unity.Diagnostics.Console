using System;
using System.Numerics;
using ImGuiNET;

namespace UnityEssentials
{
    internal static class ConsoleImGuiDrawFlyout
    {
        internal const string SuggestionsScrollId = "##console_suggestions_scroll";
        internal const int MaxVisible = 6;

        internal static void DrawImGui(ConsoleImGuiContext ctx)
        {
            var state = ctx.State;

            // No suggestions to show.
            if (!state.UserEdited)
                return;

            if (state.Suggestions.Count == 0)
                return;

            if (string.IsNullOrWhiteSpace(ConsoleUtilities.GetCommandQuery(state.Input)))
                return;

            if (!ctx.HasInputRect)
                return;

            // Position relative to the captured input item.
            var inputMin = ctx.InputRectMin;
            var inputMax = ctx.InputRectMax;

            var style = ImGui.GetStyle();
            var visibleCount = MathF.Min(MaxVisible, state.Suggestions.Count);

            var rowHeight = ImGui.GetFrameHeight();
            var contentHeight = visibleCount * rowHeight;

            var height = contentHeight + style.WindowPadding.Y;
            var width = MathF.Max(220f, inputMax.X - inputMin.X);

            // Render above the input (tooltips are drawn in ImGui's top layer).
            ImGui.SetNextWindowPos(new Vector2(inputMin.X, inputMin.Y - height - 2f), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);

            ImGui.BeginTooltip();

            // Clamp selection if the list shrank.
            if (state.SuggestionIndex >= state.Suggestions.Count)
                state.SuggestionIndex = state.Suggestions.Count - 1;

            // Once there's a token, default to the first entry.
            if (state.SuggestionIndex < 0)
                state.SuggestionIndex = 0;

            // Use a child so arrow-key navigation can keep the selection visible.
            ImGui.BeginChild(SuggestionsScrollId, Vector2.Zero, ImGuiChildFlags.None,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMouseInputs);

            for (var i = 0; i < state.Suggestions.Count; i++)
            {
                var cmd = state.Suggestions[i];
                var isSelected = i == state.SuggestionIndex;

                // Show command names only.
                var display = cmd.Name;

                ImGui.PushID(i);

                ImGui.Selectable(display, isSelected);

                // Keep the active item in view when moving with the arrow keys.
                if (isSelected)
                    ImGui.SetScrollHereY(0.5f);

                ImGui.PopID();
            }

            ImGui.EndChild();
            ImGui.EndTooltip();
        }
    }
}