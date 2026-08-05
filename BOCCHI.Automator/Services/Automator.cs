using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services.Goals;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.Translation;
using Ocelot.States;
using Ocelot.Windows;
using System.Numerics;

namespace BOCCHI.Automator.Services;

public class Automator
(
    IAutomatorMemory memory,
    Func<IStateMachine<AutomatorState>> stateMachineFactory,
    IPathCalculator calculator,
    IGoalValidator validator,
    IAutomatorContext context,
    IChainManager manager,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    IZoneProvider zones,
    IObjectTable objects,
    IChatGui chat,
    PotsConfig potsConfig,
    AutomatorConfig automatorConfig,
    UIConfig uiConfig,
    AutoRotationController autoRotation,
    IAutomationModeGuard modeGuard,
    ITranslator<MainWindow> translator,
    ILogger<Automator> logger
) : IAutomator, IOnUpdate, IOnStop
{
    private IStateMachine<AutomatorState>? stateMachine;

    private bool pausedOutsideZone;

    private IStateMachine<AutomatorState> StateMachine => stateMachine ??= stateMachineFactory();

    public bool Enabled => context.IsIllegalMode;

    public bool IsIllegalMode => context.IsIllegalMode;

    public bool IsActive => context.Enabled;

    public bool IsPotsAndTreasure => context.IsPotsAndTreasure;

    public bool SuspendedForTreasure { get; private set; }

    public AutomatorState? CurrentState =>
        IsActive && !pausedOutsideZone && !SuspendedForTreasure ? StateMachine.State : null;

    public void OnStop() => StopAutomation();

    public void SetSuspendedForTreasure(bool suspended)
    {
        if (SuspendedForTreasure == suspended)
        {
            return;
        }

        SuspendedForTreasure = suspended;
        if (!suspended)
        {
            return;
        }

        // Soft-stop movement so Treasure Hunt can own vnav; keep GoalMemory if any.
        memory.Forget<GoalPathStepMemory>();
        memory.Forget<WaitingForCriticalEncounterMemory>();
        memory.Forget<WaitingForPotFateMemory>();
        manager.CancelWhere(name => name.StartsWith("PathStep::", StringComparison.Ordinal));
        pathfinder.Stop();
        vnav.Stop();
        autoRotation.DisableForTravel();
    }

    public void Toggle()
    {
        bool turningOn = !context.IsIllegalMode;
        if (turningOn)
        {
            modeGuard.EnsureExclusive(AutomationMode.IllegalMode);
        }

        context.SetRunMode(turningOn ? AutomatorRunMode.IllegalMode : AutomatorRunMode.Off);
        chat.Print(BocchiChat.Format(translator.T(Enabled ? ".automation.automator.illegal_mode_on" : ".automation.automator.illegal_mode_off"), uiConfig));
        ApplyRunModeSideEffects(turningOn);
    }

    public void TogglePotsAndTreasure()
    {
        bool turningOn = !context.IsPotsAndTreasure;
        // Exclusivity is handled by PotsTreasureService before this is called when turning on.
        if (turningOn && context.IsIllegalMode)
        {
            StopAutomation();
        }

        context.SetRunMode(turningOn ? AutomatorRunMode.PotsAndTreasure : AutomatorRunMode.Off);
        chat.Print(BocchiChat.Format(translator.T(turningOn
            ? ".automation.pots_treasure.on"
            : ".automation.pots_treasure.off"), uiConfig));
        ApplyRunModeSideEffects(turningOn);
    }

    private void ApplyRunModeSideEffects(bool turningOn)
    {
        SuspendedForTreasure = false;
        if (!turningOn)
        {
            StopAutomation();
            return;
        }

        memory.Forget<NavigationInterruptedMemory>();
        pausedOutsideZone = false;
        autoRotation.PrepareForIllegalMode();
    }

    public void RefreshPathfinding()
    {
        if (!IsActive || pausedOutsideZone || SuspendedForTreasure)
        {
            return;
        }

        logger.Info("Refreshing pathfinding from current position");
        memory.Forget<NavigationInterruptedMemory>();
        memory.Forget<GoalPathStepMemory>();
        memory.Forget<WaitingForCriticalEncounterMemory>();
        memory.Forget<WaitingForPotFateMemory>();
        manager.CancelWhere(name => name.StartsWith("PathStep::", StringComparison.Ordinal));
        pathfinder.Stop();
        vnav.Stop();

        // GoalMemory kept — Update() will rebuild GoalPathStepMemory from here.
        if (!memory.TryRemember<GoalMemory>(out GoalMemory _))
        {
            chat.Print(BocchiChat.Format(translator.T(".automation.automator.pathfinding_refreshed_no_goal"), uiConfig));
            return;
        }

        chat.Print(BocchiChat.Format(translator.T(".automation.automator.pathfinding_refreshed"), uiConfig));
    }

    public void Render()
    {
        if (!IsActive || pausedOutsideZone || SuspendedForTreasure)
        {
            return;
        }

        StateMachine.Render();
    }

    public void Update()
    {
        if (!IsActive || SuspendedForTreasure)
        {
            return;
        }

        // Zone lock: never cast Return / path outside Occult Crescent (PvP, cities, etc.).
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            if (!pausedOutsideZone)
            {
                logger.Info("Left Occult Crescent — pausing automator (zone lock)");
                PauseOutsideZone();
                pausedOutsideZone = true;
            }

            return;
        }

        if (pausedOutsideZone)
        {
            pausedOutsideZone = false;
            logger.Info("Re-entered Occult Crescent — automator resumed");
        }

        autoRotation.Tick();

        // Mid-route cancel (vnav stop / emergency) — don't replan until mode is toggled.
        if (memory.TryRemember<NavigationInterruptedMemory>(out NavigationInterruptedMemory _))
        {
            StateMachine.Update();
            return;
        }

        if (memory.TryRemember<GoalMemory>(out GoalMemory goal))
        {
            if (!validator.Validate(goal.Goal))
            {
                if (goal.Goal.GoalType is FateGoal fateGoal)
                {
                    TryStartPotChestFarm(fateGoal.id);
                }

                logger.Info("Goal no longer valid — aborting pathfinding");
                memory.Forget<GoalMemory>();
                memory.Forget<GoalPathStepMemory>();
                memory.Forget<WaitingForCriticalEncounterMemory>();
                memory.Forget<WaitingForPotFateMemory>();
                manager.CancelWhere(name => name.StartsWith("PathStep::", StringComparison.Ordinal));
                pathfinder.Stop();
                vnav.Stop();
            }
            else if (!memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _)
                     && !memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory _)
                     && !memory.TryRemember<WaitingForPotFateMemory>(out WaitingForPotFateMemory _)
                     && !memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _))
            {
                memory.TryAdd(new GoalPathStepMemory(goal.Goal, calculator, automatorConfig.StopAfterReturn));
            }
        }

        StateMachine.Update();
    }

    private void PauseOutsideZone() => StopAutomation(resetPausedFlag: false);

    private void StopAutomation(bool resetPausedFlag = true)
    {
        if (resetPausedFlag)
        {
            pausedOutsideZone = false;
        }

        SuspendedForTreasure = false;
        memory.Wipe();
        manager.CancelAll();
        pathfinder.Stop();
        vnav.Stop();
        if (resetPausedFlag)
        {
            // Mode off / plugin stop — delete ephemeral BOCCHI AI preset.
            autoRotation.TeardownForIllegalMode();
        }
        else
        {
            // Zone pause — deactivate only; recreate isn't needed until activity resumes.
            autoRotation.DisableForTravel();
        }

        if (stateMachine != null)
        {
            StateMachine.Reset();
        }
    }

    private void TryStartPotChestFarm(FateId fateId)
    {
        bool farmChests = automatorConfig.ShouldFarmPotChests || context.IsPotsAndTreasure;
        if (!farmChests || memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _))
        {
            return;
        }

        IZone zone = zones.GetZone();
        if (!zone.IsPotFate(fateId.Value))
        {
            return;
        }

        if (!zone.GetPotChestData().TryGetValue(fateId.Value, out List<PotChestData>? chestData))
        {
            return;
        }

        List<Vector3> positions = chestData.Select(chest => chest.Position).ToList();
        if (context.IsPotsAndTreasure || potsConfig.ShouldFarmRerollPotChests)
        {
            positions.AddRange(zone.GetRerollPotChestData().Select(chest => chest.Position));
        }

        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        positions = positions
            .OrderBy(position => player.Position.Distance(position))
            .ToList();

        if (positions.Count == 0)
        {
            return;
        }

        logger.Info("Starting pot chest farm for fate {FateId} with {Count} chest positions", fateId.Value, positions.Count);
        memory.TryAdd(new PotChestFarmMemory(fateId, positions));
    }
}
