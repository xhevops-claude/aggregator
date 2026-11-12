using Collector.Lake;
using Collector.Messaging;

namespace Collector.Workers;

public sealed record WorkerSettings
{
    public const string RolesVariable = "COLLECTOR_ROLES";
    public const string MongoVariable = "COLLECTOR_MONGO";
    public const string DefaultMongo = "mongodb://localhost:27117/?replicaSet=rs0";
    public const string DatabaseName = "collector";

    public HashSet<string> Roles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string Mongo { get; init; } = DefaultMongo;
    public required LakeOptions Lake { get; init; }
    public required RabbitMqOptions Rabbit { get; init; }

    public bool Runs(string role) => Roles.Contains(role);

    public static WorkerSettings FromEnvironment()
    {
        var roles = Environment.GetEnvironmentVariable(RolesVariable) ?? WorkerRoles.Harvester;
        var names = roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new WorkerSettings
        {
            Roles = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase),
            Mongo = Environment.GetEnvironmentVariable(MongoVariable) ?? DefaultMongo,
            Lake = LakeOptions.FromEnvironment(Directory.GetCurrentDirectory()),
            Rabbit = RabbitMqOptions.FromEnvironment(),
        };
    }
}
