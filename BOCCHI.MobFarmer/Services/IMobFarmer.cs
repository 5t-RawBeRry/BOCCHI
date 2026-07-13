using System.Numerics;
using Ocelot.Lifecycle;
using Ocelot.Services.PlayerState;
using Ocelot.States;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.Data
{
    public enum FarmerPhase
    {
        Waiting,
        Buffing,
        Gathering,
        Stacking,
        Fighting,
    }
}

namespace BOCCHI.MobFarmer.Services
{
    public interface IMobFarmer : IOnUpdate
    {
        bool Running { get; }

        Vector3 StartingPoint { get; }

        Data.FarmerPhase Phase { get; }

        void Toggle();

        void Render();
    }
}
