using BOCCHI.Common.Services;
using BOCCHI.MobFarmer.Data;
using Ocelot.Lifecycle;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States;
using Ocelot.States.Flow;
using System.Numerics;

namespace BOCCHI.MobFarmer.Services;

public class MobFarmerService
(
    IMobScanner scanner,
    Func<IStateMachine<FarmerPhase>> stateMachineFactory,
    IPathfinder pathfinder,
    IPlayer player,
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
            Running = false;
            if (StateMachine is FlowStateMachine<FarmerPhase> flowOff)
            {
                flowOff.Reset();
            }

            pathfinder.Stop();
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

        // Always tick the phase machine while running — even with an empty scan list —
        // so Fighting can return to Waiting after the last mob dies/despawns.
        StateMachine.Update();
    }
}
