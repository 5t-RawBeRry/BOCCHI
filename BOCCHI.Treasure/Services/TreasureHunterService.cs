using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
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
    IGameGui gui
) : ITreasureHunter, IOnUpdate, IOnStop
{
    private const float ChestSearchRadius = 25f;

    /// <summary>
    /// Only treat a node as empty once we're essentially on top of the layout point.
    /// A larger radius (e.g. 10y) skipped chests while still outside interact range (#93).
    /// </summary>
    private const float EmptySkipRadius = 2f;
    private readonly List<TreasureLayoutDatum> layoutTreasure = [];
    private readonly List<HuntPathfinderStep> steps = [];

    private readonly Stopwatch stopwatch = new();
    private Task<ChainResult>? activeChain;

    private IHuntRoutePlanner? pathPlanner;
    private bool planningRoute;

    public void OnStop() => Teardown();

    public void Update()
    {
        if (!Running || Paused)
        {
            return;
        }

        if (!config.EnableTreasureHunt)
        {
            Teardown();
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
            List<uint> validNodes = GetValidNodes(config.HuntMaxLevel);
            steps.Clear();
            steps.AddRange(pathPlanner.FindPath(player.Position, validNodes).GetAwaiter().GetResult());
            pathPlanner = null;
            StepIndex = 0;
            return;
        }

        if (steps.Count > 0 && StepIndex >= steps.Count)
        {
            if (ShouldReturnAfterHunt())
            {
                steps.Add(HuntPathfinderStep.ReturnToBaseCamp());
                return;
            }

            Teardown();
            return;
        }

        // Step handlers (teleport/return) must see completed chains before we clear them.
        if (steps.Count > 0 && StepIndex < steps.Count && TryAdvanceCurrentStep())
        {
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

    public bool IsVnavAvailable => vnav.IsAvailable();

    public bool IsVnavReady => vnav.IsNavmeshReady();

    public void Toggle()
    {
        if (Running)
        {
            StopHunt();
            return;
        }

        if (!config.EnableTreasureHunt)
        {
            return;
        }

        stopwatch.Restart();
        StepIndex = 0;
        Paused = false;
        steps.Clear();
        layoutTreasure.Clear();
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

    private void StopHunt()
    {
        Teardown();
    }

    /// <summary>Stop movement/chains without clearing the planned route.</summary>
    private void SoftStopMovement()
    {
        chainManager.CancelWhere(name => name.StartsWith("TreasureHunt", StringComparison.Ordinal));
        pathfinder.Stop();
        vnav.Stop();
        activeChain = null;
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

        if (!vnav.IsRunning() && dist2d > OpenTreasureCofferChain.PreferredOpenDistance)
        {
            vnav.PathfindAndMoveCloseTo(destination, false, OpenTreasureCofferChain.PathArrivalRange);
        }

        MaybeMount(destination);

        StepDistance = dist2d;
        if (StepDistance > config.HuntDetectionRange)
        {
            return false;
        }

        if (present != null && IsChestOpened(present))
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

        if (StepDistance > OpenTreasureCofferChain.MaxInteractRange)
        {
            return false;
        }

        if (vnav.IsRunning() && StepDistance > OpenTreasureCofferChain.PreferredOpenDistance)
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

    private bool IsChestOpened(IGameObject chest)
    {
        unsafe
        {
            GameObject* gameObject = (GameObject*)(void*)chest.Address;
            FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure* instance = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;
            return instance->Flags.HasFlag(FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags.Opened);
        }
    }

    private bool HandleReturnToBaseCamp()
    {
        StepDistance = 0f;
        IZone zone = zones.GetZone();
        bool inCombat = conditions[ConditionFlag.InCombat];

        if (inCombat && !vnav.IsRunning())
        {
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

    private IGameObject? FindTreasureNear(Vector3 layoutDestination, float radius)
    {
        return objects
            .Where(o => o is { ObjectKind: ObjectKind.Treasure, IsDead: false }
                        && o.IsValid()
                        && IsAllowedCofferBaseId(o.BaseId)
                        && layoutDestination.Distance2D(o.Position) <= radius)
            .OrderBy(o => layoutDestination.Distance2D(o.Position))
            .FirstOrDefault();
    }

    private bool IsAllowedCofferBaseId(uint baseId)
    {
        if (!config.RestrictCofferBaseIds)
        {
            return true;
        }

        return TreasureRoutePolicy.CofferBaseIds.Contains(baseId);
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
            log,
            config.HuntReturnCost,
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
        return steps[^1].Type != HuntPathfinderStepType.ReturnToBaseCamp;
    }

    private AethernetData ResolveAethernet(HuntAethernet aethernet)
    {
        uint placeNameId = (uint)aethernet;
        return zones.GetZone().GetAetherytes().First(a => a.Id == placeNameId);
    }

    private void Teardown()
    {
        Running = false;
        Paused = false;
        planningRoute = false;

        SoftStopMovement();

        stopwatch.Stop();
        StepIndex = 0;
        StepDistance = 0f;
        steps.Clear();
        layoutTreasure.Clear();
        pathPlanner = null;
    }
}
