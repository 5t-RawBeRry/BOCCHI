namespace BOCCHI.Automator.Data;

public interface IAutomatorContext
{
    AutomatorRunMode RunMode { get; }

    /// <summary>True when Illegal Mode, Completionist, or Pots &amp; Treasure is driving the automator pipeline.</summary>
    bool Enabled { get; }

    bool IsIllegalMode { get; }

    bool IsPotsAndTreasure { get; }

    bool IsCompletionist { get; }

    /// <summary>Toggle Illegal Mode on/off. Turns off other automator run modes if active.</summary>
    void Toggle();

    /// <summary>Toggle dedicated Pots &amp; Treasure mode.</summary>
    void TogglePotsAndTreasure();

    /// <summary>Toggle Completionist Mode (note-filtered Illegal pipeline).</summary>
    void ToggleCompletionist();

    void SetRunMode(AutomatorRunMode mode);

    event Action<bool>? OnToggle;
}

public class AutomatorContext : IAutomatorContext
{
    public AutomatorRunMode RunMode { get; private set; }

    public bool Enabled => RunMode != AutomatorRunMode.Off;

    public bool IsIllegalMode => RunMode == AutomatorRunMode.IllegalMode;

    public bool IsPotsAndTreasure => RunMode == AutomatorRunMode.PotsAndTreasure;

    public bool IsCompletionist => RunMode == AutomatorRunMode.Completionist;

    public void Toggle()
    {
        SetRunMode(IsIllegalMode ? AutomatorRunMode.Off : AutomatorRunMode.IllegalMode);
    }

    public void TogglePotsAndTreasure()
    {
        SetRunMode(IsPotsAndTreasure ? AutomatorRunMode.Off : AutomatorRunMode.PotsAndTreasure);
    }

    public void ToggleCompletionist()
    {
        SetRunMode(IsCompletionist ? AutomatorRunMode.Off : AutomatorRunMode.Completionist);
    }

    public void SetRunMode(AutomatorRunMode mode)
    {
        if (RunMode == mode)
        {
            return;
        }

        bool wasEnabled = Enabled;
        RunMode = mode;
        bool nowEnabled = Enabled;
        if (wasEnabled != nowEnabled)
        {
            OnToggle?.Invoke(nowEnabled);
        }
    }

    public event Action<bool>? OnToggle;
}
