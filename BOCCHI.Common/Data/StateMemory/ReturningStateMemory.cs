namespace BOCCHI.Automator.Data.StateMemory;

public sealed class ReturningStateMemory
{
    public DateTimeOffset QueuedAt { get; } = DateTimeOffset.UtcNow;

    public TimeSpan GetTimeQueued()
    {
        return DateTimeOffset.UtcNow - QueuedAt;
    }
}
