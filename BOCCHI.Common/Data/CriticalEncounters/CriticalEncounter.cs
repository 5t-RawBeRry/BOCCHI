using System.Numerics;
using BOCCHI.Common.Data;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using Lumina.Data.Parsing.Scd;
using Ocelot.Services.Logger;

namespace BOCCHI.Common.Data.CriticalEncounters;

public class CriticalEncounter(CriticalEncounterId id, DynamicEvent ev)
{
    public readonly CriticalEncounterId Id = id;

    public readonly Vector3 Position = GetPosition(ev);

    public DynamicEventState State { get; private set; } = ev.State;

    public readonly String Name = ev.Name.ToString();

    public byte Progress { get; private set; } = ev.Progress;

    public readonly ActivityProgressTracker ProgressTracker = new();

    public float Radius {
        // Not that each CE has a padding of 7 yalms. 2 yalms are a border and 5 yalms are the kill zone.
        get => Id.Value switch
        {
            33 => 25f, // Scourge of the Mind ?
            34 => 25f, // The Black Regiment
            35 => 25f, // The Unbridled
            36 => 25f, // Crawling Death
            37 => 25f, // Calamity Bound
            38 => 20f, // Trial by Claw
            39 => 20f, // From Times Bygone ?
            40 => 25f, // Company of Stone ?
            41 => 15f, // Shark Attack ?
            42 => 25f, // On the Hunt ?
            43 => 15f, // With Extreme Prejudice
            44 => 25f, // Noise Complaint
            45 => 25f, // Cursed Concern
            46 => 20f, // Eternal Watch
            47 => 20f, // Flame of Dusk
            // 48 = The Forked Tower
            _ => 0f,
        } + 7f;
    }

    private static unsafe Vector3 GetPosition(DynamicEvent ev)
    {
        var layout = LayoutWorld.Instance()->ActiveLayout;
        if (layout == null)
        {
            return Vector3.NaN;
        }

        if (!layout->InstancesByType.TryGetValue(InstanceType.EventObject, out var eventObjects, false))
        {
            return Vector3.NaN;
        }

        var eventObjectId = ev.LGBEventObject;
        if (eventObjectId <= 0)
        {
            return Vector3.NaN;
        }

        var eventObject = eventObjects.Value->Values.FirstOrNull(e => e.Value->Id.InstanceKey == eventObjectId);
        if (eventObject == null)
        {
            return Vector3.NaN;
        }

        var trans = eventObject.Value.Value->GetTransformImpl();
        var position = trans->Translation;

        return new Vector3(position.X, position.Y, position.Z);
    }


    public void Update(DynamicEvent ev)
    {
        State = ev.State;
        Progress = ev.Progress;

        ProgressTracker.Observe(Progress);
    }

    public bool IsPreparing()
    {
        return State is DynamicEventState.Register or DynamicEventState.Warmup;
    }

    public bool IsActive()
    {
        return State is DynamicEventState.Battle;
    }
}
