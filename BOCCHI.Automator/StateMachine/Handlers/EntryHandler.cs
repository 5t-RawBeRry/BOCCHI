using BOCCHI.Automator.Data;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class EntryHandler() : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Entry)
{
    public override StatePriority GetScore() => StatePriority.Never;

    public override void Handle()
    {
    }
}
