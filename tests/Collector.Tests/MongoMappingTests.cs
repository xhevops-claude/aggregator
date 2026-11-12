using Collector.Core.Domains;
using Collector.Core.Entities;
using Collector.Core.Staging;
using Collector.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Collector.Tests;

public class MongoMappingTests
{
    private static readonly Guid ProjectId = Guid.Parse("0b7c1f8e-2d41-4c55-9f3a-1e2d3c4b5a69");
    private static readonly DateTime Harvested = new(2026, 11, 12, 9, 30, 0, DateTimeKind.Utc);

    public MongoMappingTests() => MongoMapping.Register();

    [Fact]
    public void Product_with_shoe_fields_round_trips()
    {
        var product = NewProduct();

        var document = product.ToBsonDocument();
        var back = BsonSerializer.Deserialize<Product>(document);

        Assert.Equal(product.Fields, back.Fields);
        Assert.Equal(product.Images, back.Images);
        Assert.Equal(product with { Images = back.Images, Aliases = back.Aliases }, back);
    }

    [Fact]
    public void Documents_use_camel_case_standard_guids_and_string_enums()
    {
        var listing = NewListing();

        var document = listing.ToBsonDocument();

        Assert.Equal(BsonBinarySubType.UuidStandard, document["_id"].AsBsonBinaryData.SubType);
        Assert.Equal(BsonBinarySubType.UuidStandard, document["projectId"].AsBsonBinaryData.SubType);
        Assert.Equal("InStock", document["availability"].AsString);
        Assert.Equal("43", document["sizes"][0]["label"].AsString);
    }

    [Fact]
    public void Prices_are_stored_as_decimal128()
    {
        var document = NewListing().ToBsonDocument();

        Assert.Equal(BsonType.Decimal128, document["price"].BsonType);
        Assert.Equal(4990.00m, document["price"].AsDecimal);
    }

    [Fact]
    public void Nulls_are_left_out()
    {
        var document = NewListing().ToBsonDocument();

        Assert.False(document.Contains("oldPrice"));
        Assert.False(document.Contains("productId"));
    }

    [Fact]
    public void Staging_keeps_the_concrete_entity_type()
    {
        var staged = new StagingDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = ProjectId,
            Kind = EntityKind.Listing,
            Entity = NewListing(),
        };

        var document = staged.ToBsonDocument();
        var back = BsonSerializer.Deserialize<StagingDocument>(document);

        Assert.Equal("listing", document["entity"]["_t"].AsString);
        var listing = Assert.IsType<Listing>(back.Entity);
        Assert.Equal(4990.00m, listing.Price);
    }

    private static Product NewProduct() => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = ProjectId,
        Namespace = "clothing/shoes/sportvision",
        Provider = "sportvision",
        ProviderId = "8731122",
        SourceUrl = "https://www.sportvision.mk/mk/patiki/8731122-vs-pace-2-0",
        HarvestedAt = Harvested,
        Name = "adidas VS PACE 2.0",
        Brand = "adidas",
        Sku = "HP6005",
        Images = ["https://www.sportvision.mk/files/images/slike_proizvoda/HP6005.jpg"],
        Fields = new ShoeFields { ModelCode = "LSH59", Colourway = "white" },
    };

    private static Listing NewListing() => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = ProjectId,
        Namespace = "clothing/shoes/sportvision",
        Provider = "sportvision",
        ProviderId = "8731122",
        HarvestedAt = Harvested,
        Title = "adidas VS PACE 2.0",
        Price = 4990.00m,
        Currency = "MKD",
        Availability = Availability.InStock,
        Sizes = [new Size { Label = "43", System = "EU", InStock = true }],
        Url = "https://www.sportvision.mk/mk/patiki/8731122-vs-pace-2-0",
    };
}
