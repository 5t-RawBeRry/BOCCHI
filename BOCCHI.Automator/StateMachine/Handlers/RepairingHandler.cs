using BOCCHI.Automator.Data;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class RepairingHandler(
    IZoneProvider zones,
    IRepairService repair,
    IChainManager chains,
    IPluginLog log
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Repairing)
{
    /// <summary>After a failed / hung repair, leave camp work alone so Illegal Mode is not wedged.</summary>
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(90);

    /// <summary>Hard cap if the chain never completes (dialog / YesAlready / pathing).</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(75);

    private const string ChainName = "Repairs";

    private Task<ChainResult>? task;

    private DateTime? startedUtc;

    private DateTime? suppressUntilUtc;

    public override StatePriority GetScore()
    {
        if (task is { IsCompleted: false })
        {
            return StatePriority.Always;
        }

        if (suppressUntilUtc is { } until && DateTime.UtcNow < until)
        {
            return StatePriority.Never;
        }

        return zones.GetZone().IsInBasecamp() && repair.ShouldRepair()
            ? StatePriority.AboveNormal
            : StatePriority.Never;
    }

    public override void Enter()
    {
        base.Enter();
        startedUtc = DateTime.UtcNow;
        // Manage() so we can CancelWhere on timeout / Exit.
        task = chains.Manage(repair.Repair());
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);

        if (task is { IsCompleted: false })
        {
            chains.CancelWhere(name => name == ChainName);
        }

        task = null;
        startedUtc = null;
    }

    public override void Handle()
    {
        if (task is null)
        {
            return;
        }

        if (!task.IsCompleted
            && startedUtc is { } start
            && DateTime.UtcNow - start >= AttemptTimeout)
        {
            log.Warning("Repair timed out after {Seconds}s — backing off for {Backoff}s",
                (int)AttemptTimeout.TotalSeconds,
                (int)FailureBackoff.TotalSeconds);
            chains.CancelWhere(name => name == ChainName);
            FinishAttempt(succeeded: false);
            return;
        }

        if (!task.IsCompleted)
        {
            return;
        }

        ChainResult result;
        try
        {
            result = task.Result;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Repair chain threw — backing off for {Backoff}s", (int)FailureBackoff.TotalSeconds);
            FinishAttempt(succeeded: false);
            return;
        }

        bool ok = result.IsSuccess && !repair.ShouldRepair();
        if (!ok)
        {
            log.Warning(
                "Repair did not finish ({State}{Detail}) — backing off for {Backoff}s so Illegal Mode can continue",
                result.State,
                string.IsNullOrEmpty(result.ErrorMessage) ? string.Empty : $": {result.ErrorMessage}",
                (int)FailureBackoff.TotalSeconds);
        }

        FinishAttempt(ok);
    }

    private void FinishAttempt(bool succeeded)
    {
        task = null;
        startedUtc = null;

        if (succeeded)
        {
            suppressUntilUtc = null;
            return;
        }

        suppressUntilUtc = DateTime.UtcNow + FailureBackoff;
    }
}
