namespace BOCCHI.Common.Data.EventDrops;

public readonly record struct EventDropInfo(Demiatma? Demiatma, MonsterNote? Notes, SoulShard? SoulShard)
{
    public bool HasAny => Demiatma is not null || Notes is not null || SoulShard is not null;
}
