using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.States.Score;
using Ocelot.Windows;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class IdleHandler(
    IAutomatorMemory memory,
    IZoneProvider zones,
    IObjectTable objects,
    IPathfinder pathfinder,
    IChainManager chains,
    IUIService ui,
    ITranslator<MainWindow> translator
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Idle)
{
    public override StatePriority GetScore()
    {
        return StatePriority.Lowest;
    }

    public override void Enter()
    {
        base.Enter();
        chains.CancelAll();
        pathfinder.Stop();
        memory.TryAdd<IdleStateMemory>();
    }


    public override void Exit(AutomatorState next)
    {
        base.Exit(next);
        memory.Forget<IdleStateMemory>();
        pathfinder.Stop();
    }

    public override void Handle()
    {
        if (!pathfinder.IsIdle())
        {
            return;
        }

        var zone = zones.GetZone();
        if (!zone.IsInBasecamp())
        {
            return;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        var interactPosition = zone.GetMainAetheryte().GetInteractPosition();
        var distance = player.Position.Distance2D(interactPosition);
        const float maxInteractDistance = AethernetData.InteractRadius - 0.5f;

        if (distance <= maxInteractDistance)
        {
            return;
        }

        // This 0.7f stops any jitter with the above distance check
        var goal = interactPosition.GetApproachPosition(player.Position, maxInteractDistance - 0.7f, 30f);
        pathfinder.PathfindAndMoveTo(new PathfinderConfig(goal)
        {
            DistanceThreshold = 1f,
            ShouldSnapToFloor = true,
        });
    }

    public override void Render()
    {
        base.Render();

        if (memory.TryRemember<IdleStateMemory>(out var idle))
        {
            ui.LabelledValue(translator.T(".automation.automator.time_idle"), idle.GetIdleTime().Format());
        }
    }
}
