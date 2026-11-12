using Collector.Core.Domains;
using Collector.Core.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Collector.Data;

public static class MongoMapping
{
    private static readonly Lock Gate = new();
    private static bool registered;

    public static void Register()
    {
        lock (Gate)
        {
            if (registered) return;

            var conventions = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new IgnoreIfNullConvention(true),
                new EnumRepresentationConvention(BsonType.String),
            };
            ConventionRegistry.Register("collector", conventions, IsCollectorType);

            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));

            BsonClassMap.RegisterClassMap<Entity>(map => map.AutoMap());
            BsonClassMap.RegisterClassMap<Product>(map => { map.AutoMap(); map.SetDiscriminator("product"); });
            BsonClassMap.RegisterClassMap<Listing>(map => { map.AutoMap(); map.SetDiscriminator("listing"); });
            BsonClassMap.RegisterClassMap<Category>(map => { map.AutoMap(); map.SetDiscriminator("category"); });

            BsonClassMap.RegisterClassMap<DomainFields>(map => map.AutoMap());
            BsonClassMap.RegisterClassMap<ShoeFields>(map =>
            {
                map.AutoMap();
                map.SetDiscriminator(DomainNames.Shoes);
            });

            registered = true;
        }
    }

    private static bool IsCollectorType(Type type) => type.Namespace?.StartsWith("Collector.") == true;
}
