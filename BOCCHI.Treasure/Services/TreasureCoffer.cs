using System.Numerics;
using BOCCHI.Treasure.Data;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using XIVTreasure = Lumina.Excel.Sheets.Treasure;
using TreasureFlags = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags;

namespace BOCCHI.Treasure.Services;

public class TreasureCoffer(IGameObject obj, IDataManager data)
{
    public uint Id => obj.BaseId;

    private TreasureFlags lastFlags = TreasureFlags.None;

    public unsafe bool CheckOpened()
    {
        var gameObject = (GameObject*)(void*)obj.Address;
        if (gameObject == null)
        {
            return false;
        }

        var instance = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;
        var currentFlags = instance->Flags;

        if (currentFlags == lastFlags)
        {
            return false;
        }

        var wasNotOpened = !lastFlags.HasFlag(TreasureFlags.Opened);
        var isNowOpened = currentFlags.HasFlag(TreasureFlags.Opened);
        lastFlags = currentFlags;

        return wasNotOpened && isNowOpened;
    }

    public bool IsValid()
    {
        return obj.IsValid() && obj is { IsDead: false, IsTargetable: true };
    }

    public Vector3 GetPosition()
    {
        return obj.Position;
    }

    private uint? GetModelId()
    {
        return data.GetExcelSheet<XIVTreasure>().GetRow(obj.BaseId).SGB.RowId;
    }

    public CofferType GetCofferType()
    {
        return (GetModelId() ?? 0) switch
        {
            1597 => CofferType.Silver,
            1596 => CofferType.Bronze,
            _ => CofferType.Unknown,
        };
    }

    public Vector4 GetColor()
    {
        return GetCofferType() switch
        {
            CofferType.Bronze => TreasureColors.Bronze,
            CofferType.Silver => TreasureColors.Silver,
            _ => TreasureColors.Unknown,
        };
    }

    public string GetName()
    {
        return GetCofferType() switch
        {
            CofferType.Bronze => "Bronze Treasure Coffer",
            CofferType.Silver => "Silver Treasure Coffer",
            _ => "Unknown Treasure Coffer",
        };
    }
}
