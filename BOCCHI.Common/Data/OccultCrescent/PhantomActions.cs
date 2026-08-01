namespace BOCCHI.Common.Data.OccultCrescent;

/// <summary>
///     Phantom duty-action sheet IDs for Occult Crescent (ported from WrathCombo OC helper).
///     Hotbar presses still go through <c>Actions.PhantomActionI–V</c>; these identify
///     which duty action is equipped in a slot.
/// </summary>
public static class PhantomActions
{
    // Freelancer
    public const uint OccultResuscitation = 41650;
    public const uint OccultTreasuresight = 41651;

    // Knight
    public const uint PhantomGuard = 41588;
    public const uint Pray = 41589;
    public const uint OccultHeal = 41590;
    public const uint Pledge = 41591;

    // Berserker
    public const uint Rage = 41592;
    public const uint DeadlyBlow = 41594;

    // Monk
    public const uint PhantomKick = 41595;
    public const uint OccultCounter = 41596;
    public const uint Counterstance = 41597;
    public const uint OccultChakra = 41598;

    // Ranger
    public const uint PhantomAim = 41599;
    public const uint OccultFeatherfoot = 41600;
    public const uint OccultFalcon = 41601;
    public const uint OccultUnicorn = 41602;

    // Samurai
    public const uint Mineuchi = 41603;
    public const uint Shirahadori = 41604;
    public const uint Iainuki = 41605;
    public const uint Zeninage = 41606;

    // Bard
    public const uint MightyMarch = 41607;
    public const uint OffensiveAria = 41608;
    public const uint RomeosBallad = 41609;
    public const uint HerosRime = 41610;

    // Geomancer
    public const uint BattleBell = 41611;
    public const uint Weather = 41612;
    public const uint Sunbath = 41613;
    public const uint CloudyCaress = 41614;
    public const uint BlessedRain = 41615;
    public const uint MistyMirage = 41616;
    public const uint HastyMirage = 41617;
    public const uint AetherialGain = 41618;
    public const uint RingingRespite = 41619;
    public const uint Suspend = 41620;

    // Time Mage
    public const uint OccultSlowga = 41621;
    public const uint OccultDispel = 41622;
    public const uint OccultComet = 41623;
    public const uint OccultMageMasher = 41624;
    public const uint OccultQuick = 41625;

    // Cannoneer
    public const uint PhantomFire = 41626;
    public const uint HolyCannon = 41627;
    public const uint DarkCannon = 41628;
    public const uint ShockCannon = 41629;
    public const uint SilverCannon = 41630;

    // Chemist
    public const uint OccultPotion = 41631;
    public const uint OccultEther = 41633;
    public const uint Revive = 41634;
    public const uint OccultElixir = 41635;

    // Oracle
    public const uint Predict = 41636;
    public const uint PhantomJudgment = 41637;
    public const uint Cleansing = 41638;
    public const uint Blessing = 41639;
    public const uint Starfall = 41640;
    public const uint Recuperation = 41641;
    public const uint PhantomDoom = 41642;
    public const uint PhantomRejuvenation = 41643;
    public const uint Invulnerability = 41644;

    // Thief
    public const uint Steal = 41645;
    public const uint OccultSprint = 41646;
    public const uint Vigilance = 41647;
    public const uint TrapDetection = 41648;
    public const uint PilferWeapon = 41649;

    // Mystic Knight (7.4)
    public const uint MagicShell = 46590;
    public const uint SunderingSpellblade = 46591;
    public const uint HolySpellblade = 46592;
    public const uint BlazingSpellblade = 46593;

    // Gladiator (7.4)
    public const uint Finisher = 46594;
    public const uint Defend = 46595;
    public const uint LongReach = 46596;
    public const uint BladeBlitz = 46597;

    // Dancer (7.4)
    public const uint Dance = 46598;
    public const uint PoisedToSwordDance = 46599;
    public const uint TemptedToTango = 46600;
    public const uint Jitterbug = 46601;
    public const uint WillingToWaltz = 46602;
    public const uint Quickstep = 46603;
    public const uint SteadfastStance = 46604;
    public const uint Mesmerize = 46605;

    // Ninja (7.55)
    public const uint FumaShuriken = 49062;
    public const uint Smoke = 49063;
    public const uint LightningScroll = 49064;
    public const uint FlameScroll = 49065;
    public const uint Image = 49066;

    // White Mage (7.55)
    public const uint OccultCureII = 49067;
    public const uint OccultCureIII = 49068;
    public const uint OccultBlink = 49069;
    public const uint OccultRaise = 49070;
    public const uint OccultHoly = 49071;

    // Black Mage (7.55)
    public const uint OccultFireIII = 49072;
    public const uint OccultBlizzardIII = 49073;
    public const uint OccultThunderIII = 49074;
    public const uint OccultToad = 49075;
    public const uint OccultFlare = 49076;

    // Dragoon (7.55)
    public const uint OccultJump = 49077;
    public const uint StepForth = 49078;
    public const uint Lance = 49079;

    // Summoner (7.55)
    public const uint Hellfire = 49080;
    public const uint JudgmentBolt = 49081;
    public const uint EarthenWall = 49082;
    public const uint Thunderstorm = 49083;
    public const uint Megaflare = 49084;

    // Blue Mage (7.55)
    public const uint OccultAero = 49085;
    public const uint OccultMissile = 49086;
    public const uint OccultAquaBreath = 49087;
    public const uint OccultMightyGuard = 49088;
    public const uint OccultAeroII = 49089;
    public const uint OccultWhiteWind = 49090;
    public const uint OccultAeroIII = 49091;

    // Red Mage (7.55)
    public const uint OccultFireII = 49092;
    public const uint OccultCureII_RDM = 49093;
    public const uint OccultLibra = 49094;
    public const uint OccultBlizzardII = 49095;
    public const uint OccultThunderII = 49096;

    // Necromancer (7.55)
    public const uint DrainTouch = 49097;
    public const uint DeepFreeze = 49098;
    public const uint HellWind = 49099;
    public const uint ChaosDrive = 49100;
    public const uint Doomsday = 49101;
}
