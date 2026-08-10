using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.ChainRecipes;
using BOCCHI.Treasure.Data;
using BOCCHI.Treasure.Hunt;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Extensions;
using Ocelot.Ipc.BossMod;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Lifecycle;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.Services.Translation;
using Ocelot.Windows;
using System.Diagnostics;
using System.Numerics;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace BOCCHI.Treasure.Services;

/// <summary>Authored carrot tour with aethernet hops, empty-pad skips, then Fortune Carrot → bunny.</summary>
public sealed class CarrotHunterService
(
    ICarrotTracker carrots,
    FortuneCarrotAssist fortuneCarrot,
    TreasureConfig treasureConfig,
    UIConfig uiConfig,
    AutomatorConfig automatorConfig,
    IPlayer player,
    ICondition conditions,
    IObjectTable objects,
    IVNavmeshIpc vnav,
    IPathfinder pathfinder,
    IZoneProvider zones,
    IAutomationModeGuard modeGuard,
    IChainFactory chains,
    IChainManager chainManager,
    ILifestreamIpc lifestream,
    IGameGui gui,
    IChatGui chat,
    IPluginLog log,
    ITranslator<MainWindow> translator
) : ICarrotHunter, IOnUpdate, IOnStop
{
    private const float BunnySearchRadius = 10f;

    private static readonly TimeSpan BunnySpawnTimeout = TimeSpan.FromSeconds(20);

    private const string FinishedRouteMessage = "Carrot Hunt finished the authored route.";

    private const string OutOfCarrotsMessage = "Out of Fortune Carrots — stopping Carrot Hunt.";

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

    private Task<ChainResult>? activeReturnChain;

    /// <summary>After Return succeeds: stop hunt (finish) vs continue to current authored pad.</summary>
    private bool returnThenStop;

    /// <summary>After mid-route Return, teleport from camp before walking to the pad.</summary>
    private bool returnThenAethernet;

    private float approachBestDistance = float.MaxValue;

    private DateTime approachLastProgressUtc = DateTime.MinValue;

    private int? emptyPadCandidateAuthoredId;

    private DateTime emptyPadCandidateSinceUtc = DateTime.MinValue;

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
        ClearCurrent();
        stopwatch.Restart();
        RecalculateAndAdvance();
        log.Information(
            "Carrot hunt started (nearest-neighbor TSP, {Count} spots)",
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
            StopDueToLeavingOccultCrescent();
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

        if (conditions[ConditionFlag.Unconscious])
        {
            SoftStopWhileUnconscious();
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
            case CarrotHuntPhase.Returning:
                TickReturning();
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
        // After a replan that found nothing left, or startup edge cases.
        if (vnav.IsRunning())
        {
            vnav.Stop();
        }

        if (treasureConfig.ReturnToBaseCampAfterHunt && !zones.GetZone().IsInBasecamp())
        {
            log.Information("Carrot hunt: route finished — returning to base camp");
            returnThenStop = true;
            returnThenAethernet = false;
            ClearHop();
            Phase = CarrotHuntPhase.Returning;
            return;
        }

        BocchiChat.Print(chat, uiConfig, FinishedRouteMessage);
        Teardown();
    }

    private void BeginRouteToCurrentAuthored()
    {
        ClearHop();
        returnThenStop = false;
        returnThenAethernet = false;
        activeReturnChain = null;

        if (currentAuthored is not { } authored)
        {
            Phase = CarrotHuntPhase.Pathing;
            return;
        }

        IZone zone = zones.GetZone();
        List<AethernetData> aetherytes = zone.GetAetherytes();
        AethernetData main = zone.GetMainAetheryte();

        Vector3 destination = currentTargetPosition;
        float localDist = player.Position.Distance2D(destination);
        // Keep Return for pad↔pad tour hops only — never when the target is already nearby.
        bool allowReturn = currentLiveCarrotId == null
            && localDist > HuntDistances.NearbyLiveDivertRange;

        HopMode mode = ChooseHopMode(
            player.Position,
            destination,
            aetherytes,
            main,
            out AethernetData? departure,
            out AethernetData? arrival,
            out _,
            allowReturn);

        switch (mode)
        {
            case HopMode.Return:
                Phase = CarrotHuntPhase.Returning;
                return;

            case HopMode.ReturnThenAethernet when arrival != null:
                hopDeparture = main;
                hopArrival = arrival;
                returnThenAethernet = true;
                Phase = CarrotHuntPhase.Returning;
                return;

            case HopMode.Aethernet when departure != null && arrival != null:
                if (AetheryteApproach.IsAlreadyAtAetheryte(arrival, player.Position))
                {
                    Phase = CarrotHuntPhase.Pathing;
                    return;
                }

                hopDeparture = departure;
                hopArrival = arrival;

                if (AetheryteApproach.IsReadyForLifestream(zone, lifestream, player.Position)
                    && AetheryteApproach.IsAlreadyAtAetheryte(departure, player.Position))
                {
                    Phase = CarrotHuntPhase.Teleporting;
                    return;
                }

                Phase = CarrotHuntPhase.ApproachingAetheryte;
                return;

            default:
                Phase = CarrotHuntPhase.Pathing;
                return;
        }
    }

    private void TickReturning()
    {
        if (!Running)
        {
            activeReturnChain = null;
            return;
        }

        if (!returnThenStop && TryDivertToNearbyLiveCarrot())
        {
            return;
        }

        IZone zone = zones.GetZone();
        if (zone.IsInBasecamp() && activeReturnChain == null)
        {
            OnReturnArrived();
            return;
        }

        if (activeReturnChain != null)
        {
            if (!activeReturnChain.IsCompleted)
            {
                return;
            }

            bool ok = activeReturnChain.IsCompletedSuccessfully && zone.IsInBasecamp();
            activeReturnChain = null;
            if (!ok)
            {
                log.Warning("Carrot hunt: Return failed — walking instead");
                returnThenAethernet = false;
                ClearHop();
                if (returnThenStop)
                {
                    BocchiChat.Print(chat, uiConfig, FinishedRouteMessage);
                    Teardown();
                    return;
                }

                Phase = CarrotHuntPhase.Pathing;
                return;
            }

            OnReturnArrived();
            return;
        }

        if (conditions[ConditionFlag.InCombat])
        {
            // Walk toward camp pad until combat clears (same idea as treasure hunt Return).
            if (!vnav.IsRunning())
            {
                Vector3 standOff = zone.GetMainAetheryte().GetCampStandOffPosition(player.Position);
                vnav.PathfindAndMoveCloseTo(standOff, false, AethernetNavigation.PathfindArrivalRadius);
            }

            return;
        }

        activeReturnChain = chainManager.Manage(
            ReturnToBaseCamp.Append(
                chains.Create("CarrotHunt::Return"),
                zones,
                conditions,
                gui,
                pathfinder,
                vnav));
    }

    private void OnReturnArrived()
    {
        vnav.Stop();
        if (returnThenStop)
        {
            BocchiChat.Print(chat, uiConfig, FinishedRouteMessage);
            Teardown();
            return;
        }

        if (returnThenAethernet && hopArrival != null)
        {
            returnThenAethernet = false;
            hopDeparture = zones.GetZone().GetMainAetheryte();
            if (AetheryteApproach.IsAlreadyAtAetheryte(hopDeparture, player.Position))
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
        if (TryDivertToNearbyLiveCarrot())
        {
            return;
        }

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

            ClearHop();
            Phase = CarrotHuntPhase.Pathing;
            return;
        }

        vnav.Stop();
        uint placeNameId = hopArrival?.Id ?? departure.Id;
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

        if (TryDivertToNearbyLiveCarrot())
        {
            return;
        }

        MaybeBindLiveCarrot(authored);

        if (currentLiveCarrotId == null
            && CanTrustEmptyCarrotPad(authored.Position)
            && ConfirmEmptyCarrotPad(authored.Id))
        {
            log.Information(
                "Carrot hunt: no live carrot at authored {Id} — skipping",
                authored.Id);
            ClearEmptyPadCandidate();
            SkipCurrentAuthored();
            return;
        }

        if (TryGetCurrentLiveCarrot(out Carrot live))
        {
            ClearEmptyPadCandidate();
            currentTargetPosition = live.GetPosition();
        }
        else
        {
            currentTargetPosition = authored.Position;
            ResetApproachProgress();
        }

        float distTarget = player.Position.Distance2D(currentTargetPosition);
        if (MaybeDismountNear(distTarget))
        {
            return;
        }

        if (currentLiveCarrotId != null
            && (distTarget <= HuntDistances.UseRadius || IsStuckNearTarget(distTarget)))
        {
            vnav.Stop();
            Phase = CarrotHuntPhase.UsingItem;
            return;
        }

        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveCloseTo(currentTargetPosition, false, OpenTreasureCofferChain.PathArrivalRange);
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

        float dist = player.Position.Distance2D(currentTargetPosition);
        if (MaybeDismountNear(dist))
        {
            return;
        }

        if (dist > HuntDistances.UseRadius && !IsStuckNearTarget(dist))
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
            BocchiChat.PrintError(chat, uiConfig, OutOfCarrotsMessage);
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

        float dist2d = player.Position.Distance2D(bunny.Position);
        float dist3d = player.Position.Distance(bunny.Position);

        // Path in until within Pandora-style open range (do not interact from 5–12y).
        // Bunny coffers open while mounted — only Fortune Carrot use needs a dismount.
        if (dist3d > HuntDistances.BunnyInteractRadius
            && !(dist2d <= HuntDistances.StuckNearRadius && IsStuckNearTarget(dist2d)))
        {
            if (!vnav.IsRunning())
            {
                vnav.PathfindAndMoveCloseTo(bunny.Position, false, OpenTreasureCofferChain.PathArrivalRange);
            }

            return;
        }

        if (dist3d > HuntDistances.BunnyMaxInteractRadius)
        {
            if (!vnav.IsRunning())
            {
                vnav.PathfindAndMoveCloseTo(bunny.Position, false, OpenTreasureCofferChain.PathArrivalRange);
            }

            return;
        }

        if (vnav.IsRunning())
        {
            vnav.Stop();
            return;
        }

        if (!EzThrottler.Throttle("CarrotHunt::InteractBunny", 400))
        {
            return;
        }

        unsafe
        {
            GameObject* gameObject = (GameObject*)(void*)bunny.Address;
            if (!gameObject->GetIsTargetable())
            {
                return;
            }

            TargetSystem.Instance()->InteractWithObject(gameObject, false);
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

        return false;
    }

    /// <summary>Re-solve nearest-neighbor tour on remaining pads, then begin the first hop.</summary>
    private void RecalculateAndAdvance(int? preferStartId = null)
    {
        ClearHop();
        activeReturnChain = null;
        returnThenAethernet = false;
        returnThenStop = false;
        currentAuthored = null;
        currentLiveCarrotId = null;
        currentTargetPosition = Vector3.Zero;
        itemUseIssued = false;
        waitingForBunnySince = DateTime.MinValue;
        ClearEmptyPadCandidate();
        ResetApproachProgress();
        usedLiveCarrotIdsAtPad.Clear();

        int? prefer = preferStartId ?? FindPreferredLiveNearbyPadId();
        RebuildTour(prefer);
        if (tour.Count == 0)
        {
            Phase = CarrotHuntPhase.Idle;
            return;
        }

        if (!fortuneCarrot.HasAny())
        {
            BocchiChat.PrintError(chat, uiConfig, OutOfCarrotsMessage);
            Teardown();
            return;
        }

        if (!TryBeginNextAuthored())
        {
            Phase = CarrotHuntPhase.Idle;
            return;
        }

        BeginRouteToCurrentAuthored();
        log.Information(
            "Carrot hunt: nearest-neighbor replan ({Count} remaining, start {StartId})",
            tour.Count,
            currentAuthored?.Id ?? 0);
    }

    private int? FindPreferredLiveNearbyPadId()
    {
        int? bestId = null;
        float bestDist = float.MaxValue;

        foreach (Carrot live in carrots.Carrots)
        {
            if (!live.IsValid() || usedLiveCarrotIdsAtPad.Contains(live.GameObjectId))
            {
                continue;
            }

            float dist = player.Position.Distance2D(live.GetPosition());
            if (dist > HuntDistances.NearbyLiveDivertRange)
            {
                continue;
            }

            CarrotData? pad = FindUnfinishedAuthoredPadForLive(live);
            if (pad == null)
            {
                continue;
            }

            if (dist < bestDist)
            {
                bestDist = dist;
                bestId = pad.Id;
            }
        }

        if (bestId is int id)
        {
            log.Information(
                "Carrot hunt preferring live nearby pad {Id} at {Distance:F1}y",
                id,
                bestDist);
        }

        return bestId;
    }

    private void RebuildTour(int? preferStartId = null)
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
        AethernetData main = zone.GetMainAetheryte();
        Vector3 start = player.Position;

        CarrotData current;
        if (preferStartId is int prefId && remaining.Any(c => c.Id == prefId))
        {
            current = remaining.First(c => c.Id == prefId);
        }
        else
        {
            current = PickCheapestStart(remaining, start, aetherytes, main);
        }

        tour.Add(current);
        HashSet<int> unvisited = remaining.Select(c => c.Id).Where(id => id != current.Id).ToHashSet();
        Dictionary<int, CarrotData> byId = remaining.ToDictionary(c => c.Id);

        while (unvisited.Count > 0)
        {
            Vector3 from = current.Position;
            int? nearestId = null;
            float best = float.MaxValue;
            foreach (int id in unvisited)
            {
                float d = TourCost(from, byId[id].Position, aetherytes, main, out _);
                if (d < best)
                {
                    best = d;
                    nearestId = id;
                }
            }

            if (nearestId is not int nextId)
            {
                break;
            }

            current = byId[nextId];
            tour.Add(current);
            unvisited.Remove(nextId);
        }

        log.Information(
            "Carrot hunt nearest-neighbor tour: {Count} remaining (start {Start})",
            tour.Count,
            tour[0].Id);
    }

    private CarrotData PickCheapestStart(
        List<CarrotData> remaining,
        Vector3 start,
        List<AethernetData> aetherytes,
        AethernetData main)
    {
        CarrotData best = remaining[0];
        float bestCost = float.MaxValue;
        foreach (CarrotData candidate in remaining)
        {
            float cost = TourCost(start, candidate.Position, aetherytes, main, out _);
            Carrot? liveNearPad = FindUnusedLiveCarrotNear(candidate, HuntDistances.MatchRadiusSq);
            if (liveNearPad != null)
            {
                float liveDist = start.Distance2D(liveNearPad.GetPosition());
                if (liveDist <= HuntDistances.NearbyLiveDivertRange)
                {
                    cost = Math.Min(cost, liveDist);
                }
            }

            if (cost < bestCost)
            {
                bestCost = cost;
                best = candidate;
            }
        }

        return best;
    }

    private enum HopMode
    {
        Direct,
        Aethernet,
        Return,
        ReturnThenAethernet
    }

    private static float TourCost(
        Vector3 from,
        Vector3 to,
        IReadOnlyList<AethernetData> aetherytes,
        AethernetData main,
        out HopMode mode)
    {
        mode = ChooseHopMode(from, to, aetherytes, main, out _, out _, out float cost, allowReturn: true);
        return cost;
    }

    private static HopMode ChooseHopMode(
        Vector3 from,
        Vector3 to,
        IReadOnlyList<AethernetData> aetherytes,
        AethernetData main,
        out AethernetData? departure,
        out AethernetData? arrival,
        out float bestCost,
        bool allowReturn = true)
    {
        departure = null;
        arrival = null;
        bestCost = from.Distance2D(to);
        HopMode bestMode = HopMode.Direct;

        float teleportCost = HuntRoutePlanner.AethernetHopCost;
        float returnCost = HuntRoutePlanner.ReturnCost;

        if (aetherytes.Count >= 2)
        {
            foreach (AethernetData shardA in aetherytes)
            {
                float toA = from.Distance2D(shardA.Position);
                foreach (AethernetData shardB in aetherytes)
                {
                    if (shardA.Id == shardB.Id)
                    {
                        continue;
                    }

                    float via = toA + teleportCost + shardB.Position.Distance2D(to);
                    if (via < bestCost)
                    {
                        bestCost = via;
                        bestMode = HopMode.Aethernet;
                        departure = shardA;
                        arrival = shardB;
                    }
                }
            }
        }

        if (!allowReturn)
        {
            return bestMode;
        }

        float returnWalk = returnCost + main.Position.Distance2D(to);
        if (returnWalk < bestCost)
        {
            bestCost = returnWalk;
            bestMode = HopMode.Return;
            departure = null;
            arrival = null;
        }

        foreach (AethernetData shard in aetherytes)
        {
            if (shard.Id == main.Id)
            {
                continue;
            }

            float via = returnCost + teleportCost + shard.Position.Distance2D(to);
            if (via < bestCost)
            {
                bestCost = via;
                bestMode = HopMode.ReturnThenAethernet;
                departure = main;
                arrival = shard;
            }
        }

        return bestMode;
    }

    private void MaybeBindLiveCarrot(CarrotData authored)
    {
        Carrot? live = FindUnusedLiveCarrotNear(authored, HuntDistances.MatchRadiusSq);
        currentLiveCarrotId = live?.GameObjectId;
        if (live != null)
        {
            currentTargetPosition = live.GetPosition();
        }
    }

    /// <summary>Divert to a nearer live chewed carrot (same pad rebind or other-pad replan).</summary>
    private bool TryDivertToNearbyLiveCarrot()
    {
        if (currentAuthored is not { } current)
        {
            return false;
        }

        float currentDist = player.Position.Distance2D(currentTargetPosition);
        if (currentDist < HuntDistances.NearbyLiveDivertMinCurrentDistance
            && currentLiveCarrotId != null)
        {
            return false;
        }

        Carrot? bestLive = null;
        CarrotData? bestPad = null;
        float bestDist = float.MaxValue;

        foreach (Carrot live in carrots.Carrots)
        {
            if (!live.IsValid() || usedLiveCarrotIdsAtPad.Contains(live.GameObjectId))
            {
                continue;
            }

            float distPlayer = player.Position.Distance2D(live.GetPosition());
            if (distPlayer > HuntDistances.NearbyLiveDivertRange)
            {
                continue;
            }

            CarrotData? pad = FindUnfinishedAuthoredPadForLive(live);
            if (pad == null)
            {
                continue;
            }

            if (distPlayer < bestDist)
            {
                bestDist = distPlayer;
                bestLive = live;
                bestPad = pad;
            }
        }

        if (bestLive == null || bestPad == null)
        {
            return false;
        }

        if (bestPad.Id == current.Id)
        {
            if (currentLiveCarrotId == bestLive.GameObjectId)
            {
                return false;
            }

            currentLiveCarrotId = bestLive.GameObjectId;
            currentTargetPosition = bestLive.GetPosition();
            CancelTravelForLocalCarrot();
            log.Information(
                "Carrot hunt: rebinding to live carrot on authored {Id} at {Dist:F1}y",
                bestPad.Id,
                bestDist);
            return true;
        }

        if (bestDist + HuntDistances.NearbyLiveDivertClearAdvantage >= currentDist)
        {
            return false;
        }

        if (!EzThrottler.Throttle("CarrotHuntDivert", 8000))
        {
            return false;
        }

        log.Information(
            "Carrot hunt: diverting to live carrot on authored {NearbyId} at {NearbyDist:F1}y (was {CurrentId} at {CurrentDist:F1}y)",
            bestPad.Id,
            bestDist,
            current.Id,
            currentDist);

        RecalculateAndAdvance(bestPad.Id);
        return true;
    }

    private CarrotData? FindUnfinishedAuthoredPadForLive(Carrot live)
    {
        Vector3 pos = live.GetPosition();
        float matchSq = HuntDistances.MatchRadiusSq;
        return zones.GetZone().GetCarrotData()
            .Where(c => !finishedAuthoredIds.Contains(c.Id))
            .OrderBy(c => Vector3.DistanceSquared(c.Position, pos))
            .FirstOrDefault(c => Vector3.DistanceSquared(c.Position, pos) <= matchSq);
    }

    private void CancelTravelForLocalCarrot()
    {
        ClearHop();
        activeReturnChain = null;
        returnThenAethernet = false;
        returnThenStop = false;
        if (Phase is CarrotHuntPhase.ApproachingAetheryte
            or CarrotHuntPhase.Teleporting
            or CarrotHuntPhase.Returning)
        {
            Phase = CarrotHuntPhase.Pathing;
        }

        vnav.Stop();
    }

    private bool CanTrustEmptyCarrotPad(Vector3 authoredPosition)
    {
        if (player.Position.Distance2D(authoredPosition) <= HuntDistances.EmptyPadSkipRadius)
        {
            return true;
        }

        float trustSq = HuntDistances.EmptyPadRegionTrustRadiusSq;
        return carrots.Carrots.Any(c =>
            c.IsValid()
            && Vector3.DistanceSquared(authoredPosition, c.GetPosition()) <= trustSq);
    }

    private bool ConfirmEmptyCarrotPad(int authoredId)
    {
        DateTime now = DateTime.UtcNow;
        if (emptyPadCandidateAuthoredId != authoredId)
        {
            emptyPadCandidateAuthoredId = authoredId;
            emptyPadCandidateSinceUtc = now;
            return false;
        }

        return now - emptyPadCandidateSinceUtc >= HuntDistances.EmptyPadConfirmDelay;
    }

    private void ClearEmptyPadCandidate()
    {
        emptyPadCandidateAuthoredId = null;
        emptyPadCandidateSinceUtc = DateTime.MinValue;
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
            Carrot? next = FindUnusedLiveCarrotNear(authored, HuntDistances.SamePadRecheckRadiusSq);
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
        RecalculateAndAdvance();
    }

    private void CompleteCurrentAuthored()
    {
        if (currentAuthored is { } authored)
        {
            finishedAuthoredIds.Add(authored.Id);
            log.Information("Carrot hunt: finished authored {Id} near {Pos:F0}", authored.Id, currentTargetPosition);
        }

        vnav.Stop();
        RecalculateAndAdvance();
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
        if (distance > HuntDistances.DismountRadius)
        {
            return false;
        }

        // Fortune Carrot use requires being on foot (chests do not).
        if (!DismountAssist.TryDismount(conditions))
        {
            return false;
        }

        if (vnav.IsRunning())
        {
            vnav.Stop();
        }

        return true;
    }

    private bool IsStuckNearTarget(float distance)
    {
        if (distance > HuntDistances.StuckNearRadius)
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

        if (now - approachLastProgressUtc < HuntDistances.StuckNearTimeout)
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
        ClearEmptyPadCandidate();
        ResetApproachProgress();
        ClearHop();
        activeReturnChain = null;
        returnThenStop = false;
        returnThenAethernet = false;
    }

    private void SoftStopWhileUnconscious()
    {
        chainManager.CancelWhere(name => name.StartsWith("CarrotHunt", StringComparison.Ordinal));
        activeReturnChain = null;
        activeTeleportChain = null;
        vnav.Stop();
        pathfinder.Stop();
    }

    private void StopDueToLeavingOccultCrescent()
    {
        log.Information("Left Occult Crescent — stopping carrot hunt");
        Teardown();
        BocchiChat.Print(chat, uiConfig, translator.T(".treasure.carrot_hunt_off_left_zone"));
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
        pathfinder.Stop();
        log.Information("Carrot hunt stopped");
    }
}
