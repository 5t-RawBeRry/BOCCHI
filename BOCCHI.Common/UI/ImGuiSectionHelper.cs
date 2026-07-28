using Dalamud.Bindings.ImGui;

namespace BOCCHI.Common.UI;

public static class ImGuiSectionHelper
{
    public const float DefaultListHeight = 200f;

    public static BoundedListScope BoundedList(string id, float maxHeight = DefaultListHeight) => new(id, maxHeight);

    public readonly struct BoundedListScope : IDisposable
    {
        public bool IsOpen { get; }

        public BoundedListScope(string id, float maxHeight)
        {
            IsOpen = ImGui.BeginChild(id, new(0f, maxHeight), true);
        }

        public void Dispose()
        {
            ImGui.EndChild();
        }
    }
}
