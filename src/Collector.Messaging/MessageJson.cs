using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Collector.Core.Messages;

namespace Collector.Messaging;

public static class MessageJson
{
    public const string ContentType = "application/json";

    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static byte[] Serialize(IMessage message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), Options);
    }

    public static bool TryDeserialize<T>(ReadOnlyMemory<byte> body, [NotNullWhen(true)] out T? message)
        where T : IMessage
    {
        try
        {
            message = JsonSerializer.Deserialize<T>(body.Span, Options);
        }
        catch (JsonException)
        {
            message = default;
        }

        if (message is null || message.ProjectId == Guid.Empty)
        {
            message = default;
            return false;
        }

        return true;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
