using System;
using System.Numerics;
using ImGuiNET;

namespace UnityEssentials
{
    internal static class ConsoleImGuiDrawBody
    {
        internal const string LogBodyChildId = "##console_body";

        internal static void DrawImGui(ConsoleData data, ConsoleImGuiContext ctx)
        {
            if (ctx.Body)
            {
                // Leave room for the input row.
                var footerHeight = ImGui.GetFrameHeightWithSpacing() + 6f;

                // Log output.
                var avail = ImGui.GetContentRegionAvail();
                var bodySize = new Vector2(avail.X, MathF.Max(50, avail.Y - footerHeight));
                DrawLogList(data, bodySize, ctx.Collapse);

                ImGui.Separator();
            }

            ConsoleImGuiDrawInputBar.DrawImGui(ctx);
            ConsoleImGuiDrawFlyout.DrawImGui(ctx);

            if (!ctx.InputState.UserEdited)
                ctx.InputState.LastQuery = ConsoleImGuiUtilities.GetCommandQuery(ctx.InputState.Input);
        }

        private static void DrawLogList(ConsoleData data, Vector2 bodySize, bool collapse)
        {
            ImGui.BeginChild(LogBodyChildId, bodySize, ImGuiChildFlags.None, ImGuiWindowFlags.None);
            ImGui.PushTextWrapPos(0);

            // Filtered list of newest-offset indices.
            var visibleIndices = ConsoleImGuiUtilities.GetVisibleIndices(data);
            for (var row = 0; row < visibleIndices.Count; row++)
                DrawRow(data, visibleIndices[row], collapse);

            ImGui.PopTextWrapPos();
            ImGui.EndChild();
        }

        private static void DrawRow(ConsoleData data, int newestOffset, bool collapse)
        {
            var entry = data.GetNewest(newestOffset);

            var color = ConsoleImGuiUtilities.GetColor(entry.Severity);
            if (color.HasValue)
                ImGui.PushStyleColor(ImGuiCol.Text, color.Value);

            // Show the message; add a count suffix only when collapsing duplicates.
            ImGui.TextUnformatted(entry.Message + (collapse && entry.Count > 1 ? $" (x{entry.Count})" : string.Empty));

            if (color.HasValue)
                ImGui.PopStyleColor();

            if (!string.IsNullOrEmpty(entry.StackTrace))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
                ImGui.TextUnformatted(entry.StackTrace);
                ImGui.PopStyleColor();
            }

            var atBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - ImGui.GetTextLineHeight();
            if (atBottom)
                ImGui.SetScrollHereY(1f);
        }
    }
}