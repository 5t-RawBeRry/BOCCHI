using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Automator.Services.PotTreasure;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using BOCCHI.Common.Targeting;
using BOCCHI.Treasure.ChainRecipes;
using BOCCHI.Treasure.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;
using System.Numerics;
using ECommonsPlayer = ECommons.GameHelpers.Player;

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
    AutoRotationController autoRotation,
    AutomatorConfig config,
    PotsConfig potsConfig,
    IAutomatorContext context,
    PandoraAutoOpenHold pandoraAutoOpen,
    ILogger<FarmingPotChestsHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.FarmingPotChests)
{
    private const float ChestSearchRadius = 5f;

    private const float RevealSearchRadius = 28f;

    private const float CenterArrival = 5f;

    private static readonly TimeSpan ChestSpawnWait = TimeSpan.FromSeconds(45);

    /// <summary>
    ///     Cache Me If You Can / Magical Elixir can appear shortly after the pot FATE despawns.
    ///     Keep this longer than a frame or two so we do not abandon a real reward.
    /// </summary>
    private static readonly TimeSpan BuffWaitTimeout = TimeSpan.FromSeconds(25);

    private static readonly TimeSpan HintWaitTimeout = TimeSpan.FromSeconds(4);

    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>Skip a pot treasure target when vnav can't progress toward it (off-mesh pads) (#176/#177).</summary>
    private static readonly TimeSpan ApproachStuckTimeout = TimeSpan.FromSeconds(20);

    private const float ApproachProgressThreshold = 1.5f;

    private const int MaxElixirAttempts = 3;

    private const int MaxRefineSteps = 6;

    private Task<ChainResult>? activeChain;

    /// <summary>Reveal coffers for the current tick (see <see cref="RefreshTickChests"/>).</summary>
    private readonly List<IGameObject> tickChests = [];

    private Vector3? approachTarget;

    private DateTimeOffset approachSince = DateTimeOffset.MinValue;

    private float approachBestDist = float.MaxValue;

    public override StatePriority GetScore()
    {
        if (conditions[ConditionFlag.Unconscious])
        {
            return StatePriority.Never;
        }

        // High so we beat opportunistic Return / next-goal pathing if a GoalPathStep
        // briefly overlaps (Pending handoff race). Farm start clears those latches.
        return memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _)
            ? StatePriority.High
            : StatePriority.Never;
    }

    public override void Enter()
    {
        base.Enter();
        // BossMod AI from the pot FATE otherwise keeps AutoTarget / movement during chest pathing.
        autoRotation.DisableAi();
        chainManager.CancelAll();
        pathfinder.Stop();
        activeChain = null;
        pandoraAutoOpen.Hold();
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);
        ResetApproachWatch();
        chainManager.CancelAll();
        pathfinder.Stop();
        activeChain = null;
        tickChests.Clear();
        hints.Disarm();
        pandoraAutoOpen.Release();
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

        activeChain = null;

        if (conditions[ConditionFlag.InCombat])
        {
            pathfinder.Stop();
            return;
        }

        RefreshTickChests();

        // Cache Me clears when pot chests are done or the pot dies — that is the farm end signal.
        if (farm.Phase != PotChestFarmPhase.WaitingForBuff && !HasTreasureBuff())
        {
            logger.Info("Pot treasure: Cache Me If You Can gone — ending farm");
            FinishFarm();
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
        // Status 1531 = Cache Me If You Can (Eureka row). Do not start on leftover Magical Elixir alone —
        // that caused blind sweeps after "persistent pot is too injured" with no reward.
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
            logger.Info(
                "Pot treasure: no Cache Me If You Can after wait — ending farm (not selected or pot failed)");
            FinishFarm();
        }
    }

    private void HandleApproachCenter(PotChestFarmMemory farm)
    {
        float dist = player.Position.Distance2D(farm.FateCenter);
        if (dist > CenterArrival)
        {
            farm.SettledAtUtc = DateTimeOffset.MinValue;
            EnsurePathing(farm.FateCenter);
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
                // Optional second chest — keep farming while Cache Me remains.
                farm.HintRevisionBaseline = evt.Revision;
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

            if (TryUseElixirOnFoot(farm))
            {
                return;
            }
        }
    }

    /// <summary>Use Magical Elixir only on foot — UseItem fails while mounted after the center approach.</summary>
    private bool TryUseElixirOnFoot(PotChestFarmMemory farm)
    {
        if (DismountAssist.TryDismount(conditions) || ECommonsPlayer.IsJumping)
        {
            return true; // still preparing — caller should wait this tick
        }

        if (!elixir.TryUse())
        {
            return false;
        }

        farm.ElixirAttempts++;
        farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
        farm.HintRevisionBaseline = hints.Revision;
        return true;
    }

    private void HandleSearchingCandidates(PotChestFarmMemory farm)
    {
        if (TryAcquireReveal(farm, out IGameObject? reveal) && reveal != null)
        {
            farm.Phase = PotChestFarmPhase.OpeningReveal;
            farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
            TryOpenChest(reveal);
            return;
        }

        if (hints.TryGetEventSince(farm.HintRevisionBaseline, out PotTreasureHintEvent evt))
        {
            farm.HintRevisionBaseline = evt.Revision;

            if (evt.Kind == PotTreasureHintKind.BonusOffer)
            {
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
                if (TryRedirectCandidateGroup(farm, evt))
                {
                    return;
                }

                if (!evt.IsLocalHint)
                {
                    // Same compass group, far from this spot — skip to the next candidate.
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
            logger.Info("Pot treasure: candidates exhausted — blind fallback while Cache Me remains");
            FallBackToBlind(farm);
            return;
        }

        Vector3 target = farm.RefineTarget ?? farm.Candidates.Peek().Position;
        float distance = player.Position.Distance2D(target);

        if (distance > OpenTreasureCofferChain.InteractDistance)
        {
            farm.SettledAtUtc = DateTimeOffset.MinValue;
            if (IsApproachStuck(target, distance))
            {
                logger.Warning(
                    "Pot treasure: stuck approaching {Label} at {Pos:F0} — skipping candidate ({Remaining} left)",
                    farm.Candidates.Peek().Label,
                    target,
                    farm.Candidates.Count - 1);
                SkipCurrentCandidate(farm);
                return;
            }

            EnsurePathing(target);
            return;
        }

        ResetApproachWatch();
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
            farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
            TryOpenChest(live);
            return;
        }

        // Probe with elixir while at candidate (feet — same as center cast).
        if (farm.ElixirAttempts < MaxElixirAttempts)
        {
            TryUseElixirOnFoot(farm);
        }

        // Wait from when we settled on this pad — don't softlock if UseItem never succeeds,
        // and don't use PhaseStartedUtc (that starts when the whole search begins).
        if (DateTimeOffset.UtcNow - farm.SettledAtUtc < HintWaitTimeout)
        {
            return;
        }

        // Give up on this candidate.
        SkipCurrentCandidate(farm);
    }

    private void SkipCurrentCandidate(PotChestFarmMemory farm)
    {
        if (farm.Candidates.Count > 0)
        {
            farm.Candidates.Dequeue();
        }

        farm.ElixirAttempts = 0;
        farm.RefineSteps = 0;
        farm.RefineTarget = null;
        farm.SettledAtUtc = DateTimeOffset.MinValue;
        farm.WaitingForSpawnSince = DateTimeOffset.MinValue;
        farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
        ResetApproachWatch();
        pathfinder.Stop();
    }

    private bool IsApproachStuck(Vector3 target, float distance)
    {
        Vector3 pathable = PathableTreasurePosition(target);
        if (approachTarget is not { } previous
            || previous.Distance2D(pathable) > 2f)
        {
            approachTarget = pathable;
            approachSince = DateTimeOffset.UtcNow;
            approachBestDist = distance;
            return false;
        }

        if (distance < approachBestDist - ApproachProgressThreshold)
        {
            approachBestDist = distance;
            return false;
        }

        return DateTimeOffset.UtcNow - approachSince >= ApproachStuckTimeout;
    }

    private void ResetApproachWatch()
    {
        approachTarget = null;
        approachSince = DateTimeOffset.MinValue;
        approachBestDist = float.MaxValue;
    }

    private void HandleOpeningReveal(PotChestFarmMemory farm)
    {
        if (TryAcquireReveal(farm, out IGameObject? reveal) && reveal != null)
        {
            if (OpenTreasureCofferChain.IsOpenedOrLooted(reveal))
            {
                AdvancePastOpenedReveal(farm);
                return;
            }

            // 2D — reveal Y ≈ -500 made 3D distance ~500y and blocked open forever (#170).
            float distance = player.Position.Distance2D(reveal.Position);
            if (distance > OpenTreasureCofferChain.InteractDistance)
            {
                if (IsApproachStuck(reveal.Position, distance))
                {
                    logger.Warning(
                        "Pot treasure: stuck approaching revealed coffer at {Pos:F0} - resuming search",
                        reveal.Position);
                    SkipCurrentReveal(farm);
                    return;
                }

                EnsurePathing(reveal.Position);
                return;
            }

            ResetApproachWatch();
            pathfinder.Stop();
            TryOpenChest(reveal);
            return;
        }

        if (DateTimeOffset.UtcNow - farm.PhaseStartedUtc > TimeSpan.FromSeconds(15))
        {
            logger.Info("Pot treasure: reveal timed out — resume search while Cache Me remains");
            ResumeSearchOrBlind(farm);
        }
    }

    private void AdvancePastOpenedReveal(PotChestFarmMemory farm)
    {
        pathfinder.Stop();
        if (farm.Candidates.Count > 0)
        {
            farm.Candidates.Dequeue();
        }

        farm.ElixirAttempts = 0;
        farm.RefineSteps = 0;
        farm.RefineTarget = null;
        farm.SettledAtUtc = DateTimeOffset.MinValue;
        farm.WaitingForSpawnSince = DateTimeOffset.MinValue;
        ResetApproachWatch();
        logger.Info(
            "Pot treasure: reveal already open — next candidate ({Remaining} left)",
            farm.Candidates.Count);
        ResumeSearchOrBlind(farm);
    }

    private void SkipCurrentReveal(PotChestFarmMemory farm)
    {
        pathfinder.Stop();
        if (farm.Candidates.Count > 0)
        {
            farm.Candidates.Dequeue();
        }

        farm.ElixirAttempts = 0;
        farm.RefineSteps = 0;
        farm.RefineTarget = null;
        farm.SettledAtUtc = DateTimeOffset.MinValue;
        farm.WaitingForSpawnSince = DateTimeOffset.MinValue;
        ResetApproachWatch();
        ResumeSearchOrBlind(farm);
    }

    private void ResumeSearchOrBlind(PotChestFarmMemory farm)
    {
        if (farm.Candidates.Count > 0)
        {
            farm.Phase = PotChestFarmPhase.SearchingCandidates;
            farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
            farm.SettledAtUtc = DateTimeOffset.MinValue;
            return;
        }

        FallBackToBlind(farm);
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
        float distance = player.Position.Distance2D(pathTarget);

        if (liveChest == null)
        {
            if (farm.WaitingForSpawnSince == DateTimeOffset.MinValue)
            {
                farm.WaitingForSpawnSince = DateTimeOffset.UtcNow;
            }

            if (distance > OpenTreasureCofferChain.InteractDistance)
            {
                if (IsApproachStuck(chestPosition, distance))
                {
                    SkipCurrentBlindChest(farm, chestPosition, "stuck approaching blind chest");
                    return;
                }

                EnsurePathing(chestPosition);
                return;
            }

            ResetApproachWatch();
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
            if (IsApproachStuck(pathTarget, distance))
            {
                SkipCurrentBlindChest(farm, pathTarget, "stuck approaching live blind chest");
                return;
            }

            EnsurePathing(pathTarget);
            return;
        }

        ResetApproachWatch();
        pathfinder.Stop();
        TryOpenChest(liveChest);
    }

    private void SkipCurrentBlindChest(PotChestFarmMemory farm, Vector3 target, string reason)
    {
        if (farm.Chests.Count > 0)
        {
            farm.Chests.Dequeue();
        }

        farm.WaitingForSpawnSince = DateTimeOffset.MinValue;
        farm.SettledAtUtc = DateTimeOffset.MinValue;
        ResetApproachWatch();
        pathfinder.Stop();
        logger.Warning(
            "Pot treasure: {Reason} at {Pos:F0} - skipping blind chest ({Remaining} left)",
            reason,
            target,
            farm.Chests.Count);
    }

    private bool TryRedirectCandidateGroup(PotChestFarmMemory farm, PotTreasureHintEvent evt)
    {
        string groupKey = PotTreasureIds.GroupKey(evt.Direction);
        if (string.IsNullOrEmpty(groupKey)
            || string.Equals(groupKey, farm.ActiveGroupKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        IZone zone = zones.GetZone();
        if (!PotTreasureGroups.TryGetGroup(
                farm.FateId.Value,
                groupKey,
                farm.FateCenter,
                zone,
                out IReadOnlyList<PotTreasureCandidate> group)
            || group.Count == 0)
        {
            if (!PotTreasureGroups.TryGetNearestNonEmptyGroup(
                    farm.FateId.Value,
                    groupKey,
                    farm.FateCenter,
                    zone,
                    out string fallbackKey,
                    out group))
            {
                logger.Warning(
                    "Pot treasure: hint redirected {From} → {To} but no candidates — blind fallback",
                    farm.ActiveGroupKey ?? "?",
                    groupKey);
                FallBackToBlind(farm);
                return true;
            }

            logger.Info(
                "Pot treasure: hint {To} empty — using adjacent {Alt} ({Count} candidates)",
                groupKey,
                fallbackKey,
                group.Count);
            groupKey = fallbackKey;
        }

        IEnumerable<PotTreasureCandidate> ordered = OrderNearestNeighbor(group, player.Position);
        string previous = farm.ActiveGroupKey ?? "?";
        farm.BeginCandidateSearch(groupKey, ordered);
        logger.Info(
            "Pot treasure: hint redirected {From} → {To}/{Distance} ({Count} candidates)",
            previous,
            groupKey,
            evt.Distance,
            farm.CandidateTotal);
        return true;
    }

    private void EnsurePathing(Vector3 destination)
    {
        Vector3 pathable = PathableTreasurePosition(destination);
        string throttleKey = $"PotChestFarm::Path::{MathF.Round(pathable.X)}::{MathF.Round(pathable.Z)}";
        if (pathfinder.IsIdle() && EzThrottler.Throttle(throttleKey, 750))
        {
            pathfinder.PathfindAndMoveTo(new(pathable));
        }

        // Remount only for longer walks — not while already on top of a reveal.
        if (player.Position.Distance2D(pathable) > 15f)
        {
            AutoMount.MaybeRemount(config, conditions, objects, pathable, zones.GetZone().IsInBasecamp());
        }
    }

    private void TryOpenChest(IGameObject chest)
    {
        // Pot reveals need feet — normal hunt coffers stay mounted (#175).
        if (DismountAssist.TryDismount(conditions) || ECommonsPlayer.IsJumping)
        {
            return;
        }

        Vector3 position = PathableTreasurePosition(chest.Position);
        activeChain = chainManager.Manage(
            chains.Create("PotChestFarm::Open")
                .Then<OpenTreasureCofferChain, TreasureOpenTarget>(
                    new TreasureOpenTarget(position, PotTreasureIds.RevealCofferBaseIds))
        );
    }

    private Vector3 PathableTreasurePosition(Vector3 position) =>
        TreasurePathing.PathablePosition(position, player.Position.Y);

    private bool TryAcquireReveal(PotChestFarmMemory farm, out IGameObject? reveal)
    {
        reveal = FindUnopenedRevealNear(player.Position);
        if (reveal != null)
        {
            return true;
        }

        if (farm.Candidates.Count > 0)
        {
            reveal = FindUnopenedRevealNear(farm.Candidates.Peek().Position)
                     ?? FindUnopenedChestNear(farm.Candidates.Peek().Position);
            return reveal != null;
        }

        return false;
    }

    private IGameObject? FindUnopenedRevealNear(Vector3 origin)
    {
        IGameObject? reveal = FindRevealNear(origin);
        return reveal != null && !OpenTreasureCofferChain.IsOpenedOrLooted(reveal) ? reveal : null;
    }

    private IGameObject? FindUnopenedChestNear(Vector3 position)
    {
        IGameObject? chest = FindChestNear(position);
        return chest != null && !OpenTreasureCofferChain.IsOpenedOrLooted(chest) ? chest : null;
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

        // Same reroll opt-in the blind-start path uses (Automator.TryStartPotChestFarm) — smart runs
        // that degrade to a sweep were silently dropping every reroll pad.
        if (context.IsPotsAndTreasure || potsConfig.ShouldFarmRerollPotChests)
        {
            positions.AddRange(zone.GetRerollPotChestData().Select(c => c.Position));
        }

        positions = positions
            .OrderBy(p => player.Position.Distance2D(p))
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

    /// <summary>Rebuild <see cref="tickChests"/> once per tick for reveal matching.</summary>
    private void RefreshTickChests()
    {
        tickChests.Clear();
        foreach (IGameObject obj in objects)
        {
            // Only Magic Pot reveal coffer BaseIds — layout bronze/silver can sit on the same spot.
            if (obj.IsValid()
                && !obj.IsDead
                && PotTreasureIds.RevealCofferBaseIds.Contains(obj.BaseId))
            {
                tickChests.Add(obj);
            }
        }
    }

    // Distance2D — reveal objects can sit at a bogus Y, so 3D compares miss them (#170).
    private IGameObject? FindChestNear(Vector3 position) =>
        GameObjectNearest.Find2D(tickChests, position, ChestSearchRadius);

    private IGameObject? FindRevealNear(Vector3 origin) =>
        GameObjectNearest.Find2D(tickChests, origin, RevealSearchRadius);

    private bool IsChestOpened(Vector3 position)
    {
        IGameObject? chest = FindChestNear(position) ?? FindRevealNear(position);
        return chest != null && OpenTreasureCofferChain.IsOpenedOrLooted(chest);
    }
}
