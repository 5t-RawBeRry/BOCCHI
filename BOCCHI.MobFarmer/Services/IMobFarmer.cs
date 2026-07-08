using System.Numerics;
using BOCCHI.MobFarmer.Data;

namespace BOCCHI.MobFarmer.Services;

public interface IMobFarmer
{
    bool Running { get; }

    Vector3 StartingPoint { get; }

    FarmerPhase Phase { get; }

    void Toggle();

    void Render();
}
