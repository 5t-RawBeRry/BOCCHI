using BOCCHI.MobFarmer.Data;
using Ocelot.Lifecycle;
using System.Numerics;

namespace BOCCHI.MobFarmer.Data
{
    public enum FarmerPhase
    {
        Waiting,
        Buffing,
        Gathering,
        Stacking,
        Fighting
    }
}

namespace BOCCHI.MobFarmer.Services
{
    public interface IMobFarmer : IOnUpdate
    {
        bool Running { get; }

        Vector3 StartingPoint { get; }

        FarmerPhase Phase { get; }

        void Toggle();

        void Render();
    }
}
