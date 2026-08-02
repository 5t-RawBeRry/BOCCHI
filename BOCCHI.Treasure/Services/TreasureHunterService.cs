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
    private const float ChestSearchRadius = 5f;
    private readonly List<TreasureLayoutDatum> layoutTreasure = [];
    private readonly List<HuntPathfinderStep> steps = [];

    private readonly Stopwatch stopwatch = new();
    private Task<ChainResult>? activeChain;

    private IHuntRoutePlanner? pathPlanner;
    private bool planningRoute;

    public void OnStop() => Teardown();

    public void Update()
    {
        if (!Running)
        {
            return;
        }

        if (!config.EnableTreasureHunt)
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

        TryInteractWithNearbyChest();
    }

    public bool Running { get; private set; }

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

    private void TryInteractWithNearbyChest()
    {
        if (activeChain != null)
        {
            return;
        }

        IGameObject? nearby = GetValidChests()
            .FirstOrDefault(o => player.Position.Distance(o.Position) <= ChestSearchRadius);

        if (nearby == null)
        {
            return;
        }

        activeChain = chainManager.Manage(
            chains.Create("TreasureHunt::NearbyInteract")
                .Then<OpenTreasureCofferChain, Vector3>(nearby.Position)
        );
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

        IGameObject? chest = GetValidChests()
            .FirstOrDefault(o => Vector3.Distance(layoutDestination, o.Position) <= ChestSearchRadius);

        // Prefer the live object once it exists — layout coords are often slightly off.
        Vector3 destination = chest?.Position ?? layoutDestination;

        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveTo(destination, false);
        }
        else if (chest != null)
        {
            // Re-aim at the live chest if we were still pathing to layout.
            float toLive = player.Position.Distance(chest.Position);
            if (toLive > OpenTreasureCofferChain.InteractDistance
                && player.Position.Distance(layoutDestination) <= ChestSearchRadius)
            {
                vnav.PathfindAndMoveTo(chest.Position, false);
            }
        }

        MaybeMount(destination);

        StepDistance = player.Position.Distance(destination);
        if (StepDistance > config.HuntDetectionRange)
        {
            return false;
        }

        if (chest != null && IsChestOpened(chest))
        {
            vnav.Stop();
            return true;
        }

        // Only treat as empty when we're close enough that the object should have streamed in.
        // Skipping at HuntDetectionRange (default 75y) caused "runs past" when radar already saw the chest.
        if (chest == null)
        {
            if (StepDistance <= ChestSearchRadius)
            {
                vnav.Stop();
                return true;
            }

            return false;
        }

        if (StepDistance > OpenTreasureCofferChain.InteractDistance)
        {
            return false;
        }

        activeChain = chainManager.Manage(
            chains.Create($"TreasureHunt::Open({step.NodeId})")
                .Then<OpenTreasureCofferChain, Vector3>(chest.Position)
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

        // Same AtkValues[7] filter as pre-rewrite — Return only.
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
        Vector3 destination = crystal.GetApproachPosition(player.Position, AethernetNavigation.CampApproachRadius);
        destination = new Vector3(destination.X, crystal.Y, destination.Z);

        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveCloseTo(destination, false, AethernetNavigation.PathfindArrivalRadius);
        }

        MaybeMount(destination);

        StepDistance = player.Position.Distance2D(crystal);
        return StepDistance <= AethernetData.LifestreamInteractRadius;
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

    private IEnumerable<IGameObject> GetValidChests()
    {
        return objects.Where(o => o is
        {
            ObjectKind: ObjectKind.Treasure,
            IsDead: false,
            IsTargetable: true
        } && o.IsValid() && IsAllowedCofferBaseId(o.BaseId));
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

    private AethernetData ResolveAethernet(HuntAethernet aethernet)
    {
        uint placeNameId = (uint)aethernet;
        return zones.GetZone().GetAetherytes().First(a => a.Id == placeNameId);
    }

    private void Teardown()
    {
        Running = false;
        planningRoute = false;

        chainManager.CancelWhere(name => name.StartsWith("TreasureHunt", StringComparison.Ordinal));

        pathfinder.Stop();
        vnav.Stop();

        activeChain = null;

        stopwatch.Stop();
        StepIndex = 0;
        StepDistance = 0f;
        steps.Clear();
        layoutTreasure.Clear();
        pathPlanner = null;
    }
}
