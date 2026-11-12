using System.Text;
using System.Text.Json;
using AngleSharp.Dom;

namespace Collector.Providers.Nbshop;

public static class NbshopJsonLd
{
    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static IReadOnlyList<JsonElement> Blocks(IDocument document)
    {
        var blocks = new List<JsonElement>();
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            var block = TryParse(script.TextContent);
            if (block is not null) blocks.Add(block.Value);
        }

        return blocks;
    }

    public static JsonElement? FirstOfType(IEnumerable<JsonElement> blocks, string type)
    {
        foreach (var block in blocks)
        {
            if (IsType(block, type)) return block;
        }

        return null;
    }

    public static bool IsType(JsonElement element, string type)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        if (!element.TryGetProperty("@type", out var value)) return false;

        if (value.ValueKind == JsonValueKind.String) return value.GetString() == type;
        if (value.ValueKind != JsonValueKind.Array) return false;

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() == type) return true;
        }

        return false;
    }

    public static JsonElement? TryParse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(Sanitize(text), Options);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Sanitize(string json)
    {
        var builder = new StringBuilder(json.Length);
        var inString = false;
        var escaped = false;
        foreach (var character in json)
        {
            if (!inString)
            {
                inString = character == '"';
                builder.Append(character);
            }
            else if (escaped)
            {
                escaped = false;
                builder.Append(character);
            }
            else if (character == '\\')
            {
                escaped = true;
                builder.Append(character);
            }
            else if (character == '"')
            {
                inString = false;
                builder.Append(character);
            }
            else if (character < ' ')
            {
                builder.Append(Escape(character));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string Escape(char character)
    {
        return character switch
        {
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ => $"\\u{(int)character:x4}",
        };
    }
}
