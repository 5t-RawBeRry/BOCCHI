namespace BOCCHI.Common;

public enum MainWindowSection
{
    Trackers = 0,
    Automation = 1,
    World = 2,
    Treasure = 3
}

public interface IDynamicRenderer
{
    uint Order => 0;

    MainWindowSection Section => MainWindowSection.Automation;

    string? SubsectionTitle => null;

    void Render();

    bool ShouldRender();
}
