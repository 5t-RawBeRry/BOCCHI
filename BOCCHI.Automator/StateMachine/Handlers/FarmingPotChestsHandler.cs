using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Automator.Services.PotTreasure;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
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
    IPathCalculator pathCalculator,
    IPathStepExecutor pathStepExecutor,
    IObjectTable objects,
    ICondition conditions,
    IPlayer player,
    IZoneProvider zones,
    PotTreasureHintTracker hints,
    MagicalElixirAssist elixir,
    AutoRotationController autoRotation,
    MovementConfig movement,
    PotsConfig potsConfig,
    IAutomatorContext context,
    PandoraAutoOpenHold pandoraAutoOpen,
    ILogger<FarmingPotChestsHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.FarmingPotChests)
{
    private const float ChestSearchRadius = 5f;

    private const float RevealSearchRadius = 28f;

    /// <summary>
    ///     Only used by the fallback for a reveal BaseId we do not know yet. Not enough on its own —
    ///     North Horn has hunt coffers 2.2y and 7.3y from pot spots — so <see cref="IsOnAuthoredSpot"/>
    ///     also requires the object be nearer a pot spot than any hunt coffer position.
    /// </summary>
    private const float RevealSpotTolerance = 12f;

    /// <summary>On-pad distance for the elixir probe (not coffer interact range).</summary>
    private const float CandidateProbeRadius = 5f;

    private static readonly TimeSpan ChestSpawnWait = TimeSpan.FromSeconds(45);

    /// <summary>
    ///     Cache Me If You Can / Magical Elixir can appear shortly after the pot FATE despawns.
    ///     Keep this longer than a frame or two so we do not abandon a real reward.
    /// </summary>
    private static readonly TimeSpan BuffWaitTimeout = TimeSpan.FromSeconds(25);

    private static readonly TimeSpan HintWaitTimeout = TimeSpan.FromSeconds(4);

    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>
    ///     Skip a pot treasure target when vnav sits idle this long without reaching it — it has no
    ///     route to the pad (off-mesh pads, #176/#177).
    /// </summary>
    private static readonly TimeSpan ApproachIdleTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Backstop for vnav following a path but never arriving. Deliberately long: while it is
    ///     moving it is presumed to be making real progress, however the straight-line distance looks.
    /// </summary>
    private static readonly TimeSpan ApproachHardTimeout = TimeSpan.FromSeconds(90);

    private const float ApproachProgressThreshold = 1.5f;

    /// <summary>Destination move that forces a fresh path even if one is already running.</summary>
    private const float RepathDrift = 2f;

    /// <summary>Hard cap on the whole tail after Cache Me drops, including walking to the coffer.</summary>
    private static readonly TimeSpan PostBuffGrace = TimeSpan.FromSeconds(30);

    /// <summary>How long to wait for the coffer object to appear before accepting there is none.</summary>
    private static readonly TimeSpan RevealSpawnGrace = TimeSpan.FromSeconds(5);

    /// <summary>Once a coffer has been handled, how long to stay latched for a reroll.</summary>
    private static readonly TimeSpan RerollWait = TimeSpan.FromSeconds(12);

    private const int MaxElixirAttempts = 3;

    private Task<ChainResult>? activeChain;

    /// <summary>Every treasure object this tick (see <see cref="RefreshTickChests"/>).</summary>
    private readonly List<IGameObject> tickChests = [];

    /// <summary>Pot reveal coffers this tick — matched by BaseId, any ObjectKind.</summary>
    private readonly List<IGameObject> tickReveals = [];

    private readonly List<Vector3> authoredSpots = [];

    /// <summary>Hunt coffer positions — objects nearer one of these are not pot reveals.</summary>
    private readonly List<Vector3> foreignSpots = [];

    private int authoredSpotsFate = -1;

    /// <summary>Last destination handed to the pathfinder, for drift detection.</summary>
    private Vector3? lastPathDestination;

    /// <summary>In-flight aethernet route plan for the current long hop.</summary>
    private Task<PathCalculationResult>? travelPlanTask;

    /// <summary>Destination the current plan was built for.</summary>
    private Vector3? travelPlanTarget;

    /// <summary>Remaining steps of the planned route; null when travelling on plain vnav.</summary>
    private Queue<IPathStep>? travelSteps;

    private Vector3? approachTarget;

    private DateTimeOffset approachSince = DateTimeOffset.MinValue;

    /// <summary>When vnav went idle short of the target; MinValue while it is working.</summary>
    private DateTimeOffset approachIdleSince = DateTimeOffset.MinValue;

    private float approachBestDist = float.MaxValue;

    /// <summary>True while the AI holds movement for a fight (see the combat branch in Handle).</summary>
    private bool defendingInCombat;

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
        tickReveals.Clear();
        defendingInCombat = false;
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

        // Combat is the one window where nothing of ours drives movement, so hand it to the AI:
        // it fights and dodges, and the pot trailing us comes along out of the AoE. Previously the
        // farm disabled rotation and AI outright and then stood still through the whole fight —
        // no attacking, no dodging, and a destroyed pot loses the run (#188).
        if (conditions[ConditionFlag.InCombat])
        {
            pathfinder.Stop();
            ClearTravelPlan();

            if (!defendingInCombat)
            {
                defendingInCombat = true;
                autoRotation.EnableForSelfDefence();
                logger.Debug("Pot treasure: in combat — AI is fighting and dodging until it clears");
            }

            return;
        }

        if (defendingInCombat)
        {
            defendingInCombat = false;
            autoRotation.DisableAi();
            ResetApproachWatch();
            logger.Debug("Pot treasure: combat over — taking movement back for the chest search");
        }

        RefreshTickChests(farm);

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
            farm.RerollWaitStarted = false;
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

    /// <summary>Keep farming after Cache Me drops while a revealed coffer is still in front of us.</summary>
    private bool TryFinishRevealAfterBuff(PotChestFarmMemory farm)
    {
        if (farm.BuffLostUtc == DateTimeOffset.MinValue)
        {
            farm.BuffLostUtc = DateTimeOffset.UtcNow;
        }

        TimeSpan since = DateTimeOffset.UtcNow - farm.BuffLostUtc;
        if (since >= PostBuffGrace)
        {
            return false;
        }

        // Reveal log can land on the same tick Cache Me drops — read it before the phase handlers.
        if (hints.TryGetEventSince(farm.HintRevisionBaseline, out PotTreasureHintEvent evt))
        {
            farm.HintRevisionBaseline = evt.Revision;
            if (evt.Kind == PotTreasureHintKind.CofferReveal)
            {
                farm.HoldingAfterBuffLoss = true;
            }
        }

        // Match the reveal even if it is nearer the candidate pad than the player.
        if (!TryAcquireReveal(farm, out IGameObject? reveal) || reveal == null)
        {
            if (!farm.HoldingAfterBuffLoss)
            {
                // Coffer object trails the buff drop — wait briefly before giving up.
                return since < RevealSpawnGrace;
            }

            // Coffer is gone (opened) — start the reroll wait from now, not from the buff drop.
            if (!farm.RerollWaitStarted)
            {
                farm.RerollWaitStarted = true;
                farm.BuffLostUtc = DateTimeOffset.UtcNow;
                return true;
            }

            return since < RerollWait;
        }

        if (farm.Phase != PotChestFarmPhase.OpeningReveal)
        {
            farm.Phase = PotChestFarmPhase.OpeningReveal;
            logger.Debug("Pot treasure: Cache Me gone but a coffer is revealed — opening it before ending");
        }

        farm.HoldingAfterBuffLoss = true;

        if (DismountAssist.TryDismount(conditions, ReportDismount))
        {
            return true;
        }

        float distance = player.Position.Distance2D(reveal.Position);
        if (distance > OpenTreasureCofferChain.InteractDistance)
        {
            EnsurePathing(reveal.Position, allowRemount: false);
            return true;
        }

        pathfinder.Stop();
        TryOpenChest(reveal);
        return true;
    }

    // Info, not Debug: this is the line that tells us whether the dismount fired at all, and it is
    // no use if it lands below the level people actually read.
    private void ReportDismount(string detail) =>
        logger.Debug("Pot treasure: dismount {Detail}", detail);

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

    private void HandleElixirAtCenter(PotChestFarmMemory farm)
    {
        if (hints.TryGetEventSince(farm.HintRevisionBaseline, out PotTreasureHintEvent evt))
        {
            if (evt.Kind == PotTreasureHintKind.BonusOffer)
            {
                farm.HintRevisionBaseline = evt.Revision;
                SwitchToRerollPool(farm);
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

        // Start the "did anything happen" wait from the probe rather than from arrival. The elixir
        // has a ~5s recast, so a candidate reached shortly after the previous probe could time out
        // and be skipped before its own probe had even fired.
        farm.SettledAtUtc = DateTimeOffset.UtcNow;
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
                SwitchToRerollPool(farm);
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
            // Exhausted candidates → re-read, not a 50-spot sweep.
            ResumeSearchOrBlind(farm);
            return;
        }

        Vector3 target = farm.Candidates.Peek().Position;
        float distance = player.Position.Distance2D(target);

        if (distance > CandidateProbeRadius)
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

    /// <summary>
    ///     Stuck means "vnav cannot get us there", and the signal for that is vnav being idle while
    ///     we are still short of the target — not straight-line distance failing to shrink. A long
    ///     route around terrain legitimately moves away from the goal for a while, and the old
    ///     distance timer read that as stuck and threw away reachable pads mid-walk. While vnav is
    ///     computing or following a path it gets the benefit of the doubt, bounded only by a long
    ///     backstop for the case where it walks forever without arriving.
    /// </summary>
    private bool IsApproachStuck(Vector3 target, float distance)
    {
        Vector3 pathable = PathableTreasurePosition(target);
        if (approachTarget is not { } previous
            || previous.Distance2D(pathable) > 2f)
        {
            approachTarget = pathable;
            approachSince = DateTimeOffset.UtcNow;
            approachBestDist = distance;
            approachIdleSince = DateTimeOffset.MinValue;
            return false;
        }

        // While the route planner owns travel, vnav is legitimately idle between teleport steps —
        // reading that as "no route" would skip the candidate mid-hop.
        if (travelPlanTask != null || travelSteps != null)
        {
            approachIdleSince = DateTimeOffset.MinValue;
            return false;
        }

        if (!pathfinder.IsIdle())
        {
            approachIdleSince = DateTimeOffset.MinValue;
            if (distance < approachBestDist - ApproachProgressThreshold)
            {
                approachBestDist = distance;
                approachSince = DateTimeOffset.UtcNow;
            }

            return DateTimeOffset.UtcNow - approachSince >= ApproachHardTimeout;
        }

        // Idle and not there. EnsurePathing re-issues every 750ms, so staying idle across several
        // attempts means vnav has no route to this pad — the off-mesh case from #176/#177.
        if (approachIdleSince == DateTimeOffset.MinValue)
        {
            approachIdleSince = DateTimeOffset.UtcNow;
            return false;
        }

        return DateTimeOffset.UtcNow - approachIdleSince >= ApproachIdleTimeout;
    }

    private void ResetApproachWatch()
    {
        lastPathDestination = null;
        ClearTravelPlan();
        approachTarget = null;
        approachIdleSince = DateTimeOffset.MinValue;
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

                EnsurePathing(reveal.Position, allowRemount: false);
                return;
            }

            ResetApproachWatch();
            pathfinder.Stop();
            TryOpenChest(reveal);
            return;
        }

        // Nothing to open here, and the compass is still talking: a hint arriving means the coffer is
        // elsewhere. This phase never read hints, so one that landed here sat unread until the 15s
        // timeout expired — a fifth of the whole Cache Me window spent standing still. Act on it now.
        if (hints.TryGetEventSince(farm.HintRevisionBaseline, out PotTreasureHintEvent evt)
            && evt.Kind == PotTreasureHintKind.Hint)
        {
            farm.HintRevisionBaseline = evt.Revision;
            if (TryNarrowByHint(farm, evt))
            {
                farm.Phase = PotChestFarmPhase.SearchingCandidates;
                farm.PhaseStartedUtc = DateTimeOffset.UtcNow;
                farm.SettledAtUtc = DateTimeOffset.MinValue;
                ResetApproachWatch();
                pathfinder.Stop();
            }

            return;
        }

        if (DateTimeOffset.UtcNow - farm.PhaseStartedUtc > TimeSpan.FromSeconds(15))
        {
            logger.Debug("Pot treasure: reveal timed out — resume search while Cache Me remains");
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
        logger.Debug(
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

        // Narrowed set spent — re-read from the full pool instead of a 50-spot sweep.
        if (farm.Pool.Count > 0 && farm.HintsApplied < MaxHintReadings && HasTreasureBuff())
        {
            logger.Debug(
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

    /// <param name="allowRemount">
    ///     False once we are closing on a coffer to open. The open path dismounts first, so remounting
    ///     mid-approach just fights it — the two take turns and neither wins.
    /// </param>
    private void EnsurePathing(Vector3 destination, bool allowRemount = true)
    {
        Vector3 pathable = PathableTreasurePosition(destination);

        // Long hops use the FATE/CE aethernet planner; short ones stay on vnav.
        if (TryTravelByPlan(pathable))
        {
            return;
        }

        // Re-path when the destination moves, not only when vnav is idle.
        bool drifted = lastPathDestination is not { } last || last.Distance2D(pathable) > RepathDrift;
        string throttleKey = $"PotChestFarm::Path::{MathF.Round(pathable.X)}::{MathF.Round(pathable.Z)}";
        if ((pathfinder.IsIdle() || drifted) && EzThrottler.Throttle(throttleKey, 750))
        {
            lastPathDestination = pathable;
            pathfinder.PathfindAndMoveTo(new(pathable));
        }

        // Remount only for longer walks — not while already on top of a reveal.
        if (allowRemount && player.Position.Distance2D(pathable) > 15f)
        {
            AutoMount.MaybeRemount(movement, conditions, objects, pathable, zones.GetZone().IsInBasecamp());
        }
    }

    /// <summary>
    ///     Plan and run an aethernet-assisted route to <paramref name="destination"/>.
    ///     Returns true when travel is being handled here and the caller should not walk.
    /// </summary>
    private bool TryTravelByPlan(Vector3 destination)
    {
        if (player.Position.Distance2D(destination) <= NavigationConstants.MaxDirectWalkDistance)
        {
            ClearTravelPlan();
            return false;
        }

        // Target moved: the old plan goes somewhere we no longer want.
        if (travelPlanTarget is { } planned && planned.Distance2D(destination) > RepathDrift)
        {
            ClearTravelPlan();
        }

        if (travelPlanTask is { IsCompleted: true } finished)
        {
            travelPlanTask = null;
            PathCalculationResult result = finished.IsCompletedSuccessfully
                ? finished.Result
                : PathCalculationResult.Failed();

            // No route (or the planner faulted) — fall back to walking rather than stalling.
            if (result.RoutingFailed || result.Steps.Count == 0)
            {
                travelPlanTarget = null;
                travelSteps = null;
                return false;
            }

            travelSteps = result.Steps;
            logger.Debug(
                "Pot treasure: routing {Steps} step(s) to {Pos:F0} ({Dist:F0}y)",
                travelSteps.Count,
                destination,
                player.Position.Distance2D(destination));
        }

        if (travelPlanTask != null)
        {
            return true;
        }

        if (travelSteps is { Count: > 0 })
        {
            activeChain = pathStepExecutor.Execute(travelSteps.Dequeue());
            return true;
        }

        if (travelSteps != null)
        {
            // Plan spent — the last leg lands us close enough for plain vnav to finish.
            ClearTravelPlan();
            return false;
        }

        travelPlanTarget = destination;
        travelPlanTask = pathCalculator.CalculateToPosition(destination, CandidateProbeRadius);
        return true;
    }

    private void ClearTravelPlan()
    {
        travelPlanTask = null;
        travelPlanTarget = null;
        travelSteps = null;
    }

    private void TryOpenChest(IGameObject chest)
    {
        // Pot reveals need feet — normal hunt coffers stay mounted (#175).
        if (DismountAssist.TryDismount(conditions, ReportDismount) || ECommonsPlayer.IsJumping)
        {
            return;
        }

        Vector3 position = PathableTreasurePosition(chest.Position);
        // Prefer reveal BaseIds — pot reveals are EventObj, not ObjectKind.Treasure.
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

    /// <summary>
    ///     A revealed coffer is in the object table for a beat before it can be interacted with, so
    ///     require targetable before treating one as acquired — latching early means dismounting and
    ///     pathing to something that cannot be opened yet. Not a fallback to the nearest untargetable
    ///     one either: waiting is correct, and the coffer becomes targetable on its own.
    /// </summary>
    private IGameObject? FindUnopenedRevealNear(Vector3 origin)
    {
        IGameObject? reveal = GameObjectNearest.Find2D(
            tickReveals,
            origin,
            RevealSearchRadius,
            o => o.IsTargetable);

        if (reveal == null)
        {
            if (FindRevealNear(origin) != null
                && EzThrottler.Throttle("PotChestFarm::RevealNotTargetable", 2000))
            {
                logger.Debug("Pot treasure: coffer on an authored spot is not targetable yet — waiting");
            }

            return null;
        }

        return OpenTreasureCofferChain.IsOpenedOrLooted(reveal) ? null : reveal;
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
            // Everything we know says the chest is at one of these pads, so a reading that matches
            // none of them is the odd one out — not grounds to throw away every earlier reading and
            // sweep 50 positions. Keep what we have and ignore it; only sweep with nothing left.
            if (farm.Candidates.Count > 0)
            {
                logger.Warning(
                    "Pot treasure: hint {Direction}/{Distance} matches no authored spot — ignoring it, "
                    + "keeping {Count} candidate(s)",
                    evt.Direction,
                    evt.Distance,
                    farm.Candidates.Count);
                return true;
            }

            logger.Warning(
                "Pot treasure: hint {Direction}/{Distance} matches no authored spot — blind fallback",
                evt.Direction,
                evt.Distance);
            FallBackToBlind(farm);
            return false;
        }

        farm.NarrowTo(survivors);
        logger.Debug(
            "Pot treasure: hint {Hint} {Direction}/{Distance} — {Count} spot(s) {Source}, nearest {Label}",
            farm.HintsApplied,
            evt.Direction,
            evt.Distance,
            survivors.Count,
            source,
            survivors[0].Label);
        return true;
    }

    /// <summary>
    ///     A second-chance chest hides among the reroll pads, not the FATE's own spots — so the
    ///     narrowing pool has to swap over wholesale. Without this the offer was acknowledged and
    ///     discarded, and the search carried on among spots the chest could not be at, which is why
    ///     rerolls looked like "it just keeps walking the normal route" (#188).
    ///     Those pads sit in remote second-chance areas, so this depends on the route planner being
    ///     able to teleport; walking 250-500y would spend the rest of the buff getting there.
    /// </summary>
    private void SwitchToRerollPool(PotChestFarmMemory farm)
    {
        if (!ShouldIncludeRerolls || farm.OnRerollPool)
        {
            return;
        }

        List<PotTreasureCandidate> reroll = PotTreasureFilter.BuildRerollPool(zones.GetZone());
        if (reroll.Count == 0)
        {
            logger.Warning("Pot treasure: reroll offered but this zone has no authored reroll pads");
            return;
        }

        farm.OnRerollPool = true;
        farm.SeedPool(reroll);

        // Seed with all of them; the next compass reading narrows within the reroll set.
        farm.NarrowTo(reroll);
        ResetApproachWatch();
        ClearTravelPlan();

        logger.Info(
            "Pot treasure: second chest offered — switching to {Count} reroll pad(s)",
            reroll.Count);
    }

    /// <summary>Same opt-in the blind sweep uses, so pool and sweep cover the same pads.</summary>
    private bool ShouldIncludeRerolls =>
        context.IsPotsAndTreasure || potsConfig.ShouldFarmRerollPotChests;

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
        if (ShouldIncludeRerolls)
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
        logger.Debug("Pot treasure: blind sweep with {Count} positions", positions.Count);
    }

    private void FinishFarm()
    {
        hints.Disarm();
        memory.Forget<PotChestFarmMemory>();
    }

    private bool HasTreasureBuff() =>
        player.PlayerCharacter?.StatusList.Has(PotTreasureIds.TreasureBuffStatusId) == true;

    /// <summary>
    ///     Every authored pot chest position for the current FATE, including rerolls. A pot reveal
    ///     only ever appears on one of these, which is what separates it from ordinary field coffers.
    /// </summary>
    private void EnsureAuthoredSpots(PotChestFarmMemory farm)
    {
        if (authoredSpotsFate == farm.FateId.Value)
        {
            return;
        }

        authoredSpotsFate = farm.FateId.Value;
        authoredSpots.Clear();

        IZone zone = zones.GetZone();
        if (zone.GetPotChestData().TryGetValue(farm.FateId.Value, out List<PotChestData>? chests))
        {
            authoredSpots.AddRange(chests.Select(c => c.Position));
        }

        authoredSpots.AddRange(zone.GetRerollPotChestData().Select(c => c.Position));

        foreignSpots.Clear();
        foreignSpots.AddRange(
            zone.GetTreasureData()
                .Where(t => t.Position.HasValue)
                .Select(t => t.Position!.Value));
    }

    private bool IsOnAuthoredSpot(Vector3 position)
    {
        float nearestPot = float.MaxValue;
        foreach (Vector3 spot in authoredSpots)
        {
            nearestPot = MathF.Min(nearestPot, position.Distance2D(spot));
        }

        if (nearestPot > RevealSpotTolerance)
        {
            return false;
        }

        // North Horn puts an ordinary hunt coffer 2.2y from a pot spot, so no radius can tell the
        // two apart on its own. Both sit on their own authored position though, so whichever one
        // this object is nearer to is what it is.
        foreach (Vector3 known in foreignSpots)
        {
            if (position.Distance2D(known) < nearestPot)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Rebuild <see cref="tickChests"/> once per tick for reveal matching.</summary>
    private void RefreshTickChests(PotChestFarmMemory farm)
    {
        EnsureAuthoredSpots(farm);
        tickChests.Clear();
        tickReveals.Clear();

        foreach (IGameObject obj in objects)
        {
            if (!obj.IsValid() || obj.IsDead)
            {
                continue;
            }

            // Pot reveals are EventObj matched by BaseId, not ObjectKind.Treasure.
            if (PotTreasureIds.RevealCofferBaseIds.Contains(obj.BaseId))
            {
                tickReveals.Add(obj);
                continue;
            }

            if (obj.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure)
            {
                continue;
            }

            tickChests.Add(obj);

            // Safety net for a reveal id we do not know yet: a coffer sitting on an authored pot
            // spot, and nearer that than any hunt coffer, is a reveal even if its BaseId is new.
            if (IsOnAuthoredSpot(obj.Position))
            {
                tickReveals.Add(obj);
                if (EzThrottler.Throttle("PotChestFarm::UnknownRevealId", 5000))
                {
                    logger.Info(
                        "Pot treasure: coffer {BaseId} on a pot spot is not a known reveal id — "
                        + "accepting it, worth adding to RevealCofferBaseIds",
                        obj.BaseId);
                }
            }
        }
    }

    // Distance2D — reveal objects can sit at a bogus Y, so 3D compares miss them (#170).
    private IGameObject? FindChestNear(Vector3 position) =>
        GameObjectNearest.Find2D(tickChests, position, ChestSearchRadius);

    private IGameObject? FindRevealNear(Vector3 origin) =>
        GameObjectNearest.Find2D(tickReveals, origin, RevealSearchRadius);

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
