using System;
using System.Numerics;
using ImGuiNET;

namespace UnityEssentials
{
    internal static class ConsoleImGuiLayout
    {
        // Overlay (no-header) layout tuning
        // Keep these as one-stop knobs for positioning/sizing.
        private const float OverlayAnchorRatioY = 5f / 6f;           // Window bottom is anchored at this fraction of the work-rect height.
        private const float OverlayWidthRatio = 2f / 3f;             // Default width is this fraction of the work-rect width.
        private const float OverlayMinWidth = 420f;
        private const float OverlayMaxWidth = 900f;

        private const float OverlayBodyHeightRatio = 0.45f;          // When body is visible (not input-only).
        private const float OverlayBodyMinHeight = 220f;
        private const float OverlayBodyMaxHeight = 520f;

        internal readonly struct LayoutContext
        {
            public readonly bool InputOnly;
            public readonly bool HeaderOnly;
            public readonly Vector2 WorkPos;
            public readonly Vector2 WorkSize;

            public LayoutContext(bool inputOnly, bool headerOnly, Vector2 workPos, Vector2 workSize)
            {
                InputOnly = inputOnly;
                HeaderOnly = headerOnly;
                WorkPos = workPos;
                WorkSize = workSize;
            }
        }

        public static LayoutContext CreateLayoutContext(bool header, bool body)
        {
            var inputOnly = !header && !body;
            var headerOnly = header && !body;

            if (!header)
            {
                var vp = ImGui.GetMainViewport();
                return new LayoutContext(inputOnly, headerOnly, vp.WorkPos, vp.WorkSize);
            }

            return new LayoutContext(inputOnly, headerOnly, default, default);
        }

        public static void ConfigureLayout(bool header, in LayoutContext ctx)
        {
            if (!header)
                ConfigureOverlayLayout(ctx.InputOnly, ctx.WorkPos, ctx.WorkSize);

            if (ctx.InputOnly)
                ConfigureInputOnlyLayout();
            else if (ctx.HeaderOnly)
                ConfigureHeaderOnlyLayout();
            else
                ImGui.SetNextWindowSize(new Vector2(900, 500), ImGuiCond.FirstUseEver);
        }

        private static void ConfigureOverlayLayout(bool inputOnly, Vector2 workPos, Vector2 workSize)
        {
            // Undecorated overlay layout.
            // This mode is intended to be fully controlled by code (NoMove/NoResize).
            //
            // Behavior:
            // - Size: width is a fraction of the work-rect width; height is single-row for input-only or a clamped ratio for body.
            // - Position: centered horizontally; vertically anchored so the window's bottom sits at OverlayAnchorRatioY of the work-rect.

            var defaultWidth = Math.Clamp(workSize.X * OverlayWidthRatio, OverlayMinWidth, OverlayMaxWidth);
            defaultWidth = MathF.Min(defaultWidth, workSize.X);

            var defaultX = workPos.X + (workSize.X - defaultWidth) * 0.5f;

            // Height: one row when input-only, otherwise a reasonable fraction of the work area.
            var defaultHeight = inputOnly
                ? (ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y * 2f)
                : Math.Clamp(workSize.Y * OverlayBodyHeightRatio, OverlayBodyMinHeight, OverlayBodyMaxHeight);

            defaultHeight = MathF.Min(defaultHeight, workSize.Y);

            // Anchor the bottom of the window at OverlayAnchorRatioY from the top of the work area.
            var targetBottomY = workPos.Y + workSize.Y * OverlayAnchorRatioY;
            var defaultY = targetBottomY - defaultHeight;

            // Clamp to the work area.
            var minX = workPos.X;
            var maxX = workPos.X + workSize.X - defaultWidth;
            var minY = workPos.Y;
            var maxY = workPos.Y + workSize.Y - defaultHeight;

            defaultX = Math.Clamp(defaultX, minX, maxX);
            defaultY = Math.Clamp(defaultY, minY, maxY);

            var pos = new Vector2(defaultX, defaultY);
            var size = new Vector2(defaultWidth, defaultHeight);

            // Force every frame so it always follows the current work rect.
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        }

        /// <summary>
        /// Call after Begin for headerless overlay mode, to hard-override anything ImGui applied during Begin
        /// (saved settings, previous state, etc.).
        /// </summary>
        public static void ForceOverlayWindowToLayout(bool inputOnly, Vector2 workPos, Vector2 workSize)
        {
            // Replicate the overlay computation and then apply with SetWindow* (post-Begin).
            var defaultWidth = Math.Clamp(workSize.X * OverlayWidthRatio, OverlayMinWidth, OverlayMaxWidth);
            defaultWidth = MathF.Min(defaultWidth, workSize.X);

            var defaultX = workPos.X + (workSize.X - defaultWidth) * 0.5f;

            var defaultHeight = inputOnly
                ? (ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y * 2f)
                : Math.Clamp(workSize.Y * OverlayBodyHeightRatio, OverlayBodyMinHeight, OverlayBodyMaxHeight);

            defaultHeight = MathF.Min(defaultHeight, workSize.Y);

            var targetBottomY = workPos.Y + workSize.Y * OverlayAnchorRatioY;
            var defaultY = targetBottomY - defaultHeight;

            var minX = workPos.X;
            var maxX = workPos.X + workSize.X - defaultWidth;
            var minY = workPos.Y;
            var maxY = workPos.Y + workSize.Y - defaultHeight;

            defaultX = Math.Clamp(defaultX, minX, maxX);
            defaultY = Math.Clamp(defaultY, minY, maxY);

            var pos = new Vector2(defaultX, defaultY);
            var size = new Vector2(defaultWidth, defaultHeight);

            ImGui.SetWindowPos(pos, ImGuiCond.Always);
            ImGui.SetWindowSize(size, ImGuiCond.Always);
        }

        private static void ConfigureInputOnlyLayout()
        {
            // Input-only: lock the window height to a single row.
            var style = ImGui.GetStyle();
            var height = ImGui.GetFrameHeight() + style.WindowPadding.Y * 2f;

            ImGui.SetNextWindowSizeConstraints(
                new Vector2(200, height),
                new Vector2(float.MaxValue, height));

            ImGui.SetNextWindowSize(new Vector2(900, height), ImGuiCond.Always);
        }

        private static void ConfigureHeaderOnlyLayout()
        {
            // Header + input: lock height, allow horizontal resize.
            var style = ImGui.GetStyle();

            var contentHeight = ImGui.GetFrameHeight();

            // Approximate the title bar height using the frame height.
            var titleBarHeight = ImGui.GetFrameHeight();
            var height = titleBarHeight + contentHeight + style.WindowPadding.Y * 2f;

            ImGui.SetNextWindowSizeConstraints(
                new Vector2(260, height),
                new Vector2(float.MaxValue, height));

            ImGui.SetNextWindowSize(new Vector2(900, height), ImGuiCond.FirstUseEver);
        }

        public static ImGuiWindowFlags BuildWindowFlags(bool header, bool inputOnly)
        {
            var flags = ImGuiWindowFlags.NoCollapse;

            if (!header)
            {
                flags |= ImGuiWindowFlags.NoTitleBar;
                flags |= ImGuiWindowFlags.NoMove;
                flags |= ImGuiWindowFlags.NoResize;

                if (inputOnly)
                    flags |= ImGuiWindowFlags.NoSavedSettings;
            }

            return flags;
        }

        public static void ClampToWorkArea(Vector2 workPos, Vector2 workSize)
        {
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();

            var minX = workPos.X;
            var maxX = workPos.X + workSize.X - size.X;
            var minY = workPos.Y;
            var maxY = workPos.Y + workSize.Y - size.Y;

            var clamped = new Vector2(
                Math.Clamp(pos.X, minX, maxX),
                Math.Clamp(pos.Y, minY, maxY));

            if (clamped.X != pos.X || clamped.Y != pos.Y)
                ImGui.SetWindowPos(clamped);
        }
    }
}
