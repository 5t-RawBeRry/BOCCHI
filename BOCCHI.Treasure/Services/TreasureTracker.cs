using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Data;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ocelot.Extensions;
using Ocelot.Lifecycle;
using Ocelot.Services.PlayerState;
using System.Text.RegularExpressions;

namespace BOCCHI.Treasure.Services;

public class TreasureTracker : ITreasureTracker, IOnUpdate, IDisposable
{
    private const uint ActiveChestLogMessageId = 10965;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly IDataManager data;

    private readonly IObjectTable objects;
    private readonly TimeSpan parseWideTextCooldown = TimeSpan.FromSeconds(5);
    private readonly IPlayer player;
    private readonly IZoneProvider zones;

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

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(AddonEvent.PostDraw, "_WideText", OnWideTextPostDraw);
    }

    public void Update()
    {
        Dictionary<uint, IGameObject> worldTreasures = objects
            .Where(o => o is { ObjectKind: ObjectKind.Treasure })
            .ToDictionary(o => o.BaseId, o => o);

        HashSet<uint> knownIds = treasures.Select(t => t.Id).ToHashSet();

        for(int i = treasures.Count - 1; i >= 0; i--)
        {
            TreasureCoffer treasure = treasures[i];
            if (!worldTreasures.ContainsKey(treasure.Id) || !treasure.IsValid())
            {
                treasures.RemoveAt(i);
            }
        }

        foreach((uint objectId, IGameObject obj) in worldTreasures)
        {
            if (knownIds.Contains(objectId))
            {
                continue;
            }

            TreasureCoffer treasure = new(obj, data);
            if (treasure.IsValid())
            {
                treasures.Add(treasure);
            }
        }

        treasures = treasures.OrderBy(t => player.Position.Distance(t.GetPosition())).ToList();

        foreach(TreasureCoffer treasure in treasures)
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

    public IReadOnlyList<TreasureCoffer> Treasures => treasures;

    public bool CountInitialised { get; private set; }

    public DateTime LastCountUpdateUtc { get; private set; } = DateTime.MinValue;

    public int BronzeChests { get; private set; }

    public int SilverChests { get; private set; }

    private unsafe void OnWideTextPostDraw(AddonEvent type, AddonArgs args)
    {
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        AtkUnitBase* addon = (AtkUnitBase*)args.Addon.Address;
        if (!addon->IsVisible)
        {
            return;
        }

        if (DateTime.Now - lastParseWideText < parseWideTextCooldown)
        {
            return;
        }

        lastParseWideText = DateTime.Now;

        string pattern = LogMessageHelper.GetLogMessagePattern(data, ActiveChestLogMessageId);
        string text = addon->GetNodeById(3)->GetAsAtkTextNode()->NodeText.ToString();
        Match match = Regex.Match(text, pattern);
        if (!match.Success)
        {
            return;
        }

        SilverChests = int.Parse(match.Groups[1].Value);
        BronzeChests = int.Parse(match.Groups[2].Value);
        CountInitialised = true;
        LastCountUpdateUtc = DateTime.UtcNow;
    }
}
