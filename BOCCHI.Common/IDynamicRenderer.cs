namespace BOCCHI.Common;

public enum MainWindowSection
{
    Trackers = 0,
    Automation = 1,
    PotsTreasure = 2,
    MobFarmer = 3,
    World = 4,
    Treasure = 5
}

public interface IDynamicRenderer
{
    uint Order => 0;

    MainWindowSection Section => MainWindowSection.Automation;

    string? SubsectionTitle => null;

    void Render();

    bool ShouldRender();
}
