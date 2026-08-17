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

    /// <summary>
    ///     Window after Cache Me drops in which a revealed coffer still gets opened. Only needs to
    ///     cover dismount plus the last few yards — the reveal is already within matching range.
    /// </summary>
    private static readonly TimeSpan PostBuffGrace = TimeSpan.FromSeconds(30);

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

        // High priority vs Return/next-goal handoff race.
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

        // Cache Me clears when the coffer is found, when the chests are done, or when the pot dies.
        // Finding it is the common case, so check for a coffer to open before treating this as the end.
        if (farm.Phase != PotChestFarmPhase.WaitingForBuff && !HasTreasureBuff())
        {
            if (TryFinishRevealAfterBuff(farm))
            {
                return;
            }

            logger.Info("Pot treasure: Cache Me If You Can gone — ending farm");
            FinishFarm();
            return;
        }

        farm.BuffLostUtc = DateTimeOffset.MinValue;

        // Buff is back (reroll) — drop the grace latch and pick the search straight back up. Leaving
        // the phase on OpeningReveal would idle 15s waiting on a coffer that is already looted.
        if (farm.HoldingAfterBuffLoss)
        {
            farm.HoldingAfterBuffLoss = false;
            logger.Info("Pot treasure: Cache Me back after the coffer — continuing (reroll)");
            ResumeSearchOrBlind(farm);
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

    /// <summary>
    ///     Finding the coffer <b>is</b> what clears Cache Me — with no reroll offered the buff just
    ///     ends. So "buff gone → stop" fired at the exact moment of success and walked away from the
    ///     chest the whole farm was for. Keep going while an unopened coffer is actually in front of
    ///     us, bounded in time so a stray layout chest cannot hold the farm open indefinitely.
    /// </summary>
    private bool TryFinishRevealAfterBuff(PotChestFarmMemory farm)
    {
        if (farm.BuffLostUtc == DateTimeOffset.MinValue)
        {
            farm.BuffLostUtc = DateTimeOffset.UtcNow;
        }
        else if (DateTimeOffset.UtcNow - farm.BuffLostUtc >= PostBuffGrace)
        {
            return false;
        }

        // Same acquisition the normal path uses: the reveal can be nearer the candidate we were
        // walking to than to us, and player-only matching would miss it and end the farm.
        if (!TryAcquireReveal(farm, out IGameObject? reveal) || reveal == null)
        {
            // Nothing left to open. If we already opened one here, stay latched for the rest of the
            // window: a reroll re-grants the buff a moment later and needs the farm to still exist.
            return farm.HoldingAfterBuffLoss;
        }

        if (!farm.HoldingAfterBuffLoss)
        {
            farm.HoldingAfterBuffLoss = true;
            farm.Phase = PotChestFarmPhase.OpeningReveal;
            logger.Info("Pot treasure: Cache Me gone but a coffer is revealed — opening it before ending");
        }

        if (DismountAssist.TryDismount(conditions, ReportDismount))
        {
            return true;
        }

        float distance = player.Position.Distance2D(reveal.Position);
        if (distance > OpenTreasureCofferChain.InteractDistance)
        {
            EnsurePathing(reveal.Position);
            return true;
        }

        pathfinder.Stop();
        TryOpenChest(reveal);
        return true;
    }

    private void ReportDismount(string detail) =>
        logger.Debug("Pot treasure: {Detail}", detail);

    private void HandleWaitingForBuff(PotChestFarmMemory farm)
    {
        // Require Cache Me (1531); not Magical Elixir alone.
        if (HasTreasureBuff())
        {
            hints.Arm();

            // Read the first hint where we are. Walking back to the pot centre only mattered when
            // spots were binned by direction *from* the centre; hints are player-relative now, so
            // the trip is pure overhead — and after a chest it dragged us back across the map.
            pathfinder.Stop();
            farm.Phase = PotChestFarmPhase.ElixirAtCenter;
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

    /// <summary>
    ///     Kept only so a farm latched before the player-relative rewrite does not stall on a phase
    ///     nothing sets any more — just read the hint here instead of walking to the centre.
    /// </summary>
    private void HandleApproachCenter(PotChestFarmMemory farm)
    {
        pathfinder.Stop();
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
                farm.SeedPool(PotTreasureFilter.BuildPool(zones.GetZone(), farm.FateId.Value));
                if (farm.Pool.Count == 0)
                {
                    logger.Warning("Pot treasure: no authored chest spots for this pot — blind fallback");
                    FallBackToBlind(farm);
                    return;
                }

                if (!TryNarrowByHint(farm, evt))
                {
                    return;
                }

                farm.HintRevisionBaseline = hints.Revision;
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

            if (TryUseElixir(farm))
            {
                return;
            }
        }
    }

    /// <summary>
    ///     Magical Elixir is a key item, so UseItem takes the KeyItems inventory path and works while
    ///     mounted — dismounting here only cost the dismount and its landing beat. Reveals still need
    ///     feet, but TryOpenChest dismounts itself once one actually appears (#175).
    /// </summary>
    private bool TryUseElixir(PotChestFarmMemory farm)
    {
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

            if (evt.Kind == PotTreasureHintKind.Hint)
            {
                if (!TryNarrowByHint(farm, evt))
                {
                    return;
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
                continue;
            }

            break;
        }

        if (farm.Candidates.Count == 0)
        {
            // Go through ResumeSearchOrBlind so this exhausts into a fresh reading, not straight
            // into a 50-spot sweep — this path was missed when that re-read was added.
            ResumeSearchOrBlind(farm);
            return;
        }

        Vector3 target = farm.Candidates.Peek().Position;
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

        // Probe with elixir at the candidate — mounted is fine, it is a key item.
        if (farm.ElixirAttempts < MaxElixirAttempts)
        {
            TryUseElixir(farm);
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
            // Restart the clock on progress: the timeout means "not getting closer for 20s", not
            // "20s since we set off". Without this every candidate more than 20s of travel away is
            // reported stuck mid-run, however well the approach is going.
            approachBestDist = distance;
            approachSince = DateTimeOffset.UtcNow;
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

            // Get on foot before closing the last stretch. Travel and the elixir are fine mounted,
            // but the open needs to be within 2y of the coffer and that is not reliable from a
            // mount — least of all in the air, where Dismount cannot land us on the spot.
            if (DismountAssist.TryDismount(conditions, ReportDismount))
            {
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
        farm.SettledAtUtc = DateTimeOffset.MinValue;
        farm.WaitingForSpawnSince = DateTimeOffset.MinValue;
        ResetApproachWatch();
        ResumeSearchOrBlind(farm);
    }

    /// <summary>Give up on narrowing after this many readings and just sweep.</summary>
    private const int MaxHintReadings = 10;

    private void ResumeSearchOrBlind(PotChestFarmMemory farm)
    {
        if (farm.Candidates.Count > 0)
        {
            farm.Phase = PotChestFarmPhase.SearchingCandidates;
            farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
            farm.SettledAtUtc = DateTimeOffset.MinValue;
            return;
        }

        // Spending the narrowed set does not mean the compass stopped working — it usually means the
        // last reading pointed at a spot that did not pop. Re-read from the full set instead of
        // throwing the hints away for a 50-spot sweep.
        if (farm.Pool.Count > 0 && farm.HintsApplied < MaxHintReadings && HasTreasureBuff())
        {
            logger.Info(
                "Pot treasure: narrowed set spent — re-reading from {Count} spots",
                farm.Pool.Count);
            farm.NarrowTo(farm.Pool);
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
        if (DismountAssist.TryDismount(conditions, ReportDismount) || ECommonsPlayer.IsJumping)
        {
            return;
        }

        Vector3 position = PathableTreasurePosition(chest.Position);
        activeChain = chainManager.Manage(
            chains.Create("PotChestFarm::Open")
                // No preferred BaseIds — the chain then falls back to ObjectKind.Treasure, which is
                // what actually matches a reveal.
                .Then<OpenTreasureCofferChain, TreasureOpenTarget>(
                    new TreasureOpenTarget(position))
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

    /// <summary>
    ///     Apply one hint: keep the spots lying in that direction <b>from where we are standing</b>.
    ///     Narrows the survivors first so successive readings triangulate; if that leaves nothing the
    ///     reading disagrees with the ones before it, so re-acquire from the full set before giving up.
    /// </summary>
    /// <returns>False when the farm fell back to a blind sweep and the caller should stop.</returns>
    private bool TryNarrowByHint(PotChestFarmMemory farm, PotTreasureHintEvent evt)
    {
        Vector3 from = player.Position;
        IEnumerable<PotTreasureCandidate> basis = farm.Candidates.Count > 0 ? farm.Candidates : farm.Pool;

        List<PotTreasureCandidate> survivors = PotTreasureFilter.Narrow(
            basis, from, evt.Direction, evt.Distance, PotTreasureFilter.OctantTolerance);

        string source = "narrowed";
        if (survivors.Count == 0)
        {
            survivors = PotTreasureFilter.Narrow(
                farm.Pool, from, evt.Direction, evt.Distance, PotTreasureFilter.OctantTolerance);
            source = "re-acquired";
        }

        if (survivors.Count == 0)
        {
            survivors = PotTreasureFilter.Narrow(
                farm.Pool, from, evt.Direction, evt.Distance, PotTreasureFilter.WideTolerance);
            source = "widened";
        }

        if (survivors.Count == 0)
        {
            logger.Warning(
                "Pot treasure: hint {Direction}/{Distance} matches no authored spot — blind fallback",
                evt.Direction,
                evt.Distance);
            FallBackToBlind(farm);
            return false;
        }

        farm.NarrowTo(survivors);
        logger.Info(
            "Pot treasure: hint {Hint} {Direction}/{Distance} — {Count} spot(s) {Source}, nearest {Label}",
            farm.HintsApplied,
            evt.Direction,
            evt.Distance,
            survivors.Count,
            source,
            survivors[0].Label);
        return true;
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

        // Include reroll pads on the same opt-in as the blind-start path.
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
            // Any treasure object, not a fixed BaseId list: the reveal did not match the three ids
            // we hardcoded, so a correctly-located chest was invisible to the farm. ObjectKind still
            // scopes this to coffers — blanket interaction would hit aetherytes and NPCs.
            if (obj.IsValid()
                && !obj.IsDead
                && obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure)
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

    /// <summary>
    ///     A spot counts as spent only when there is a coffer there and none of them are still
    ///     closed. Now that any treasure matches, "nearest one is open" would let a leftover layout
    ///     bronze on the same spot retire a candidate whose pot chest has not been touched.
    /// </summary>
    private bool IsChestOpened(Vector3 position) =>
        (FindChestNear(position) ?? FindRevealNear(position)) != null
        && FindUnopenedChestNear(position) == null
        && FindUnopenedRevealNear(position) == null;
}
