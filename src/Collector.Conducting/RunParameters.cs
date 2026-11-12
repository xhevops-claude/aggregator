using System.Globalization;
using Collector.Core.Messages;
using Collector.Core.Runs;

namespace Collector.Conducting;

public static class RunParameters
{
    public const string LimitKey = "limit";

    public static Dictionary<string, string> From(RunRequested request)
    {
        var parameters = new Dictionary<string, string>(request.Parameters);
        if (request.Limit is { } limit) parameters[LimitKey] = limit.ToString(CultureInfo.InvariantCulture);
        return parameters;
    }

    public static int? Limit(Run run)
    {
        if (!run.Parameters.TryGetValue(LimitKey, out var text)) return null;
        return int.TryParse(text, CultureInfo.InvariantCulture, out var limit) && limit > 0 ? limit : null;
    }

    public static Dictionary<string, string> ForCommand(Run run)
    {
        var parameters = new Dictionary<string, string>(run.Parameters);
        parameters.Remove(LimitKey);
        return parameters;
    }
}
