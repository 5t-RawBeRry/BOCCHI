using BOCCHI.Buff.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Extensions;
using Dalamud.Plugin.Services;
using Lumina.Extensions;
namespace BOCCHI.Buff.Services;

public class BuffProvider
(
    IObjectTable objects,
    BuffConfig config,
    ISupportJobFactory supportJobs
) : IBuffProvider
{
    public IEnumerable<BuffData> GetBuffs() => BuffData.All;

    public BuffData GetBuffForState(BuffState state)
    {
        BuffData? buff = GetBuffs().FirstOrNull(b => b.State == state);
        if (buff == null)
        {
            throw new ArgumentOutOfRangeException();
        }

        return buff.Value;
    }

    public bool ShouldRefreshAny() => GetBuffs().Any(ShouldRefreshBuff);

    private bool CanRefreshBuff(BuffData buff)
    {
        SupportJob job = supportJobs.Create(buff.SupportJobId);

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
