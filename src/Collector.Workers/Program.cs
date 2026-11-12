using Collector.Data;
using Collector.Workers;
using MongoDB.Driver;

var settings = WorkerSettings.FromEnvironment();
MongoMapping.Register();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(settings);
var database = new MongoClient(settings.Mongo).GetDatabase(WorkerSettings.DatabaseName);
builder.Services.AddSingleton(new CollectorDatabase(database));

if (settings.Runs(WorkerRoles.Harvester)) builder.Services.AddHostedService<HarvesterService>();
if (settings.Runs(WorkerRoles.Adapter)) builder.Services.AddHostedService<AdapterService>();
if (settings.Runs(WorkerRoles.StoreWriter)) builder.Services.AddHostedService<StoreWriterService>();

if (settings.Runs(WorkerRoles.Conductor))
{
    builder.Services.AddHostedService<RunRequestService>();
    builder.Services.AddHostedService<CompletionService>();
    builder.Services.AddHostedService<LakeSignalService>();
}

var host = builder.Build();
await host.RunAsync();
