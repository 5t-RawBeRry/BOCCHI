using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Services.Commands;
using Ocelot.Services.PlayerState;
using Ocelot.Services.Translation;
using System.Numerics;

namespace BOCCHI.Commands;

public class TeleportCommand
(
    IActivityNavigation navigation,
    ICriticalEncounterRepository criticalEncounters,
    IFateRepository fates,
    IZoneProvider zones,
    IPlayer player,
    IChatGui chat,
    ITranslator<TeleportCommand> translator
) : OcelotCommand(translator)
{
    public override string Command => "tp";

    public override List<string> Aliases => ["teleport"];

    public override void Execute(CommandContext context)
    {
        string mode = context.Args.Length > 0 ? context.Args[0].ToLowerInvariant() : "";

        Vector3? destination = mode switch
        {
            "fate" => GetFateDestination(pots: false),
            "pot" => GetFateDestination(pots: true),
            "ce" => GetCriticalEncounterDestination(),
            "" => GetCriticalEncounterDestination()
                  ?? GetFateDestination(pots: false)
                  ?? GetFateDestination(pots: true),
            _ => null,
        };

        if (mode is not ("" or "fate" or "pot" or "ce"))
        {
            chat.PrintError("Usage: /bocchi tp [fate|ce|pot]");
            return;
        }

        if (destination is not { } target)
        {
            chat.PrintError("No matching fate/CE found.");
            return;
        }

        if (!navigation.CanTeleport(target, out string? reason))
        {
            chat.PrintError(reason ?? "Cannot teleport.");
            return;
        }

        navigation.TeleportToward(target, "Slash teleport", "slash_tp");
        chat.Print("Teleporting…");
    }

    private Vector3? GetFateDestination(bool pots)
    {
        IZone zone = zones.GetZone();
        Fate? match = fates.Snapshot()
            .Where(f => pots == zone.IsPotFate(f.Id.Value))
            .OrderBy(f => Vector3.DistanceSquared(player.Position, f.Position))
            .FirstOrDefault();

        return match?.Position;
    }

    private Vector3? GetCriticalEncounterDestination()
    {
        CriticalEncounter? match = criticalEncounters.SnapshotWithoutForkedTower()
            .Where(ce => ce.State is DynamicEventState.Register or DynamicEventState.Warmup)
            .Where(ce => !float.IsNaN(ce.Position.X))
            .OrderBy(ce => Vector3.DistanceSquared(player.Position, ce.Position))
            .FirstOrDefault();

        return match?.Position;
    }
}
