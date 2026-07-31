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
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
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
    IPluginLog log
) : ITreasureHunter, IOnUpdate
{
    private const float ChestSearchRadius = 5f;
    private readonly List<TreasureLayoutDatum> layoutTreasure = [];
    private readonly List<HuntPathfinderStep> steps = [];

    private readonly Stopwatch stopwatch = new();
    private Task<ChainResult>? activeChain;

    private IHuntRoutePlanner? pathPlanner;
    private bool planningRoute;

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

        if (!IsVnavReady)
        {
            return;
        }

        if (activeChain is { IsCompleted: false })
        {
            return;
        }

        if (activeChain is { IsCompleted: true })
        {
            if (steps.Count > 0
                && StepIndex < steps.Count
                && TryAdvanceCurrentStep())
            {
                StepIndex++;
                StepDistance = 0f;
            }

            return;
        }

        TryInteractWithNearbyChest();

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

        if (steps.Count == 0)
        {
            return;
        }

        if (StepIndex >= steps.Count)
        {
            Teardown();
            return;
        }

        if (TryAdvanceCurrentStep())
        {
            StepIndex++;
            StepDistance = 0f;
        }
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

        Vector3 destination = layoutTreasure.First(t => t.Id == step.NodeId).Position;

        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveTo(destination, false);
        }

        MaybeMount(destination);

        StepDistance = player.Position.Distance(destination);
        if (StepDistance > config.HuntDetectionRange)
        {
            return false;
        }

        IGameObject? chest = GetValidChests()
            .FirstOrDefault(o => Vector3.Distance(destination, o.Position) <= ChestSearchRadius);

        if (IsChestComplete(destination))
        {
            vnav.Stop();
            return true;
        }

        if (chest == null)
        {
            vnav.Stop();
            return true;
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

    private bool IsChestComplete(Vector3 destination)
    {
        IGameObject? chest = GetValidChests()
            .FirstOrDefault(o => Vector3.Distance(destination, o.Position) <= ChestSearchRadius);

        if (chest == null)
        {
            return true;
        }

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
                    _ => ValueTask.FromResult(zone.IsInBasecamp()),
                    TimeSpan.FromSeconds(120),
                    TimeSpan.FromMilliseconds(500),
                    "TreasureHunt::WaitForBasecamp"
                )
        );

        return false;
    }

    private bool HandleWalkToAethernet(HuntPathfinderStep step)
    {
        if (!Running)
        {
            vnav.Stop();
            return true;
        }

        Vector3 destination = ResolveAethernet(step.Aethernet).Position;

        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveTo(destination, false);
        }

        MaybeMount(destination);

        StepDistance = player.Position.Distance(destination);
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

            bool teleported = activeChain.IsCompletedSuccessfully;
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
        if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            return;
        }

        if (player.Position.Distance(destination) > 50f)
        {
            Actions.MountRoulette.Cast();
        }
    }

    private IEnumerable<IGameObject> GetValidChests()
    {
        return objects.Where(o => o is
        {
            ObjectKind: ObjectKind.Treasure,
            IsDead: false,
            IsTargetable: true
        } && o.IsValid());
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
