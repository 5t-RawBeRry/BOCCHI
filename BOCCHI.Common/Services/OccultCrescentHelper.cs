using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace BOCCHI.Common.Services;

public static unsafe class OccultCrescentHelper
{
    public static OccultCrescentState* GetState()
    {
        return PublicContentOccultCrescent.GetState();
    }

    public static bool IsStateAvailable()
    {
        var instance = PublicContentOccultCrescent.GetInstance();
        return instance != null && instance->StateLoaded && GetState() != null;
    }

    public static uint GetTotalSupportJobExperience()
    {
        var state = GetState();
        if (state == null)
        {
            return 0;
        }

        uint total = 0;
        foreach (var jobExp in state->SupportJobExperience)
        {
            total += jobExp;
        }

        return total;
    }

    public static ushort GetSilver()
    {
        var state = GetState();
        return state == null ? (ushort)0 : state->Silver;
    }

    public static ushort GetGold()
    {
        var state = GetState();
        return state == null ? (ushort)0 : state->Gold;
    }
}
