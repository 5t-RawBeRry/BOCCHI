using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class InCriticalEncounterHandler
(
    IAutomatorMemory memory,
    ICriticalEncounterContext context,
    IObjectTable objects,
    ICondition conditions,
    IPathfinder pathfinder,
    AutoRotationController autoRotation,
    IPlayer playerState
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.InCriticalEncounter)
{
    public override StatePriority GetScore() => context.IsInCriticalEncounter() ? StatePriority.VeryHigh : StatePriority.Never;

    public override void Enter()
    {
        base.Enter();
        memory.Forget<WaitingForCriticalEncounterMemory>();
        autoRotation.EnableForActivity();
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        if (conditions[ConditionFlag.Mounted]
            && EzThrottler.Throttle("InCriticalEncounter::Unmount")
            && Actions.Unmount.CanCast())
        {
            Actions.Unmount.Cast();
            pathfinder.Stop();
        }

        InitialCombatApproachMemory<CriticalEncounterId> approach =
            GetApproachMemory(context.GetCriticalEncounterId());

        if (CombatActivityHandler.HandleTargets(
                player,
                playerState,
                context.GetTargets(),
                conditions,
                pathfinder,
                "InCriticalEncounter",
                approach.IsPending,
                deferCombatToBossModAi: true))
        {
            approach.Complete();
        }
    }

    private InitialCombatApproachMemory<CriticalEncounterId> GetApproachMemory(CriticalEncounterId? encounterId)
    {
        if (!memory.TryRemember(out InitialCombatApproachMemory<CriticalEncounterId> approach))
        {
            approach = new();
            memory.TryAdd(approach);
        }

        approach.Track(encounterId);
        return approach;
    }
}
