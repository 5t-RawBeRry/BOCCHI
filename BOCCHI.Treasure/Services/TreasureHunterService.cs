using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Treasure.ChainRecipes;
using BOCCHI.Treasure.Hunt;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using Lumina.Excel.Sheets;
using TreasureSheet = Lumina.Excel.Sheets.Treasure;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Lifecycle;
using Ocelot.Services.PlayerState;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;

namespace BOCCHI.Treasure.Services;

public class TreasureHunterService(
    TreasureConfig config,
    IZoneProvider zones,
    IVNavmeshIpc vnav,
    IChainFactory chains,
    IChainManager chainManager,
    IObjectTable objects,
    ICondition conditions,
    IPlayer player,
    IDataManager data,
    IPluginLog log
) : ITreasureHunter, IOnUpdate
{
    private const float ChestSearchRadius = 5f;

    private readonly Stopwatch stopwatch = new();
    private readonly List<TreasureLayoutDatum> layoutTreasure = [];
    private readonly List<HuntPathfinderStep> steps = [];

    private IHuntRoutePlanner? pathPlanner;
    private Task<ChainResult>? activeChain;
    private int stepIndex;
    private float stepDistance;
    private bool running;
    private bool planningRoute;

    public bool Running => running;

    public int StepIndex => stepIndex;

    public int StepCount => steps.Count;

    public float StepDistance => stepDistance;

    public TimeSpan Elapsed => stopwatch.Elapsed;

    public bool IsVnavReady => vnav.IsReady();

    public void Toggle()
    {
        if (!config.EnableTreasureHunt)
        {
            return;
        }

        running = !running;
        if (!running)
        {
            Teardown();
            return;
        }

        stopwatch.Restart();
        stepIndex = 0;
        steps.Clear();
        layoutTreasure.Clear();
        pathPlanner = CreatePathPlanner();
        if (pathPlanner == null || pathPlanner.State != HuntPathfinderState.FileLoaded)
        {
            log.Error("Failed to initialize treasure hunt path data");
            Teardown();
            return;
        }

        planningRoute = true;
    }

    public HuntPathfinderStep? GetCurrentStep()
    {
        if (stepIndex < 0 || stepIndex >= steps.Count)
        {
            return null;
        }

        return steps[stepIndex];
    }

    public void Update()
    {
        if (!running || !config.EnableTreasureHunt)
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

        activeChain = null;

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
            var validNodes = GetValidNodes(config.HuntMaxLevel);
            steps.Clear();
            steps.AddRange(pathPlanner.FindPath(player.Position, validNodes).GetAwaiter().GetResult());
            pathPlanner = null;
            stepIndex = 0;
            return;
        }

        if (steps.Count == 0)
        {
            return;
        }

        if (stepIndex >= steps.Count)
        {
            Teardown();
            return;
        }

        if (TryAdvanceCurrentStep())
        {
            stepIndex++;
            stepDistance = 0f;
        }
    }

    private void TryInteractWithNearbyChest()
    {
        if (activeChain != null)
        {
            return;
        }

        var nearby = GetValidChests()
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
        var step = steps[stepIndex];
        return step.Type switch
        {
            HuntPathfinderStepType.WalkToNode => HandleWalkToNode(step),
            HuntPathfinderStepType.ReturnToBaseCamp => HandleReturnToBaseCamp(),
            HuntPathfinderStepType.WalkToAethernet => HandleWalkToAethernet(step),
            HuntPathfinderStepType.TeleportToAethernet => HandleTeleportToAethernet(step),
            _ => true,
        };
    }

    private bool HandleWalkToNode(HuntPathfinderStep step)
    {
        var destination = layoutTreasure.First(t => t.Id == step.NodeId).Position;

        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveTo(destination, false);
        }

        MaybeMount(destination);

        stepDistance = player.Position.Distance(destination);
        if (stepDistance > config.HuntDetectionRange)
        {
            return false;
        }

        var chest = GetValidChests()
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

        if (stepDistance > OpenTreasureCofferChain.InteractDistance)
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
        var chest = GetValidChests()
            .FirstOrDefault(o => Vector3.Distance(destination, o.Position) <= ChestSearchRadius);

        if (chest == null)
        {
            return true;
        }

        unsafe
        {
            var gameObject = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(void*)chest.Address;
            var instance = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;
            return instance->Flags.HasFlag(FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags.Opened);
        }
    }

    private bool HandleReturnToBaseCamp()
    {
        stepDistance = 0f;
        var zone = zones.GetZone();
        var inCombat = conditions[ConditionFlag.InCombat];

        if (inCombat && !vnav.IsRunning())
        {
            vnav.PathfindAndMoveTo(zone.GetMainAetheryte().Position, false);
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

        _ = chainManager.Manage(
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

        return true;
    }

    private bool HandleWalkToAethernet(HuntPathfinderStep step)
    {
        var destination = ResolveAethernet(step.Aethernet).Position;

        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveTo(destination, false);
        }

        MaybeMount(destination);

        stepDistance = player.Position.Distance(destination);
        return stepDistance <= 4f;
    }

    private bool HandleTeleportToAethernet(HuntPathfinderStep step)
    {
        stepDistance = 0f;
        var placeNameId = (uint)step.Aethernet;

        _ = chainManager.Manage(
            chains.Create($"TreasureHunt::Teleport({placeNameId})")
                .Then<HuntTeleportChain, uint>(placeNameId)
        );

        return true;
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
            IsTargetable: true,
        } && o.IsValid());
    }

    private List<uint> GetValidNodes(int maxLevel)
    {
        return zones.GetZone()
            .GetTreasureData()
            .Where(node => node.Level <= maxLevel)
            .Select(node => (uint)node.Id)
            .ToList();
    }

    private TreasureHuntPathfinder? CreatePathPlanner()
    {
        layoutTreasure.Clear();

        unsafe
        {
            var layout = LayoutWorld.Instance()->ActiveLayout;
            if (layout == null)
            {
                log.Warning("No active layout for treasure hunt");
                return null;
            }

            if (!layout->InstancesByType.TryGetValue(InstanceType.Treasure, out var mapPtr, false))
            {
                log.Warning("No active treasure layout instances");
                return null;
            }

            foreach (ILayoutInstance* instance in mapPtr.Value->Values)
            {
                var transform = instance->GetTransformImpl();
                var position = transform->Translation;
                if (position.Y <= -10f)
                {
                    continue;
                }

                var treasureRowId = Unsafe.Read<uint>((byte*)instance + 0x30);
                var sgbId = data.GetExcelSheet<TreasureSheet>().GetRow(treasureRowId).SGB.RowId;
                if (sgbId != 1596 && sgbId != 1597)
                {
                    continue;
                }

                layoutTreasure.Add(new TreasureLayoutDatum(treasureRowId, position, sgbId));
            }
        }

        if (layoutTreasure.Count == 0)
        {
            log.Warning("No treasure layout nodes found for hunt");
            return null;
        }

        layoutTreasure.Sort((a, b) => a.Id.CompareTo(b.Id));

        var zone = zones.GetZone();
        return new TreasureHuntPathfinder(
            zone.ZoneId,
            layoutTreasure,
            log,
            config.HuntReturnCost,
            config.HuntTeleportCost
        );
    }

    private AethernetData ResolveAethernet(HuntAethernet aethernet)
    {
        var placeNameId = (uint)aethernet;
        return zones.GetZone().GetAetherytes().First(a => a.Id == placeNameId);
    }

    private void Teardown()
    {
        stopwatch.Stop();
        running = false;
        stepIndex = 0;
        stepDistance = 0f;
        steps.Clear();
        layoutTreasure.Clear();
        pathPlanner = null;
        planningRoute = false;
        vnav.Stop();
        chainManager.CancelAll();
        activeChain = null;
    }
}
