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

        public readonly List<string> History = new(32);
        public readonly List<ConsoleCommandRegistry.Command> Suggestions = new(16);

        public int SuggestionIndex = -1;
        public bool RequestFocusInput;

        public readonly ConsoleImGuiDrawInputBar.InputState InputState = new();

        public System.Numerics.Vector2 InputRectMin;
        public System.Numerics.Vector2 InputRectMax;
        public bool HasInputRect;

        public void BeginFrame() =>
            HasInputRect = false;
    }
}