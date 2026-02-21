using System;
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
                var bodySize = new System.Numerics.Vector2(avail.X, MathF.Max(50, avail.Y - footerHeight));
                DrawLogList(data, bodySize, ctx.Collapse);

                ImGui.Separator();
            }

            ConsoleImGuiDrawInputBar.DrawImGui(ctx);
            ConsoleImGuiDrawFlyout.DrawImGui(ctx);

            // Keep query bookkeeping here (the orchestrator).
            if (!ctx.InputState.UserEdited)
                ctx.InputState.LastQuery = ConsoleImGuiUtilities.GetCommandQuery(ctx.InputState.Input);
        }

        private static unsafe void DrawLogList(ConsoleData data, System.Numerics.Vector2 bodySize, bool collapse)
        {
            ImGui.BeginChild(LogBodyChildId, bodySize, ImGuiChildFlags.None, ImGuiWindowFlags.None);

            ImGui.PushTextWrapPos(0);

            // Only draw visible rows.
            var visibleIndices = ConsoleImGuiUtilities.GetVisibleIndices(data);

            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            clipper.Begin(visibleIndices.Count);

            while (clipper.Step())
            {
                for (var row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
                {
                    var idx = visibleIndices[row];
                    var entry = data.GetNewest(idx);

                    var color = ConsoleImGuiUtilities.GetColor(entry.Severity);
                    if (color.HasValue)
                        ImGui.PushStyleColor(ImGuiCol.Text, color.Value);

                    // Show the message; add a count suffix only when collapsing duplicates.
                    ImGui.TextUnformatted(entry.Message + (collapse && entry.Count > 1 ? $" (x{entry.Count})" : string.Empty));

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

            // Auto-scroll only when we're already at the bottom.
            var atBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - ImGui.GetTextLineHeight();
            if (atBottom)
                ImGui.SetScrollHereY(1f);

            ImGui.EndChild();
        }
    }
}