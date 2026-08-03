using BOCCHI.Common.Data;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using System.Numerics;

namespace BOCCHI.Common.Data.CriticalEncounters;

public readonly record struct CriticalEncounterId(ushort Value)
{
    public override string ToString() => Value.ToString();
}

public class CriticalEncounter(CriticalEncounterId id, DynamicEvent ev, float radius, Vector3 fallbackPosition)
{
    private readonly Vector3 fallbackPosition = fallbackPosition;

    public readonly CriticalEncounterId Id = id;

    public readonly string Name = ev.Name.ToString();

    public readonly ActivityProgressTracker ProgressTracker = new();

    public readonly float Radius = radius;

    public Vector3 Position { get; private set; } = ResolvePosition(ev, fallbackPosition);

    public DynamicEventState State { get; private set; } = ev.State;

    public byte Progress { get; private set; } = ev.Progress;

    private static unsafe Vector3 TryReadLayoutPosition(DynamicEvent ev)
    {
        LayoutManager* layout = LayoutWorld.Instance()->ActiveLayout;
        if (layout == null)
        {
            return Vector3.NaN;
        }

        if (!layout->InstancesByType.TryGetValue(InstanceType.EventObject, out Pointer<StdMap<ulong, Pointer<ILayoutInstance>>> eventObjects, false))
        {
            return Vector3.NaN;
        }

        uint eventObjectId = ev.LGBEventObject;
        if (eventObjectId <= 0)
        {
            return Vector3.NaN;
        }

        Pointer<ILayoutInstance>? eventObject = eventObjects.Value->Values.FirstOrNull(e => e.Value->Id.InstanceKey == eventObjectId);
        if (eventObject == null)
        {
            return Vector3.NaN;
        }

        Transform* trans = eventObject.Value.Value->GetTransformImpl();
        Vector3 position = trans->Translation;

        return new(position.X, position.Y, position.Z);
    }

    private static Vector3 ResolvePosition(DynamicEvent ev, Vector3 fallbackPosition)
    {
        // Authored staging points win when present — live LGB markers can sit under elevated CEs
        // (e.g. Accept No Imitators on the tower: live at base, player at y=56).
        if (!float.IsNaN(fallbackPosition.X))
        {
            return fallbackPosition;
        }

        return TryReadLayoutPosition(ev);
    }

    public void Update(DynamicEvent ev)
    {
        State = ev.State;
        Progress = ev.Progress;

        // Keep authored destination stable; only adopt live layout when we have no fallback.
        if (float.IsNaN(fallbackPosition.X))
        {
            Vector3 live = TryReadLayoutPosition(ev);
            if (!float.IsNaN(live.X))
            {
                Position = live;
            }
        }

        ProgressTracker.Observe(Progress);
    }

    public bool IsPreparing() => State is DynamicEventState.Register or DynamicEventState.Warmup;

    public bool IsActive() => State is DynamicEventState.Battle;
}
