using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Ocelot.Extensions;

namespace BOCCHI.Treasure.Services;

/// <summary>Live foray knowledge threats that warrant Ninja Hide.</summary>
public static class KnowledgeThreat
{
    public const uint OccultIsleblazerBaseId = 17900;

    public const float IsleblazerUnhideDistance = 5f;

    /// <summary>Occult Crescent Knowledge cap (North Horn / 7.55+).</summary>
    public const int MaxKnowledgeLevel = 40;

    public static unsafe int? TryGetPlayerForayLevel(IObjectTable objects)
    {
        if (objects.LocalPlayer is not { } player)
        {
            return null;
        }

        byte level = ((BattleChara*)player.Address)->ForayInfo.Level;
        return level > 0 ? level : null;
    }

    public static unsafe bool TryFindThreat(
        IObjectTable objects,
        Vector3 origin,
        int hideAtOrAbove,
        float radius,
        out IBattleNpc? threat,
        out float distance)
    {
        threat = null;
        distance = float.MaxValue;

        foreach (IGameObject obj in objects)
        {
            if (obj is not IBattleNpc battle
                || battle is { IsDead: true }
                || !battle.IsTargetable
                || !battle.IsHostile())
            {
                continue;
            }

            if (battle.BaseId == OccultIsleblazerBaseId)
            {
                continue;
            }

            byte knowledge = ((BattleChara*)battle.Address)->ForayInfo.Level;
            if (knowledge < hideAtOrAbove)
            {
                continue;
            }

            float dist = origin.Distance2D(battle.Position);
            if (dist > radius || dist >= distance)
            {
                continue;
            }

            threat = battle;
            distance = dist;
        }

        return threat != null;
    }

    public static bool TryFindIsleblazer(IObjectTable objects, Vector3 origin, float radius, out float distance)
    {
        distance = float.MaxValue;
        bool found = false;

        foreach (IGameObject obj in objects)
        {
            if (obj is not IBattleNpc battle
                || battle.BaseId != OccultIsleblazerBaseId
                || battle.IsDead
                || !battle.IsTargetable)
            {
                continue;
            }

            float dist = origin.Distance2D(battle.Position);
            if (dist > radius || dist >= distance)
            {
                continue;
            }

            distance = dist;
            found = true;
        }

        return found;
    }

    public static int HideAtOrAbove(int playerForayLevel, int hideOffset) =>
        Math.Clamp(playerForayLevel + hideOffset, 1, MaxKnowledgeLevel);
}
