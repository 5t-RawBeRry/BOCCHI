using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data;
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
using Ocelot.Extensions;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Lifecycle;
using Ocelot.Services.PlayerState;
using System.Diagnostics;
using System.Numerics;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace BOCCHI.Treasure.Services;

/// <summary>
///     Authored carrot tour (SH/NH): nearest-neighbor from the player, skip empty pads once
///     within tether range with no live chewed carrot, then Fortune Carrot → bunny gold chest.
/// </summary>
public sealed class CarrotHunterService
(
    ICarrotTracker carrots,
    FortuneCarrotAssist fortuneCarrot,
    UIConfig uiConfig,
    IPlayer player,
    ICondition conditions,
    IObjectTable objects,
    IVNavmeshIpc vnav,
    IZoneProvider zones,
    IAutomationModeGuard modeGuard,
    IChatGui chat,
    IPluginLog log
) : ICarrotHunter, IOnUpdate, IOnStop
{
    private const float UseThreshold = 2.0f;

    private const float PathArrivalRange = 1.0f;

    private const float BunnySearchRadius = 8f;

    private const float BunnyInteractRange = 2.5f;

    private static readonly TimeSpan BunnySpawnTimeout = TimeSpan.FromSeconds(20);

    private readonly Stopwatch stopwatch = new();

    private readonly HashSet<int> finishedAuthoredIds = [];

    private readonly List<CarrotData> tour = [];

    private int tourIndex;

    private CarrotData? currentAuthored;

    private ulong? currentLiveCarrotId;

    private Vector3 currentTargetPosition;

    private DateTime waitingForBunnySince = DateTime.MinValue;

    private bool itemUseIssued;

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
        Phase = CarrotHuntPhase.Pathing;
        log.Debug(
            "Carrot hunt: pathing to authored {Id} at {Pos}",
            currentAuthored!.Id,
            currentAuthored.Position);
    }

    private void TickPathing()
    {
        if (currentAuthored is not { } authored)
        {
            Phase = CarrotHuntPhase.Idle;
            return;
        }

        MaybeBindLiveCarrot(authored);

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
            float dist = player.Position.Distance(currentTargetPosition);
            if (dist <= UseThreshold)
            {
                vnav.Stop();
                Phase = CarrotHuntPhase.UsingItem;
                return;
            }
        }
        else
        {
            currentTargetPosition = authored.Position;
        }

        float distTarget = player.Position.Distance(currentTargetPosition);
        if (distTarget <= UseThreshold && currentLiveCarrotId != null)
        {
            vnav.Stop();
            Phase = CarrotHuntPhase.UsingItem;
            return;
        }

        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveCloseTo(currentTargetPosition, false, PathArrivalRange);
        }
    }

    private void TickUsingItem()
    {
        if (!TryGetCurrentLiveCarrot(out Carrot carrot))
        {
            // Lost the live object at interact range — treat as empty / done for this pad.
            SkipCurrentAuthored();
            return;
        }

        currentTargetPosition = carrot.GetPosition();

        if (player.IsCasting() || conditions[ConditionFlag.Casting])
        {
            return;
        }

        if (player.IsMounted() || conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            if (EzThrottler.Throttle("CarrotHunt::Dismount", 500) && Actions.Dismount.CanCast())
            {
                Actions.Dismount.Cast();
            }

            return;
        }

        float dist = player.Position.Distance(currentTargetPosition);
        if (dist > UseThreshold)
        {
            Phase = CarrotHuntPhase.Pathing;
            return;
        }

        if (!itemUseIssued)
        {
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
            log.Debug("Carrot hunt: Fortune Carrot used at {Pos}", currentTargetPosition);
            return;
        }

        waitingForBunnySince = DateTime.UtcNow;
        Phase = CarrotHuntPhase.WaitingForBunny;
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
            CompleteCurrentAuthored();
            return;
        }

        if (player.IsMounted() || conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            if (EzThrottler.Throttle("CarrotHunt::DismountBunny", 500) && Actions.Dismount.CanCast())
            {
                Actions.Dismount.Cast();
            }

            return;
        }

        float dist = player.Position.Distance(bunny.Position);
        if (dist > BunnyInteractRange)
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

        IReadOnlyList<Vector3> aetherytePositions = zone.GetAetherytes()
            .Select(a => a.Position)
            .ToList();
        float teleportCost = HuntRoutePlanner.AethernetHopCost;

        Vector3 start = player.Position;
        CarrotData current = remaining[0];
        float bestStart = float.MaxValue;
        bool firstViaAethernet = false;
        foreach (CarrotData candidate in remaining)
        {
            float cost = TourCost(start, candidate.Position, aetherytePositions, teleportCost, out bool viaAethernet);
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
                float d = TourCost(from, byId[id].Position, aetherytePositions, teleportCost, out bool viaAethernet);
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

        log.Information("Carrot hunt aethernet-aware nearest-neighbor tour: {Count} remaining", tour.Count);
    }

    /// <summary>
    ///     min(direct Euclidean, min over aetheryte pairs of walk-to-A + teleport + walk-from-B).
    /// </summary>
    private static float TourCost(
        Vector3 from,
        Vector3 to,
        IReadOnlyList<Vector3> aetherytePositions,
        float teleportCost,
        out bool viaAethernet)
    {
        float direct = Vector3.Distance(from, to);
        viaAethernet = false;
        if (aetherytePositions.Count == 0)
        {
            return direct;
        }

        float bestVia = float.MaxValue;
        foreach (Vector3 shardA in aetherytePositions)
        {
            float toA = Vector3.Distance(from, shardA);
            foreach (Vector3 shardB in aetherytePositions)
            {
                float via = toA + teleportCost + Vector3.Distance(shardB, to);
                if (via < bestVia)
                {
                    bestVia = via;
                }
            }
        }

        if (bestVia < direct)
        {
            viaAethernet = true;
            return bestVia;
        }

        return direct;
    }

    private void MaybeBindLiveCarrot(CarrotData authored)
    {
        float matchSq = CarrotHuntDistances.MatchRadiusSq;
        Carrot? live = carrots.Carrots
            .Where(c => c.IsValid())
            .Where(c => Vector3.DistanceSquared(authored.Position, c.GetPosition()) <= matchSq)
            .OrderBy(c => Vector3.DistanceSquared(authored.Position, c.GetPosition()))
            .FirstOrDefault();

        currentLiveCarrotId = live?.GameObjectId;
        if (live != null)
        {
            currentTargetPosition = live.GetPosition();
        }
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
            log.Debug("Carrot hunt: finished authored {Id} near {Pos}", authored.Id, currentTargetPosition);
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

    private void ClearCurrent()
    {
        currentAuthored = null;
        currentLiveCarrotId = null;
        currentTargetPosition = Vector3.Zero;
        itemUseIssued = false;
        waitingForBunnySince = DateTime.MinValue;
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
