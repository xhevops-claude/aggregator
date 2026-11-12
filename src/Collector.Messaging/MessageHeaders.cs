using System.Globalization;
using System.Text;
using Collector.Core.Messages;

namespace Collector.Messaging;

public static class MessageHeaders
{
    public const string ProjectId = "projectId";
    public const string RunId = "runId";
    public const string ItemId = "itemId";
    public const string Attempt = "x-attempt";

    public static Dictionary<string, object?> For(IMessage message)
    {
        return new Dictionary<string, object?>
        {
            [ProjectId] = message.ProjectId.ToString(),
            [RunId] = message.RunId.ToString(),
            [ItemId] = message.ItemId.ToString(),
        };
    }

    public static int ReadAttempt(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(Attempt, out var value))
        {
            return 1;
        }

        var attempt = value switch
        {
            int number => number,
            long number => (int)number,
            short number => number,
            byte number => number,
            byte[] text => Parse(Encoding.UTF8.GetString(text)),
            _ => 1,
        };
        return Math.Max(1, attempt);
    }

    public static string? ReadText(IDictionary<string, object?>? headers, string name)
    {
        if (headers is null || !headers.TryGetValue(name, out var value))
        {
            return null;
        }

        return value switch
        {
            byte[] text => Encoding.UTF8.GetString(text),
            string text => text,
            _ => value?.ToString(),
        };
    }

    private static int Parse(string text)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return 1;
    }
}
