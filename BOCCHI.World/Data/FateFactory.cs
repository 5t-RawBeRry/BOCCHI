using BOCCHI.Common.Data.Fates;
using Dalamud.Game.ClientState.Fates;
using Ocelot.Services.Data;
using FateData = Lumina.Excel.Sheets.Fate;

namespace BOCCHI.Fates.Data;

public class FateFactory(IDataRepository<FateData> fateDataRepository) : IFateFactory
{
    public Fate Create(IFate context)
    {
        FateId id = new(context.FateId);

        return new(id, context, fateDataRepository);
    }
}
