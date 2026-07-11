using BOCCHI.Buff.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Extensions;
using Dalamud.Plugin.Services;
using Lumina.Extensions;
using Ocelot.Services.PlayerState;

namespace BOCCHI.Buff.Services;

public class BuffProvider(
    IObjectTable objects,
    BuffConfig config,
    ISupportJobFactory supportJobs
) : IBuffProvider
{
    public IEnumerable<BuffData> GetBuffs()
    {
        return BuffData.All;
    }

    public BuffData GetBuffForState(BuffState state)
    {
        var buff = GetBuffs().FirstOrNull(b => b.State == state);
        if (buff == null)
        {
            throw new ArgumentOutOfRangeException();
        }

        return buff.Value;
    }

    public bool ShouldRefreshAny()
    {
        return GetBuffs().Any(ShouldRefreshBuff);
    }

    private bool CanRefreshBuff(BuffData buff)
    {
        var job = supportJobs.Create(buff.SupportJobId);

        return job.Level >= buff.RequiredLevel;
    }

    private bool ShouldRefreshBuff(BuffData buff)
    {
        if (objects.LocalPlayer is not { } player)
        {
            return false;
        }

        return buff.ShouldApply(config)
            && CanRefreshBuff(buff)
            && player.GetRemainingMinutes(buff.StatusId) <= config.ReapplyThreshold;
    }
}
