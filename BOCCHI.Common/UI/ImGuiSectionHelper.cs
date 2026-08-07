using Dalamud.Bindings.ImGui;

namespace BOCCHI.Common.UI;

public static class ImGuiSectionHelper
{
    public const float DefaultListHeight = 200f;

    /// <summary>
    ///     Child height is content-sized up to <paramref name="maxHeight"/> (avoids empty padding for short lists).
    ///     Prefer this overload for main-window and config lists.
    /// </summary>
    public static BoundedListScope BoundedList(
        string id,
        int itemCount,
        float maxHeight = DefaultListHeight,
        float extraPerItem = 0f)
    {
        if (itemCount <= 0)
        {
            // Still open a minimal child so callers can keep a uniform Dispose path.
            float min = ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y * 2f;
            return new BoundedListScope(id, min);
        }

        float row = ImGui.GetTextLineHeightWithSpacing() + ImGui.GetFrameHeightWithSpacing() + extraPerItem;
        float padding = ImGui.GetStyle().WindowPadding.Y * 2f;
        float minHeight = row + padding;
        // Math.Clamp throws if min > max (tall rows + small maxHeight / high UI scale).
        float height = Math.Clamp(itemCount * row + padding, minHeight, Math.Max(minHeight, maxHeight));
        return new BoundedListScope(id, height);
    }

    /// <summary>Fixed-height child. Prefer the itemCount overload when the list length is known.</summary>
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
