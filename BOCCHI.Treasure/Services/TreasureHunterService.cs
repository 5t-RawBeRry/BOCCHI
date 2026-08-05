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
    CofferObservationCatalogService cofferCatalog,
    NinjaHideAssist ninjaHide
) : ITreasureHunter, IOnUpdate, IOnStop
{
    private const float ChestSearchRadius = 25f;

    /// <summary>Match layout coffers to crowdsourced centroids (API cluster radius is 1.5).</summary>
    private const float CrowdsourcedMatchRadius = 3.5f;

    private const float CrowdsourcedMatchRadiusSq = CrowdsourcedMatchRadius * CrowdsourcedMatchRadius;

    /// <summary>
    /// Only treat a node as empty once we're essentially on top of the layout point.
    /// A larger radius (e.g. 10y) skipped chests while still outside interact range (#93).
    /// </summary>
    private const float EmptySkipRadius = 2f;

    /// <summary>How long to wait for WideText after casting Treasure Sight.</summary>
    private static readonly TimeSpan SightCountWait = TimeSpan.FromSeconds(8);

    private readonly List<TreasureLayoutDatum> layoutTreasure = [];
    private readonly List<HuntPathfinderStep> steps = [];

    private readonly Stopwatch stopwatch = new();
    private Task<ChainResult>? activeChain;

    private IHuntRoutePlanner? pathPlanner;
    private bool planningRoute;
    private bool usingCrowdsourcedRoute;
    private bool pendingStartSight;
    private bool waitingForSightCounts;
    private DateTime sightCastUtc = DateTime.MinValue;
    private int locationsSinceLastSight;

    /// <summary>Hysteresis: Hide required until threats leave exit distance.</summary>
    private bool ninjaHideRequired;

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
            List<uint> validNodes = GetValidNodes(config.HuntMaxLevel)
                .Where(id => !IsLayoutCofferOpened(id))
                .ToList();
            steps.Clear();
            steps.AddRange(pathPlanner.FindPath(player.Position, validNodes).GetAwaiter().GetResult());
            pathPlanner = null;
            StepIndex = 0;
            pendingStartSight = config.CastTreasureSightDuringHunt && CanCastTreasureSight();
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

        if (steps.Count > 0 && StepIndex >= steps.Count)
        {
            if (ShouldReturnAfterHunt())
            {
                steps.Add(HuntPathfinderStep.ReturnToBaseCamp());
                return;
            }

            CompleteHunt();
            return;
        }

        // Teleport/return handlers must observe completed chains before we clear them.
        // Clearing first re-starts the same teleport forever (#123 / #125).
        if (steps.Count > 0 && StepIndex < steps.Count && TryAdvanceCurrentStep())
        {
            HuntPathfinderStep completed = steps[StepIndex];
            if (completed.Type == HuntPathfinderStepType.WalkToNode)
            {
                LastCheckedNodeId = completed.NodeId;
                locationsSinceLastSight++;
            }

            StepIndex++;
            StepDistance = 0f;
        }

        if (activeChain is { IsCompleted: true })
        {
            activeChain = null;
        }
    }

    public bool Running { get; private set; }

    public bool Paused { get; private set; }

    public int StepIndex { get; private set; }

    public int StepCount => steps.Count;

    public float StepDistance { get; private set; }

    public TimeSpan Elapsed => stopwatch.Elapsed;

    public uint? LastCheckedNodeId { get; private set; }

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

        stopwatch.Restart();
        StepIndex = 0;
        LastCheckedNodeId = null;
        ManagedByPotsTreasure = false;
        ManagedByIllegalModeFiller = false;
        Paused = false;
        steps.Clear();
        layoutTreasure.Clear();
        pendingStartSight = false;
        waitingForSightCounts = false;
        sightCastUtc = DateTime.MinValue;
        locationsSinceLastSight = 0;
        usingCrowdsourcedRoute = false;
        cofferCatalog.EnsureFreshForHunt();
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
    }

    private bool CanCastTreasureSight() => SupportJobTreasureSight.CanCast(supportJobs);

    private bool TryBeginTreasureSight()
    {
        if (!config.CastTreasureSightDuringHunt || !CanCastTreasureSight())
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

        // Defer while fighting — Sight dismounts + swaps PJ; remount fails in combat (#128).
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

        return false;
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

        Vector3 layoutDestination = layoutTreasure.First(t => t.Id == step.NodeId).Position;

        // Presence: don't require IsTargetable (often false until inside interact range).
        IGameObject? present = FindTreasureNear(layoutDestination, ChestSearchRadius);

        // Use layout while far; live position only when close (avoids repath jitter).
        Vector3 destination = layoutDestination;
        float distToLayout = player.Position.Distance2D(layoutDestination);
        if (present != null && distToLayout <= OpenTreasureCofferChain.MaxInteractRange * 2f)
        {
            destination = present.Position;
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

        if (StepDistance > config.HuntDetectionRange)
        {
            return false;
        }

        if (present != null && OpenTreasureCofferChain.IsOpenedOrLooted(present))
        {
            vnav.Stop();
            return true;
        }

        // Empty / unspawned: only skip once we're on the layout point with no live coffer nearby.
        if (present == null)
        {
            if (StepDistance <= EmptySkipRadius && !vnav.IsRunning())
            {
                vnav.Stop();
                return true;
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
        activeChain = chainManager.Manage(
            chains.Create($"TreasureHunt::Open({step.NodeId})")
                .Then<OpenTreasureCofferChain, Vector3>(present.Position)
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
            SprintAssist.MaybeCast(automatorConfig.SprintOnAetheryteApproach);
            vnav.PathfindAndMoveTo(zone.GetMainAetheryte().GetInteractPosition(), false);
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

        Vector3 crystal = ResolveAethernet(step.Aethernet).Position;
        StepDistance = player.Position.Distance2D(crystal);

        if (StepDistance <= AethernetData.LifestreamInteractRadius)
        {
            vnav.Stop();
            return true;
        }

        // Keep stand-off + arrival inside LifestreamInteractRadius (#113).
        float standOff;
        float arrival;
        if (StepDistance <= AethernetData.LifestreamInteractRadius + AethernetNavigation.PathfindArrivalRadius + 0.5f)
        {
            standOff = 0.75f;
            arrival = 0.5f;
        }
        else
        {
            standOff = Math.Min(
                AethernetNavigation.CampApproachRadius,
                AethernetData.LifestreamInteractRadius - AethernetNavigation.PathfindArrivalRadius - 0.25f);
            arrival = AethernetNavigation.PathfindArrivalRadius;
        }

        Vector3 destination = crystal.GetApproachPosition(player.Position, standOff);
        destination = new Vector3(destination.X, crystal.Y, destination.Z);

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
                .Then<HuntTeleportChain, uint>(placeNameId)
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

        if (player.Position.Distance(destination) > NavigationConstants.MountMinDistance)
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
        if (usingCrowdsourcedRoute)
        {
            // Layout list already matched crowdsourced centroids; level-gate via authored when known.
            return layoutTreasure
                .Where(t =>
                {
                    TreasureData? authored = treasureData.FirstOrDefault(d => d.Matches(t.Id, t.Position));
                    return authored == null || authored.Level <= maxLevel;
                })
                .Select(t => t.Id)
                .ToList();
        }

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

    /// <summary>True when a live opened/looted coffer sits on this layout node (skip when resuming).</summary>
    private bool IsLayoutCofferOpened(uint nodeId)
    {
        TreasureLayoutDatum layout = layoutTreasure.FirstOrDefault(t => t.Id == nodeId);
        if (layout.Id != nodeId)
        {
            return false;
        }

        IGameObject? present = FindTreasureNear(layout.Position, ChestSearchRadius);
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
            IReadOnlyList<CrowdsourcedCofferCandidate> liveSpots = cofferCatalog.GetAcceptedForCurrentZone();
            bool preferCrowdsourced = liveSpots.Count > 0;
            usingCrowdsourcedRoute = false;

            foreach(ILayoutInstance* instance in mapPtr.Value->Values)
            {
                Transform* transform = instance->GetTransformImpl();
                Vector3 position = transform->Translation;
                if (position.Y <= -10f && !hasPositionData && !preferCrowdsourced)
                {
                    continue;
                }

                uint treasureRowId = Unsafe.Read<uint>((byte*)instance + 0x30);
                uint sgbId = data.GetExcelSheet<TreasureSheet>().GetRow(treasureRowId).SGB.RowId;
                if (sgbId != 1596 && sgbId != 1597)
                {
                    continue;
                }

                if (preferCrowdsourced)
                {
                    if (!liveSpots.Any(c => Vector3.DistanceSquared(c.Position, position) <= CrowdsourcedMatchRadiusSq))
                    {
                        continue;
                    }
                }
                else if (hasPositionData && !treasureData.Any(d => d.Matches(treasureRowId, position)))
                {
                    continue;
                }

                layoutTreasure.Add(new(treasureRowId, position, sgbId));
            }

            if (preferCrowdsourced && layoutTreasure.Count == 0)
            {
                log.Info(
                    "Crowdsourced catalog has {Count} spot(s) but none matched layout — falling back to authored map",
                    liveSpots.Count);
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
            else if (preferCrowdsourced)
            {
                usingCrowdsourcedRoute = true;
                log.Info("Treasure hunt using {Count} crowdsourced layout match(es)", layoutTreasure.Count);
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
            zone.GetMainAetheryte().GetInteractPosition(),
            log,
            config.HuntTeleportCost
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

    private AethernetData ResolveAethernet(HuntAethernet aethernet)
    {
        uint placeNameId = (uint)aethernet;
        return zones.GetZone().GetAetherytes().First(a => a.Id == placeNameId);
    }

    private void CompleteHunt()
    {
        PlayHuntCompleteSound();
        Teardown();
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
        bool wasStandalone = Running && !ManagedByPotsTreasure && !ManagedByIllegalModeFiller;
        bool wasIllegalFiller = ManagedByIllegalModeFiller;

        Running = false;
        Paused = false;
        planningRoute = false;
        pendingStartSight = false;
        waitingForSightCounts = false;
        sightCastUtc = DateTime.MinValue;
        locationsSinceLastSight = 0;
        ninjaHideRequired = false;

        SoftStopMovement();

        stopwatch.Stop();
        StepIndex = 0;
        StepDistance = 0f;
        LastCheckedNodeId = null;
        ManagedByPotsTreasure = false;
        ManagedByIllegalModeFiller = false;
        layoutTreasure.Clear();
        pathPlanner = null;
        usingCrowdsourcedRoute = false;

        if (wasStandalone)
        {
            modeGuard.NotifyStandaloneTreasureHuntEnded();
        }
        else if (wasIllegalFiller)
        {
            modeGuard.NotifyIllegalModeFillerHuntEnded();
        }
    }
}
