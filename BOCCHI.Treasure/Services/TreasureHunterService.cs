using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.ChainRecipes;
using BOCCHI.Treasure.Hunt;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using Ocelot.Actions;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Extensions;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Lifecycle;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using TreasureSheet = Lumina.Excel.Sheets.Treasure;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace BOCCHI.Treasure.Services;

public class TreasureHunterService
(
    TreasureConfig config,
    AutomatorConfig automatorConfig,
    IZoneProvider zones,
    IVNavmeshIpc vnav,
    IPathfinder pathfinder,
    IChainFactory chains,
    IChainManager chainManager,
    IObjectTable objects,
    ICondition conditions,
    IPlayer player,
    IDataManager data,
    IDalamudPluginInterface plugin,
    IPluginLog log,
    IGameGui gui,
    ITreasureTracker tracker,
    ISupportJobFactory supportJobs,
    IClientState client,
    IAutomationModeGuard modeGuard,
    IMp3SoundPlayer sounds,
    NinjaHideAssist ninjaHide
) : ITreasureHunter, IOnUpdate, IOnStop
{
    private const float ChestSearchRadius = 25f;

    /// <summary>Start open attempts once this close to the coffer (yalms).</summary>
    private const float CofferOpenAttemptRadius = 75f;

    /// <summary>
    /// Wider layout↔live match when spawns drift from layout (e.g. Moldering 2048 ~40y).
    /// Only accepted when this layout is the nearest node to the live coffer.
    /// </summary>
    private const float ChestDriftSearchRadius = 50f;

    /// <summary>How long to wait for WideText after casting Treasure Sight.</summary>
    private static readonly TimeSpan SightCountWait = TimeSpan.FromSeconds(8);

    /// <summary>First stuck recovery: lateral nudge around blocking geometry (#156).</summary>
    private static readonly TimeSpan StuckNudgeTimeout = TimeSpan.FromSeconds(12);

    /// <summary>How long to tolerate no progress toward a coffer before skipping that node.</summary>
    private static readonly TimeSpan StuckNodeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Minimum distance improvement toward the destination that counts as progress.</summary>
    private const float StuckProgressThreshold = 1.5f;

    private const float StuckDetectionMinDistance = 8f;

    /// <summary>Reprioritize when a live remaining coffer is this close and much nearer than the current target.</summary>
    private const float NearbyLiveReprioritizeRange = 60f;

    private const float NearbyLiveReprioritizeMinCurrentDistance = 80f;

    private readonly List<TreasureLayoutDatum> layoutTreasure = [];
    private readonly List<HuntPathfinderStep> steps = [];
    private readonly HashSet<uint> checkedNodeIds = [];
    private readonly HashSet<uint> lastCompletedRunNodeIds = [];

    private readonly Stopwatch stopwatch = new();
    private Task<ChainResult>? activeChain;

    private IHuntRoutePlanner? pathPlanner;
    private bool planningRoute;
    private bool pendingStartSight;
    private bool waitingForSightCounts;
    private DateTime sightCastUtc = DateTime.MinValue;
    private int locationsSinceLastSight;
    private HashSet<uint> excludedNodeIdsForNextRun = [];
    private int? maxLevelOverrideForNextRun;
    private uint? stuckWatchNodeId;
    private float stuckWatchBestDistance = float.MaxValue;
    private DateTime stuckWatchLastProgressUtc = DateTime.MinValue;
    private DateTime stuckWatchStartedUtc = DateTime.MinValue;
    private bool stuckNudgeIssued;

    /// <summary>Hysteresis: Hide required until threats leave exit distance.</summary>
    private bool ninjaHideRequired;

    /// <summary>Via-points for the current WalkToNode (departure of previous + approach of current).</summary>
    private readonly List<Vector3> walkVias = [];

    private int walkViaIndex;
    private int walkViaStepIndex = -1;

    public void OnStop() => Teardown();

    public void Update()
    {
        if (!Running || Paused)
        {
            return;
        }

        if (!zones.GetZone().IsOccultCrescentZone())
        {
            Teardown();
            return;
        }

        if (config.SkipUnsafeTreasureWindows && IsUnsafeTreasureWindow())
        {
            return;
        }

        if (!IsVnavReady)
        {
            return;
        }

        if (activeChain is { IsCompleted: false })
        {
            return;
        }

        if (pathPlanner != null)
        {
            if (pathPlanner.State != HuntPathfinderState.FileLoaded)
            {
                return;
            }

            if (!planningRoute)
            {
                return;
            }

            planningRoute = false;
            List<uint> validNodes = GetValidNodesForNextPlan();
            steps.Clear();
            uint? preferStart = FindPreferredLiveNearbyNode(validNodes);
            steps.AddRange(pathPlanner.FindPath(player.Position, validNodes, preferStart).GetAwaiter().GetResult());
            pathPlanner = null;
            StepIndex = 0;
            pendingStartSight = config.CastTreasureSightDuringHunt && SupportJobTreasureSight.CanCast(supportJobs);
            if (steps.Count == 0)
            {
                log.Warning(
                    "Treasure hunt planned an empty route ({ValidCount} valid node(s) after filters) — ending session",
                    validNodes.Count);
                CompleteHunt();
            }

            return;
        }

        if (TryFinishSightAndMaybeAbort())
        {
            return;
        }

        if (TryBeginTreasureSight())
        {
            return;
        }

        if (TryReprioritizeNearbyLiveCoffer())
        {
            return;
        }

        if (steps.Count == 0 || StepIndex >= steps.Count)
        {
            if (steps.Count > 0 && ShouldReturnAfterHunt())
            {
                steps.Add(HuntPathfinderStep.ReturnToBaseCamp());
                return;
            }

            CompleteHunt();
            return;
        }

        // Teleport/return handlers must observe completed chains before we clear them.
        // Clearing first re-starts the same teleport forever.
        if (steps.Count > 0 && StepIndex < steps.Count && TryAdvanceCurrentStep())
        {
            HuntPathfinderStep completed = steps[StepIndex];
            if (completed.Type == HuntPathfinderStepType.WalkToNode)
            {
                LastCheckedNodeId = completed.NodeId;
                checkedNodeIds.Add(completed.NodeId);
                locationsSinceLastSight++;
                walkViaStepIndex = -1;
                walkVias.Clear();
                walkViaIndex = 0;
                StepDistance = 0f;
                // Fresh nearest-neighbor from here after every open / empty skip completion.
                RecalculateRoute();
                if (activeChain is { IsCompleted: true })
                {
                    activeChain = null;
                }

                return;
            }

            StepIndex++;
            StepDistance = 0f;
            walkViaStepIndex = -1;
            walkVias.Clear();
            walkViaIndex = 0;
        }

        if (activeChain is { IsCompleted: true })
        {
            activeChain = null;
        }
    }

    public bool Running { get; private set; }

    public bool Paused { get; private set; }

    /// <inheritdoc />
    public bool WaitingForSafeWindow =>
        Running
        && !Paused
        && config.SkipUnsafeTreasureWindows
        && IsUnsafeTreasureWindow();

    public int StepIndex { get; private set; }

    public int StepCount => steps.Count;

    public float StepDistance { get; private set; }

    public TimeSpan Elapsed => stopwatch.Elapsed;

    public uint? LastCheckedNodeId { get; private set; }

    public IReadOnlySet<uint> LastCompletedRunNodeIds => lastCompletedRunNodeIds;

    public bool ManagedByPotsTreasure { get; set; }

    public bool ManagedByIllegalModeFiller { get; set; }

    public bool IsVnavAvailable => vnav.IsAvailable();

    public bool IsVnavReady => vnav.IsNavmeshReady();

    public void Toggle()
    {
        if (Running)
        {
            Teardown();
            return;
        }

        modeGuard.EnsureExclusive(AutomationMode.TreasureHunt);
        ManagedByPotsTreasure = false;
        ManagedByIllegalModeFiller = false;
        BeginHuntSession();
    }

    public void StartManaged()
    {
        if (Running)
        {
            return;
        }

        BeginHuntSession();
    }

    public void ConfigureManagedRun(IReadOnlySet<uint> excludedNodeIds, int? maxLevelOverride = null)
    {
        ManagedByPotsTreasure = true;
        excludedNodeIdsForNextRun = excludedNodeIds.ToHashSet();
        maxLevelOverrideForNextRun = maxLevelOverride;
    }

    public bool RecalculateRoute()
    {
        if (!Running || Paused || !IsVnavReady)
        {
            return false;
        }

        TreasureHuntPathfinder? planner = CreatePathPlanner();
        if (planner == null || planner.State != HuntPathfinderState.FileLoaded)
        {
            log.Warning("Failed to initialize treasure hunt path data for route recalculation");
            return false;
        }

        SoftStopMovement();
        steps.Clear();
        StepIndex = 0;
        StepDistance = 0f;
        pendingStartSight = false;
        waitingForSightCounts = false;
        sightCastUtc = DateTime.MinValue;
        locationsSinceLastSight = 0;
        pathPlanner = planner;
        planningRoute = true;

        log.Info("Treasure hunt route recalculation requested; {CheckedCount} checked nodes excluded", checkedNodeIds.Count);
        return true;
    }

    private void BeginHuntSession()
    {
        stopwatch.Restart();
        StepIndex = 0;
        LastCheckedNodeId = null;
        Paused = false;
        steps.Clear();
        layoutTreasure.Clear();
        pendingStartSight = false;
        waitingForSightCounts = false;
        sightCastUtc = DateTime.MinValue;
        locationsSinceLastSight = 0;
        ninjaHideRequired = false;
        checkedNodeIds.Clear();
        ResetStuckWatch();
        if (!ManagedByPotsTreasure)
        {
            lastCompletedRunNodeIds.Clear();
            excludedNodeIdsForNextRun.Clear();
            maxLevelOverrideForNextRun = null;
        }

        pathPlanner = CreatePathPlanner();
        if (pathPlanner == null || pathPlanner.State != HuntPathfinderState.FileLoaded)
        {
            log.Error("Failed to initialize treasure hunt path data");
            Teardown();
            return;
        }

        Running = true;
        planningRoute = true;
    }

    public void Pause()
    {
        if (!Running || Paused)
        {
            return;
        }

        Paused = true;
        SoftStopMovement();
        stopwatch.Stop();
    }

    public void Resume()
    {
        if (!Running || !Paused)
        {
            return;
        }

        Paused = false;
        if (!stopwatch.IsRunning)
        {
            stopwatch.Start();
        }
    }

    public HuntPathfinderStep? GetCurrentStep()
    {
        if (StepIndex < 0 || StepIndex >= steps.Count)
        {
            return null;
        }

        return steps[StepIndex];
    }

    public bool TryGetResumeCoffer(out uint nodeId, out Vector3 position)
    {
        nodeId = 0;
        position = default;
        if (!Running || steps.Count == 0)
        {
            return false;
        }

        for (int i = Math.Max(0, StepIndex); i < steps.Count; i++)
        {
            HuntPathfinderStep step = steps[i];
            if (step.Type != HuntPathfinderStepType.WalkToNode)
            {
                continue;
            }

            int layoutIndex = layoutTreasure.FindIndex(t => t.Id == step.NodeId);
            if (layoutIndex < 0)
            {
                continue;
            }

            nodeId = step.NodeId;
            position = layoutTreasure[layoutIndex].Position;
            return true;
        }

        return false;
    }

    public unsafe bool FlagResumePoint()
    {
        if (!TryGetResumeCoffer(out uint nodeId, out Vector3 position))
        {
            return false;
        }

        AgentMap* map = AgentMap.Instance();
        if (map == null)
        {
            return false;
        }

        map->SetFlagMapMarker(client.TerritoryType, client.MapId, position);
        log.Info("Flagged treasure hunt resume coffer {NodeId} at {Position:f0}", nodeId, position);
        return true;
    }

    /// <summary>Stop movement/chains without clearing the planned route.</summary>
    private void SoftStopMovement()
    {
        chainManager.CancelWhere(name => name.StartsWith("TreasureHunt", StringComparison.Ordinal));
        pathfinder.Stop();
        vnav.Stop();
        activeChain = null;
        ResetStuckWatch();
    }

    private bool TryRecoverFromStuckWalk(HuntPathfinderStep step, float distance)
    {
        if (distance <= StuckDetectionMinDistance)
        {
            ResetStuckWatch();
            return false;
        }

        DateTime now = DateTime.UtcNow;
        if (stuckWatchNodeId != step.NodeId)
        {
            StartStuckWatch(step.NodeId, distance, now);
            return false;
        }

        // Progress toward the destination (not absolute movement) — circling a rock no longer resets forever.
        if (distance < stuckWatchBestDistance - StuckProgressThreshold)
        {
            stuckWatchBestDistance = distance;
            stuckWatchLastProgressUtc = now;
            return false;
        }

        // #156: try a lateral nudge around geometry before giving up on the node.
        if (!stuckNudgeIssued && now - stuckWatchStartedUtc >= StuckNudgeTimeout)
        {
            stuckNudgeIssued = true;
            stuckWatchLastProgressUtc = now;
            TryIssueStuckNudge(step);
            return true;
        }

        if (now - stuckWatchStartedUtc < StuckNodeTimeout)
        {
            return false;
        }

        log.Warning(
            "Treasure hunt appears stuck reaching coffer {NodeId}; excluding it and recalculating the route",
            step.NodeId);
        checkedNodeIds.Add(step.NodeId);
        LastCheckedNodeId = step.NodeId;
        ResetStuckWatch();
        return RecalculateRoute();
    }

    private void TryIssueStuckNudge(HuntPathfinderStep step)
    {
        TreasureLayoutDatum layout = layoutTreasure.FirstOrDefault(t => t.Id == step.NodeId);
        Vector3 dest = layout.Id == step.NodeId ? layout.Position : player.Position;
        Vector3 toDest = dest - player.Position;
        toDest.Y = 0f;
        if (toDest.LengthSquared() < 0.25f)
        {
            toDest = new Vector3(1f, 0f, 0f);
        }

        Vector3 forward = Vector3.Normalize(toDest);
        Vector3 lateral = new(-forward.Z, 0f, forward.X);
        Vector3 nudge = player.Position + (lateral * 8f);
        nudge = new Vector3(nudge.X, player.Position.Y, nudge.Z);

        log.Info("Treasure hunt stuck near {NodeId} — nudging sideways around geometry (#156)", step.NodeId);
        pathfinder.Stop();
        vnav.Stop();
        vnav.PathfindAndMoveCloseTo(nudge, false, 1.5f);
    }

    private void StartStuckWatch(uint nodeId, float distance, DateTime now)
    {
        stuckWatchNodeId = nodeId;
        stuckWatchBestDistance = distance;
        stuckWatchLastProgressUtc = now;
        stuckWatchStartedUtc = now;
        stuckNudgeIssued = false;
    }

    private void ResetStuckWatch()
    {
        stuckWatchNodeId = null;
        stuckWatchBestDistance = float.MaxValue;
        stuckWatchLastProgressUtc = DateTime.MinValue;
        stuckWatchStartedUtc = DateTime.MinValue;
        stuckNudgeIssued = false;
    }

    /// <summary>
    /// Prefer a remaining layout node that already has a live coffer near the player
    /// (Nearby list is independent of the TSP — without this, bronzes next to you get walked past).
    /// </summary>
    private uint? FindPreferredLiveNearbyNode(IReadOnlyList<uint> validNodes)
    {
        uint? bestId = null;
        float bestDist = float.MaxValue;

        foreach (uint nodeId in validNodes)
        {
            TreasureLayoutDatum layout = layoutTreasure.FirstOrDefault(t => t.Id == nodeId);
            if (layout.Id != nodeId)
            {
                continue;
            }

            IGameObject? present = FindTreasureForLayout(layout.Position, nodeId);
            if (present == null || OpenTreasureCofferChain.IsOpenedOrLooted(present))
            {
                continue;
            }

            float distToPlayer = player.Position.Distance2D(present.Position);
            if (distToPlayer > NearbyLiveReprioritizeRange)
            {
                continue;
            }

            if (distToPlayer < bestDist)
            {
                bestDist = distToPlayer;
                bestId = nodeId;
            }
        }

        if (bestId is uint id)
        {
            log.Info(
                "Treasure hunt preferring live nearby coffer {NodeId} at {Distance:F1}y",
                id,
                bestDist);
        }

        return bestId;
    }

    /// <summary>
    /// Mid-route: if a live remaining coffer sits near the player while the current target is far,
    /// recalculate so FindPath can start on that coffer.
    /// </summary>
    private bool TryReprioritizeNearbyLiveCoffer()
    {
        if (planningRoute || pathPlanner != null || activeChain != null)
        {
            return false;
        }

        HuntPathfinderStep? current = GetCurrentStep();
        if (current is not { Type: HuntPathfinderStepType.WalkToNode })
        {
            return false;
        }

        float currentDist = StepDistance;
        if (currentDist < NearbyLiveReprioritizeMinCurrentDistance)
        {
            return false;
        }

        List<uint> remaining = GetRemainingWalkNodeIds();
        remaining.Remove(current.NodeId);
        uint? prefer = FindPreferredLiveNearbyNode(remaining);
        if (prefer is not uint nearbyId)
        {
            return false;
        }

        TreasureLayoutDatum layout = layoutTreasure.FirstOrDefault(t => t.Id == nearbyId);
        if (layout.Id != nearbyId)
        {
            return false;
        }

        IGameObject? present = FindTreasureForLayout(layout.Position, nearbyId);
        if (present == null)
        {
            return false;
        }

        float nearbyDist = player.Position.Distance2D(present.Position);
        if (nearbyDist >= currentDist * 0.5f)
        {
            return false;
        }

        if (!EzThrottler.Throttle("TreasureHuntReprioritize", 8000))
        {
            return false;
        }

        log.Info(
            "Treasure hunt diverting to live coffer {NearbyId} at {NearbyDist:F1}y (was heading to {CurrentId} at {CurrentDist:F1}y)",
            nearbyId,
            nearbyDist,
            current.NodeId,
            currentDist);
        return RecalculateRoute();
    }

    private List<uint> GetRemainingWalkNodeIds()
    {
        List<uint> ids = [];
        for (int i = StepIndex; i < steps.Count; i++)
        {
            if (steps[i].Type == HuntPathfinderStepType.WalkToNode)
            {
                ids.Add(steps[i].NodeId);
            }
        }

        return ids;
    }

    private bool TryBeginTreasureSight()
    {
        if (!config.CastTreasureSightDuringHunt || !SupportJobTreasureSight.CanCast(supportJobs))
        {
            pendingStartSight = false;
            return false;
        }

        if (waitingForSightCounts || activeChain != null)
        {
            return false;
        }

        // Don't interrupt return / teleport mid-step.
        HuntPathfinderStep? step = GetCurrentStep();
        if (step is { Type: HuntPathfinderStepType.ReturnToBaseCamp or HuntPathfinderStepType.TeleportToAethernet })
        {
            return false;
        }

        bool dueForStart = pendingStartSight;
        bool dueForRefresh = !pendingStartSight
                             && steps.Count > 0
                             && StepIndex < steps.Count
                             && locationsSinceLastSight >= config.TreasureSightEveryNLocations;

        if (!dueForStart && !dueForRefresh)
        {
            return false;
        }

        // Defer while fighting — Sight dismounts + swaps PJ; remount fails in combat.
        if (conditions[ConditionFlag.InCombat])
        {
            return false;
        }

        SoftStopMovement();
        pendingStartSight = false;
        waitingForSightCounts = true;
        sightCastUtc = DateTime.UtcNow;
        locationsSinceLastSight = 0;

        activeChain = chainManager.Manage(
            chains.Create("TreasureHunt::TreasureSight")
                .Then<HuntTreasureSightChain>()
        );

        return true;
    }

    /// <returns>True when the caller should skip the rest of this tick.</returns>
    private bool TryFinishSightAndMaybeAbort()
    {
        if (!waitingForSightCounts)
        {
            return false;
        }

        if (activeChain is { IsCompleted: false })
        {
            return true;
        }

        if (activeChain is { IsCompleted: true })
        {
            bool castOk = activeChain.IsCompletedSuccessfully && (activeChain.Result?.IsSuccess ?? false);
            activeChain = null;
            if (!castOk)
            {
                waitingForSightCounts = false;
                log.Warning("Treasure Sight cast during hunt failed; continuing route");
                return false;
            }
        }

        bool refreshed = tracker.LastCountUpdateUtc >= sightCastUtc;
        bool timedOut = DateTime.UtcNow - sightCastUtc >= SightCountWait;
        if (!refreshed && !timedOut)
        {
            return true;
        }

        waitingForSightCounts = false;

        if (ShouldAbortForNoChests())
        {
            FinishHuntEarly("Treasure Sight reports no remaining coffers");
            return true;
        }

        int trimmed = TrimNearbyEmptyNodesAfterSight();
        log.Info(
            "Treasure Sight refresh: {Bronze} bronze / {Silver} silver remaining; trimmed {Trimmed} nearby empty pad(s)",
            tracker.BronzeChests,
            tracker.SilverChests,
            trimmed);
        RecalculateRoute();
        return true;
    }

    /// <summary>
    /// After Sight, drop remaining layout nodes that are already in tether range with no live coffer
    /// so we do not walk onto known empties before the next hop.
    /// </summary>
    private int TrimNearbyEmptyNodesAfterSight()
    {
        int trimmed = 0;
        foreach (uint nodeId in GetValidNodesForNextPlan().ToList())
        {
            TreasureLayoutDatum spot = layoutTreasure.FirstOrDefault(t => t.Id == nodeId);
            if (spot.Id != nodeId)
            {
                continue;
            }

            float dist = player.Position.Distance2D(spot.Position);
            if (dist > ChestSearchRadius)
            {
                continue;
            }

            if (FindTreasureForLayout(spot.Position, nodeId) != null)
            {
                continue;
            }

            checkedNodeIds.Add(nodeId);
            trimmed++;
            log.Info("Treasure Sight: trimming empty pad {NodeId} within tether range", nodeId);
        }

        return trimmed;
    }

    private bool ShouldAbortForNoChests()
    {
        if (!config.CastTreasureSightDuringHunt || !tracker.CountInitialised)
        {
            return false;
        }

        if (tracker.BronzeChests + tracker.SilverChests > 0)
        {
            return false;
        }

        // Still have route work that isn't the epilogue Return.
        for (int i = StepIndex; i < steps.Count; i++)
        {
            if (steps[i].Type != HuntPathfinderStepType.ReturnToBaseCamp)
            {
                return true;
            }
        }

        return false;
    }

    private void FinishHuntEarly(string reason)
    {
        log.Info($"Treasure hunt ending early: {reason}");
        SoftStopMovement();
        waitingForSightCounts = false;
        pendingStartSight = false;

        if (StepIndex < steps.Count)
        {
            steps.RemoveRange(StepIndex, steps.Count - StepIndex);
        }
    }

    private bool TryAdvanceCurrentStep()
    {
        HuntPathfinderStep step = steps[StepIndex];
        return step.Type switch
        {
            HuntPathfinderStepType.WalkToNode => HandleWalkToNode(step),
            HuntPathfinderStepType.ReturnToBaseCamp => HandleReturnToBaseCamp(),
            HuntPathfinderStepType.WalkToAethernet => HandleWalkToAethernet(step),
            HuntPathfinderStepType.TeleportToAethernet => HandleTeleportToAethernet(step),
            var _ => true
        };
    }

    private bool HandleWalkToNode(HuntPathfinderStep step)
    {
        if (!Running)
        {
            vnav.Stop();
            return true;
        }

        EnsureWalkVias(step);
        if (walkViaIndex < walkVias.Count)
        {
            Vector3 via = walkVias[walkViaIndex];
            float viaDist = player.Position.Distance2D(via);
            StepDistance = viaDist;

            const float viaArrival = 2.5f;
            if (viaDist > viaArrival)
            {
                if (!vnav.IsRunning())
                {
                    vnav.PathfindAndMoveCloseTo(via, false, OpenTreasureCofferChain.PathArrivalRange);
                }

                MaybeMount(via);
                return false;
            }

            walkViaIndex++;
            vnav.Stop();
            return false;
        }

        Vector3 layoutDestination = layoutTreasure.First(t => t.Id == step.NodeId).Position;

        // Presence: don't require IsTargetable (often false until inside interact range).
        IGameObject? present = FindTreasureForLayout(layoutDestination, step.NodeId);

        // Stick to layout while far when spawn matches; switch to live when close or when layout drifted.
        Vector3 destination = layoutDestination;
        if (present != null)
        {
            float layoutToLive = layoutDestination.Distance2D(present.Position);
            float distToLayout = player.Position.Distance2D(layoutDestination);
            if (layoutToLive > 5f || distToLayout <= OpenTreasureCofferChain.MaxInteractRange * 2f)
            {
                destination = present.Position;
            }
        }

        float dist2d = player.Position.Distance2D(destination);
        StepDistance = dist2d;

        if (!vnav.IsRunning() && dist2d > OpenTreasureCofferChain.PreferredOpenDistance)
        {
            vnav.PathfindAndMoveCloseTo(destination, false, OpenTreasureCofferChain.PathArrivalRange);
        }

        MaybeMount(destination);

        if (!ApplyNinjaHideGate())
        {
            return false;
        }

        if (TryRecoverFromStuckWalk(step, StepDistance))
        {
            return false;
        }

        if (StepDistance > CofferOpenAttemptRadius)
        {
            return false;
        }

        if (present != null && OpenTreasureCofferChain.IsOpenedOrLooted(present))
        {
            vnav.Stop();
            ResetStuckWatch();
            return true;
        }

        // Empty / unspawned: once within normal tether range with no object-table match, skip + replan.
        if (present == null)
        {
            float distToLayout = player.Position.Distance2D(layoutDestination);
            if (distToLayout <= ChestSearchRadius)
            {
                log.Info(
                    "Treasure hunt: no live coffer at layout {NodeId} within tether range — skipping and recalculating",
                    step.NodeId);
                checkedNodeIds.Add(step.NodeId);
                LastCheckedNodeId = step.NodeId;
                ResetStuckWatch();
                RecalculateRoute();
                return false;
            }

            return false;
        }

        float dist3d = Vector3.Distance(player.Position, present.Position);
        if (dist3d > OpenTreasureCofferChain.MaxInteractRange)
        {
            return false;
        }

        if (vnav.IsRunning() && dist3d > OpenTreasureCofferChain.PreferredOpenDistance)
        {
            return false;
        }

        vnav.Stop();
        ResetStuckWatch();
        activeChain = chainManager.Manage(
            chains.Create($"TreasureHunt::Open({step.NodeId})")
                .Then<OpenTreasureCofferChain, TreasureOpenTarget>(present.Position)
        );

        return false;
    }

    private bool HandleReturnToBaseCamp()
    {
        StepDistance = 0f;
        IZone zone = zones.GetZone();
        bool inCombat = conditions[ConditionFlag.InCombat];

        if (inCombat && !vnav.IsRunning())
        {
            SprintAssist.MaybeCast(automatorConfig.SprintOnAetheryteApproach, zone.IsInBasecamp());
            Vector3 standOff = zone.GetMainAetheryte().GetCampStandOffPosition(player.Position);
            vnav.PathfindAndMoveCloseTo(standOff, false, AethernetNavigation.PathfindArrivalRadius);
            return false;
        }

        if (!inCombat && vnav.IsRunning())
        {
            vnav.Stop();
        }

        if (inCombat)
        {
            return false;
        }

        if (conditions[ConditionFlag.Unconscious])
        {
            return false;
        }

        if (zone.IsInBasecamp())
        {
            return true;
        }

        if (activeChain != null)
        {
            if (!activeChain.IsCompleted)
            {
                return false;
            }

            bool returned = activeChain.IsCompletedSuccessfully && zone.IsInBasecamp();
            activeChain = null;
            return returned;
        }

        activeChain = chainManager.Manage(
            chains.Create("TreasureHunt::Return")
                .Then(_ =>
                {
                    if (Actions.Return.CanCast())
                    {
                        Actions.Return.Cast();
                    }

                    return StepResult.Success();
                }, "TreasureHunt::CastReturn")
                .WaitUntil(
                    _ =>
                    {
                        TryConfirmReturnDialog();
                        return ValueTask.FromResult(zones.GetZone().IsInBasecamp());
                    },
                    TimeSpan.FromSeconds(120),
                    TimeSpan.FromMilliseconds(250),
                    "TreasureHunt::WaitForBasecamp"
                )
        );

        return false;
    }

    private unsafe void TryConfirmReturnDialog()
    {
        // Death prompts also use SelectYesno — don't force-respawn while unconscious.
        if (conditions[ConditionFlag.Unconscious])
        {
            return;
        }

        if (!EzThrottler.Throttle("TreasureHunt::SelectYesno", 250))
        {
            return;
        }

        AddonSelectYesno* yesno = gui.GetAddonByName<AddonSelectYesno>("SelectYesno");
        if (yesno == null)
        {
            return;
        }

        ReturnYesNo.TryAccept(&yesno->AtkUnitBase);
    }

    private bool HandleWalkToAethernet(HuntPathfinderStep step)
    {
        if (!Running)
        {
            vnav.Stop();
            return true;
        }

        AethernetData aethernet = ResolveAethernet(step.Aethernet);
        Vector3 crystal = aethernet.Position;
        Vector3 destination = aethernet.GetCampStandOffPosition(player.Position);
        StepDistance = player.Position.Distance2D(crystal);

        // Prefer Lifestream-ready (magenta) over raw crystal distance — stand-off may sit on the pad.
        if (zones.GetZone().IsWithinLifestreamRange(player.Position)
            || player.Position.Distance2D(destination) <= AethernetNavigation.PathfindArrivalRadius + 0.35f)
        {
            vnav.Stop();
            return true;
        }

        float arrival = AethernetNavigation.PathfindArrivalRadius;
        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveCloseTo(destination, false, arrival);
        }

        MaybeMount(destination);

        if (!ApplyNinjaHideGate())
        {
            return false;
        }

        return false;
    }

    private bool HandleTeleportToAethernet(HuntPathfinderStep step)
    {
        StepDistance = 0f;

        if (activeChain != null)
        {
            if (!activeChain.IsCompleted)
            {
                return false;
            }

            bool teleported = activeChain.IsCompletedSuccessfully
                              && (activeChain.Result?.IsSuccess ?? false);
            activeChain = null;
            return teleported;
        }

        uint placeNameId = (uint)step.Aethernet;
        activeChain = chainManager.Manage(
            chains.Create($"TreasureHunt::Teleport({placeNameId})")
                .Then<AethernetTeleportChain, uint>(placeNameId)
        );

        return false;
    }

    private void MaybeMount(Vector3 destination)
    {
        if (ninjaHideRequired || ninjaHide.IsStealthed)
        {
            return;
        }

        if (!automatorConfig.ShouldAutoMount)
        {
            return;
        }

        if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            return;
        }

        if (player.Position.Distance(destination) > NavigationConstants.MountMinDistance
            && !zones.GetZone().IsInBasecamp())
        {
            MountWait.TryCast(automatorConfig.PreferredMountId);
        }
    }

    /// <summary>
    ///     When enabled and a knowledge threat is in range: gearset → dismount → Hide before continuing on foot.
    ///     Returns false while still preparing (caller should wait).
    /// </summary>
    private bool ApplyNinjaHideGate()
    {
        if (!config.UseNinjaHideOnDangerousRoutes)
        {
            ninjaHideRequired = false;
            return true;
        }

        UpdateNinjaHideRequired();

        if (!ninjaHideRequired)
        {
            ninjaHide.RestorePreviousGearsetIfNeeded();
            return true;
        }

        if (ninjaHide.EnsureReady(config.NinjaGearsetNumber))
        {
            return true;
        }

        // Stand still to cast Hide / swap gear; keep pathing while still mounted toward the threat.
        if (!ninjaHide.IsMounted)
        {
            vnav.Stop();
        }

        return false;
    }

    private void UpdateNinjaHideRequired()
    {
        if (KnowledgeThreat.TryFindIsleblazer(
                objects,
                player.Position,
                KnowledgeThreat.IsleblazerUnhideDistance,
                out _))
        {
            ninjaHideRequired = false;
            return;
        }

        if (KnowledgeThreat.TryGetPlayerForayLevel(objects) is not int foray)
        {
            ninjaHideRequired = false;
            return;
        }

        int hideAt = KnowledgeThreat.HideAtOrAbove(foray, config.KnowledgeHideOffset);
        float enter = config.KnowledgeThreatEnterDistance;
        float exit = Math.Max(config.KnowledgeThreatExitDistance, enter);

        if (ninjaHideRequired)
        {
            if (!KnowledgeThreat.TryFindThreat(objects, player.Position, hideAt, exit, out _, out _))
            {
                ninjaHideRequired = false;
            }

            return;
        }

        if (KnowledgeThreat.TryFindThreat(objects, player.Position, hideAt, enter, out _, out _))
        {
            ninjaHideRequired = true;
        }
    }

    private IGameObject? FindTreasureNear(Vector3 layoutDestination, float radius)
    {
        return objects
            .Where(o => o is { ObjectKind: ObjectKind.Treasure, IsDead: false }
                        && o.IsValid()
                        && layoutDestination.Distance2D(o.Position) <= radius)
            .OrderBy(o => layoutDestination.Distance2D(o.Position))
            .FirstOrDefault();
    }

    /// <summary>
    /// Live coffer for a layout node, including drifted spawns — only if this layout is nearest to that object.
    /// </summary>
    private IGameObject? FindTreasureForLayout(Vector3 layoutDestination, uint nodeId)
    {
        IGameObject? close = FindTreasureNear(layoutDestination, ChestSearchRadius);
        if (close != null)
        {
            return close;
        }

        IGameObject? drifted = FindTreasureNear(layoutDestination, ChestDriftSearchRadius);
        if (drifted == null)
        {
            return null;
        }

        TreasureLayoutDatum nearest = layoutTreasure
            .OrderBy(t => t.Position.Distance2D(drifted.Position))
            .FirstOrDefault();
        return nearest.Id == nodeId ? drifted : null;
    }

    private bool IsUnsafeTreasureWindow()
    {
        TreasureRoutePolicy policy = zones.GetZone().GetTreasureRoutePolicy();
        int eorzeaMinute = TreasureRoutePolicy.GetEorzeaMinuteOfDay(DateTimeOffset.UtcNow);
        if (policy.IsAshkinPeriod(eorzeaMinute))
        {
            return true;
        }

        byte weatherId = GetCurrentWeatherId();
        return weatherId != 0 && policy.IsUnsafeWeather(weatherId);
    }

    private static unsafe byte GetCurrentWeatherId()
    {
        FFXIVClientStructs.FFXIV.Client.Graphics.Environment.EnvManager* env =
            FFXIVClientStructs.FFXIV.Client.Graphics.Environment.EnvManager.Instance();
        return env == null ? (byte)0 : env->ActiveWeather;
    }

    private List<uint> GetValidNodes(int maxLevel)
    {
        List<TreasureData> treasureData = zones.GetZone().GetTreasureData();
        if (treasureData.Exists(d => d.Position.HasValue))
        {
            return layoutTreasure
                .Where(t => treasureData.Any(d => d.Level <= maxLevel && d.Matches(t.Id, t.Position)))
                .Select(t => t.Id)
                .ToList();
        }

        return treasureData
            .Where(node => node.Level <= maxLevel)
            .Select(node => (uint)node.Id)
            .ToList();
    }

    private List<uint> GetValidNodesForNextPlan()
    {
        int maxLevel = maxLevelOverrideForNextRun ?? config.HuntMaxLevel;
        List<uint> validNodes = GetValidNodes(maxLevel)
            .Where(id => !excludedNodeIdsForNextRun.Contains(id))
            .Where(id => !checkedNodeIds.Contains(id))
            .Where(id => !IsLayoutCofferOpened(id))
            .ToList();

        if (validNodes.Count > 0 || excludedNodeIdsForNextRun.Count == 0)
        {
            return validNodes;
        }

        log.Info("Pots & Treasure visited every known treasure node; starting a fresh treasure route.");
        excludedNodeIdsForNextRun.Clear();
        return GetValidNodes(maxLevel)
            .Where(id => !checkedNodeIds.Contains(id))
            .Where(id => !IsLayoutCofferOpened(id))
            .ToList();
    }

    /// <summary>True when a live opened/looted coffer sits on this layout node (skip when resuming).</summary>
    private bool IsLayoutCofferOpened(uint nodeId)
    {
        TreasureLayoutDatum layout = layoutTreasure.FirstOrDefault(t => t.Id == nodeId);
        if (layout.Id != nodeId)
        {
            return false;
        }

        IGameObject? present = FindTreasureForLayout(layout.Position, nodeId);
        return present != null && OpenTreasureCofferChain.IsOpenedOrLooted(present);
    }

    private TreasureHuntPathfinder? CreatePathPlanner()
    {
        layoutTreasure.Clear();

        unsafe
        {
            LayoutManager* layout = LayoutWorld.Instance()->ActiveLayout;
            if (layout == null)
            {
                log.Warning("No active layout for treasure hunt");
                return null;
            }

            if (!layout->InstancesByType.TryGetValue(InstanceType.Treasure, out Pointer<StdMap<ulong, Pointer<ILayoutInstance>>> mapPtr, false))
            {
                log.Warning("No active treasure layout instances");
                return null;
            }

            List<TreasureData> treasureData = zones.GetZone().GetTreasureData();
            bool hasPositionData = treasureData.Exists(d => d.Position.HasValue);

            foreach(ILayoutInstance* instance in mapPtr.Value->Values)
            {
                Transform* transform = instance->GetTransformImpl();
                Vector3 position = transform->Translation;
                if (position.Y <= -10f && !hasPositionData)
                {
                    continue;
                }

                uint treasureRowId = Unsafe.Read<uint>((byte*)instance + 0x30);
                uint sgbId = data.GetExcelSheet<TreasureSheet>().GetRow(treasureRowId).SGB.RowId;
                if (sgbId != 1596 && sgbId != 1597)
                {
                    continue;
                }

                if (hasPositionData && !treasureData.Any(d => d.Matches(treasureRowId, position)))
                {
                    continue;
                }

                layoutTreasure.Add(new(treasureRowId, position, sgbId));
            }
        }

        if (layoutTreasure.Count == 0)
        {
            log.Warning("No treasure layout nodes found for hunt");
            return null;
        }

        layoutTreasure.Sort((a, b) => a.Id.CompareTo(b.Id));

        IZone zone = zones.GetZone();
        return new(
            zone.ZoneId,
            plugin,
            layoutTreasure,
            log
        );
    }

    private bool ShouldReturnAfterHunt()
    {
        if (!config.ReturnToBaseCampAfterHunt)
        {
            return false;
        }

        if (zones.GetZone().IsInBasecamp())
        {
            return false;
        }

        // Already appended the epilogue Return for this run.
        return steps.Count == 0 || steps[^1].Type != HuntPathfinderStepType.ReturnToBaseCamp;
    }

    private void EnsureWalkVias(HuntPathfinderStep step)
    {
        if (walkViaStepIndex == StepIndex)
        {
            return;
        }

        walkViaStepIndex = StepIndex;
        walkViaIndex = 0;
        walkVias.Clear();

        ZoneId zoneId = zones.GetZone().ZoneId;

        // Leave previous coffer through its safe exit before heading to the next.
        for (int i = StepIndex - 1; i >= 0; i--)
        {
            HuntPathfinderStep prev = steps[i];
            if (prev.Type != HuntPathfinderStepType.WalkToNode)
            {
                continue;
            }

            if (TreasureHuntPathOverrides.TryGetDeparture(zoneId, prev.NodeId, out IReadOnlyList<Vector3> departure))
            {
                walkVias.AddRange(departure);
            }

            break;
        }

        if (TreasureHuntPathOverrides.TryGetApproach(zoneId, step.NodeId, out IReadOnlyList<Vector3> approach))
        {
            walkVias.AddRange(approach);
        }

        // Skip vias we are already on (e.g. resumed mid-route next to the safe spot).
        while (walkViaIndex < walkVias.Count
               && player.Position.Distance2D(walkVias[walkViaIndex]) <= 3f)
        {
            walkViaIndex++;
        }

        if (walkVias.Count > 0)
        {
            log.Info(
                "Treasure hunt: {Count} via(s) for node {NodeId} (index {Index})",
                walkVias.Count,
                step.NodeId,
                walkViaIndex);
        }
    }

    private AethernetData ResolveAethernet(HuntAethernet aethernet)
    {
        uint placeNameId = (uint)aethernet;
        return zones.GetZone().GetAetherytes().First(a => a.Id == placeNameId);
    }

    private void CompleteHunt()
    {
        CaptureCompletedRun();
        PlayHuntCompleteSound();
        Teardown();
    }

    private void CaptureCompletedRun()
    {
        if (!ManagedByPotsTreasure)
        {
            return;
        }

        lastCompletedRunNodeIds.Clear();
        foreach (uint nodeId in checkedNodeIds)
        {
            lastCompletedRunNodeIds.Add(nodeId);
        }
    }

    private void PlayHuntCompleteSound()
    {
        if (!config.PlaySoundOnHuntComplete)
        {
            return;
        }

        sounds.Play(config.HuntCompleteSound);
    }

    private void Teardown()
    {
        bool wasManagedByPotsTreasure = ManagedByPotsTreasure;
        bool wasStandalone = Running && !wasManagedByPotsTreasure && !ManagedByIllegalModeFiller;
        bool wasIllegalFiller = ManagedByIllegalModeFiller;

        Running = false;
        Paused = false;
        planningRoute = false;
        pendingStartSight = false;
        waitingForSightCounts = false;
        sightCastUtc = DateTime.MinValue;
        locationsSinceLastSight = 0;
        ninjaHideRequired = false;
        ninjaHide.RestorePreviousGearsetIfNeeded();
        walkViaStepIndex = -1;
        walkViaIndex = 0;
        walkVias.Clear();

        SoftStopMovement();

        stopwatch.Stop();
        StepIndex = 0;
        StepDistance = 0f;
        LastCheckedNodeId = null;
        ManagedByPotsTreasure = false;
        ManagedByIllegalModeFiller = false;
        checkedNodeIds.Clear();
        excludedNodeIdsForNextRun.Clear();
        maxLevelOverrideForNextRun = null;
        if (!wasManagedByPotsTreasure)
        {
            lastCompletedRunNodeIds.Clear();
        }

        layoutTreasure.Clear();
        pathPlanner = null;

        if (wasStandalone || wasIllegalFiller)
        {
            modeGuard.NotifyTreasureHuntEnded();
        }
    }
}
