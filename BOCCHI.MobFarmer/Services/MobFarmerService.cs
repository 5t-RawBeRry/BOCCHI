using System.Numerics;
using BOCCHI.MobFarmer.Data;
using Ocelot.Lifecycle;
using Ocelot.Services.PlayerState;
using Ocelot.States;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.Services;

public class MobFarmerService(
    IMobScanner scanner,
    IStateMachine<FarmerPhase> stateMachine,
    IRotationPlugin rotation,
    IPlayer player
) : IMobFarmer, IOnUpdate
{
    public bool Running { get; private set; }

    public Vector3 StartingPoint { get; private set; }

    public FarmerPhase Phase => stateMachine.State;

    public void Toggle()
    {
        Running = !Running;
        if (stateMachine is FlowStateMachine<FarmerPhase> flow)
        {
            flow.Reset();
        }
        rotation.PhantomJobOff();

        if (!Running)
        {
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

        stateMachine.Render();
    }

    public void Update()
    {
        scanner.Update();

        if (!Running || scanner.Mobs.Count == 0)
        {
            return;
        }

        stateMachine.Update();
    }
}
