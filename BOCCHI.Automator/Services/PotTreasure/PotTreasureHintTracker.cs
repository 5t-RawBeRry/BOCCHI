using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using Ocelot.Lifecycle;

namespace BOCCHI.Automator.Services.PotTreasure;

/// <summary>Parses Magical Elixir / Cache Me If You Can LogMessages (AOCC TreasureHintTracker).</summary>
public sealed class PotTreasureHintTracker(IChatGui chat) : IOnStart, IOnStop
{
    private readonly object gate = new();

    private bool armed;

    private int revision;

    private PotTreasureHintEvent? initialHint;

    private PotTreasureHintEvent? lastEvent;

    private bool revealLatched;

    public int Revision
    {
        get
        {
            lock (gate)
            {
                return revision;
            }
        }
    }

    public PotTreasureHintEvent? InitialHint
    {
        get
        {
            lock (gate)
            {
                return initialHint;
            }
        }
    }

    public PotTreasureHintEvent? LastEvent
    {
        get
        {
            lock (gate)
            {
                return lastEvent;
            }
        }
    }

    public bool RevealLatched
    {
        get
        {
            lock (gate)
            {
                return revealLatched;
            }
        }
    }

    public void OnStart() => chat.LogMessage += OnLogMessage;

    public void OnStop()
    {
        chat.LogMessage -= OnLogMessage;
        Disarm();
    }

    public void Arm()
    {
        lock (gate)
        {
            armed = true;
            revision = 0;
            initialHint = null;
            lastEvent = null;
            revealLatched = false;
        }
    }

    public void Disarm()
    {
        lock (gate)
        {
            armed = false;
            initialHint = null;
            lastEvent = null;
            revealLatched = false;
        }
    }

    public bool TryGetEventSince(int baselineRevision, out PotTreasureHintEvent hintEvent)
    {
        lock (gate)
        {
            if (lastEvent is { } e && e.Revision > baselineRevision)
            {
                hintEvent = e;
                return true;
            }
        }

        hintEvent = null!;
        return false;
    }

    private void OnLogMessage(ILogMessage message)
    {
        if (!TryClassify(message, out PotTreasureHintEvent parsed))
        {
            return;
        }

        lock (gate)
        {
            if (!armed)
            {
                return;
            }

            revision++;
            parsed.Revision = revision;
            lastEvent = parsed;

            if (parsed.Kind == PotTreasureHintKind.Hint && initialHint == null)
            {
                initialHint = parsed;
            }

            if (parsed.Kind == PotTreasureHintKind.CofferReveal)
            {
                revealLatched = true;
            }
        }
    }

    private static bool TryClassify(ILogMessage message, out PotTreasureHintEvent parsed)
    {
        parsed = null!;
        uint id = message.LogMessageId;

        if (id == PotTreasureIds.CofferRevealLogMessageId)
        {
            parsed = new PotTreasureHintEvent
            {
                Kind = PotTreasureHintKind.CofferReveal,
                LogMessageId = id,
            };
            return true;
        }

        if (id == PotTreasureIds.ElixirPromptLogMessageId)
        {
            parsed = new PotTreasureHintEvent
            {
                Kind = PotTreasureHintKind.ElixirPrompt,
                LogMessageId = id,
            };
            return true;
        }

        if (id == PotTreasureIds.BonusOfferLogMessageId)
        {
            parsed = new PotTreasureHintEvent
            {
                Kind = PotTreasureHintKind.BonusOffer,
                LogMessageId = id,
            };
            return true;
        }

        PotTreasureDistanceBucket distance = PotTreasureIds.MapDistance(id);
        if (distance == PotTreasureDistanceBucket.Unknown)
        {
            return false;
        }

        if (!message.TryGetIntParameter(0, out int directionValue))
        {
            return false;
        }

        PotTreasureDirection direction = PotTreasureIds.MapDirection(directionValue);
        if (direction == PotTreasureDirection.Unknown)
        {
            return false;
        }

        parsed = new PotTreasureHintEvent
        {
            Kind = PotTreasureHintKind.Hint,
            LogMessageId = id,
            Direction = direction,
            Distance = distance,
        };
        return true;
    }
}
