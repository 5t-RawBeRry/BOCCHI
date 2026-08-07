namespace BOCCHI.Common.Data.OccultCrescent;

/// <summary>
///     Phantom combat buff/debuff/trait status IDs (ported from WrathCombo OC helper).
///     Job-identity statuses stay on <see cref="SupportJobs.SupportJob.StatusId"/>.
/// </summary>
public static class PhantomBuffs
{
    public const ushort PhantomGuard = 4231;
    public const ushort Pray = 4232;
    public const ushort EnduringFortitude = 4233;
    public const ushort Pledge = 4234;
    public const ushort Rage = 4235;
    public const ushort PentupRage = 4236;
    public const ushort PhantomKick = 4237;
    public const ushort Counterstance = 4238;
    public const ushort Fleetfooted = 4239;
    public const ushort PhantomAim = 4240;
    public const ushort OccultUnicorn = 4243;
    public const ushort RomeosBallad = 4244;
    public const ushort Shirahadori = 4245;
    public const ushort MightyMarch = 4246;
    public const ushort OffensiveAria = 4247;
    public const ushort HerosRime = 4249;
    public const ushort BattleBell = 4251;
    public const ushort BattlesClangor = 4252;
    public const ushort BlessedRain = 4253;
    public const ushort MistyMirage = 4254;
    public const ushort HastyMirage = 4255;
    public const ushort AetherialGain = 4256;
    public const ushort RingingRespite = 4257;
    public const ushort Suspend = 4258;
    public const ushort OccultQuick = 4260;
    public const ushort OccultSprint = 4261;
    public const ushort OccultSwift = 4262;
    public const ushort PredictionOfJudgment = 4265;
    public const ushort PredictionOfCleansing = 4266;
    public const ushort PredictionOfBlessing = 4267;
    public const ushort PredictionOfStarfall = 4268;
    public const ushort Recuperation = 4271;
    public const ushort FortifiedRecuperation = 4272;
    public const ushort PhantomDoom = 4273;
    public const ushort PhantomRejuvenation = 4274;
    public const ushort Invulnerability = 4275;
    public const ushort Vigilance = 4277;
    public const ushort CloudyCaress = 4280;
    public const ushort BlazingSpellblade = 4790;
    public const ushort FinishingFervor = 4793;
    public const ushort PoisedToSwordDance = 4794;
    public const ushort TemptedToTango = 4795;
    public const ushort Jitterbugged = 4796;
    public const ushort WillingToWaltz = 4797;
    public const ushort Quickstep = 4798;
    /// <summary>Knowledge-crystal party buff from Dancer Quickstep / Freelancer Inquiring Mind (30m).</summary>
    public const ushort QuickerStep = 4799;
    public const ushort SteadfastStance = 4800;
    public const ushort OccultBlink = 5316;
    public const ushort JumpVulnerabilityDown = 5318;
    public const ushort Lance = 5319;
    public const ushort EarthenWall = 5320;
    public const ushort OccultMightyGuard = 5321;
    public const ushort DrainTouch = 5326;
    public const ushort Smoke = 5327;
    public const ushort Dualcast = 5438;
}

public static class PhantomDebuffs
{
    public const ushort Blind = 15;
    public const ushort Paralysis = 17;
    public const ushort Slow = 3493;
    public const ushort OccultMageMasher = 4259;
    public const ushort SilverSickness = 4264;
    public const ushort FalsePrediction = 4269;
    public const ushort WeaponPilfered = 4279;
    public const ushort OccultToad = 5317;
    public const ushort FireWeakness = 5322;
    public const ushort IceWeakness = 5323;
    public const ushort LightningWeakness = 5324;
    public const ushort WindWeakness = 5325;
}

public static class PhantomTraits
{
    public const ushort EnhancedPhantomGuard = 0;
    public const ushort EnhancedPray = 1;
    public const ushort EnhancedPhantomKick = 2;
    public const ushort EnhancedPhantomKickII = 3;
    public const ushort Lockpicker = 4;
    public const ushort EnhancedRage = 5;
    public const ushort EnhancedPhantomAim = 6;
    public const ushort EnhancedPhantomAimII = 7;
    public const ushort EnhancedVocals = 8;
    public const ushort EnhancedPhantomFire = 9;
    public const ushort EnhancedIainuki = 10;
    public const ushort EnhancedBell = 11;
}

public static class PhantomItems
{
    public const ushort OccultPotion = 47741;
    public const ushort OccultElixir = 47743;
}

/// <summary>Common player statuses used by Occult automation (not phantom-job exclusives).</summary>
public static class PlayerStatuses
{
    /// <summary>Pending raise prompt on a corpse — skip these for Triage Mode.</summary>
    public const ushort Raise = 148;
}
