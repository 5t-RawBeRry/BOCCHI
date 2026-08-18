namespace BOCCHI.Common.Data;

public readonly record struct DeltaSnapshot(long Delta, DateTime Time);

public sealed class DeltaRateTracker(Func<TimeSpan> getTrackedWindow)
{
    /// <summary>
    ///     How much history a per-hour rate averages over. Was a setting; nobody needs to tune it
    ///     and it only made the trackers page longer.
    /// </summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(5);

    /// <summary>Width of one bar in the tracker graphs. Also formerly a setting.</summary>
    public static readonly TimeSpan DefaultGraphBucket = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan RecoveryWindow = TimeSpan.FromSeconds(2);

    private readonly List<DeltaSnapshot> snapshots = [];

    private long lastValue;

    private long? valueBeforeDrop;

    private DateTime dropAt = DateTime.MinValue;

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
        valueBeforeDrop = null;
        dropAt = DateTime.MinValue;
    }

    /// <summary>Drop samples and baseline so a zone/OC reload does not inflate per-hour rates.</summary>
    public void Reset()
    {
        snapshots.Clear();
        lastValue = 0;
        HasValue = false;
        valueBeforeDrop = null;
        dropAt = DateTime.MinValue;
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

        if (delta < 0)
        {
            // Remember pre-drop balance so a shop/state dip→recover is not counted as a gain.
            valueBeforeDrop = lastValue;
            dropAt = DateTime.UtcNow;
            lastValue = current;
            return;
        }

        if (valueBeforeDrop is { } before
            && DateTime.UtcNow - dropAt <= RecoveryWindow)
        {
            lastValue = current;
            valueBeforeDrop = null;
            // Absorb one recovery sample after a dip (typically back to post-spend balance).
            if (current <= before)
            {
                return;
            }

            delta = current - before;
        }
        else
        {
            valueBeforeDrop = null;
            lastValue = current;
        }

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

        foreach (DeltaSnapshot snapshot in relevant)
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

        for (int i = 0; i < bucketCount; i++)
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
