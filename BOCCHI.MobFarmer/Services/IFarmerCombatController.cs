namespace BOCCHI.MobFarmer.Services;

public interface IFarmerCombatController
{
    void Prepare();

    void EnableFighting();

    void Disable();

    void Tick();

    void Teardown();
}
