using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Services.Gate;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class ReturningHandler
(
    IAutomatorMemory memory,
    IZoneProvider zones,
    ICondition conditions,
    IAddonLifecycle addons,
    AutomatorConfig config,
    IFateRepository fates,
    IPlayer player,
    IGateService gate
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Returning)
{
    public override StatePriority GetScore()
    {
        // Return while dead accepts the death prompt and force-respawns.
        if (conditions[ConditionFlag.Unconscious])
        {
            return StatePriority.Never;
        }

        if (memory.TryRemember<ReturningStateMemory>(out ReturningStateMemory _))
        {
            return StatePriority.VeryHigh;
        }

        if (!memory.TryRemember<IdleStateMemory>(out IdleStateMemory idle) || zones.GetZone().IsInBasecamp())
        {
            return StatePriority.Never;
        }

        // Waiting inside / near the goal FATE circle — don't Return-to-base (#84).
        if (IsNearActiveFateGoal())
        {
            return StatePriority.Never;
        }

        TimeSpan time = idle.GetIdleTime();
        TimeSpan maxRemoteIdle = TimeSpan.FromSeconds(config.MaxRemoteIdleTimeSeconds);

        return time >= maxRemoteIdle ? StatePriority.VeryLow : StatePriority.Never;
    }

    public override void Handle()
    {
        if (conditions[ConditionFlag.Unconscious])
        {
            memory.Forget<ReturningStateMemory>();
            return;
        }

        if (gate.Milliseconds(this, "ReturningHandler::Gate", 500))
        {
            return;
        }

        bool isCasting = conditions[ConditionFlag.Casting] || conditions[ConditionFlag.Casting87];
        bool isBetweenAreas = conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51];

        if (isCasting || isBetweenAreas)
        {
            return;
        }

        IZone zone = zones.GetZone();
        if (zone.IsInBasecamp())
        {
            memory.Forget<ReturningStateMemory>();
            return;
        }

        // Still mid-return (BetweenAreas already gated above). Don't re-cast while on CD /
        // after a successful cast left ReturningStateMemory stuck with a bad IsInBasecamp().
        if (memory.TryRemember<ReturningStateMemory>(out ReturningStateMemory _))
        {
            if (!Actions.Return.CanCast())
            {
                return;
            }
        }

        if (Actions.Return.CanCast())
        {
            memory.TryAdd<ReturningStateMemory>();
            Actions.Return.Cast();
        }
    }

    public override void Enter()
    {
        base.Enter();
        addons.RegisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoListener);
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);
        addons.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoListener);
    }

    private unsafe void SelectYesNoListener(AddonEvent ev, AddonArgs args)
    {
        // Death / raise prompts also use SelectYesno — never auto-accept while unconscious.
        if (conditions[ConditionFlag.Unconscious])
        {
            return;
        }

        // Same filter as pre-rewrite TeleporterModule — only Return, not shops/etc.
        ReturnYesNo.TryAccept((AtkUnitBase*)args.Addon.Address);
    }

    private bool IsNearActiveFateGoal()
    {
        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal) || goal.Goal.GoalType is not FateGoal fateGoal)
        {
            return false;
        }

        Fate? fate = fates.Snapshot().FirstOrDefault(f => f.Id.Value == fateGoal.id.Value);
        if (fate == null)
        {
            return false;
        }

        float radius = fate.Radius > 0f
            ? fate.Radius * 0.9f
            : NavigationConstants.EventArrivalRadius;
        return player.Position.Distance2D(fate.Position) <= radius;
    }
}
