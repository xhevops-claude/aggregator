namespace Collector.Storing;

public static class RejectionReasons
{
    public const string UnknownProject = "unknown project";
    public const string ProjectMismatch = "entity belongs to another project";
    public const string MissingIds = "missing ids";
    public const string EntityIdMismatch = "staged entity id differs from the entity";
    public const string WrongKind = "wrong kind";
    public const string Unreadable = "unreadable staging document";
}
