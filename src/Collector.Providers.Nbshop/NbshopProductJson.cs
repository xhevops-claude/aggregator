using System.Text.Json;
using Collector.Core.Entities;

namespace Collector.Providers.Nbshop;

public static class NbshopProductJson
{
    public static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;

        return Text(value);
    }

    public static string? Brand(JsonElement product)
    {
        if (!product.TryGetProperty("brand", out var brand)) return null;
        if (brand.ValueKind == JsonValueKind.Object) return Text(brand, "name");

        return Text(brand);
    }

    public static List<string> Images(JsonElement product)
    {
        var images = new List<string>();
        if (!product.TryGetProperty("image", out var image)) return images;

        if (image.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in image.EnumerateArray())
            {
                var url = Text(item);
                if (url is not null) images.Add(url);
            }
        }
        else
        {
            var url = Text(image);
            if (url is not null) images.Add(url);
        }

        return images;
    }

    public static JsonElement? Offer(JsonElement product)
    {
        if (!product.TryGetProperty("offers", out var offers)) return null;
        if (offers.ValueKind == JsonValueKind.Object) return offers;
        if (offers.ValueKind != JsonValueKind.Array) return null;

        foreach (var offer in offers.EnumerateArray())
        {
            if (offer.ValueKind == JsonValueKind.Object) return offer;
        }

        return null;
    }

    public static Availability ParseAvailability(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Availability.Unknown;

        var segment = value.TrimEnd('/').Split('/')[^1];
        return segment switch
        {
            "InStock" => Availability.InStock,
            "OutOfStock" or "SoldOut" or "Discontinued" => Availability.OutOfStock,
            _ => Availability.Unknown,
        };
    }

    private static string? Text(JsonElement value)
    {
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
