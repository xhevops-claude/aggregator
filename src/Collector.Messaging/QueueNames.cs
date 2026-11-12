namespace Collector.Messaging;

public static class QueueNames
{
    public const string HarvesterHarvestCommand = "harvester.harvest-command";
    public const string ConductorLakeItemStored = "conductor.lake-item-stored";
    public const string AdapterLakeItemStored = "adapter.lake-item-stored";
    public const string AdapterAdaptCommand = "adapter.adapt-command";
    public const string StoreWriterEntityAdapted = "store-writer.entity-adapted";
    public const string ConductorItemCompleted = "conductor.item-completed";
}
