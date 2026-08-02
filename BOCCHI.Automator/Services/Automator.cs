using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services.Goals;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.States;
using System.Numerics;

namespace BOCCHI.Automator.Services;

public class Automator
(
    IAutomatorMemory memory,
    Func<IStateMachine<AutomatorState>> stateMachineFactory,
    IPathCalculator calculator,
    IGoalValidator validator,
    IAutomatorContext context,
    IChainManager manager,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    IZoneProvider zones,
    IObjectTable objects,
    IChatGui chat,
    FatesConfig fatesConfig,
    ILogger<Automator> logger
) : IAutomator, IOnUpdate, IOnStop
{
    private IStateMachine<AutomatorState>? stateMachine;

    private IStateMachine<AutomatorState> StateMachine => stateMachine ??= stateMachineFactory();

    public bool Enabled => context.Enabled;

    public AutomatorState? CurrentState => Enabled ? StateMachine.State : null;

    public void OnStop() => StopAutomation();

    public void Toggle()
    {
        context.Toggle();
        chat.Print(Enabled ? "[BOCCHI] Illegal Mode On" : "[BOCCHI] Illegal Mode Off");
        if (!Enabled)
        {
            StopAutomation();
        }
    }

    public void Render()
    {
        StateMachine.Render();
    }

    public void Update()
    {
        if (!Enabled)
        {
            return;
        }

        if (memory.TryRemember<GoalMemory>(out GoalMemory goal))
        {
            if (!validator.Validate(goal.Goal))
            {
                if (goal.Goal.GoalType is FateGoal fateGoal)
                {
                    TryStartPotChestFarm(fateGoal.id);
                }

                logger.Info("Goal no longer valid — aborting pathfinding");
                memory.Forget<GoalMemory>();
                memory.Forget<GoalPathStepMemory>();
                memory.Forget<WaitingForCriticalEncounterMemory>();
                manager.CancelWhere(name => name.StartsWith("PathStep::", StringComparison.Ordinal));
                pathfinder.Stop();
                vnav.Stop();
            }
            else if (!memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _)
                     && !memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory _)
                     && !memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _))
            {
                memory.TryAdd(new GoalPathStepMemory(goal.Goal, calculator));
            }
        }

        StateMachine.Update();
    }

    private void StopAutomation()
    {
        memory.Wipe();
        manager.CancelAll();
        pathfinder.Stop();
        vnav.Stop();
    }

    private void TryStartPotChestFarm(FateId fateId)
    {
        if (!fatesConfig.ShouldFarmPotChests || memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _))
        {
            return;
        }

        IZone zone = zones.GetZone();
        if (!zone.IsPotFate(fateId.Value))
        {
            return;
        }

        if (!zone.GetPotChestData().TryGetValue(fateId.Value, out List<PotChestData>? chestData))
        {
            return;
        }

        List<Vector3> positions = chestData.Select(chest => chest.Position).ToList();
        if (fatesConfig.ShouldFarmRerollPotChests)
        {
            positions.AddRange(zone.GetRerollPotChestData().Select(chest => chest.Position));
        }

        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        positions = positions
            .OrderBy(position => player.Position.Distance(position))
            .ToList();

        if (positions.Count == 0)
        {
            return;
        }

        logger.Info("Starting pot chest farm for fate {FateId} with {Count} chest positions", fateId.Value, positions.Count);
        memory.TryAdd(new PotChestFarmMemory(fateId, positions));
    }
}
