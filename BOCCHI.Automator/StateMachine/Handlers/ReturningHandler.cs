using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
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
using FFXIVClientStructs.FFXIV.Client.UI;
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
    IFateRepository fates,
    IPlayer player,
    IGateService gate,
    IGameGui gui,
    AutoRotationController autoRotation
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

        // Opportunistic Return while idle (OC has no Return CD). Keep below ChoosingActivity.
        return idle.IsReadyToReturn() ? StatePriority.VeryLow : StatePriority.Never;
    }

    public override void Enter()
    {
        base.Enter();
        autoRotation.DisableForTravel();
        addons.RegisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoListener);
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

        // Return needs feet on the ground — lingering mount after a FATE/CE looked like a long wait.
        if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            if (Actions.Dismount.CanCast())
            {
                Actions.Dismount.Cast();
            }

            return;
        }

        IZone zone = zones.GetZone();
        if (zone.IsInBasecamp())
        {
            memory.Forget<ReturningStateMemory>();
            return;
        }

        // Poll confirm — PostSetup alone can miss when BossMod slows UI setup (#107).
        if (TryConfirmReturnDialog())
        {
            return;
        }

        if (IsReturnDialogVisible())
        {
            return;
        }

        // Path handoff: hold Returning while the rolled 2..max delay elapses.
        if (memory.TryRemember<ReturningStateMemory>(out ReturningStateMemory returning))
        {
            if (!returning.IsReadyToCast())
            {
                return;
            }

            if (!Actions.Return.CanCast())
            {
                return;
            }
        }

        if (Actions.Return.CanCast())
        {
            // Opportunistic cast already waited via IdleStateMemory — no second delay.
            memory.TryAdd(new ReturningStateMemory(TimeSpan.Zero));
            Actions.Return.Cast();
        }
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);
        addons.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoListener);
    }

    private unsafe void SelectYesNoListener(AddonEvent ev, AddonArgs args)
    {
        if (conditions[ConditionFlag.Unconscious])
        {
            return;
        }

        ReturnYesNo.TryAccept((AtkUnitBase*)args.Addon.Address);
    }

    private unsafe bool TryConfirmReturnDialog()
    {
        AddonSelectYesno* yesno = gui.GetAddonByName<AddonSelectYesno>("SelectYesno");
        if (yesno == null)
        {
            return false;
        }

        return ReturnYesNo.TryAccept(&yesno->AtkUnitBase);
    }

    private unsafe bool IsReturnDialogVisible()
    {
        AddonSelectYesno* yesno = gui.GetAddonByName<AddonSelectYesno>("SelectYesno");
        if (yesno == null)
        {
            return false;
        }

        return ReturnYesNo.IsReturnConfirmation(&yesno->AtkUnitBase);
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
