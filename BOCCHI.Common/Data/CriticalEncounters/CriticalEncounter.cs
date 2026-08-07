using BOCCHI.Common.Data;
using BOCCHI.Common.Data.Zones.Graph;
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

public class CriticalEncounter(
    CriticalEncounterId id,
    DynamicEvent ev,
    float radius,
    Vector3 fallbackPosition,
    ActivityAreaShape areaShape = ActivityAreaShape.Circle)
{
    private readonly Vector3 fallbackPosition = fallbackPosition;

    public readonly CriticalEncounterId Id = id;

    public readonly string Name = ev.Name.ToString();

    public readonly ActivityProgressTracker ProgressTracker = new();

    /// <summary>Padded size used for debug outer ring (circle radius or square half-extent).</summary>
    public readonly float Radius = radius;

    public readonly ActivityAreaShape AreaShape = areaShape;

    public Vector3 Position { get; private set; } = ResolvePosition(ev, fallbackPosition, areaShape);

    public DynamicEventState State { get; private set; } = ev.State;

    public byte Progress { get; private set; } = ev.Progress;

    /// <summary>Unix seconds when registration/start is scheduled (game DynamicEvent).</summary>
    public int StartTimestamp { get; private set; } = ev.StartTimestamp;

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

    private static Vector3 ResolvePosition(DynamicEvent ev, Vector3 fallbackPosition, ActivityAreaShape shape)
    {
        // Square CEs: live LGB is usually the blue-zone center; authored staging can sit off-square.
        if (shape == ActivityAreaShape.Square)
        {
            Vector3 live = TryReadLayoutPosition(ev);
            if (!float.IsNaN(live.X))
            {
                return live;
            }
        }

        // Authored staging points win for circular CEs — live LGB markers can sit under elevated CEs
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
        StartTimestamp = ev.StartTimestamp;

        if (AreaShape == ActivityAreaShape.Square || float.IsNaN(fallbackPosition.X))
        {
            Vector3 live = TryReadLayoutPosition(ev);
            if (!float.IsNaN(live.X))
            {
                Position = live;
            }
        }

        ProgressTracker.Observe(Progress);
    }

    public TimeSpan? GetTimeUntilStart()
    {
        if (StartTimestamp == 0)
        {
            return null;
        }

        TimeSpan remaining = DateTimeOffset.FromUnixTimeSeconds(StartTimestamp) - DateTimeOffset.UtcNow;
        return remaining;
    }

    public bool IsPreparing() => State is DynamicEventState.Register or DynamicEventState.Warmup;

    public bool IsActive() => State is DynamicEventState.Battle;
}
