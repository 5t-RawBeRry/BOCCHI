using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Data;
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
///     Nearby-only carrot loop: path to a live chewed carrot → Fortune Carrot → open bunny gold chest.
/// </summary>
public sealed class CarrotHunterService
(
    ICarrotTracker carrots,
    FortuneCarrotAssist fortuneCarrot,
    TreasureConfig config,
    UIConfig uiConfig,
    IPlayer player,
    ICondition conditions,
    IObjectTable objects,
    IVNavmeshIpc vnav,
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

    private readonly HashSet<ulong> finishedCarrotIds = [];

    private ulong? currentCarrotId;

    private Vector3 currentCarrotPosition;

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

        if (!config.EnableCarrotHunt)
        {
            BocchiChat.PrintError(chat, uiConfig, "Carrot Hunt is disabled in Treasure settings.");
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

        modeGuard.EnsureExclusive(AutomationMode.CarrotHunt);
        Running = true;
        Phase = CarrotHuntPhase.Idle;
        finishedCarrotIds.Clear();
        ClearCurrent();
        stopwatch.Restart();
        log.Information("Carrot hunt started (nearby mode)");
    }

    public void Update()
    {
        if (!Running)
        {
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
        Carrot? next = FindNextCarrot();
        if (next == null)
        {
            if (vnav.IsRunning())
            {
                vnav.Stop();
            }

            return;
        }

        if (!fortuneCarrot.HasAny())
        {
            BocchiChat.PrintError(chat, uiConfig, "Out of Fortune Carrots — stopping Carrot Hunt.");
            Teardown();
            return;
        }

        currentCarrotId = next.GameObjectId;
        currentCarrotPosition = next.GetPosition();
        itemUseIssued = false;
        Phase = CarrotHuntPhase.Pathing;
        log.Debug("Carrot hunt: pathing to carrot {Id} at {Pos}", next.GameObjectId, currentCarrotPosition);
    }

    private void TickPathing()
    {
        if (!TryGetCurrentCarrot(out Carrot carrot))
        {
            log.Debug("Carrot hunt: target carrot despawned while pathing");
            ClearCurrent();
            Phase = CarrotHuntPhase.Idle;
            vnav.Stop();
            return;
        }

        currentCarrotPosition = carrot.GetPosition();
        float dist = player.Position.Distance(currentCarrotPosition);

        if (dist <= UseThreshold)
        {
            vnav.Stop();
            Phase = CarrotHuntPhase.UsingItem;
            return;
        }

        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveCloseTo(currentCarrotPosition, false, PathArrivalRange);
        }
    }

    private void TickUsingItem()
    {
        if (!TryGetCurrentCarrot(out Carrot carrot))
        {
            ClearCurrent();
            Phase = CarrotHuntPhase.Idle;
            return;
        }

        currentCarrotPosition = carrot.GetPosition();

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

        float dist = player.Position.Distance(currentCarrotPosition);
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
            log.Debug("Carrot hunt: Fortune Carrot used at {Pos}", currentCarrotPosition);
            return;
        }

        // Use already issued but phase not advanced — wait for cast / bunny.
        waitingForBunnySince = DateTime.UtcNow;
        Phase = CarrotHuntPhase.WaitingForBunny;
    }

    private void TickWaitingForBunny()
    {
        if (player.IsCasting() || conditions[ConditionFlag.Casting])
        {
            return;
        }

        IGameObject? bunny = FindBunnyNear(currentCarrotPosition);
        if (bunny != null)
        {
            Phase = CarrotHuntPhase.OpeningBunny;
            return;
        }

        if (DateTime.UtcNow - waitingForBunnySince > BunnySpawnTimeout)
        {
            log.Warning("Carrot hunt: no bunny chest near {Pos} — skipping", currentCarrotPosition);
            if (currentCarrotId is { } id)
            {
                finishedCarrotIds.Add(id);
            }

            ClearCurrent();
            Phase = CarrotHuntPhase.Idle;
        }
    }

    private void TickOpeningBunny()
    {
        IGameObject? bunny = FindBunnyNear(currentCarrotPosition);
        if (bunny == null)
        {
            // Chest gone = opened / despawned.
            CompleteCurrentCarrot();
            return;
        }

        if (player.IsCasting() || conditions[ConditionFlag.Casting] || player.IsInteracting())
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

    private void CompleteCurrentCarrot()
    {
        if (currentCarrotId is { } id)
        {
            finishedCarrotIds.Add(id);
        }

        log.Debug("Carrot hunt: finished carrot near {Pos}", currentCarrotPosition);
        ClearCurrent();
        Phase = CarrotHuntPhase.Idle;
    }

    private Carrot? FindNextCarrot()
    {
        float maxRange = config.CarrotHuntDetectionRange;
        float maxSq = maxRange * maxRange;
        Vector3 origin = player.Position;

        return carrots.Carrots
            .Where(c => c.IsValid() && !finishedCarrotIds.Contains(c.GameObjectId))
            .Where(c => Vector3.DistanceSquared(origin, c.GetPosition()) <= maxSq)
            .OrderBy(c => Vector3.DistanceSquared(origin, c.GetPosition()))
            .FirstOrDefault();
    }

    private bool TryGetCurrentCarrot(out Carrot carrot)
    {
        carrot = null!;
        if (currentCarrotId is not { } id)
        {
            return false;
        }

        Carrot? match = carrots.Carrots.FirstOrDefault(c => c.IsValid() && c.GameObjectId == id);
        if (match == null)
        {
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
        currentCarrotId = null;
        currentCarrotPosition = default;
        itemUseIssued = false;
        waitingForBunnySince = DateTime.MinValue;
    }

    private void Teardown()
    {
        bool wasRunning = Running;
        Running = false;
        Phase = CarrotHuntPhase.Idle;
        ClearCurrent();
        finishedCarrotIds.Clear();
        stopwatch.Stop();
        vnav.Stop();

        if (wasRunning)
        {
            log.Information("Carrot hunt stopped");
        }
    }
}
