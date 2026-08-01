namespace BOCCHI.Common.Data;

public class ProgressEstimate
{
    public DateTimeOffset? CompletionTime { get; set; }

    public double RSquared { get; set; }

    public TimeSpan Time => CompletionTime?.Subtract(DateTime.Now) ?? TimeSpan.Zero;
}

public readonly record struct ProgressEntry(DateTimeOffset Timestamp, byte Progress);

public sealed class ActivityProgressTracker
{
    private readonly List<ProgressEntry> entries = [];
    private byte previous;

    public void Observe(byte progress)
    {
        if (progress <= previous)
        {
            return;
        }

        previous = progress;
        entries.Add(new(DateTimeOffset.Now, progress));
    }

    public ProgressEstimate? Estimate()
    {
        if (entries.Count < 3)
        {
            return null;
        }

        double[] x = entries.Select(p => (p.Timestamp - entries[0].Timestamp).TotalMinutes).ToArray();
        double[] y = entries.Select(p => (double)p.Progress).ToArray();

        double xMean = x.Average();
        double yMean = y.Average();

        double numerator = x.Zip(y, (xi, yi) => (xi - xMean) * (yi - yMean)).Sum();
        double denominator = x.Sum(xi => Math.Pow(xi - xMean, 2));

        double slope = numerator / denominator;
        double intercept = yMean - slope * xMean;

        double ssTotal = y.Sum(yi => Math.Pow(yi - yMean, 2));
        double ssResidual = x.Zip(y, (xi, yi) => Math.Pow(yi - (slope * xi + intercept), 2)).Sum();
        double rSquared = 1 - ssResidual / ssTotal;

        if (slope <= 0)
        {
            return new()
                { CompletionTime = null, RSquared = rSquared };
        }

        double minutesTo100 = (100 - intercept) / slope;
        DateTimeOffset completionTime = entries[0].Timestamp.AddMinutes(minutesTo100);

        return new()
        {
            CompletionTime = completionTime,
            RSquared = rSquared
        };
    }
}
