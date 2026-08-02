namespace BOCCHI.Automator.Data;

public interface IAutomatorContext
{
    bool Enabled { get; }

    void Toggle();

    event Action<bool>? OnToggle;
}

public class AutomatorContext : IAutomatorContext
{
    public bool Enabled { get; private set; }

    public void Toggle()
    {
        Enabled = !Enabled;
        OnToggle?.Invoke(Enabled);
    }

    public event Action<bool>? OnToggle;
}
