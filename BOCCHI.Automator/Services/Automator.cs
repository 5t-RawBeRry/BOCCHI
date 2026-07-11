using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services.Goals;
using BOCCHI.Automator.Services.Paths;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using Ocelot.States;

namespace BOCCHI.Automator.Services;

public class Automator(
    IAutomatorMemory memory,
    IStateMachine<AutomatorState> stateMachine,
    IPathCalculator calculator,
    IGoalValidator validator,
    IAutomatorContext context,
    IChainManager manager,
    IZoneProvider zones,
    IObjectTable objects,
    FatesConfig fatesConfig,
    ILogger<Automator> logger
) : IAutomator, IOnUpdate
{
    public bool Enabled
    {
        get => context.Enabled;
    }

    public void Toggle()
    {
        context.Toggle();
    }

    public void Render()
    {
        stateMachine.Render();
    }

    public void Update()
    {
        if (!Enabled)
        {
            memory.Wipe();
            manager.CancelAll();
            return;
        }

        if (memory.TryRemember<GoalMemory>(out var goal))
        {
            if (!validator.Validate(goal.Goal))
            {
                if (goal.Goal.GoalType is FateGoal fateGoal)
                {
                    TryStartPotChestFarm(fateGoal.id);
                }

                memory.Forget<GoalMemory>();
                memory.Forget<GoalPathStepMemory>();
                memory.Forget<WaitingForCriticalEncounterMemory>();
            }
            else if (!memory.TryRemember<GoalPathStepMemory>(out var _)
                     && !memory.TryRemember<WaitingForCriticalEncounterMemory>(out var _)
                     && !memory.TryRemember<ApplyingBuffsMemory>(out var _))
            {
                memory.TryAdd(new GoalPathStepMemory(goal.Goal, calculator, logger));
            }
        }

        stateMachine.Update();
    }

    private void TryStartPotChestFarm(FateId fateId)
    {
        if (!fatesConfig.ShouldFarmPotChests || memory.TryRemember<PotChestFarmMemory>(out _))
        {
            return;
        }

        var zone = zones.GetZone();
        if (!zone.IsPotFate(fateId.Value))
        {
            return;
        }

        if (!zone.GetPotChestData().TryGetValue((int)fateId.Value, out var chestData))
        {
            return;
        }

        var positions = chestData.Select(chest => chest.Position).ToList();
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
