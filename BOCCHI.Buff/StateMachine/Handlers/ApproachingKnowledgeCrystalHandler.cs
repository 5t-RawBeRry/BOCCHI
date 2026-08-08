using BOCCHI.Buff.Data;
using BOCCHI.Common.Data.KnowledgeCrystals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Flow;
using System.Numerics;

namespace BOCCHI.Buff.StateMachine.Handlers;

public class ApproachingKnowledgeCrystalHandler
(
    IZoneProvider zones,
    IPlayer player,
    IPathfinder pathfinder,
    ICondition conditions,
    IAutomatorMemory memory
) : FlowStateHandler<BuffState>(BuffState.ApproachingKnowledgeCrystal)
{
    private const float CrystalInteractionRange = 5f;

    public override BuffState? Handle()
    {
        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            return null;
        }

        List<KnowledgeCrystalData> crystals = zone.GetNearbyKnowledgeCrystals().ToList();
        if (crystals.Count == 0)
        {
            return BuffState.NoCrystalsFound;
        }

        bool manual = memory.TryRemember<ManualBuffRunMemory>(out ManualBuffRunMemory _);
        bool inRange = zone.IsInBuffCastRange(player.Position);

        if (inRange)
        {
            pathfinder.Stop();

        if (DismountAssist.TryDismount(conditions))
        {
            return null;
        }

        return BuffState.ChoosingBuffToApply;
        }

        // Standalone Apply Buffs /buff — cast in place only; Illegal Mode still walks in.
        if (manual)
        {
            pathfinder.Stop();
            memory.Forget<ApplyingBuffsMemory>();
            memory.Forget<ManualBuffRunMemory>();
            memory.Forget<InquiringMindAttemptedMemory>();
            return null;
        }

        if (pathfinder.GetState() != PathfindingState.Idle)
        {
            return null;
        }

        BuffZone? buffZone = zone.GetBuffZone();
        KnowledgeCrystalData closest = crystals[0];
        // Prefer the authored camp annulus only when the closest crystal is that camp crystal.
        Vector3 destination = buffZone is { } bz
            && Vector3.DistanceSquared(closest.Position, bz.Center) <= 900f
                ? bz.GetApproachPoint(player.Position)
                : closest.Position.GetApproachPosition(player.Position, CrystalInteractionRange - 0.2f);

        pathfinder.PathfindAndMoveTo(new(destination)
        {
            DistanceThreshold = 1.0f,
            ShouldSnapToFloor = true
        });

        return null;
    }
}
