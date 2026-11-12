using Collector.Cli;
using Collector.Cli.Commands;
using Collector.Cli.Seeding;
using Collector.Data;
using Collector.Messaging;
using MongoDB.Bson;

const string Usage = """
    usage: collector <command>
      db init                                  check the replica set, create indexes
      mq init                                  declare the RabbitMQ exchanges and queues
      seed                                     create the mk-shoes project, operations, sources, jobs
      harvest <operation> <command> [options]  run one harvest command into the lake
          --listing <path|query>  --brand <name>  --limit <n>  --target <id>[=<url>]  --from-store [--retry]
      adapt <operation> [--key <lake key>]     adapt the latest lake items of the leaf into staging
      apply [--batch <n>]                      apply pending staging documents to the store
      stats                                    counts per collection
    """;

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };

var cliArgs = new CliArgs(args);
if (cliArgs.Positional.Count == 0)
{
    Console.Error.WriteLine(Usage);
    return 1;
}

await using var cli = new CliContext();
try
{
    return cliArgs.Positional[0] switch
    {
        "db" => await DbInitAsync(cli, stop.Token),
        "mq" => await MqInitAsync(cli, stop.Token),
        "seed" => await SeedAsync(cli, stop.Token),
        "harvest" => await HarvestCli.RunAsync(cli, cliArgs, stop.Token),
        "adapt" => await AdaptCli.RunAsync(cli, cliArgs, stop.Token),
        "apply" => await ApplyCli.RunAsync(cli, cliArgs, stop.Token),
        "stats" => await StatsCli.RunAsync(cli, stop.Token),
        _ => UsageError(),
    };
}
catch (CliException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
    Console.Error.WriteLine("cancelled");
    return 130;
}
catch (Exception e)
{
    Console.Error.WriteLine($"unexpected error: {e}");
    return 70;
}

int UsageError()
{
    Console.Error.WriteLine(Usage);
    return 1;
}

static async Task<int> DbInitAsync(CliContext cli, CancellationToken cancellationToken)
{
    var hello = await cli.Mongo.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), null, cancellationToken);
    if (!hello.TryGetValue("setName", out var replicaSet))
    {
        Console.Error.WriteLine("MongoDB is not a replica set, transactions will not work");
        return 2;
    }

    var indexes = await IndexSetup.EnsureAsync(cli.Mongo, cancellationToken);
    Console.WriteLine($"replica set {replicaSet}, database {CliContext.DatabaseName}, {indexes} indexes ready");
    return 0;
}

static async Task<int> MqInitAsync(CliContext cli, CancellationToken cancellationToken)
{
    await using var channel = await cli.RabbitChannelAsync(cancellationToken);
    await TopologySetup.EnsureAsync(channel, cli.Rabbit.Prefix, cancellationToken);
    Console.WriteLine($"topology ready on {cli.Rabbit.Uri.Host}:{cli.Rabbit.Uri.Port}, prefix {cli.Rabbit.Prefix}");
    return 0;
}

static async Task<int> SeedAsync(CliContext cli, CancellationToken cancellationToken)
{
    Console.WriteLine(await Seeder.SeedShoesAsync(cli.Db, cancellationToken));
    return 0;
}
