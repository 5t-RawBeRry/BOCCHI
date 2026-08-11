using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.MobFarmer.Data;
using Dalamud.Plugin.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.Services.Translation;
using Ocelot.States;
using Ocelot.States.Flow;
using Ocelot.Windows;
using System.Numerics;

namespace BOCCHI.MobFarmer.Services;

public class MobFarmerService
(
    IMobScanner scanner,
    Func<IStateMachine<FarmerPhase>> stateMachineFactory,
    IPathfinder pathfinder,
    IPlayer player,
    IZoneProvider zones,
    IChatGui chat,
    UIConfig uiConfig,
    ITranslator<MainWindow> translator,
    IAutomationModeGuard modeGuard
) : IMobFarmer, IOnUpdate, IOnStop
{
    private IStateMachine<FarmerPhase>? stateMachine;

    private IStateMachine<FarmerPhase> StateMachine => stateMachine ??= stateMachineFactory();

    public bool Running { get; private set; }

    public Vector3 StartingPoint { get; private set; }

    public FarmerPhase Phase => StateMachine.State;

    public void OnStop()
    {
        Running = false;
        pathfinder.Stop();
    }

    public void Toggle()
    {
        if (Running)
        {
            StopInternal();
            return;
        }

        modeGuard.EnsureExclusive(AutomationMode.MobFarmer);

        Running = true;
        if (StateMachine is FlowStateMachine<FarmerPhase> flow)
        {
            flow.Reset();
        }

        StartingPoint = player.Position;
    }

    public void Render()
    {
        if (!Running)
        {
            return;
        }

        StateMachine.Render();
    }

    public void Update()
    {
        scanner.Update();

        if (!Running)
        {
            return;
        }

        if (!zones.GetZone().IsOccultCrescentZone())
        {
            StopInternal();
            BocchiChat.Print(chat, uiConfig, translator.T(".automation.mob_farmer.off_left_zone"));
            return;
        }

        // Always tick the phase machine while running — even with an empty scan list —
        // so Fighting can return to Waiting after the last mob dies/despawns.
        StateMachine.Update();
    }

    private void StopInternal()
    {
        Running = false;
        if (StateMachine is FlowStateMachine<FarmerPhase> flowOff)
        {
            flowOff.Reset();
        }

        pathfinder.Stop();
    }
}
