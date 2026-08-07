using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.States.Score;
using Action = Ocelot.Actions.Action;

namespace BOCCHI.Automator.StateMachine.Handlers;

/// <summary>
///     After FATE/CE with nearby dead players: Chemist → Revive (skip Raise pending) → restore job → Return.
/// </summary>
public class TriagingHandler
(
    IAutomatorContext context,
    IAutomatorMemory memory,
    AutomatorConfig config,
    ICondition conditions,
    IObjectTable objects,
    ITargetManager targetManager,
    ISupportJobFactory supportJobs,
    ISupportJobChanger changer,
    IPathfinder pathfinder,
    ILogger<TriagingHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Triaging)
{
    private static readonly Action Revive = new(ActionType.Action, PhantomActions.Revive);

    private static readonly TimeSpan JobSwapSettle = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(90);

    private DateTimeOffset sessionStartedUtc = DateTimeOffset.MinValue;

    public override StatePriority GetScore()
    {
        if (!context.IsIllegalMode || !config.EnableTriageMode)
        {
            TriageSession.Clear(memory);
            return StatePriority.Never;
        }

        if (memory.TryRemember<TriagingMemory>(out TriagingMemory _))
        {
            return conditions[ConditionFlag.Unconscious] ? StatePriority.Never : StatePriority.Critical;
        }

        if (conditions[ConditionFlag.Unconscious] || conditions[ConditionFlag.InCombat])
        {
            return StatePriority.Never;
        }

        if (!memory.TryRemember<PendingTriageMemory>(out PendingTriageMemory _))
        {
            return StatePriority.Never;
        }

        if (!SupportJobChemist.IsUnlocked(supportJobs) || !RaiseableCorpses.Any(objects))
        {
            // No bodies (or Chemist unavailable) — clear and let Return proceed.
            memory.Forget<PendingTriageMemory>();
            return StatePriority.Never;
        }

        return StatePriority.Always;
    }

    public override void Enter()
    {
        base.Enter();
        sessionStartedUtc = DateTimeOffset.UtcNow;
        pathfinder.Stop();
        memory.TryAdd<TriagingMemory>();
        logger.Info("Triage Mode: raising nearby players");
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);
        FinishTriage();
    }

    public override void Handle()
    {
        if (!EzThrottler.Throttle("TriagingHandler::Gate", 250))
        {
            return;
        }

        if (DateTimeOffset.UtcNow - sessionStartedUtc >= SessionTimeout)
        {
            logger.Info("Triage Mode: session timeout");
            FinishTriage();
            return;
        }

        if (conditions[ConditionFlag.Unconscious])
        {
            return;
        }

        if (conditions[ConditionFlag.InCombat] || PhantomJobChangeGate.IsBlocked(conditions))
        {
            return;
        }

        if (DismountAssist.TryDismount(conditions))
        {
            return;
        }

        IPlayerCharacter? corpse = RaiseableCorpses.FindNearest(objects);
        if (corpse == null)
        {
            logger.Info("Triage Mode: done — restoring job then Return");
            FinishTriage();
            return;
        }

        if (!supportJobs.TryGetCurrent(out SupportJob current) || current.Id != SupportJobId.PhantomChemist)
        {
            TrySwapToChemist(current);
            return;
        }

        if (conditions[ConditionFlag.Casting] || conditions[ConditionFlag.Casting87])
        {
            return;
        }

        float distance = objects.LocalPlayer is { } self
            ? self.Position.Distance2D(corpse.Position)
            : float.MaxValue;

        if (distance > RaiseableCorpses.CastRangeYalms)
        {
            if (pathfinder.IsIdle())
            {
                pathfinder.PathfindAndMoveTo(new(corpse.Position)
                {
                    DistanceThreshold = RaiseableCorpses.CastRangeYalms - 2f,
                    ShouldSnapToFloor = true
                });
            }

            return;
        }

        pathfinder.Stop();
        targetManager.Target = corpse;

        if (!Revive.CanCast())
        {
            return;
        }

        if (TryCastRevive(corpse))
        {
            logger.Info("Triage Mode: Revive on {Name}", corpse.Name.TextValue);
        }
    }

    private void TrySwapToChemist(SupportJob? current)
    {
        if (DateTimeOffset.UtcNow - sessionStartedUtc < JobSwapSettle)
        {
            return;
        }

        if (changer.IsBusy() || !EzThrottler.Throttle("TriagingHandler::JobSwap", 2500))
        {
            return;
        }

        if (current is { Id: not SupportJobId.PhantomChemist }
            && !memory.TryRemember<TriageSupportJobMemory>(out TriageSupportJobMemory _))
        {
            memory.TryAdd(new TriageSupportJobMemory(current.Id));
        }

        changer.Change(SupportJobId.PhantomChemist);
    }

    private void FinishTriage()
    {
        pathfinder.Stop();
        TriageSession.Clear(memory);
    }

    private static unsafe bool TryCastRevive(IGameObject target) =>
        ActionManager.Instance()->UseAction(ActionType.Action, PhantomActions.Revive, target.GameObjectId);
}
