using Collector.Cli.Seeding;
using Collector.Core.Platform;
using Collector.Data;
using Collector.Lake;
using Collector.Messaging;
using MongoDB.Driver;
using RabbitMQ.Client;

namespace Collector.Cli;

public sealed class CliContext : IAsyncDisposable
{
    public const string DefaultMongo = "mongodb://localhost:27117/?replicaSet=rs0";
    public const string DatabaseName = "collector";

    private IConnection? rabbit;
    private RabbitMqPublisher? publisher;

    public CliContext()
    {
        MongoMapping.Register();
        var connection = Environment.GetEnvironmentVariable("COLLECTOR_MONGO") ?? DefaultMongo;
        Mongo = new MongoClient(connection).GetDatabase(DatabaseName);
        Db = new CollectorDatabase(Mongo);
    }

    public IMongoDatabase Mongo { get; }
    public CollectorDatabase Db { get; }
    public TimeProvider Time { get; } = TimeProvider.System;
    public RabbitMqOptions Rabbit { get; } = RabbitMqOptions.FromEnvironment();

    public LakeStore Lake() => new(Db, LakeOptions.FromEnvironment(RepositoryRoot()));

    public async Task<IChannel> RabbitChannelAsync(CancellationToken cancellationToken)
    {
        rabbit ??= await RabbitMqConnectionFactory.CreateAsync(Rabbit, "collector-cli", cancellationToken);
        return await rabbit.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    public async Task<RabbitMqPublisher> PublisherAsync(CancellationToken cancellationToken)
    {
        if (publisher is not null) return publisher;

        await using (var channel = await RabbitChannelAsync(cancellationToken))
        {
            await TopologySetup.EnsureAsync(channel, Rabbit.Prefix, cancellationToken);
        }

        rabbit ??= await RabbitMqConnectionFactory.CreateAsync(Rabbit, "collector-cli", cancellationToken);
        publisher = await RabbitMqPublisher.CreateAsync(rabbit, Rabbit, cancellationToken);
        return publisher;
    }

    public async Task<Project> ProjectAsync(CancellationToken cancellationToken)
    {
        var project = await Db.Projects.Find(p => p.Id == ShoesProject.Id).FirstOrDefaultAsync(cancellationToken);
        if (project is null) throw new CliException("project mk-shoes not found, run: collector seed");
        return project;
    }

    public async Task<Leaf> LeafAsync(Project project, string path, CancellationToken cancellationToken)
    {
        var operations = await Db.Operations.Find(o => o.ProjectId == project.Id).ToListAsync(cancellationToken);
        var operation = operations.FirstOrDefault(o => o.Path == path.Trim('/'));
        if (operation is null) throw new CliException($"no operation {path}");
        if (operation.SourceId is null) throw new CliException($"{path} is not a leaf operation");

        var sourceId = operation.SourceId.Value;
        var source = await Db.Sources.Find(s => s.Id == sourceId).FirstOrDefaultAsync(cancellationToken);
        if (source is null) throw new CliException($"source of {path} not found");

        var domain = DomainOf(operation, operations);
        if (domain is null) throw new CliException($"no domain on {path} or its parents");
        return new Leaf(operation, source, domain);
    }

    public async ValueTask DisposeAsync()
    {
        if (publisher is not null) await publisher.DisposeAsync();
        if (rabbit is not null) await rabbit.DisposeAsync();
    }

    private static string? DomainOf(Operation operation, List<Operation> all)
    {
        var current = operation;
        while (current is not null)
        {
            if (current.Domain is not null) return current.Domain;
            current = all.FirstOrDefault(o => o.Id == current.ParentId);
        }

        return null;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Collector.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
