using BOCCHI.Common.Data.Aethernet;
using Dalamud.Plugin.Services;
using Ocelot.Lifecycle;

namespace BOCCHI.Common.Services;

/// <summary>Loads localized SelectYesno templates used by <see cref="ReturnYesNo"/>.</summary>
public sealed class ReturnYesNoInitializer(IDataManager data) : IOnStart
{
    public void OnStart() => ReturnYesNo.Initialize(data);
}
