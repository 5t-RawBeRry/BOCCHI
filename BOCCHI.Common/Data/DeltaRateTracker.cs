namespace BOCCHI.Common.Data;

public readonly record struct DeltaSnapshot(long Delta, DateTime Time);

public sealed class DeltaRateTracker(Func<TimeSpan> getTrackedWindow)
{
    private readonly List<DeltaSnapshot> snapshots = [];

    private long lastValue;

    public bool HasValue { get; private set; }

    public double PerHour
    {
        get
        {
            if (snapshots.Count == 0)
            {
                return 0;
            }

            TimeSpan duration = getTrackedWindow();
            DateTime now = DateTime.UtcNow;
            DateTime windowStart = now - duration;

            DateTime oldest = snapshots[0].Time;
            DateTime start = oldest > windowStart ? oldest : windowStart;

            TimeSpan elapsed = now - start;

            if (elapsed < TimeSpan.FromSeconds(10))
            {
                return 0;
            }

            double hours = elapsed.TotalHours;
            if (hours <= 0)
            {
                return 0;
            }

            return snapshots.Sum(s => s.Delta) / hours;
        }
    }

    public void SyncBaseline(long value)
    {
        lastValue = value;
        HasValue = true;
    }

    public void RecordPositiveDelta(long current)
    {
        Prune();

        if (!HasValue)
        {
            lastValue = current;
            HasValue = true;
            return;
        }

        long delta = current - lastValue;
        lastValue = current;

        if (delta <= 0)
        {
            return;
        }

        snapshots.Add(new(delta, DateTime.UtcNow));
    }

    public float[] GetHistory(TimeSpan sampleDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleDuration, TimeSpan.Zero);

        TimeSpan duration = getTrackedWindow();
        DateTime now = DateTime.UtcNow;
        DateTime start = now - duration;

        List<DeltaSnapshot> relevant = snapshots
            .Where(s => s.Time >= start)
            .OrderBy(s => s.Time)
            .ToList();

        if (relevant.Count == 0)
        {
            return [];
        }

        int bucketCount = (int)Math.Floor(duration.TotalSeconds / sampleDuration.TotalSeconds);
        if (bucketCount <= 0)
        {
            return [];
        }

        double[] bucketTotals = new double[bucketCount];

        foreach(DeltaSnapshot snapshot in relevant)
        {
            double secondsFromStart = (snapshot.Time - start).TotalSeconds;
            int index = (int)(secondsFromStart / sampleDuration.TotalSeconds);

            if (index < 0 || index >= bucketCount)
            {
                continue;
            }

            bucketTotals[index] += snapshot.Delta;
        }

        float[] result = new float[bucketCount];
        double bucketSeconds = sampleDuration.TotalSeconds;

        for(int i = 0; i < bucketCount; i++)
        {
            double amount = bucketTotals[i];

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

        DateTime cutoff = DateTime.UtcNow - getTrackedWindow();

        int index = 0;
        while(index < snapshots.Count && snapshots[index].Time < cutoff)
        {
            index++;
        }

        if (index > 0)
        {
            snapshots.RemoveRange(0, index);
        }
    }
}
