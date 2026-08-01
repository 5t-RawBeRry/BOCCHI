namespace BOCCHI.Common.Data.Mobs;

[Flags]
public enum MobElement : byte
{
    None = 0,
    Fire = 1 << 0,
    Ice = 1 << 1,
    Wind = 1 << 2,
    Thunder = 1 << 3
}

[Flags]
public enum MobSusceptibility : ushort
{
    None = 0,
    Doom = 1 << 0,
    Ashkin = 1 << 1,
    Paralysis = 1 << 2,
    Stop = 1 << 3,
    Slow = 1 << 4,
    Blind = 1 << 5,
    Stun = 1 << 6,
    Frog = 1 << 7,
    Sleep = 1 << 8,
    Heavy = 1 << 9,
    Bind = 1 << 10
}

public enum MobSpawnCondition : byte
{
    None = 0,
    Rain,
    Clouds,
    ClearSkies,
    AtmosphericPhantasms,
    Night
}

/// <summary>Authored open-world mob metadata for Mob Farmer / future filters.</summary>
public readonly record struct MobProfile(
    MobElement Weaknesses,
    byte Level = 0,
    MobSpawnCondition SpawnCondition = MobSpawnCondition.None,
    MobSusceptibility Susceptible = MobSusceptibility.None);
