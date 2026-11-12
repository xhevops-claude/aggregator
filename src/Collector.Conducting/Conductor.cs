using Collector.Core.Messages;
using Collector.Core.Platform;
using Collector.Core.Runs;
using Collector.Core.Staging;
using Collector.Data;
using MongoDB.Driver;

namespace Collector.Conducting;

public sealed class Conductor(CollectorDatabase db, IMessagePublisher publisher, TimeProvider time)
{
    public const string DefaultStrategy = "dev";

    private readonly LeafResolver leaves = new(db);
    private readonly TargetSelector targets = new(db);
    private readonly RunLog events = new(db, time);

    public async Task<Run> StartAsync(RunRequested request, CancellationToken token)
    {
        var runId = request.RunId == Guid.Empty ? Guid.NewGuid() : request.RunId;
        var existing = await db.Runs.Find(r => r.Id == runId).FirstOrDefaultAsync(token);
        if (existing is not null) return existing;

        var leaf = await leaves.ResolveAsync(request.ProjectId, request.Namespace, token);
        var job = await FindJobAsync(request, leaf, token);
        var strategy = await FindStrategyAsync(request, token);

        var run = new Run
        {
            Id = runId,
            ProjectId = request.ProjectId,
            Namespace = leaf.Operation.Path,
            JobId = job.Id,
            StrategyId = strategy.Id,
            Parameters = RunParameters.From(request),
            StartedBy = request.RequestedBy,
            State = RunState.Running,
            StartedAt = Now(),
        };
        await db.Runs.InsertOneAsync(run, cancellationToken: token);

        var commands = job.Commands.Select((command, order) => NewCommand(run, command, order)).ToList();
        await db.RunCommands.InsertManyAsync(commands, cancellationToken: token);
        await events.AddAsync(run, "started", $"{job.Name} with {strategy.Name}", token);

        await DispatchAsync(run, leaf, strategy, commands[0], [], token);
        return run;
    }

    public async Task OnItemCompletedAsync(ItemCompleted completed, CancellationToken token)
    {
        var run = await db.Runs.Find(r => r.Id == completed.RunId).FirstOrDefaultAsync(token);
        if (run is null) return;

        var item = await db.RunItems.Find(i => i.Id == completed.ItemId).FirstOrDefaultAsync(token);
        if (item is not null && await SettleItemAsync(item, completed, token))
        {
            await ContinueAsync(run, item, completed, token);
        }

        await FinishWhenDoneAsync(run, token);
    }

    private async Task<bool> SettleItemAsync(RunItem item, ItemCompleted completed, CancellationToken token)
    {
        var filter = Builders<RunItem>.Filter;
        var stillRunning = filter.Eq(i => i.Id, item.Id) & filter.Eq(i => i.State, RunItemState.Running);
        var set = Builders<RunItem>.Update;
        var update = set.Set(i => i.State, completed.State).Set(i => i.FinishedAt, Now());
        update = update.Set(i => i.ErrorClass, completed.ErrorClass).Set(i => i.ErrorText, completed.ErrorText);
        update = update.Set(i => i.LakeKeys, completed.LakeKeys);

        var result = await db.RunItems.UpdateOneAsync(stillRunning, update, cancellationToken: token);
        if (result.ModifiedCount == 0) return false;

        var commandState = completed.State == RunItemState.Done ? RunState.Done : RunState.WaitingForDecision;
        var commandUpdate = Builders<RunCommand>.Update.Set(c => c.State, commandState);
        await db.RunCommands.UpdateOneAsync(c => c.Id == item.CommandId, commandUpdate, cancellationToken: token);
        return true;
    }

    private async Task ContinueAsync(Run run, RunItem item, ItemCompleted completed, CancellationToken token)
    {
        var commands = await db.RunCommands.Find(c => c.RunId == run.Id).SortBy(c => c.Order).ToListAsync(token);
        var finished = commands.First(c => c.Id == item.CommandId);
        await events.AddAsync(run, "completed", $"{finished.Name}: {completed.State}", token);

        var next = commands.FirstOrDefault(c => c.Order == finished.Order + 1);
        if (next is null) return;

        var leaf = await leaves.ResolveAsync(run.ProjectId, run.Namespace, token);
        var strategy = await db.Strategies.Find(s => s.Id == run.StrategyId).FirstAsync(token);
        await DispatchAsync(run, leaf, strategy, next, completed.LakeKeys, token);
    }

    private async Task DispatchAsync(Run run, ResolvedLeaf leaf, Strategy strategy, RunCommand command,
        List<string> keys, CancellationToken token)
    {
        var item = new RunItem
        {
            Id = Guid.NewGuid(),
            ProjectId = run.ProjectId,
            RunId = run.Id,
            CommandId = command.Id,
            State = RunItemState.Running,
            Attempts = 1,
            StartedAt = Now(),
        };

        IMessage? message = command.Stage switch
        {
            CommandStage.Harvest => await HarvestAsync(run, leaf, strategy, command, item, token),
            CommandStage.Adapt => keys.Count == 0 ? null : Adapt(run, command, item, keys),
            _ => null,
        };

        if (message is null)
        {
            await SkipAsync(run, command, token);
            return;
        }

        await db.RunItems.InsertOneAsync(item, cancellationToken: token);
        var running = Builders<RunCommand>.Update.Set(c => c.State, RunState.Running);
        await db.RunCommands.UpdateOneAsync(c => c.Id == command.Id, running, cancellationToken: token);
        await events.AddAsync(run, "dispatched", command.Name, token);

        var name = command.Stage == CommandStage.Harvest ? MessageNames.HarvestCommand : MessageNames.AdaptCommand;
        await PublishAsync(name, message, token);
    }

    private async Task<HarvestCommand?> HarvestAsync(Run run, ResolvedLeaf leaf, Strategy strategy,
        RunCommand command, RunItem item, CancellationToken token)
    {
        var limit = RunParameters.Limit(run);
        var selected = await targets.SelectAsync(run, command.Name, limit, token);
        if (selected is { Count: 0 }) return null;

        return new HarvestCommand
        {
            ProjectId = run.ProjectId,
            RunId = run.Id,
            ItemId = item.Id,
            Namespace = run.Namespace,
            Command = command.Name,
            Source = leaf.Source.Key,
            Targets = selected ?? [],
            Parameters = RunParameters.ForCommand(run),
            Limit = limit,
            Mechanism = strategy.Mechanism,
            Pacing = strategy.Pacing,
        };
    }

    private static AdaptCommand Adapt(Run run, RunCommand command, RunItem item, List<string> keys)
    {
        return new AdaptCommand
        {
            ProjectId = run.ProjectId,
            RunId = run.Id,
            ItemId = item.Id,
            Namespace = run.Namespace,
            Command = command.Name,
            LakeKeys = keys,
        };
    }

    private async Task SkipAsync(Run run, RunCommand command, CancellationToken token)
    {
        var done = Builders<RunCommand>.Update.Set(c => c.State, RunState.Done);
        await db.RunCommands.UpdateOneAsync(c => c.Id == command.Id, done, cancellationToken: token);
        await events.AddAsync(run, "skipped", $"{command.Name}: nothing to do", token);

        var after = await db.RunCommands.Find(c => c.RunId == run.Id && c.Order > command.Order).ToListAsync(token);
        foreach (var later in after)
        {
            await db.RunCommands.UpdateOneAsync(c => c.Id == later.Id, done, cancellationToken: token);
            await events.AddAsync(run, "skipped", $"{later.Name}: no input", token);
        }

        await FinishWhenDoneAsync(run, token);
    }

    private async Task FinishWhenDoneAsync(Run run, CancellationToken token)
    {
        var open = new[] { RunState.Queued, RunState.Running };
        var commandFilter = Builders<RunCommand>.Filter;
        var openCommands = commandFilter.Eq(c => c.RunId, run.Id) & commandFilter.In(c => c.State, open);
        if (await db.RunCommands.CountDocumentsAsync(openCommands, cancellationToken: token) > 0) return;

        var stagingFilter = Builders<StagingDocument>.Filter;
        var ofRun = stagingFilter.Eq(s => s.ProjectId, run.ProjectId) & stagingFilter.Eq(s => s.RunId, run.Id);
        var pending = ofRun & stagingFilter.Eq(s => s.State, StagingState.Pending);
        if (await db.Staging.CountDocumentsAsync(pending, cancellationToken: token) > 0) return;

        var rejected = ofRun & stagingFilter.Eq(s => s.State, StagingState.Rejected);
        var anyRejected = await db.Staging.CountDocumentsAsync(rejected, cancellationToken: token) > 0;
        var waiting = commandFilter.Eq(c => c.State, RunState.WaitingForDecision);
        var stuck = commandFilter.Eq(c => c.RunId, run.Id) & waiting;
        var anyStuck = await db.RunCommands.CountDocumentsAsync(stuck, cancellationToken: token) > 0;
        var state = anyRejected || anyStuck ? RunState.WaitingForDecision : RunState.Done;

        var runFilter = Builders<Run>.Filter;
        var stillRunning = runFilter.Eq(r => r.Id, run.Id) & runFilter.Eq(r => r.State, RunState.Running);
        var finish = Builders<Run>.Update.Set(r => r.State, state).Set(r => r.FinishedAt, Now());
        var result = await db.Runs.UpdateOneAsync(stillRunning, finish, cancellationToken: token);
        if (result.ModifiedCount == 1) await events.AddAsync(run, "finished", state.ToString(), token);
    }

    private async Task<Job> FindJobAsync(RunRequested request, ResolvedLeaf leaf, CancellationToken token)
    {
        var jobs = db.Jobs.Find(j => j.ProjectId == request.ProjectId && j.Name == request.Job);
        var job = await jobs.FirstOrDefaultAsync(token);
        if (job is null) throw new InvalidOperationException($"Unknown job {request.Job}");
        if (job.Commands.Count == 0) throw new InvalidOperationException($"Job {job.Name} has no commands");

        var pairedHere = leaf.Operation.Jobs.Any(pair => pair.JobId == job.Id);
        if (!pairedHere) throw new InvalidOperationException($"Job {job.Name} is not paired with {request.Namespace}");
        return job;
    }

    private async Task<Strategy> FindStrategyAsync(RunRequested request, CancellationToken token)
    {
        var name = string.IsNullOrWhiteSpace(request.Strategy) ? DefaultStrategy : request.Strategy;
        var strategies = db.Strategies.Find(s => s.ProjectId == request.ProjectId && s.Name == name);
        var strategy = await strategies.FirstOrDefaultAsync(token);
        if (strategy is null) throw new InvalidOperationException($"Unknown strategy {name}");
        return strategy;
    }

    private static RunCommand NewCommand(Run run, JobCommand command, int order) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = run.ProjectId,
        RunId = run.Id,
        Order = order,
        Stage = command.Stage,
        Name = command.Name,
        State = RunState.Queued,
    };

    private Task PublishAsync(string name, IMessage message, CancellationToken token) => message switch
    {
        HarvestCommand harvest => publisher.PublishAsync(name, harvest, token),
        AdaptCommand adapt => publisher.PublishAsync(name, adapt, token),
        _ => throw new InvalidOperationException($"Cannot publish {message.GetType().Name}"),
    };

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
