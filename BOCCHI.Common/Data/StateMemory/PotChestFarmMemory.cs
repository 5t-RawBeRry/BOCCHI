using System.Numerics;
using BOCCHI.Common.Data.Fates;

namespace BOCCHI.Common.Data.StateMemory;

public sealed class PotChestFarmMemory(FateId fateId, IEnumerable<Vector3> chestPositions)
{
    public FateId FateId { get; } = fateId;

    public Queue<Vector3> Chests { get; } = new(chestPositions);

    public int TotalChests { get; } = chestPositions.Count();

    public int RemainingChests => Chests.Count;
}
