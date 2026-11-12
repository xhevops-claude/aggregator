using Collector.Core.Lake;
using Collector.Core.Messages;
using Collector.Core.Platform;
using Collector.Core.Runs;
using Collector.Lake;

namespace Collector.Harvesting;

public sealed class HarvestRunner
{
    public const string ErrorClass = "harvest";
    public const string LakeErrorClass = "lake";

    private const int FailuresShown = 5;

    private readonly HarvestCommandCatalog commands;
    private readonly ISourceFetcher fetcher;
    private readonly HostBudget budget;
    private readonly ILakeWriter lake;
    private readonly IMessagePublisher publisher;
    private readonly TimeProvider time;

    public HarvestRunner(HarvestCommandCatalog commands, ISourceFetcher fetcher, HostBudget budget, ILakeWriter lake,
        IMessagePublisher publisher, TimeProvider time)
    {
        this.commands = commands;
        this.fetcher = fetcher;
        this.budget = budget;
        this.lake = lake;
        this.publisher = publisher;
        this.time = time;
    }

    public async Task<HarvestReport> RunAsync(HarvestCommand message, Source source, LakeRetention retention,
        CancellationToken cancellationToken)
    {
        if (message.Source.Length > 0 && message.Source != source.Key)
        {
            var text = $"The command is for source '{message.Source}', the source given is '{source.Key}'";
            throw new ArgumentException(text, nameof(source));
        }

        fetcher.Register(source);
        var command = commands.Find(message.Command);
        HarvestReport report;
        if (command is null)
        {
            report = new HarvestReport { Error = $"unknown harvest command '{message.Command}'" };
        }
        else
        {
            report = await HarvestAsync(command, message, source, retention, cancellationToken);
        }

        await publisher.PublishAsync(MessageNames.ItemCompleted, Completed(message, report), cancellationToken);
        return report;
    }

    private async Task<HarvestReport> HarvestAsync(IHarvestCommand command, HarvestCommand message, Source source,
        LakeRetention retention, CancellationToken token)
    {
        var report = new HarvestReport();
        var expected = message.Targets.Count > 0 ? message.Targets.Count : message.Limit;
        var paced = new PacedFetcher(fetcher, message.Pacing, expected, budget, time);
        await using var items = command.RunAsync(Request(message, source), paced, token).GetAsyncEnumerator(token);
        while (true)
        {
            try
            {
                if (!await items.MoveNextAsync()) return report;
            }
            catch (Exception exception) when (!IsCancellation(exception, token))
            {
                return report with { Error = $"{exception.GetType().Name}: {exception.Message}" };
            }

            var item = items.Current;
            if (item.Failed)
            {
                report.Failed.Add(Failure(item, item.ErrorClass ?? "", item.ErrorText ?? ""));
                continue;
            }

            LakeWriteResult result;
            try
            {
                result = await lake.WriteAsync(LakeWriteFor(message, source, item), retention, token);
            }
            catch (Exception exception) when (!IsCancellation(exception, token))
            {
                report.Failed.Add(Failure(item, LakeErrorClass, exception.Message));
                continue;
            }

            var entry = Entry(item, result.Item);
            if (!result.NewVersion)
            {
                report.Unchanged.Add(entry);
                continue;
            }

            await publisher.PublishAsync(MessageNames.LakeItemStored, Stored(message, result.Item), token);
            report.Stored.Add(entry);
        }
    }

    private static HarvestRequest Request(HarvestCommand message, Source source)
    {
        return new HarvestRequest
        {
            ProjectId = message.ProjectId,
            Namespace = message.Namespace,
            Source = source,
            Targets = message.Targets,
            Parameters = message.Parameters,
            Limit = message.Limit,
        };
    }

    private LakeWrite LakeWriteFor(HarvestCommand message, Source source, HarvestItem item)
    {
        var harvestedAt = item.HarvestedAt == default ? time.GetUtcNow().UtcDateTime : item.HarvestedAt;
        return new LakeWrite
        {
            ProjectId = message.ProjectId,
            Source = source.Key,
            Kind = item.Kind,
            ProviderId = item.ProviderId,
            Url = item.Url,
            ContentType = item.ContentType,
            HarvestedAt = harvestedAt,
            Body = item.Body,
        };
    }

    private static LakeItemStored Stored(HarvestCommand message, LakeItem item)
    {
        return new LakeItemStored
        {
            ProjectId = message.ProjectId,
            RunId = message.RunId,
            ItemId = message.ItemId,
            Key = item.Key,
            Version = item.Version,
            Hash = item.Hash,
            Size = item.Size,
        };
    }

    private static ItemCompleted Completed(HarvestCommand message, HarvestReport report)
    {
        var keys = report.Stored.Concat(report.Unchanged).Select(item => item.Key).Distinct().ToList();
        var completed = new ItemCompleted
        {
            ProjectId = message.ProjectId,
            RunId = message.RunId,
            ItemId = message.ItemId,
            State = RunItemState.Done,
            LakeKeys = keys,
        };
        if (report.Succeeded) return completed;

        return completed with { State = RunItemState.Failed, ErrorClass = ErrorClass, ErrorText = Summary(report) };
    }

    private static string Summary(HarvestReport report)
    {
        var parts = new List<string>();
        if (report.Error is not null) parts.Add($"command stopped: {report.Error}");
        if (report.Failed.Count > 0)
        {
            var shown = string.Join("; ", report.Failed.Take(FailuresShown).Select(Describe));
            var more = report.Failed.Count - FailuresShown;
            var tail = more > 0 ? $"; and {more} more" : "";
            parts.Add($"{report.Failed.Count} of {report.Total} items failed: {shown}{tail}");
        }

        return string.Join(". ", parts);
    }

    private static string Describe(HarvestFailure failure)
    {
        return $"{failure.ProviderId} ({failure.ErrorClass}: {failure.ErrorText})";
    }

    private static HarvestReportItem Entry(HarvestItem item, LakeItem stored)
    {
        return new HarvestReportItem
        {
            Kind = item.Kind,
            ProviderId = item.ProviderId,
            Key = stored.Key,
            Version = stored.Version,
        };
    }

    private static HarvestFailure Failure(HarvestItem item, string errorClass, string errorText)
    {
        return new HarvestFailure
        {
            Kind = item.Kind,
            ProviderId = item.ProviderId,
            ErrorClass = errorClass,
            ErrorText = errorText,
        };
    }

    private static bool IsCancellation(Exception exception, CancellationToken token)
    {
        return exception is OperationCanceledException && token.IsCancellationRequested;
    }
}
