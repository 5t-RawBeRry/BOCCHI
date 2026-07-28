using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ocelot.Actions;
using Ocelot.Services.Gate;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class ReturningHandler
(
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
        if (memory.TryRemember<ReturningStateMemory>(out ReturningStateMemory _))
        {
            return StatePriority.VeryHigh;
        }

        if (!memory.TryRemember<IdleStateMemory>(out IdleStateMemory idle) || zones.GetZone().IsInBasecamp())
        {
            return StatePriority.Never;
        }

        TimeSpan time = idle.GetIdleTime();
        TimeSpan maxRemoteIdle = TimeSpan.FromSeconds(config.MaxRemoteIdleTimeSeconds);

        return time >= maxRemoteIdle ? StatePriority.VeryLow : StatePriority.Never;
    }

    public override void Handle()
    {
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
        AtkUnitBase* addon = (AtkUnitBase*)args.Addon.Address;
        if (!addon->IsVisible)
        {
            return;
        }

        addon->FireCallbackInt(0);
    }
}
