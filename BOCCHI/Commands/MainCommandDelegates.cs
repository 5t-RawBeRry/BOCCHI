using Ocelot.Services.Commands;

namespace BOCCHI.Commands;

public class BuffCommandDelegate(BuffCommand command) : IMainCommandDelegate
{
    public IOcelotCommand Command { get; } = command;
}

public class TeleportCommandDelegate(TeleportCommand command) : IMainCommandDelegate
{
    public IOcelotCommand Command { get; } = command;
}

public class IllegalCommandDelegate(IllegalCommand command) : IMainCommandDelegate
{
    public IOcelotCommand Command { get; } = command;
}

public class CmdCommandDelegate(CmdCommand command) : IMainCommandDelegate
{
    public IOcelotCommand Command { get; } = command;
}

public class DebugCommandDelegate(DebugCommand command) : IMainCommandDelegate
{
    public IOcelotCommand Command { get; } = command;
}
