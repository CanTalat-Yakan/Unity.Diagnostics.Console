using System.Numerics;
using System.Collections.Generic;

namespace UnityEssentials
{
    /// <summary>
    /// Shared state for the console ImGui renderer.
    /// Keeps the draw helpers from passing big parameter lists around.
    /// </summary>
    internal sealed class ConsoleImGuiContext
    {
        public bool Header;
        public bool Body;
        public bool Collapse;

        public readonly ConsoleInputState State = new();
        public bool RequestFocusInput;

        public Vector2 InputRectMin;
        public Vector2 InputRectMax;
        public bool HasInputRect;

        public void BeginFrame() =>
            HasInputRect = false;
    }
}