using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using Ocelot.Services.PlayerState;
using System.Numerics;
using ECommonsPlayer = ECommons.GameHelpers.Player;

namespace BOCCHI.Common.Services;

/// <summary>
///     Hops when vnav believes it is running but the character has stopped moving.
///     Rocks, low ledges and stair lips catch the mesh in places it thinks are walkable, and vnav
///     keeps happily reporting "moving" while the character stands still against them — so nothing
///     downstream notices. A jump clears nearly all of them (#185).
///     <para>
///     This is stuck <i>recovery</i>, not jump-aware routing: it tells the router nothing and needs
///     no authored takeoff/landing data, so it also covers snags nobody has reported. It stays
///     useful even if vnavmesh gains real jump links, which would only replace routing.
///     </para>
///     Self-contained on purpose — it watches position itself rather than hanging off a movement
///     hook, because the treasure and carrot hunts drive vnav directly and would miss one.
/// </summary>
public sealed class StuckJumpAssist(
    IVNavmeshIpc vnav,
    IPlayer player,
    ICondition conditions,
    IZoneProvider zones,
    MovementConfig config,
    ILogger<StuckJumpAssist> logger
) : IOnUpdate
{
    /// <summary>Leave time to land and for vnav to make real progress before hopping again.</summary>
    private const int RetryThrottleMs = 2000;

    /// <summary>Horizontal movement that counts as progress. Generous — a snag moves you nowhere.</summary>
    private const float ProgressThreshold = 1f;

    private Vector3 lastPosition;

    private DateTime movedAtUtc = DateTime.MinValue;

    public UpdateLimit UpdateLimit =>
        new()
        {
            Mode = UpdateLimitMode.Milliseconds,
            Limit = 250,
        };

    public void Update()
    {
        if (!config.ShouldJumpWhenStuck || !zones.GetZone().IsOccultCrescentZone() || !vnav.IsRunning())
        {
            movedAtUtc = DateTime.MinValue;
            return;
        }

        // Casting is the one thing a jump would actively break, and the rest mean the character is
        // not under our control anyway — standing still then is expected, not stuck.
        if (conditions[ConditionFlag.Casting]
            || conditions[ConditionFlag.Casting87]
            || conditions[ConditionFlag.BetweenAreas]
            || conditions[ConditionFlag.Occupied]
            || conditions[ConditionFlag.OccupiedInQuestEvent]
            || conditions[ConditionFlag.Unconscious]
            || ECommonsPlayer.IsJumping)
        {
            movedAtUtc = DateTime.MinValue;
            return;
        }

        Vector3 position = player.Position;

        // 2D: falling is vertical-only and cannot be helped by jumping, so it must not read as
        // progress — and a snag against geometry stops horizontal movement specifically.
        if (movedAtUtc == DateTime.MinValue || position.Distance2D(lastPosition) > ProgressThreshold)
        {
            lastPosition = position;
            movedAtUtc = DateTime.UtcNow;
            return;
        }

        if (DateTime.UtcNow - movedAtUtc < TimeSpan.FromSeconds(config.JumpWhenStuckSeconds)
            || !EzThrottler.Throttle("StuckJumpAssist::Jump", RetryThrottleMs))
        {
            return;
        }

        logger.Info(
            "Stuck at {Pos:F0} with vnav still running — jumping to break free",
            position);
        Actions.Jump.Cast();

        // Give the hop a chance to land before judging progress again.
        movedAtUtc = DateTime.UtcNow;
    }
}
