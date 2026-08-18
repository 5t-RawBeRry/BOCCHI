using BOCCHI.Common.Data.OccultCrescent;
using Dalamud.Plugin.Services;
using Ocelot.Lifecycle;

namespace BOCCHI.Common.Services;

/// <summary>Loads Occult Crescent action and currency ids from excel before other start hooks run.</summary>
public sealed class OccultExcelInitializer(IDataManager data) : IOnStart
{
    public int Order => int.MaxValue;

    public void OnStart()
    {
        PhantomActions.Initialize(data);
        OccultCurrencies.Initialize(data);
        PhantomBuffs.Initialize(data);
        PhantomJobStatuses.Initialize(data);
    }
}
