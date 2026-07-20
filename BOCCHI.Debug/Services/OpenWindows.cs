using Microsoft.Extensions.DependencyInjection;
using Ocelot.Lifecycle;
using Ocelot.Windows;
namespace BOCCHI.Debug.Services;

public class OpenWindows(IServiceProvider services) : IOnUpdate
{
    private bool opened;

    public int Order => int.MaxValue;

    public void Update()
    {
        if (opened)
        {
            return;
        }

        opened = true;

        services.GetService<IMainWindow>()?.IsOpen = true;
        services.GetService<IConfigWindow>()?.IsOpen = true;
        services.GetService<IDebugWindow>()?.IsOpen = true;
    }
}
