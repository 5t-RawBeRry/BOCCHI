using Dalamud.Game.ClientState.Fates;
using Ocelot.Services.Data;
using System.Numerics;
using FateData = Lumina.Excel.Sheets.Fate;

namespace BOCCHI.Common.Data.Fates;

public readonly record struct FateId(ushort Value);

public class Fate(FateId id, IFate context, IDataRepository<FateData> fateDataRepository)
{
    public readonly FateData GameData = fateDataRepository.Get(context.FateId);
    public readonly FateId Id = id;

    public readonly string Name = context.Name.ToString();

    public readonly ActivityProgressTracker ProgressTracker = new();

    public readonly float Radius = context.Radius;

    public Vector3 Position { get; private set; } = context.Position;

    public FateState State { get; private set; } = context.State;

    public byte Progress { get; private set; } = context.Progress;

    public void Update(IFate context)
    {
        Position = context.Position;
        State = context.State;
        Progress = context.Progress;

        ProgressTracker.Observe(Progress);
    }
}
