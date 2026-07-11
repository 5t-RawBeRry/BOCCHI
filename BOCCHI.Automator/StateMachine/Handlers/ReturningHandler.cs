using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Services.Gate;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class ReturningHandler(
    IAutomatorMemory memory,
    IZoneProvider zones,
    ICondition conditions,
    IAddonLifecycle addons,
    AutomatorConfig config,
    IGateService gate
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Returning)
{
    public override StatePriority GetScore()
    {
        if (memory.TryRemember<ReturningStateMemory>(out var _))
        {
            return StatePriority.VeryHigh;
        }

        if (!memory.TryRemember<IdleStateMemory>(out var idle) || zones.GetZone().IsInBasecamp())
        {
            return StatePriority.Never;
        }

        var time = idle.GetIdleTime();
        var maxRemoteIdle = TimeSpan.FromSeconds(config.MaxRemoteIdleTimeSeconds);

        return time >= maxRemoteIdle ? StatePriority.VeryLow : StatePriority.Never;
    }

    public override void Handle()
    {
        if (gate.Milliseconds(this, "ReturningHandler::Gate", 500))
        {
            return;
        }

        var isCasting = conditions[ConditionFlag.Casting] || conditions[ConditionFlag.Casting87];
        var isBetweenAreas = conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51];

        if (isCasting || isBetweenAreas)
        {
            return;
        }

        if (zones.GetZone().IsInBasecamp())
        {
            memory.Forget<ReturningStateMemory>();
            return;
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
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (!addon->IsVisible)
        {
            return;
        }

        addon->FireCallbackInt(0);
    }
}
