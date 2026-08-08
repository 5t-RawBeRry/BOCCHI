using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Ipc.VNavmesh;
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
    IVNavmeshIpc vnav,
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
        PathStepSoftStop.Cancel(chains);
        // Survey / World PathTo use ActivityGoto chains — don't kill them on idle handoff.
        if (!IsNavigationInterrupted())
        {
            StopMovement();
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
            StopMovement();
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

        // Inside cyan (idle band or closer / magenta) — stop; do not path into the crystal.
        if (zone.IsWithinIdleWait(player.Position))
        {
            idle.ApproachCandidateIndex = 0;
            StopMovement();
            return;
        }

        if (!pathfinder.IsIdle())
        {
            return;
        }

        // Path to spots spread between magenta (Lifestream) and cyan (idle outer).
        List<Vector3> candidates = zone.GetIdleWaitCandidates(player.Position)
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

    private void StopMovement()
    {
        pathfinder.Stop();
        vnav.Stop();
    }

    private bool IsNavigationInterrupted() =>
        memory.TryRemember<NavigationInterruptedMemory>(out NavigationInterruptedMemory _);
}
