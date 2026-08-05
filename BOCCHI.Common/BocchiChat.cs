using BOCCHI.Common.Config;

namespace BOCCHI.Common;

/// <summary>Formats chat messages with an optional [BOCCHI] prefix.</summary>
public static class BocchiChat
{
    public const string Tag = "[BOCCHI]";

    public static string Format(string message, UIConfig ui) => Format(message, ui.ShowBocchiChatPrefix);

    public static string Format(string message, bool showPrefix)
    {
        string body = Strip(message);
        return showPrefix ? $"{Tag} {body}" : body;
    }

    public static string Strip(string message)
    {
        if (message.StartsWith($"{Tag} ", StringComparison.Ordinal))
        {
            return message[(Tag.Length + 1)..];
        }

        if (message.StartsWith(Tag, StringComparison.Ordinal))
        {
            return message[Tag.Length..].TrimStart();
        }

        return message;
    }
}
