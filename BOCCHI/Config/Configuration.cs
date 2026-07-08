using BOCCHI.Common.Config;
using Ocelot.Config;

namespace BOCCHI.Config;

public class Configuration : IConfiguration
{
    public const int CurrentVersion = 2;

    [ConfigHidden] public int Version { get; set; } = CurrentVersion;

    public ExperienceConfig ExperienceConfig { get; set; } = new();

    public CurrencyConfig CurrencyConfig { get; set; } = new();

    public UIConfig UIConfig { get; set; } = new();

    public AutomatorConfig AutomatorConfig { get; set; } = new();

    public BuffConfig BuffConfig { get; set; } = new();

    public CombatConfig CombatConfig { get; set; } = new();

    public MobFarmerConfig MobFarmerConfig { get; set; } = new();
}
