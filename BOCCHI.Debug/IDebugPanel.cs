namespace BOCCHI.Debug;

public interface IDebugPanel
{
    string Name { get; }

    void Render();
}
