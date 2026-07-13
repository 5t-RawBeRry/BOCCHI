namespace BOCCHI.Common.Data;

public readonly record struct DeltaSnapshot(long Delta, DateTime Time);

public sealed class DeltaRateTracker(Func<TimeSpan> getTrackedWindow)
{
    private long lastValue;

    private bool hasValue;

    private readonly List<DeltaSnapshot> snapshots = [];

    public bool HasValue => hasValue;

    public void SyncBaseline(long value)
    {
        lastValue = value;
        hasValue = true;
    }

    public void RecordPositiveDelta(long current)
    {
        Prune();

        if (!hasValue)
        {
            lastValue = current;
            hasValue = true;
            return;
        }

        var delta = current - lastValue;
        lastValue = current;

        if (delta <= 0)
        {
            return;
        }

        snapshots.Add(new DeltaSnapshot(delta, DateTime.UtcNow));
    }

    public double PerHour
    {
        get
        {
            if (snapshots.Count == 0)
            {
                return 0;
            }

            var duration = getTrackedWindow();
            var now = DateTime.UtcNow;
            var windowStart = now - duration;

            var oldest = snapshots[0].Time;
            var start = oldest > windowStart ? oldest : windowStart;

            var elapsed = now - start;

            if (elapsed < TimeSpan.FromSeconds(10))
            {
                return 0;
            }

            var hours = elapsed.TotalHours;
            if (hours <= 0)
            {
                return 0;
            }

            return snapshots.Sum(s => s.Delta) / hours;
        }
    }

    public float[] GetHistory(TimeSpan sampleDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleDuration, TimeSpan.Zero);

        var duration = getTrackedWindow();
        var now = DateTime.UtcNow;
        var start = now - duration;

        var relevant = snapshots
            .Where(s => s.Time >= start)
            .OrderBy(s => s.Time)
            .ToList();

        if (relevant.Count == 0)
        {
            return [];
        }

        var bucketCount = (int)Math.Floor(duration.TotalSeconds / sampleDuration.TotalSeconds);
        if (bucketCount <= 0)
        {
            return [];
        }

        var bucketTotals = new double[bucketCount];

        foreach (var snapshot in relevant)
        {
            var secondsFromStart = (snapshot.Time - start).TotalSeconds;
            var index = (int)(secondsFromStart / sampleDuration.TotalSeconds);

            if (index < 0 || index >= bucketCount)
            {
                continue;
            }

            bucketTotals[index] += snapshot.Delta;
        }

        var result = new float[bucketCount];
        var bucketSeconds = sampleDuration.TotalSeconds;

        for (var i = 0; i < bucketCount; i++)
        {
            var amount = bucketTotals[i];

            if (amount <= 0)
            {
                result[i] = 0f;
                continue;
            }

            result[i] = (float)(amount / bucketSeconds * 3600.0);
        }

        return result;
    }

    private void Prune()
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        var cutoff = DateTime.UtcNow - getTrackedWindow();

        var index = 0;
        while (index < snapshots.Count && snapshots[index].Time < cutoff)
        {
            index++;
        }

        if (index > 0)
        {
            snapshots.RemoveRange(0, index);
        }
    }
}
