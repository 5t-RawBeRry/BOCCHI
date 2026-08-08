using System.Runtime.InteropServices;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
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
///     If the player uses a combat action while pathfinding, stop movement
///     (manual World Path and Illegal Mode travel).
/// </summary>
public sealed unsafe class CombatPathfindCancelService
(
    IGameInteropProvider interop,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    IChainManager chains,
    IAutomatorMemory memory,
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
        if (actionType != ActionType.Action)
        {
            return false;
        }

        // General Sprint / mounts / items come through other ActionTypes.
        // Skip empty / invalid.
        if (actionId == 0)
        {
            return false;
        }

        PathfindingState state = pathfinder.GetState();
        return state is PathfindingState.Moving or PathfindingState.Pathfinding
               || vnav.IsRunning()
               || vnav.IsPathfinding();
    }

    private void CancelPathfinding()
    {
        logger.Info("Combat action used — canceling pathfinding");
        pathfinder.Stop();
        vnav.Stop();
        chains.CancelWhere(name =>
            name.StartsWith("ActivityGoto::", StringComparison.Ordinal)
            || name.StartsWith("PathStep::", StringComparison.Ordinal));

        // Soft-pause Illegal Mode travel until the user toggles it again.
        if (memory.TryRemember<GoalPathStepMemory>(out _)
            || memory.TryRemember<GoalMemory>(out _))
        {
            memory.Forget<GoalPathStepMemory>();
            memory.Forget<GoalMemory>();
            memory.Forget<BaseTeleportDelayMemory>();
            memory.TryAdd<NavigationInterruptedMemory>();
        }
    }
}
