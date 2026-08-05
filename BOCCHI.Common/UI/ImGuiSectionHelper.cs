using Dalamud.Bindings.ImGui;

namespace BOCCHI.Common.UI;

public static class ImGuiSectionHelper
{
    public const float DefaultListHeight = 200f;

    public static BoundedListScope BoundedList(string id, float maxHeight = DefaultListHeight) => new(id, maxHeight);

    /// <summary>
    ///     Child height is content-sized up to <paramref name="maxHeight"/> (avoids empty padding for short lists).
    /// </summary>
    public static BoundedListScope BoundedList(
        string id,
        int itemCount,
        float maxHeight = DefaultListHeight,
        float extraPerItem = 0f)
    {
        float row = ImGui.GetTextLineHeightWithSpacing() + ImGui.GetFrameHeightWithSpacing() + extraPerItem;
        float padding = ImGui.GetStyle().WindowPadding.Y * 2f;
        float height = Math.Clamp(itemCount * row + padding, row + padding, maxHeight);
        return new BoundedListScope(id, height);
    }

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
