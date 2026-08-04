namespace BOCCHI.Common;

public enum MainWindowSection
{
    Trackers = 0,
    Automation = 1,
    MobFarmer = 2,
    World = 3,
    Treasure = 4
}

public interface IDynamicRenderer
{
    uint Order => 0;

    MainWindowSection Section => MainWindowSection.Automation;

    string? SubsectionTitle => null;

    void Render();

    bool ShouldRender();
}
