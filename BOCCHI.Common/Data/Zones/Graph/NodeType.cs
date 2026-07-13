namespace BOCCHI.Common.Data.Zones.Graph;

public enum NodeType
{
    // Teleports
    BaseCampReturnPosition, // Where you end up when you cast return
    BaseCampAetheryte, // The main zone aetheryte
    AethernetShard, // Other shards around the zone

    // Activities
    NormalFate,
    PotFate,
    CriticalEncounter,

    // Points of interest
    KnowledgeCrystal,
    Treasure,
    _SilverChest, // unused (left for type id)
    PotChest,
    _PotChestB, // unused (left for type id)
    PostChestReroll, // Getting a reroll on any pot chest from any pool (including this one) will roll in this pool
    Carrot,
}
