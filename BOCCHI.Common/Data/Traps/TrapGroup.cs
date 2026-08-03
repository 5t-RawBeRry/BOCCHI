using System.Numerics;

namespace BOCCHI.Common.Data.Traps;

public sealed class TrapGroup(List<TrapDatum> traps, uint max = 1)
{
    public IReadOnlyList<TrapDatum> Traps { get; } = traps;

    public uint MaxInGroup { get; } = max;

    public Vector3 GetCenter()
    {
        if (Traps.Count == 0)
        {
            return Vector3.Zero;
        }

        Vector3 sum = Vector3.Zero;
        foreach (TrapDatum trap in Traps)
        {
            sum += trap.Position;
        }

        return sum / Traps.Count;
    }

    public float GetDistance2D(Vector3 from)
    {
        if (Traps.Count == 0)
        {
            return float.MaxValue;
        }

        float best = float.MaxValue;
        foreach (TrapDatum trap in Traps)
        {
            float dx = trap.Position.X - from.X;
            float dz = trap.Position.Z - from.Z;
            float dist = MathF.Sqrt((dx * dx) + (dz * dz));
            if (dist < best)
            {
                best = dist;
            }
        }

        return best;
    }
}
