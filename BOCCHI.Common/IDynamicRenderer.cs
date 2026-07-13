namespace BOCCHI.Common;

public enum MainWindowSection
{
    Trackers = 0,
    Automation = 1,
    World = 2,
    Treasure = 3,
}

public interface IDynamicRenderer
{
    uint Order
    {
        get => 0;
    }

    MainWindowSection Section
    {
        get => MainWindowSection.Automation;
    }

    string? SubsectionTitle
    {
        get => null;
    }

    void Render();

    bool ShouldRender();
}
