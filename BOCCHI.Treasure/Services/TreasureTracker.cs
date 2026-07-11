using System.Text.RegularExpressions;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Data;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ocelot.Extensions;
using Ocelot.Lifecycle;
using Ocelot.Services.PlayerState;

namespace BOCCHI.Treasure.Services;

public class TreasureTracker : ITreasureTracker, IOnUpdate, IDisposable
{
    private const uint ActiveChestLogMessageId = 10965;

    private readonly IObjectTable objects;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly IDataManager data;
    private readonly IZoneProvider zones;
    private readonly IPlayer player;
    private readonly TimeSpan parseWideTextCooldown = TimeSpan.FromSeconds(5);

    private DateTime lastParseWideText = DateTime.MinValue;
    private List<TreasureCoffer> treasures = [];

    public TreasureTracker(
        IObjectTable objects,
        IAddonLifecycle addonLifecycle,
        IDataManager data,
        IZoneProvider zones,
        IPlayer player
    )
    {
        this.objects = objects;
        this.addonLifecycle = addonLifecycle;
        this.data = data;
        this.zones = zones;
        this.player = player;
        addonLifecycle.RegisterListener(AddonEvent.PostDraw, "_WideText", OnWideTextPostDraw);
    }

    public IReadOnlyList<TreasureCoffer> Treasures => treasures;

    public bool CountInitialised { get; private set; }

    public int BronzeChests { get; private set; }

    public int SilverChests { get; private set; }

    public void Update()
    {
        var worldTreasures = objects
            .Where(o => o is { ObjectKind: ObjectKind.Treasure })
            .ToDictionary(o => o.BaseId, o => o);

        var knownIds = treasures.Select(t => t.Id).ToHashSet();

        for (var i = treasures.Count - 1; i >= 0; i--)
        {
            var treasure = treasures[i];
            if (!worldTreasures.ContainsKey(treasure.Id) || !treasure.IsValid())
            {
                treasures.RemoveAt(i);
            }
        }

        foreach (var (objectId, obj) in worldTreasures)
        {
            if (knownIds.Contains(objectId))
            {
                continue;
            }

            var treasure = new TreasureCoffer(obj, data);
            if (treasure.IsValid())
            {
                treasures.Add(treasure);
            }
        }

        treasures = treasures.OrderBy(t => player.Position.Distance(t.GetPosition())).ToList();

        foreach (var treasure in treasures)
        {
            if (!treasure.CheckOpened())
            {
                continue;
            }

            if (treasure.GetCofferType() == CofferType.Bronze)
            {
                BronzeChests = Math.Max(0, BronzeChests - 1);
            }
            else if (treasure.GetCofferType() == CofferType.Silver)
            {
                SilverChests = Math.Max(0, SilverChests - 1);
            }
        }
    }

    private unsafe void OnWideTextPostDraw(AddonEvent type, AddonArgs args)
    {
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (!addon->IsVisible)
        {
            return;
        }

        if (DateTime.Now - lastParseWideText < parseWideTextCooldown)
        {
            return;
        }

        lastParseWideText = DateTime.Now;

        var pattern = LogMessageHelper.GetLogMessagePattern(data, ActiveChestLogMessageId);
        var text = addon->GetNodeById(3)->GetAsAtkTextNode()->NodeText.ToString();
        var match = Regex.Match(text, pattern);
        if (!match.Success)
        {
            return;
        }

        SilverChests = int.Parse(match.Groups[1].Value);
        BronzeChests = int.Parse(match.Groups[2].Value);
        CountInitialised = true;
    }

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(AddonEvent.PostDraw, "_WideText", OnWideTextPostDraw);
    }
}
