using BOCCHI.Treasure.Data;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Numerics;
using XIVTreasure = Lumina.Excel.Sheets.Treasure;
using TreasureFlags = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags;

namespace BOCCHI.Treasure.Data
{
    public enum CofferType
    {
        Unknown,
        Bronze,
        Silver
    }

    public static class TreasureColors
    {
        public static readonly Vector4 Bronze = new(0.72f, 0.45f, 0.20f, 1f);

        public static readonly Vector4 Silver = new(0.82f, 0.84f, 0.88f, 1f);

        public static readonly Vector4 Unknown = new(0.6f, 0.2f, 0.8f, 1f);
    }
}

namespace BOCCHI.Treasure.Services
{
    public class TreasureCoffer(IGameObject obj, IDataManager data)
    {
        private TreasureFlags lastFlags = TreasureFlags.None;

        /// <summary>Treasure sheet row (shared by every bronze/silver of that type).</summary>
        public uint Id => obj.BaseId;

        /// <summary>Unique live instance id — use this to track multiple coffers of the same type.</summary>
        public ulong GameObjectId => obj.GameObjectId;

        public unsafe bool CheckOpened()
        {
            GameObject* gameObject = (GameObject*)(void*)obj.Address;
            if (gameObject == null)
            {
                return false;
            }

            FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure* instance = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;
            TreasureFlags currentFlags = instance->Flags;

            if (currentFlags == lastFlags)
            {
                return false;
            }

            bool wasNotOpened = !lastFlags.HasFlag(TreasureFlags.Opened);
            bool isNowOpened = currentFlags.HasFlag(TreasureFlags.Opened);
            lastFlags = currentFlags;

            return wasNotOpened && isNowOpened;
        }

        // Do not require IsTargetable — coffers often stay non-targetable until you are close,
        // and radar / nearby list should still show them (same as Umbra object-table markers).
        public bool IsValid() => obj.IsValid() && obj is { IsDead: false };

        public Vector3 GetPosition() => obj.Position;

        private uint? GetModelId()
        {
            // Some ObjectKind.Treasure entries use BaseIds outside the Treasure sheet
            // (e.g. 2007457) — GetRow throws ArgumentOutOfRangeException.
            if (!data.GetExcelSheet<XIVTreasure>().TryGetRow(obj.BaseId, out XIVTreasure row))
            {
                return null;
            }

            return row.SGB.RowId;
        }

        public CofferType GetCofferType()
        {
            return GetModelId() switch
            {
                1597 => CofferType.Silver,
                1596 => CofferType.Bronze,
                var _ => CofferType.Unknown
            };
        }

        public Vector4 GetColor()
        {
            return GetCofferType() switch
            {
                CofferType.Bronze => TreasureColors.Bronze,
                CofferType.Silver => TreasureColors.Silver,
                var _ => TreasureColors.Unknown
            };
        }

        public string GetName()
        {
            return GetCofferType() switch
            {
                CofferType.Bronze => "Bronze Treasure Coffer",
                CofferType.Silver => "Silver Treasure Coffer",
                var _ => "Unknown Treasure Coffer"
            };
        }
    }
}
