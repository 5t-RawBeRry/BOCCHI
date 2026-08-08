using System.Runtime.InteropServices;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Ocelot.Actions;
using Ocelot.Chain;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Services;

/// <summary>
///     In Occult Crescent only: if the player uses a combat action while pathfinding, stop
///     BOCCHI movement (World Path / Illegal Mode). Must not touch vnav outside OC — other
///     plugins (e.g. AutoDuty) own pathfinding there.
/// </summary>
public sealed unsafe class CombatPathfindCancelService
(
    IGameInteropProvider interop,
    IClientState client,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    IChainManager chains,
    IAutomatorMemory memory,
    IAutomator automator,
    IZoneProvider zones,
    ILogger<CombatPathfindCancelService> logger
) : IOnStart, IOnStop, IDisposable
{
    private delegate bool UseActionDelegate(
        ActionManager* manager,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        bool* outOptAreaTargeted);

    private Hook<UseActionDelegate>? useActionHook;

    public void OnStart()
    {
        try
        {
            useActionHook = interop.HookFromAddress<UseActionDelegate>(
                (nint)ActionManager.MemberFunctionPointers.UseAction,
                UseActionDetour);
            useActionHook.Enable();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to hook UseAction for combat pathfind cancel");
        }
    }

    public void OnStop() => Dispose();

    public void Dispose()
    {
        useActionHook?.Disable();
        useActionHook?.Dispose();
        useActionHook = null;
    }

    private bool UseActionDetour(
        ActionManager* manager,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        bool* outOptAreaTargeted)
    {
        try
        {
            if (!ActionCastScope.IsSuppressingPathfindCancel
                && ShouldCancelPathfinding(actionType, actionId))
            {
                CancelPathfinding();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Combat pathfind cancel failed");
        }

        return useActionHook!.Original(
            manager, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
    }

    private bool ShouldCancelPathfinding(ActionType actionType, uint actionId)
    {
        // Never steal vnav from dungeon/overworld plugins (e.g. AutoDuty) (#160).
        // Territory ids are authoritative — zone map alone must not decide this hot path.
        if (!IsInOccultCrescentTerritory())
        {
            return false;
        }

        if (actionType != ActionType.Action || actionId == 0)
        {
            return false;
        }

        // Travel utility actions — not combat. General Sprint is ActionType.GeneralAction and
        // never reaches here; Occult Sprint is a normal action id (#157).
        if (actionId == PhantomActions.OccultSprint)
        {
            return false;
        }

        PathfindingState state = pathfinder.GetState();
        return state is PathfindingState.Moving or PathfindingState.Pathfinding
               || vnav.IsRunning()
               || vnav.IsPathfinding();
    }

    private bool IsInOccultCrescentTerritory()
    {
        ushort territory = (ushort)client.TerritoryType;
        if (territory is not ((ushort)ZoneId.SouthHorn or (ushort)ZoneId.NorthHorn))
        {
            return false;
        }

        // Belt-and-suspenders with the zone provider (NullZone → false).
        return zones.GetZone().IsOccultCrescentZone();
    }

    private void CancelPathfinding()
    {
        logger.Info("Combat action used — canceling pathfinding");
        pathfinder.Stop();
        vnav.Stop();
        chains.CancelWhere(name =>
            name.StartsWith("ActivityGoto::", StringComparison.Ordinal)
            || PathStepSoftStop.IsPathStepChain(name));

        // Drop the in-flight route only — keep GoalMemory so Illegal Mode can enter the
        // FATE/CE or replan after combat (#157 / #159). Soft-pause is for World Path /
        // non-automator travel (toggle to resume).
        memory.Forget<GoalPathStepMemory>();
        memory.Forget<BaseTeleportDelayMemory>();

        if (automator.IsActive)
        {
            return;
        }

        if (memory.TryRemember<GoalMemory>(out _))
        {
            memory.Forget<GoalMemory>();
            memory.TryAdd<NavigationInterruptedMemory>();
        }
    }
}
