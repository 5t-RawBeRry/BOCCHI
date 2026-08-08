using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
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
using System.Numerics;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class IdleHandler(
    IAutomatorMemory memory,
    IZoneProvider zones,
    IObjectTable objects,
    IPathfinder pathfinder,
    IChainManager chains,
    AutomatorConfig config,
    AutoRotationController autoRotation,
    IUIService ui,
    ITranslator<MainWindow> translator
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Idle)
{
    public override StatePriority GetScore() => StatePriority.Lowest;

    public override void Enter()
    {
        base.Enter();
        chains.CancelWhere(name => name.StartsWith("PathStep::", StringComparison.Ordinal));
        // Survey / World PathTo use ActivityGoto chains — don't kill them on idle handoff.
        if (!IsNavigationInterrupted())
        {
            pathfinder.Stop();
        }

        autoRotation.DisableForTravel();
        memory.TryAdd(new IdleStateMemory(ReturnDelay.Roll(config)));
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);
        memory.Forget<IdleStateMemory>();
        if (!IsNavigationInterrupted())
        {
            pathfinder.Stop();
        }
    }

    public override void Handle()
    {
        if (IsNavigationInterrupted())
        {
            return;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        IZone zone = zones.GetZone();
        if (!zone.IsInBasecamp())
        {
            return;
        }

        if (!memory.TryRemember<IdleStateMemory>(out IdleStateMemory idle))
        {
            return;
        }

        // Stay within Lifestream range of the crystal (Destination pads can sit outside it).
        if (zone.IsWithinLifestreamRange(player.Position))
        {
            idle.ApproachCandidateIndex = 0;
            pathfinder.Stop();
            return;
        }

        if (!pathfinder.IsIdle())
        {
            return;
        }

        List<Vector3> candidates = zone.GetApproachCandidates(player.Position)
            .OrderBy(candidate => player.Position.Distance2D(candidate))
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        if (idle.ApproachCandidateIndex >= candidates.Count)
        {
            idle.ApproachCandidateIndex = 0;
        }

        Vector3 target = candidates[idle.ApproachCandidateIndex];
        idle.ApproachCandidateIndex++;

        SprintAssist.MaybeCast(config.SprintOnAetheryteApproach, inBasecamp: true);
        pathfinder.PathfindAndMoveTo(new PathfinderConfig(target)
        {
            DistanceThreshold = AethernetNavigation.PathfindArrivalRadius,
            ShouldSnapToFloor = false,
        });
    }

    public override void Render()
    {
        base.Render();

        if (memory.TryRemember<IdleStateMemory>(out IdleStateMemory idle))
        {
            ui.LabelledValue(translator.T(".automation.automator.time_idle"), idle.GetIdleTime().Format());
        }
    }

    private bool IsNavigationInterrupted() =>
        memory.TryRemember<NavigationInterruptedMemory>(out NavigationInterruptedMemory _);
}
