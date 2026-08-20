using BOCCHI.Automator.Services;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using BOCCHI.Treasure.Services;
using Ocelot.Lifecycle;

namespace BOCCHI.Services;

/// <summary>
///     Yields Mob Farmer to pots, Treasure Hunt, or knowledge-crystal buffs, then resumes.
/// </summary>
public sealed class MobFarmerYieldService
(
    IMobFarmer farmer,
    IPotsTreasureMode pots,
    ITreasureHunter hunter,
    ITreasureTracker tracker,
    IBuffRunner buffRunner,
    IBuffProvider buffs,
    IPotCycleTracker potCycle,
    IFateRepository fates,
    IZoneProvider zones,
    MobFarmerConfig farmerConfig,
    PotsConfig potsConfig,
    BuffConfig buffConfig,
    TreasureConfig treasureConfig
) : IOnUpdate
{
    public int Order => -10;

    private DateTimeOffset nextHuntAt = DateTimeOffset.MinValue;

    private bool startedPots;

    private bool startedHunt;

    private bool startedBuffs;

    private bool sawRunning;

    public void Update()
    {
        if (!farmer.Running)
        {
            sawRunning = false;
            AbortYields();
            return;
        }

        if (!sawRunning)
        {
            sawRunning = true;
            nextHuntAt = DateTimeOffset.UtcNow
                         + TimeSpan.FromMinutes(Math.Max(1, farmerConfig.TreasureHuntIntervalMinutes));
        }

        if (farmer.Suspended)
        {
            TickSuspended();
            return;
        }

        if (!farmer.CanAcceptYield)
        {
            return;
        }

        if (farmerConfig.YieldToPots && NeedsPotWork())
        {
            farmer.SetSuspended(true, FarmerYieldReason.Pots);
            if (pots.StartManagedFromFarmer())
            {
                startedPots = true;
            }
            else
            {
                farmer.SetSuspended(false);
            }

            return;
        }

        if (farmerConfig.YieldToCrystalBuffs && buffConfig.ShouldAutomateBuffs && buffs.ShouldRefreshAny())
        {
            if (!buffRunner.CanStart)
            {
                return;
            }

            farmer.SetSuspended(true, FarmerYieldReason.CrystalBuffs);
            buffRunner.Start();
            startedBuffs = true;
            return;
        }

        if (farmerConfig.YieldToTreasureHunt && HuntIsDue())
        {
            farmer.SetSuspended(true, FarmerYieldReason.TreasureHunt);
            hunter.ManagedByMobFarmer = true;
            hunter.StartManaged();
            startedHunt = hunter.Running;
            if (!startedHunt)
            {
                hunter.ManagedByMobFarmer = false;
                farmer.SetSuspended(false);
                return;
            }

            nextHuntAt = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(farmerConfig.TreasureHuntIntervalMinutes);
        }
    }

    private void TickSuspended()
    {
        switch (farmer.YieldReason)
        {
            case FarmerYieldReason.Pots:
                if (startedPots && !pots.ManagedByMobFarmer)
                {
                    startedPots = false;
                    farmer.SetSuspended(false);
                }

                break;

            case FarmerYieldReason.TreasureHunt:
                if (startedHunt && (!hunter.Running || !hunter.ManagedByMobFarmer))
                {
                    startedHunt = false;
                    if (farmer.Suspended)
                    {
                        farmer.SetSuspended(false);
                    }
                }

                break;

            case FarmerYieldReason.CrystalBuffs:
                if (startedBuffs && !buffRunner.IsRunning)
                {
                    startedBuffs = false;
                    farmer.SetSuspended(false);
                }

                break;
        }
    }

    private void AbortYields()
    {
        nextHuntAt = DateTimeOffset.MinValue;
        if (startedPots)
        {
            startedPots = false;
            pots.StopManagedFromFarmer();
        }

        if (startedHunt)
        {
            startedHunt = false;
            if (hunter.ManagedByMobFarmer && hunter.Running)
            {
                hunter.Toggle();
            }

            hunter.ManagedByMobFarmer = false;
        }

        if (startedBuffs)
        {
            startedBuffs = false;
            if (buffRunner.IsRunning)
            {
                buffRunner.Stop();
            }
        }
    }

    private bool NeedsPotWork()
    {
        IZone zone = zones.GetZone();
        if (fates.Snapshot().Any(f => zone.IsPotFate(f.Id.Value)))
        {
            return true;
        }

        PotCycleSnapshot cycle = potCycle.Snapshot;
        if (cycle.CurrentActivePotFateId != 0)
        {
            return true;
        }

        if (!cycle.HasPredictedNextPot)
        {
            return false;
        }

        return PotFallbackWindow.ShouldPreposition(
            cycle,
            DateTimeOffset.UtcNow,
            potsConfig.PotSpawnLeadMinutes,
            potFarmingEnabled: true);
    }

    private bool HuntIsDue()
    {
        if (DateTimeOffset.UtcNow < nextHuntAt)
        {
            return false;
        }

        if (!hunter.IsVnavAvailable || !hunter.IsVnavReady)
        {
            return false;
        }

        if (!tracker.CountInitialised)
        {
            return false;
        }

        float bronzePct = tracker.BronzeChests / 30f * 100f;
        float silverPct = tracker.SilverChests / 8f * 100f;
        bool bronzeOk = bronzePct >= farmerConfig.TreasureHuntMinBronzePercent;
        bool silverOk = silverPct >= farmerConfig.TreasureHuntMinSilverPercent;
        if (treasureConfig.HuntSilverChestsOnly)
        {
            return silverOk;
        }

        return bronzeOk || silverOk;
    }
}
