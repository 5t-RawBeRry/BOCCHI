using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
namespace BOCCHI.Common.Services;

public static unsafe class OccultCrescentHelper
{
    public static OccultCrescentState* GetState() => PublicContentOccultCrescent.GetState();

    public static bool IsStateAvailable()
    {
        PublicContentOccultCrescent* instance = PublicContentOccultCrescent.GetInstance();
        return instance != null && instance->StateLoaded && GetState() != null;
    }

    public static uint GetTotalSupportJobExperience()
    {
        OccultCrescentState* state = GetState();
        if (state == null)
        {
            return 0;
        }

        uint total = 0;
        foreach(uint jobExp in state->SupportJobExperience)
        {
            total += jobExp;
        }

        return total;
    }

    public static ushort GetSilver()
    {
        OccultCrescentState* state = GetState();
        return state == null ? (ushort)0 : state->Silver;
    }

    public static ushort GetGold()
    {
        OccultCrescentState* state = GetState();
        return state == null ? (ushort)0 : state->Gold;
    }
}
