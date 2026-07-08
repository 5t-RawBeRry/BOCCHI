namespace BOCCHI.MobFarmer.Services;

public interface IRotationPlugin : IDisposable
{
    void PhantomJobOn();

    void PhantomJobOff();
}
