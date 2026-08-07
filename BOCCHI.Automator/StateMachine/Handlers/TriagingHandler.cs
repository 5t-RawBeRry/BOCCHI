using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.States.Score;
using Action = Ocelot.Actions.Action;

namespace BOCCHI.Automator.StateMachine.Handlers;

/// <summary>
///     After FATE/CE: Chemist Revive on nearby dead players. Skips anyone who already has Raise pending.
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

    private const float RaiseRangeYalms = 28f;

    /// <summary>How long to watch for corpses after latch before giving up (no Chemist swap).</summary>
    private static readonly TimeSpan CorpseWaitWindow = TimeSpan.FromSeconds(8);

    /// <summary>Wait after entering before attempting a phantom-job change (game rejects immediate swaps).</summary>
    private static readonly TimeSpan JobSwapSettle = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(90);

    private DateTimeOffset sessionStartedUtc = DateTimeOffset.MinValue;

    public override StatePriority GetScore()
    {
        if (!context.IsIllegalMode || !config.EnableTriageMode)
        {
            memory.Forget<PendingTriageMemory>();
            memory.Forget<TriagingMemory>();
            return StatePriority.Never;
        }

        if (memory.TryRemember<TriagingMemory>(out TriagingMemory _))
        {
            if (conditions[ConditionFlag.Unconscious])
            {
                return StatePriority.Never;
            }

            return StatePriority.Critical;
        }

        if (conditions[ConditionFlag.Unconscious] || conditions[ConditionFlag.InCombat])
        {
            return StatePriority.Never;
        }

        if (!memory.TryRemember<PendingTriageMemory>(out PendingTriageMemory pending))
        {
            return StatePriority.Never;
        }

        SupportJob chemist = supportJobs.Create(SupportJobId.PhantomChemist);
        if (chemist.Level < 1)
        {
            memory.Forget<PendingTriageMemory>();
            return StatePriority.Never;
        }

        // Do not enter / swap Chemist unless someone nearby actually needs a raise.
        if (FindNearestRaiseableCorpse() != null)
        {
            return StatePriority.Always;
        }

        if (DateTimeOffset.UtcNow - pending.LatchedUtc >= CorpseWaitWindow)
        {
            memory.Forget<PendingTriageMemory>();
            logger.Info("Triage Mode skipped — no raisable targets nearby");
        }

        return StatePriority.Never;
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

        // Combat can start mid-triage — pause, stay committed via GetScore.
        if (conditions[ConditionFlag.InCombat] || IsJobChangeBlocked())
        {
            return;
        }

        if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            if (!conditions[ConditionFlag.Mounting])
            {
                Actions.Dismount.Cast();
            }

            return;
        }

        IPlayerCharacter? corpse = FindNearestRaiseableCorpse();
        if (corpse == null)
        {
            logger.Info("Triage Mode: no more raisable targets");
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

        if (distance > RaiseRangeYalms)
        {
            if (pathfinder.IsIdle())
            {
                pathfinder.PathfindAndMoveTo(new(corpse.Position)
                {
                    DistanceThreshold = RaiseRangeYalms - 2f,
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

        if (changer.IsBusy())
        {
            return;
        }

        // Game rejects rapid ChangeSupportJob spam with "unable to change phantom jobs".
        if (!EzThrottler.Throttle("TriagingHandler::JobSwap", 2500))
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

    private bool IsJobChangeBlocked()
    {
        return conditions[ConditionFlag.BetweenAreas]
            || conditions[ConditionFlag.BetweenAreas51]
            || conditions[ConditionFlag.Casting]
            || conditions[ConditionFlag.Casting87]
            || conditions[ConditionFlag.Jumping]
            || conditions[ConditionFlag.Jumping61]
            || conditions[ConditionFlag.Occupied]
            || conditions[ConditionFlag.Occupied30]
            || conditions[ConditionFlag.Occupied33]
            || conditions[ConditionFlag.Occupied38]
            || conditions[ConditionFlag.Occupied39]
            || conditions[ConditionFlag.OccupiedInEvent]
            || conditions[ConditionFlag.OccupiedInQuestEvent]
            || conditions[ConditionFlag.OccupiedSummoningBell]
            || conditions[ConditionFlag.OccupiedInCutSceneEvent];
    }

    private void FinishTriage()
    {
        pathfinder.Stop();
        memory.Forget<PendingTriageMemory>();
        memory.Forget<TriagingMemory>();
    }

    private IPlayerCharacter? FindNearestRaiseableCorpse()
    {
        if (objects.LocalPlayer is not { } self)
        {
            return null;
        }

        IPlayerCharacter? best = null;
        float bestDist = float.MaxValue;

        foreach (IGameObject obj in objects)
        {
            if (obj is not IPlayerCharacter player
                || player.EntityId == self.EntityId
                || !player.IsDead
                || !player.IsTargetable)
            {
                continue;
            }

            // Already has a raise prompt — do not waste Revive (Discord reminder).
            if (player.StatusList.Has(PlayerStatuses.Raise))
            {
                continue;
            }

            float dist = self.Position.Distance2D(player.Position);
            if (dist > RaiseRangeYalms + 15f || dist >= bestDist)
            {
                continue;
            }

            best = player;
            bestDist = dist;
        }

        return best;
    }

    private static unsafe bool TryCastRevive(IGameObject target)
    {
        return ActionManager.Instance()->UseAction(
            ActionType.Action,
            PhantomActions.Revive,
            target.GameObjectId);
    }
}
