using BOCCHI.Common;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using Ocelot.Services.UI;

namespace BOCCHI.CriticalEncounters;

public class CriticalEncountersRenderer(
    ICriticalEncounterRepository criticalEncounters,
    IBrandingService branding,
    IUIService ui
) : IDynamicRenderer
{
    public void Render()
    {
        foreach (var criticalEncounter in criticalEncounters.SnapshotWithoutForkedTower())
        {
            ActivitySnapshotRenderer.Render(
                ui,
                branding.DalamudYellow,
                criticalEncounter.Name,
                null,
                ("Id", criticalEncounter.Id),
                ("Position", criticalEncounter.Position.ToString("f2")),
                ("State", criticalEncounter.State)
            );
        }
    }

    public bool ShouldRender()
    {
        return true;
    }
}
