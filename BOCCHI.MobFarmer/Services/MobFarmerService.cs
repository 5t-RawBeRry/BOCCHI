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
    IPlayer player
) : IMobFarmer, IOnUpdate
{
    private IStateMachine<FarmerPhase>? stateMachine;

    private IStateMachine<FarmerPhase> StateMachine => stateMachine ??= stateMachineFactory();

    public bool Running { get; private set; }

    public Vector3 StartingPoint { get; private set; }

    public FarmerPhase Phase => StateMachine.State;

    public void Toggle()
    {
        Running = !Running;
        if (StateMachine is FlowStateMachine<FarmerPhase> flow)
        {
            flow.Reset();
        }

        if (!Running)
        {
            pathfinder.Stop();
            return;
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

        if (!Running || scanner.Mobs.Count == 0)
        {
            return;
        }

        StateMachine.Update();
    }
}
