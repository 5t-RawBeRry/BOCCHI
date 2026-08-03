using System.Numerics;

namespace BOCCHI.Common.Data.Traps;

public sealed class TrapDatum(Vector3 position, uint type)
{
    public Vector3 Position { get; } = position;

    public uint Type { get; } = type;
}
