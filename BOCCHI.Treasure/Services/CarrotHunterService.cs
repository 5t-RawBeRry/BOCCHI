using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Data;
using BOCCHI.Treasure.Hunt;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Ocelot.Actions;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Extensions;
using Ocelot.Ipc.BossMod;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Lifecycle;
using Ocelot.Services.PlayerState;
using System.Diagnostics;
using System.Numerics;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace BOCCHI.Treasure.Services;

/// <summary>Authored carrot tour with aethernet hops, empty-pad skips, then Fortune Carrot → bunny.</summary>
public sealed class CarrotHunterService
(
    ICarrotTracker carrots,
    FortuneCarrotAssist fortuneCarrot,
    UIConfig uiConfig,
    AutomatorConfig automatorConfig,
    IPlayer player,
    ICondition conditions,
    IObjectTable objects,
    IVNavmeshIpc vnav,
    IZoneProvider zones,
    IAutomationModeGuard modeGuard,
    IChainFactory chains,
    IChainManager chainManager,
    ILifestreamIpc lifestream,
    IChatGui chat,
    IPluginLog log
) : ICarrotHunter, IOnUpdate, IOnStop
{
    private const float PathArrivalRange = 1.5f;

    private const float BunnySearchRadius = 10f;

    private static readonly TimeSpan BunnySpawnTimeout = TimeSpan.FromSeconds(20);

    private readonly Stopwatch stopwatch = new();

    private readonly HashSet<int> finishedAuthoredIds = [];

    private readonly HashSet<ulong> usedLiveCarrotIdsAtPad = [];

    private readonly List<CarrotData> tour = [];

    private int tourIndex;

    private CarrotData? currentAuthored;

    private ulong? currentLiveCarrotId;

    private Vector3 currentTargetPosition;

    private DateTime waitingForBunnySince = DateTime.MinValue;

    private bool itemUseIssued;

    private AethernetData? hopDeparture;

    private AethernetData? hopArrival;

    private Task<ChainResult>? activeTeleportChain;

    private float approachBestDistance = float.MaxValue;

    private DateTime approachLastProgressUtc = DateTime.MinValue;

    public bool Running { get; private set; }

    public CarrotHuntPhase Phase { get; private set; } = CarrotHuntPhase.Idle;

    public TimeSpan Elapsed => stopwatch.Elapsed;

    public int FortuneCarrotsRemaining => fortuneCarrot.Count();

    public bool IsVnavAvailable => vnav.IsAvailable();

    public bool IsVnavReady => vnav.IsNavmeshReady();

    public void OnStop()
    {
        Teardown();
    }

    public void Toggle()
    {
        if (Running)
        {
            Teardown();
            return;
        }

        if (!IsVnavAvailable || !IsVnavReady)
        {
            BocchiChat.PrintError(chat, uiConfig, "Carrot Hunt needs vnavmesh ready.");
            return;
        }

        if (!fortuneCarrot.HasAny())
        {
            BocchiChat.PrintError(chat, uiConfig, "No Fortune Carrots in inventory.");
            return;
        }

        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone() || zone.GetCarrotData().Count == 0)
        {
            BocchiChat.PrintError(chat, uiConfig, "No authored carrot map for this zone.");
            return;
        }

        modeGuard.EnsureExclusive(AutomationMode.CarrotHunt);
        Running = true;
        Phase = CarrotHuntPhase.Idle;
        finishedAuthoredIds.Clear();
        RebuildTour();
        ClearCurrent();
        stopwatch.Restart();
        log.Information(
            "Carrot hunt started (authored nearest-neighbor, {Count} spots)",
            tour.Count);
    }

    public bool UseFortuneCarrot()
    {
        if (!fortuneCarrot.HasAny())
        {
            BocchiChat.PrintError(chat, uiConfig, "No Fortune Carrots in inventory.");
            return false;
        }

        if (!fortuneCarrot.TryUse(manual: true))
        {
            return false;
        }

        log.Information("Carrot hunt: manual Fortune Carrot use");
        return true;
    }

    public void Update()
    {
        if (!Running)
        {
            return;
        }

        if (!zones.GetZone().IsOccultCrescentZone())
        {
            Teardown();
            return;
        }

        if (!IsVnavReady)
        {
            vnav.Stop();
            return;
        }

        if (player.PlayerCharacter == null || player.IsBetweenAreas())
        {
            return;
        }

        switch (Phase)
        {
            case CarrotHuntPhase.Idle:
                TickIdle();
                break;
            case CarrotHuntPhase.ApproachingAetheryte:
                TickApproachingAetheryte();
                break;
            case CarrotHuntPhase.Teleporting:
                TickTeleporting();
                break;
            case CarrotHuntPhase.Pathing:
                TickPathing();
                break;
            case CarrotHuntPhase.UsingItem:
                TickUsingItem();
                break;
            case CarrotHuntPhase.WaitingForBunny:
                TickWaitingForBunny();
                break;
            case CarrotHuntPhase.OpeningBunny:
                TickOpeningBunny();
                break;
        }
    }

    private void TickIdle()
    {
        if (!TryBeginNextAuthored())
        {
            if (vnav.IsRunning())
            {
                vnav.Stop();
            }

            BocchiChat.Print(chat, uiConfig, "Carrot Hunt finished the authored route.");
            Teardown();
            return;
        }

        if (!fortuneCarrot.HasAny())
        {
            BocchiChat.PrintError(chat, uiConfig, "Out of Fortune Carrots — stopping Carrot Hunt.");
            Teardown();
            return;
        }

        itemUseIssued = false;
        BeginRouteToCurrentAuthored();
        log.Debug(
            "Carrot hunt: next authored {Id} at {Pos:F0} via {Phase}",
            currentAuthored!.Id,
            currentAuthored.Position,
            Phase);
    }

    private void BeginRouteToCurrentAuthored()
    {
        ClearHop();

        if (currentAuthored is not { } authored)
        {
            Phase = CarrotHuntPhase.Pathing;
            return;
        }

        List<AethernetData> aetherytes = zones.GetZone().GetAetherytes();
        if (TryBestAethernetHop(
                player.Position,
                authored.Position,
                aetherytes,
                HuntRoutePlanner.AethernetHopCost,
                out AethernetData departure,
                out AethernetData arrival,
                out _))
        {
            // Already at the arrival aetheryte — walk the last stretch.
            if (AetheryteApproach.IsAlreadyAtAetheryte(arrival, player.Position))
            {
                log.Debug(
                    "Carrot hunt: already at arrival aetheryte {Id} — walking to authored {AuthoredId}",
                    arrival.Id,
                    authored.Id);
                Phase = CarrotHuntPhase.Pathing;
                return;
            }

            hopDeparture = departure;
            hopArrival = arrival;
            log.Debug(
                "Carrot hunt: aethernet hop {From} → {To} toward authored {Id}",
                departure.Id,
                arrival.Id,
                authored.Id);

            if (AetheryteApproach.IsReadyForLifestream(zones.GetZone(), lifestream, player.Position)
                && AetheryteApproach.IsAlreadyAtAetheryte(departure, player.Position))
            {
                Phase = CarrotHuntPhase.Teleporting;
                return;
            }

            Phase = CarrotHuntPhase.ApproachingAetheryte;
            return;
        }

        Phase = CarrotHuntPhase.Pathing;
    }

    private void TickApproachingAetheryte()
    {
        if (hopDeparture is not { } departure)
        {
            Phase = CarrotHuntPhase.Pathing;
            return;
        }

        IZone zone = zones.GetZone();
        Vector3 standOff = departure.GetCampStandOffPosition(player.Position);

        if (zone.IsWithinLifestreamRange(player.Position)
            || player.Position.Distance2D(standOff) <= AethernetNavigation.PathfindArrivalRadius + 0.35f)
        {
            log.Debug(
                "Carrot hunt: at departure aetheryte {Id} (dist to stand-off {Dist:F1}y) — teleporting",
                departure.Id,
                player.Position.Distance2D(standOff));
            vnav.Stop();
            Phase = CarrotHuntPhase.Teleporting;
            return;
        }

        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveCloseTo(standOff, false, AethernetNavigation.PathfindArrivalRadius);
        }

        MaybeMount(standOff);
    }

    private void TickTeleporting()
    {
        if (hopArrival is { } arrival
            && AetheryteApproach.IsAlreadyAtAetheryte(arrival, player.Position))
        {
            log.Debug("Carrot hunt: arrived at aetheryte {Id} — resuming path to carrot", arrival.Id);
            activeTeleportChain = null;
            ClearHop();
            Phase = CarrotHuntPhase.Pathing;
            return;
        }

        if (hopDeparture is not { } departure)
        {
            ClearHop();
            Phase = CarrotHuntPhase.Pathing;
            return;
        }

        if (activeTeleportChain != null)
        {
            if (!activeTeleportChain.IsCompleted)
            {
                return;
            }

            bool teleported = activeTeleportChain.IsCompletedSuccessfully
                              && (activeTeleportChain.Result?.IsSuccess ?? false);
            activeTeleportChain = null;

            if (!teleported)
            {
                log.Warning(
                    "Carrot hunt: aethernet teleport to {Id} failed — walking instead",
                    hopArrival?.Id ?? 0);
                ClearHop();
                Phase = CarrotHuntPhase.Pathing;
                return;
            }

            log.Debug(
                "Carrot hunt: aethernet teleport to {Id} succeeded",
                hopArrival?.Id ?? 0);
            ClearHop();
            Phase = CarrotHuntPhase.Pathing;
            return;
        }

        vnav.Stop();
        uint placeNameId = hopArrival?.Id ?? departure.Id;
        log.Debug("Carrot hunt: starting Lifestream teleport to PlaceNameId {Id}", placeNameId);
        activeTeleportChain = chainManager.Manage(
            chains.Create($"CarrotHunt::Teleport({placeNameId})")
                .Then<AethernetTeleportChain, uint>(placeNameId));
    }

    private void TickPathing()
    {
        if (currentAuthored is not { } authored)
        {
            Phase = CarrotHuntPhase.Idle;
            return;
        }

        ulong? beforeBind = currentLiveCarrotId;
        MaybeBindLiveCarrot(authored);
        if (currentLiveCarrotId is { } boundId && beforeBind != boundId)
        {
            log.Debug(
                "Carrot hunt: bound live carrot {ObjId} for authored {Id}",
                boundId,
                authored.Id);
        }

        float distToAuthored = player.Position.Distance2D(authored.Position);
        if (distToAuthored <= CarrotHuntDistances.TetherRadius && currentLiveCarrotId == null)
        {
            log.Information(
                "Carrot hunt: no live carrot at authored {Id} within tether range — skipping",
                authored.Id);
            SkipCurrentAuthored();
            return;
        }

        if (TryGetCurrentLiveCarrot(out Carrot live))
        {
            currentTargetPosition = live.GetPosition();
        }
        else
        {
            currentTargetPosition = authored.Position;
            ResetApproachProgress();
        }

        float distTarget = player.Position.Distance(currentTargetPosition);
        if (MaybeDismountNear(distTarget))
        {
            return;
        }

        if (currentLiveCarrotId != null
            && (distTarget <= CarrotHuntDistances.UseRadius || IsStuckNearTarget(distTarget)))
        {
            log.Debug(
                "Carrot hunt: in use range of authored {Id} ({Dist:F1}y) — using Fortune Carrot",
                authored.Id,
                distTarget);
            vnav.Stop();
            Phase = CarrotHuntPhase.UsingItem;
            return;
        }

        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveCloseTo(currentTargetPosition, false, PathArrivalRange);
        }

        MaybeMount(currentTargetPosition);
    }

    private void TickUsingItem()
    {
        if (!TryGetCurrentLiveCarrot(out Carrot carrot))
        {
            SkipCurrentAuthored();
            return;
        }

        currentTargetPosition = carrot.GetPosition();

        if (player.IsCasting() || conditions[ConditionFlag.Casting])
        {
            return;
        }

        float dist = player.Position.Distance(currentTargetPosition);
        if (MaybeDismountNear(dist))
        {
            return;
        }

        if (dist > CarrotHuntDistances.StuckNearRadius)
        {
            Phase = CarrotHuntPhase.Pathing;
            return;
        }

        if (itemUseIssued)
        {
            waitingForBunnySince = DateTime.UtcNow;
            Phase = CarrotHuntPhase.WaitingForBunny;
            return;
        }

        if (!fortuneCarrot.HasAny())
        {
            BocchiChat.PrintError(chat, uiConfig, "Out of Fortune Carrots — stopping Carrot Hunt.");
            Teardown();
            return;
        }

        if (!fortuneCarrot.TryUse())
        {
            return;
        }

        itemUseIssued = true;
        waitingForBunnySince = DateTime.UtcNow;
        Phase = CarrotHuntPhase.WaitingForBunny;
        log.Information("Carrot hunt: Fortune Carrot used at {Pos:F0}", currentTargetPosition);
    }

    private void TickWaitingForBunny()
    {
        if (player.IsCasting() || conditions[ConditionFlag.Casting])
        {
            return;
        }

        IGameObject? bunny = FindBunnyNear(currentTargetPosition);
        if (bunny != null)
        {
            log.Information("Carrot hunt: bunny chest spawned near {Pos:F0}", bunny.Position);
            Phase = CarrotHuntPhase.OpeningBunny;
            return;
        }

        if (DateTime.UtcNow - waitingForBunnySince > BunnySpawnTimeout)
        {
            log.Warning("Carrot hunt: no bunny chest near {Pos} — skipping", currentTargetPosition);
            SkipCurrentAuthored();
        }
    }

    private void TickOpeningBunny()
    {
        IGameObject? bunny = FindBunnyNear(currentTargetPosition);
        if (bunny == null)
        {
            // Bunny opened — stay if another chewed carrot shares this pad (double spawn).
            CompleteOrRebindSamePad();
            return;
        }

        float dist = player.Position.Distance(bunny.Position);
        if (MaybeDismountNear(dist))
        {
            return;
        }

        if (dist > CarrotHuntDistances.UseRadius && !IsStuckNearTarget(dist))
        {
            if (!vnav.IsRunning())
            {
                vnav.PathfindAndMoveCloseTo(bunny.Position, false, PathArrivalRange);
            }

            return;
        }

        if (vnav.IsRunning())
        {
            vnav.Stop();
        }

        if (!EzThrottler.Throttle("CarrotHunt::InteractBunny", 400))
        {
            return;
        }

        unsafe
        {
            GameObject* gameObject = (GameObject*)(void*)bunny.Address;
            TargetSystem.Instance()->InteractWithObject(gameObject);
        }
    }

    private bool TryBeginNextAuthored()
    {
        while (tourIndex < tour.Count)
        {
            CarrotData next = tour[tourIndex++];
            if (finishedAuthoredIds.Contains(next.Id))
            {
                continue;
            }

            currentAuthored = next;
            currentLiveCarrotId = null;
            currentTargetPosition = next.Position;
            ResetApproachProgress();
            MaybeBindLiveCarrot(next);
            return true;
        }

        // Replan remaining unfinished spots from the player (nearest-neighbor).
        RebuildTour();
        if (tour.Count == 0)
        {
            return false;
        }

        return TryBeginNextAuthored();
    }

    private void RebuildTour()
    {
        IZone zone = zones.GetZone();
        List<CarrotData> remaining = zone.GetCarrotData()
            .Where(c => !finishedAuthoredIds.Contains(c.Id))
            .ToList();

        tour.Clear();
        tourIndex = 0;
        if (remaining.Count == 0)
        {
            return;
        }

        List<AethernetData> aetherytes = zone.GetAetherytes();
        float teleportCost = HuntRoutePlanner.AethernetHopCost;

        Vector3 start = player.Position;
        CarrotData current = remaining[0];
        float bestStart = float.MaxValue;
        bool firstViaAethernet = false;
        foreach (CarrotData candidate in remaining)
        {
            float cost = TourCost(start, candidate.Position, aetherytes, teleportCost, out bool viaAethernet);
            if (cost < bestStart)
            {
                bestStart = cost;
                current = candidate;
                firstViaAethernet = viaAethernet;
            }
        }

        if (firstViaAethernet)
        {
            log.Debug(
                "Carrot hunt: first hop to authored {Id} prefers aethernet over direct walk",
                current.Id);
        }

        tour.Add(current);
        HashSet<int> unvisited = remaining.Select(c => c.Id).Where(id => id != current.Id).ToHashSet();
        Dictionary<int, CarrotData> byId = remaining.ToDictionary(c => c.Id);

        while (unvisited.Count > 0)
        {
            Vector3 from = current.Position;
            int? nearestId = null;
            float best = float.MaxValue;
            bool bestViaAethernet = false;
            foreach (int id in unvisited)
            {
                float d = TourCost(from, byId[id].Position, aetherytes, teleportCost, out bool viaAethernet);
                if (d < best)
                {
                    best = d;
                    nearestId = id;
                    bestViaAethernet = viaAethernet;
                }
            }

            if (nearestId is not int nextId)
            {
                break;
            }

            if (bestViaAethernet)
            {
                log.Debug(
                    "Carrot hunt: hop to authored {Id} prefers aethernet (cost {Cost:F1})",
                    nextId,
                    best);
            }

            current = byId[nextId];
            tour.Add(current);
            unvisited.Remove(nextId);
        }

        log.Information("Carrot hunt tour rebuilt: {Count} remaining", tour.Count);
    }

    /// <summary>Direct walk cost, or best distinct aetheryte hop when cheaper.</summary>
    private static float TourCost(
        Vector3 from,
        Vector3 to,
        IReadOnlyList<AethernetData> aetherytes,
        float teleportCost,
        out bool viaAethernet)
    {
        if (TryBestAethernetHop(from, to, aetherytes, teleportCost, out _, out _, out float viaCost))
        {
            viaAethernet = true;
            return viaCost;
        }

        viaAethernet = false;
        return Vector3.Distance(from, to);
    }

    private static bool TryBestAethernetHop(
        Vector3 from,
        Vector3 to,
        IReadOnlyList<AethernetData> aetherytes,
        float teleportCost,
        out AethernetData departure,
        out AethernetData arrival,
        out float viaCost)
    {
        departure = null!;
        arrival = null!;
        viaCost = float.MaxValue;

        float direct = Vector3.Distance(from, to);
        if (aetherytes.Count < 2)
        {
            return false;
        }

        float bestVia = float.MaxValue;
        AethernetData? bestDep = null;
        AethernetData? bestArr = null;

        foreach (AethernetData shardA in aetherytes)
        {
            float toA = Vector3.Distance(from, shardA.Position);
            foreach (AethernetData shardB in aetherytes)
            {
                if (shardA.Id == shardB.Id)
                {
                    continue;
                }

                float via = toA + teleportCost + Vector3.Distance(shardB.Position, to);
                if (via < bestVia)
                {
                    bestVia = via;
                    bestDep = shardA;
                    bestArr = shardB;
                }
            }
        }

        if (bestDep == null || bestArr == null || bestVia >= direct)
        {
            return false;
        }

        departure = bestDep;
        arrival = bestArr;
        viaCost = bestVia;
        return true;
    }

    private void MaybeBindLiveCarrot(CarrotData authored)
    {
        Carrot? live = FindUnusedLiveCarrotNear(authored, CarrotHuntDistances.MatchRadiusSq);
        currentLiveCarrotId = live?.GameObjectId;
        if (live != null)
        {
            currentTargetPosition = live.GetPosition();
        }
    }

    private Carrot? FindUnusedLiveCarrotNear(CarrotData authored, float matchRadiusSq)
    {
        return carrots.Carrots
            .Where(c => c.IsValid())
            .Where(c => !usedLiveCarrotIdsAtPad.Contains(c.GameObjectId))
            .Where(c => Vector3.DistanceSquared(authored.Position, c.GetPosition()) <= matchRadiusSq)
            .OrderBy(c => Vector3.DistanceSquared(authored.Position, c.GetPosition()))
            .FirstOrDefault();
    }

    private void CompleteOrRebindSamePad()
    {
        if (currentLiveCarrotId is { } usedId)
        {
            usedLiveCarrotIdsAtPad.Add(usedId);
        }

        itemUseIssued = false;
        currentLiveCarrotId = null;
        waitingForBunnySince = DateTime.MinValue;
        ResetApproachProgress();

        if (currentAuthored is { } authored)
        {
            Carrot? next = FindUnusedLiveCarrotNear(authored, CarrotHuntDistances.SamePadRecheckRadiusSq);
            if (next != null)
            {
                currentLiveCarrotId = next.GameObjectId;
                currentTargetPosition = next.GetPosition();
                log.Information(
                    "Carrot hunt: another chewed carrot at authored {Id} — staying for double spawn",
                    authored.Id);
                Phase = CarrotHuntPhase.Pathing;
                return;
            }
        }

        CompleteCurrentAuthored();
    }

    private void SkipCurrentAuthored()
    {
        if (currentAuthored is { } authored)
        {
            finishedAuthoredIds.Add(authored.Id);
        }

        vnav.Stop();
        ClearCurrent();
        Phase = CarrotHuntPhase.Idle;
    }

    private void CompleteCurrentAuthored()
    {
        if (currentAuthored is { } authored)
        {
            finishedAuthoredIds.Add(authored.Id);
            log.Information("Carrot hunt: finished authored {Id} near {Pos:F0}", authored.Id, currentTargetPosition);
        }

        vnav.Stop();
        ClearCurrent();
        Phase = CarrotHuntPhase.Idle;
    }

    private bool TryGetCurrentLiveCarrot(out Carrot carrot)
    {
        carrot = null!;
        if (currentLiveCarrotId is not { } id)
        {
            return false;
        }

        Carrot? match = carrots.Carrots.FirstOrDefault(c => c.IsValid() && c.GameObjectId == id);
        if (match == null)
        {
            // Live list may have refreshed — rebind from authored if still near.
            if (currentAuthored is { } authored)
            {
                MaybeBindLiveCarrot(authored);
                if (currentLiveCarrotId is { } rebound)
                {
                    match = carrots.Carrots.FirstOrDefault(c => c.IsValid() && c.GameObjectId == rebound);
                }
            }
        }

        if (match == null)
        {
            currentLiveCarrotId = null;
            return false;
        }

        carrot = match;
        return true;
    }

    private IGameObject? FindBunnyNear(Vector3 position)
    {
        return objects
            .Where(o => o is { ObjectKind: DalamudObjectKind.EventObj, IsDead: false } && o.IsValid())
            .Where(o => o.BaseId == OccultObjectType.BunnyChest)
            .OrderBy(o => Vector3.DistanceSquared(position, o.Position))
            .FirstOrDefault(o => Vector3.Distance(position, o.Position) <= BunnySearchRadius);
    }

    private void ClearHop()
    {
        hopDeparture = null;
        hopArrival = null;
        activeTeleportChain = null;
    }

    private void MaybeMount(Vector3 destination)
    {
        MountWait.TryCastIfNeeded(
            conditions,
            objects,
            destination,
            automatorConfig.ShouldAutoMount,
            automatorConfig.PreferredMountId,
            zones.GetZone().IsInBasecamp());
    }

    private bool MaybeDismountNear(float distance)
    {
        if (distance > CarrotHuntDistances.DismountRadius
            || (!conditions[ConditionFlag.Mounted]
                && !conditions[ConditionFlag.Mounting]
                && !player.IsMounted()))
        {
            return false;
        }

        if (vnav.IsRunning())
        {
            vnav.Stop();
        }

        if (EzThrottler.Throttle("CarrotHunt::Dismount", 500) && Actions.Dismount.CanCast())
        {
            Actions.Dismount.Cast();
        }

        return true;
    }

    private bool IsStuckNearTarget(float distance)
    {
        if (distance > CarrotHuntDistances.StuckNearRadius)
        {
            ResetApproachProgress();
            return false;
        }

        DateTime now = DateTime.UtcNow;
        if (approachLastProgressUtc == DateTime.MinValue || distance < approachBestDistance - 0.5f)
        {
            approachBestDistance = distance;
            approachLastProgressUtc = now;
            return false;
        }

        if (now - approachLastProgressUtc < CarrotHuntDistances.StuckNearTimeout)
        {
            return false;
        }

        log.Information(
            "Carrot hunt: stuck near target at {Dist:F1}y — trying interact from here",
            distance);
        return true;
    }

    private void ResetApproachProgress()
    {
        approachBestDistance = float.MaxValue;
        approachLastProgressUtc = DateTime.MinValue;
    }

    private void ClearCurrent()
    {
        currentAuthored = null;
        currentLiveCarrotId = null;
        currentTargetPosition = Vector3.Zero;
        itemUseIssued = false;
        waitingForBunnySince = DateTime.MinValue;
        usedLiveCarrotIdsAtPad.Clear();
        ResetApproachProgress();
        ClearHop();
    }

    private void Teardown()
    {
        if (!Running)
        {
            return;
        }

        Running = false;
        Phase = CarrotHuntPhase.Idle;
        finishedAuthoredIds.Clear();
        tour.Clear();
        tourIndex = 0;
        ClearCurrent();
        stopwatch.Reset();
        vnav.Stop();
        log.Information("Carrot hunt stopped");
    }
}
