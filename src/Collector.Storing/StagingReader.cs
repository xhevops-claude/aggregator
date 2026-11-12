using Collector.Core.Staging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Collector.Storing;

internal static class StagingReader
{
    public static StagingDocument? TryRead(BsonDocument raw)
    {
        try
        {
            var staging = BsonSerializer.Deserialize<StagingDocument>(raw);
            if (staging.Entity is null) return null;
            return staging;
        }
        catch (Exception exception) when (exception is FormatException or BsonException)
        {
            return null;
        }
    }

    public static bool IsApplied(BsonDocument raw)
    {
        var state = raw.GetValue(Elements.State, BsonNull.Value);
        return state.IsString && state.AsString == nameof(StagingState.Applied);
    }
}
