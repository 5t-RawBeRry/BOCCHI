using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services.PotTreasure;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.ChainRecipes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;
using System.Numerics;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class FarmingPotChestsHandler
(
    IAutomatorMemory memory,
    IChainFactory chains,
    IChainManager chainManager,
    IPathfinder pathfinder,
    IObjectTable objects,
    ICondition conditions,
    IPlayer player,
    IZoneProvider zones,
    PotTreasureHintTracker hints,
    MagicalElixirAssist elixir,
    ILogger<FarmingPotChestsHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.FarmingPotChests)
{
    private const float ChestSearchRadius = 5f;

    private const float RevealSearchRadius = 28f;

    private const float CenterArrival = 5f;

    private static readonly TimeSpan ChestSpawnWait = TimeSpan.FromSeconds(45);

    private static readonly TimeSpan BuffWaitTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan HintWaitTimeout = TimeSpan.FromSeconds(4);

    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(300);

    private const int MaxElixirAttempts = 3;

    private const int MaxRefineSteps = 6;

    private Task<ChainResult>? activeChain;

    public override StatePriority GetScore()
    {
        if (memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _))
        {
            return StatePriority.Never;
        }

        return memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _)
            ? StatePriority.Normal
            : StatePriority.Never;
    }

    public override void Enter()
    {
        base.Enter();
        chainManager.CancelAll();
        pathfinder.Stop();
        activeChain = null;
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);
        chainManager.CancelAll();
        pathfinder.Stop();
        activeChain = null;
        hints.Disarm();
    }

    public override void Handle()
    {
        if (!memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory farm))
        {
            return;
        }

        if (activeChain is { IsCompleted: false })
        {
            return;
        }

        if (activeChain is { IsCompleted: true }
            && memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory completedFarm)
            && completedFarm.FinishAfterOpen)
        {
            activeChain = null;
            FinishFarm();
            return;
        }

        activeChain = null;

        if (conditions[ConditionFlag.InCombat])
        {
            pathfinder.Stop();
            return;
        }

        if (farm.Mode == PotChestFarmMode.Blind || farm.Phase == PotChestFarmPhase.BlindSweep)
        {
            HandleBlindSweep(farm);
            return;
        }

        switch (farm.Phase)
        {
            case PotChestFarmPhase.WaitingForBuff:
                HandleWaitingForBuff(farm);
                break;
            case PotChestFarmPhase.ApproachCenter:
                HandleApproachCenter(farm);
                break;
            case PotChestFarmPhase.ElixirAtCenter:
                HandleElixirAtCenter(farm);
                break;
            case PotChestFarmPhase.SearchingCandidates:
                HandleSearchingCandidates(farm);
                break;
            case PotChestFarmPhase.OpeningReveal:
                HandleOpeningReveal(farm);
                break;
            default:
                FallBackToBlind(farm);
                break;
        }
    }

    private void HandleWaitingForBuff(PotChestFarmMemory farm)
    {
        if (HasTreasureBuff())
        {
            hints.Arm();
            farm.Phase = PotChestFarmPhase.ApproachCenter;
            farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
            farm.SettledAtUtc = DateTimeOffset.MinValue;
            farm.ElixirAttempts = 0;
            return;
        }

        if (DateTimeOffset.UtcNow - farm.PhaseStartedUtc >= BuffWaitTimeout)
        {
            logger.Info("Pot treasure: no Cache Me If You Can buff — falling back to blind sweep");
            FallBackToBlind(farm);
        }
    }

    private void HandleApproachCenter(PotChestFarmMemory farm)
    {
        if (!HasTreasureBuff())
        {
            FallBackToBlind(farm);
            return;
        }

        float dist = player.Position.Distance2D(farm.FateCenter);
        if (dist > CenterArrival)
        {
            farm.SettledAtUtc = DateTimeOffset.MinValue;
            if (pathfinder.IsIdle())
            {
                pathfinder.PathfindAndMoveTo(new(farm.FateCenter));
            }

            return;
        }

        pathfinder.Stop();
        if (farm.SettledAtUtc == DateTimeOffset.MinValue)
        {
            farm.SettledAtUtc = DateTimeOffset.UtcNow;
            return;
        }

        if (DateTimeOffset.UtcNow - farm.SettledAtUtc < SettleDelay)
        {
            return;
        }

        farm.Phase = PotChestFarmPhase.ElixirAtCenter;
        farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
        farm.ElixirAttempts = 0;
        farm.HintRevisionBaseline = hints.Revision;
    }

    private void HandleElixirAtCenter(PotChestFarmMemory farm)
    {
        if (hints.TryGetEventSince(farm.HintRevisionBaseline, out PotTreasureHintEvent evt))
        {
            if (evt.Kind == PotTreasureHintKind.BonusOffer)
            {
                logger.Info("Pot treasure: bonus offer — ending farm");
                FinishFarm();
                return;
            }

            if (evt.Kind == PotTreasureHintKind.Hint)
            {
                string groupKey = PotTreasureIds.GroupKey(evt.Direction);
                if (!PotTreasureGroups.TryGetGroup(
                        farm.FateId.Value,
                        groupKey,
                        farm.FateCenter,
                        zones.GetZone(),
                        out IReadOnlyList<PotTreasureCandidate> group)
                    || group.Count == 0)
                {
                    logger.Warning("Pot treasure: no candidates for {Group} — blind fallback", groupKey);
                    FallBackToBlind(farm);
                    return;
                }

                IEnumerable<PotTreasureCandidate> ordered = OrderNearestNeighbor(group, farm.FateCenter);
                farm.BeginCandidateSearch(groupKey, ordered);
                farm.HintRevisionBaseline = hints.Revision;
                logger.Info(
                    "Pot treasure: hint {Direction}/{Distance} → {Group} ({Count} candidates)",
                    evt.Direction,
                    evt.Distance,
                    groupKey,
                    farm.CandidateTotal);
                return;
            }

            // ElixirPrompt / Reveal without initial hint — keep waiting, bump baseline.
            farm.HintRevisionBaseline = evt.Revision;
        }

        if (!HasTreasureBuff() && !hints.RevealLatched)
        {
            FallBackToBlind(farm);
            return;
        }

        if (farm.ElixirAttempts >= MaxElixirAttempts
            && DateTimeOffset.UtcNow - farm.PhaseStartedUtc >= HintWaitTimeout)
        {
            logger.Info("Pot treasure: no compass hint after elixir — blind fallback");
            FallBackToBlind(farm);
            return;
        }

        if (farm.ElixirAttempts < MaxElixirAttempts
            && (farm.ElixirAttempts == 0
                || DateTimeOffset.UtcNow - farm.PhaseStartedUtc >= HintWaitTimeout))
        {
            if (!elixir.HasElixir())
            {
                logger.Info("Pot treasure: no Magical Elixir — blind fallback");
                FallBackToBlind(farm);
                return;
            }

            if (elixir.TryUse())
            {
                farm.ElixirAttempts++;
                farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
                farm.HintRevisionBaseline = hints.Revision;
            }
        }
    }

    private void HandleSearchingCandidates(PotChestFarmMemory farm)
    {
        if (TryAcquireReveal(farm, out IGameObject? reveal) && reveal != null)
        {
            farm.Phase = PotChestFarmPhase.OpeningReveal;
            OpenChest(reveal.Position, finishAfterOpen: farm.Mode == PotChestFarmMode.Smart);
            return;
        }

        if (hints.TryGetEventSince(farm.HintRevisionBaseline, out PotTreasureHintEvent evt))
        {
            farm.HintRevisionBaseline = evt.Revision;

            if (evt.Kind == PotTreasureHintKind.BonusOffer)
            {
                FinishFarm();
                return;
            }

            if (evt.Kind == PotTreasureHintKind.CofferReveal)
            {
                farm.Phase = PotChestFarmPhase.OpeningReveal;
                farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
                return;
            }

            if (evt.Kind == PotTreasureHintKind.Hint && farm.Candidates.Count > 0)
            {
                if (!evt.IsLocalHint)
                {
                    // Far at this candidate → skip to next.
                    farm.Candidates.Dequeue();
                    farm.ElixirAttempts = 0;
                    farm.RefineSteps = 0;
                    farm.RefineTarget = null;
                    farm.SettledAtUtc = DateTimeOffset.MinValue;
                    return;
                }

                // Local: refine along hint direction.
                if (farm.RefineSteps < MaxRefineSteps)
                {
                    Vector3 from = farm.RefineTarget ?? farm.Candidates.Peek().Position;
                    Vector3 step = PotTreasureIds.DirectionVector(evt.Direction) * PotTreasureIds.RefineStep(evt.Distance);
                    farm.RefineTarget = from + step;
                    farm.RefineSteps++;
                    farm.SettledAtUtc = DateTimeOffset.MinValue;
                    farm.ElixirAttempts = 0;
                }
            }
        }

        while (farm.Candidates.Count > 0)
        {
            PotTreasureCandidate peek = farm.Candidates.Peek();
            if (IsChestOpened(peek.Position))
            {
                farm.Candidates.Dequeue();
                farm.WaitingForSpawnSince = DateTimeOffset.MinValue;
                farm.SettledAtUtc = DateTimeOffset.MinValue;
                farm.ElixirAttempts = 0;
                farm.RefineTarget = null;
                continue;
            }

            break;
        }

        if (farm.Candidates.Count == 0)
        {
            logger.Info("Pot treasure: candidates exhausted");
            FinishFarm();
            return;
        }

        Vector3 target = farm.RefineTarget ?? farm.Candidates.Peek().Position;
        float distance = player.Position.Distance(target);

        if (distance > OpenTreasureCofferChain.InteractDistance)
        {
            farm.SettledAtUtc = DateTimeOffset.MinValue;
            if (pathfinder.IsIdle())
            {
                pathfinder.PathfindAndMoveTo(new(target));
            }

            return;
        }

        pathfinder.Stop();
        if (farm.SettledAtUtc == DateTimeOffset.MinValue)
        {
            farm.SettledAtUtc = DateTimeOffset.UtcNow;
            return;
        }

        if (DateTimeOffset.UtcNow - farm.SettledAtUtc < SettleDelay)
        {
            return;
        }

        IGameObject? live = FindChestNear(target) ?? FindRevealNear(player.Position);
        if (live != null)
        {
            farm.Phase = PotChestFarmPhase.OpeningReveal;
            OpenChest(live.Position, finishAfterOpen: true);
            return;
        }

        // Probe with elixir while at candidate.
        if (farm.ElixirAttempts < MaxElixirAttempts)
        {
            if (elixir.TryUse())
            {
                farm.ElixirAttempts++;
                farm.HintRevisionBaseline = hints.Revision;
                farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
            }

            return;
        }

        if (DateTimeOffset.UtcNow - farm.PhaseStartedUtc < HintWaitTimeout)
        {
            return;
        }

        // Give up on this candidate.
        farm.Candidates.Dequeue();
        farm.ElixirAttempts = 0;
        farm.RefineSteps = 0;
        farm.RefineTarget = null;
        farm.SettledAtUtc = DateTimeOffset.MinValue;
    }

    private void HandleOpeningReveal(PotChestFarmMemory farm)
    {
        if (TryAcquireReveal(farm, out IGameObject? reveal) && reveal != null)
        {
            float distance = player.Position.Distance(reveal.Position);
            if (distance > OpenTreasureCofferChain.InteractDistance)
            {
                if (pathfinder.IsIdle())
                {
                    pathfinder.PathfindAndMoveTo(new(reveal.Position));
                }

                return;
            }

            pathfinder.Stop();
            OpenChest(reveal.Position, finishAfterOpen: true);
            return;
        }

        if (DateTimeOffset.UtcNow - farm.PhaseStartedUtc > TimeSpan.FromSeconds(15))
        {
            logger.Info("Pot treasure: reveal timed out");
            FinishFarm();
        }
    }

    private void HandleBlindSweep(PotChestFarmMemory farm)
    {
        while (farm.Chests.Count > 0)
        {
            Vector3 target = farm.Chests.Peek();
            if (IsChestOpened(target))
            {
                farm.Chests.Dequeue();
                farm.WaitingForSpawnSince = DateTimeOffset.MinValue;
                continue;
            }

            break;
        }

        if (farm.Chests.Count == 0)
        {
            FinishFarm();
            return;
        }

        Vector3 chestPosition = farm.Chests.Peek();
        IGameObject? liveChest = FindChestNear(chestPosition);
        Vector3 pathTarget = liveChest?.Position ?? chestPosition;
        float distance = player.Position.Distance(pathTarget);

        if (liveChest == null)
        {
            if (farm.WaitingForSpawnSince == DateTimeOffset.MinValue)
            {
                farm.WaitingForSpawnSince = DateTimeOffset.UtcNow;
            }

            if (distance > OpenTreasureCofferChain.InteractDistance)
            {
                if (pathfinder.IsIdle())
                {
                    pathfinder.PathfindAndMoveTo(new(chestPosition));
                }

                return;
            }

            pathfinder.Stop();

            if (DateTimeOffset.UtcNow - farm.WaitingForSpawnSince >= ChestSpawnWait)
            {
                farm.Chests.Dequeue();
                farm.WaitingForSpawnSince = DateTimeOffset.MinValue;
            }

            return;
        }

        farm.WaitingForSpawnSince = DateTimeOffset.MinValue;

        if (distance > OpenTreasureCofferChain.InteractDistance)
        {
            if (pathfinder.IsIdle())
            {
                pathfinder.PathfindAndMoveTo(new(pathTarget));
            }

            return;
        }

        pathfinder.Stop();
        OpenChest(liveChest.Position);
    }

    private void OpenChest(Vector3 position, bool finishAfterOpen = false)
    {
        if (finishAfterOpen
            && memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory farm))
        {
            farm.FinishAfterOpen = true;
        }

        activeChain = chainManager.Manage(
            chains.Create("PotChestFarm::Open")
                .Then<OpenTreasureCofferChain, Vector3>(position)
        );
    }

    private bool TryAcquireReveal(PotChestFarmMemory farm, out IGameObject? reveal)
    {
        reveal = FindRevealNear(player.Position);
        if (reveal != null)
        {
            return true;
        }

        if (farm.Candidates.Count > 0)
        {
            reveal = FindRevealNear(farm.Candidates.Peek().Position)
                     ?? FindChestNear(farm.Candidates.Peek().Position);
            return reveal != null;
        }

        return false;
    }

    private void FallBackToBlind(PotChestFarmMemory farm)
    {
        hints.Disarm();
        IZone zone = zones.GetZone();
        List<Vector3> positions = [];
        if (zone.GetPotChestData().TryGetValue(farm.FateId.Value, out List<PotChestData>? chests))
        {
            positions.AddRange(chests.Select(c => c.Position));
        }

        positions = positions
            .OrderBy(p => player.Position.Distance(p))
            .ToList();

        if (positions.Count == 0)
        {
            FinishFarm();
            return;
        }

        farm.BeginBlindFallback(positions);
        logger.Info("Pot treasure: blind sweep with {Count} positions", positions.Count);
    }

    private void FinishFarm()
    {
        hints.Disarm();
        memory.Forget<PotChestFarmMemory>();
    }

    private static IEnumerable<PotTreasureCandidate> OrderNearestNeighbor(
        IReadOnlyList<PotTreasureCandidate> group,
        Vector3 origin)
    {
        List<PotTreasureCandidate> remaining = group.ToList();
        List<PotTreasureCandidate> ordered = new(remaining.Count);
        Vector3 cursor = origin;

        while (remaining.Count > 0)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < remaining.Count; i++)
            {
                float d = Vector3.DistanceSquared(cursor, remaining[i].Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            PotTreasureCandidate next = remaining[best];
            remaining.RemoveAt(best);
            ordered.Add(next);
            cursor = next.Position;
        }

        return ordered;
    }

    private bool HasTreasureBuff() =>
        player.PlayerCharacter?.StatusList.Has(PotTreasureIds.TreasureBuffStatusId) == true;

    private IEnumerable<IGameObject> GetValidChests()
    {
        return objects.Where(o => o is
        {
            ObjectKind: DalamudObjectKind.Treasure,
            IsDead: false
        } && o.IsValid());
    }

    private IGameObject? FindChestNear(Vector3 position)
    {
        return GetValidChests()
            .FirstOrDefault(o => Vector3.Distance(NormalizeY(o.Position), NormalizeY(position)) <= ChestSearchRadius);
    }

    private IGameObject? FindRevealNear(Vector3 origin)
    {
        IGameObject? best = null;
        float bestDist = float.MaxValue;
        Vector3 from = NormalizeY(origin);

        foreach (IGameObject obj in objects)
        {
            if (!obj.IsValid() || obj.IsDead)
            {
                continue;
            }

            bool isRevealBase = PotTreasureIds.RevealCofferBaseIds.Contains(obj.BaseId);
            bool isTreasure = obj.ObjectKind == DalamudObjectKind.Treasure;
            if (!isRevealBase && !isTreasure)
            {
                continue;
            }

            if (isTreasure && !isRevealBase && obj.ObjectKind != DalamudObjectKind.Treasure)
            {
                continue;
            }

            Vector3 pos = NormalizeY(obj.Position);
            float dist = Vector3.Distance(from, pos);
            if (dist > RevealSearchRadius || dist >= bestDist)
            {
                continue;
            }

            best = obj;
            bestDist = dist;
        }

        return best;
    }

    private static Vector3 NormalizeY(Vector3 position)
    {
        // Reveal objects sometimes sit at Y ≈ -500.
        if (MathF.Abs(position.Y + 500f) < 0.5f)
        {
            return position with { Y = 0f };
        }

        return position;
    }

    private bool IsChestOpened(Vector3 position)
    {
        IGameObject? chest = FindChestNear(position) ?? FindRevealNear(position);
        return chest != null && OpenTreasureCofferChain.IsOpenedOrLooted(chest);
    }
}
