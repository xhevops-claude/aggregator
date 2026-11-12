using Collector.Core.Domains;
using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Data;
using MongoDB.Bson;

namespace Collector.Storing.Tests;

[Trait("Category", "Integration")]
public class StoreWriterHarvestOrderTests(RigDatabase rig) : IClassFixture<RigDatabase>
{
    private const string CatalogUrl = "https://sportm.example/ProductCatalog?Segment=03&Page=1";
    private const string PageUrl = "https://sportm.example/ProductDetails?id=JH6206";
    private const string TileImage = "https://sportm.example/img/JH6206.webp";

    private static readonly DateTime TileAt = FixedTime.Start;
    private static readonly DateTime PageAt = FixedTime.Start.AddSeconds(10);
    private static readonly DateTime LaterTileAt = FixedTime.Start.AddSeconds(20);
    private static readonly Guid CategoryId = Guid.Parse("6f1c3e0a-41f5-4c55-9a43-0b5d9c1f7e21");
    private static readonly string[] PageImages = [.. Enumerable.Range(0, 8).Select(GalleryImage)];
    private static readonly string[] SeededElements =
        ["_id", "projectId", "createdAt", "updatedAt", "aliases", "aliasClaims", "productId"];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_older_tile_only_fills_what_the_newer_product_page_left_empty(bool tileFirst)
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var tile = Tile(store, TileAt);
        var page = Page(store, PageAt);

        await ApplyAsync(store, tileFirst ? [tile, page] : [page, tile]);

        var product = await store.ProductAsync(CanonicalId(store));
        Assert.Equal(PageImages, product.Images);
        Assert.Equal(PageUrl, product.SourceUrl);
        Assert.Equal(PageAt, product.HarvestedAt);
        Assert.Equal("adidas Runfalcon 5", product.Name);
        Assert.Equal(CategoryId, product.CategoryId);
        var fields = Assert.IsType<ShoeFields>(product.Fields);
        Assert.Equal("RUNFALCON", fields.ModelCode);
        Assert.Equal("grey", fields.Colourway);

        var listing = await store.ListingAsync(page.Listing.Id);
        Assert.Equal("adidas Runfalcon 5", listing.Title);
        Assert.Equal(PageUrl, listing.SourceUrl);
        Assert.Equal(PageAt, listing.HarvestedAt);
        Assert.Equal(4490m, listing.Price);
        Assert.Equal(4990m, listing.OldPrice);
        Assert.Equal(Availability.InStock, listing.Availability);
        Assert.Equal("42", Assert.Single(listing.Sizes).Label);
        Assert.Equal(PageAt, listing.InfoCheckedAt);
        Assert.Equal(PageAt, listing.StockCheckedAt);
        Assert.Equal(TimeSpan.FromHours(6), listing.RefreshInterval);
        Assert.Equal(product.Id, listing.ProductId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_older_product_page_never_brings_back_an_old_price_the_newer_tile_dropped(bool tileFirst)
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var tile = Tile(store, LaterTileAt);
        var page = Page(store, PageAt);

        await ApplyAsync(store, tileFirst ? [tile, page] : [page, tile]);

        var listing = await store.ListingAsync(tile.Listing.Id);
        Assert.Equal(4990m, listing.Price);
        Assert.Null(listing.OldPrice);
        Assert.False((await store.RawAsync(CollectionNames.Listings, tile.Listing.Id)).Contains("oldPrice"));
        Assert.Equal("adidas Runfalcon", listing.Title);
        Assert.Equal(CatalogUrl, listing.SourceUrl);
        Assert.Equal(LaterTileAt, listing.HarvestedAt);
        Assert.Equal(Availability.InStock, listing.Availability);
        Assert.Equal("42", Assert.Single(listing.Sizes).Label);
        Assert.Equal(PageAt, listing.StockCheckedAt);
        Assert.Equal("Mesh upper", listing.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public async Task Tile_and_product_page_give_the_same_documents_in_either_order(int tileSeconds)
    {
        var tileAt = FixedTime.Start.AddSeconds(tileSeconds);
        var tileFirst = await DocumentsAfterAsync(tileAt, tileFirst: true);
        var pageFirst = await DocumentsAfterAsync(tileAt, tileFirst: false);

        Assert.Equal(tileFirst[0], pageFirst[0]);
        Assert.Equal(tileFirst[1], pageFirst[1]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_shared_product_keeps_the_newest_provider_view(bool olderFirst)
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var sportm = Page(store, PageAt).Product;
        var intersport = store.NewProduct("intersport", "4061612", "adidas", "JH6206") with
        {
            HarvestedAt = TileAt,
            SourceUrl = "https://intersport.example/patiki",
            Name = "Runfalcon 5 adidas",
            Images = ["https://intersport.example/4061612.jpg"],
        };

        Product[] order = olderFirst ? [intersport, sportm] : [sportm, intersport];
        foreach (var product in order) await store.StageAndApplyAsync(product);

        var stored = await store.ProductAsync(CanonicalId(store));
        Assert.Equal(PageImages, stored.Images);
        Assert.Equal(PageUrl, stored.SourceUrl);
        Assert.Equal("adidas Runfalcon 5", stored.Name);
        Assert.Equal(PageAt, stored.HarvestedAt);
        Assert.Equal(new[] { sportm.Id, intersport.Id }.Order(), stored.Aliases.Order());
    }

    [Fact]
    public async Task Checked_dates_only_move_forward()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var listing = store.NewListing("sportm", "JH6206");
        var later = FixedTime.Start.AddDays(1);

        await store.StageAndApplyAsync(listing with { InfoCheckedAt = later, StockCheckedAt = later });
        var earlier = FixedTime.Start;
        await store.StageAndApplyAsync(listing with { InfoCheckedAt = earlier, StockCheckedAt = earlier });

        var stored = await store.ListingAsync(listing.Id);
        Assert.Equal(later, stored.InfoCheckedAt);
        Assert.Equal(later, stored.StockCheckedAt);
    }

    private async Task<BsonDocument[]> DocumentsAfterAsync(DateTime tileAt, bool tileFirst)
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var tile = Tile(store, tileAt);
        var page = Page(store, PageAt);
        await ApplyAsync(store, tileFirst ? [tile, page] : [page, tile]);

        var product = await store.RawAsync(CollectionNames.Products, CanonicalId(store));
        var listing = await store.RawAsync(CollectionNames.Listings, tile.Listing.Id);
        return [Comparable(product), Comparable(listing)];
    }

    private static async Task ApplyAsync(StoreScenario store, Observation[] observations)
    {
        foreach (var observation in observations)
        {
            await store.StageAndApplyAsync(observation.Product);
            await store.StageAndApplyAsync(observation.Listing);
        }
    }

    private static Observation Tile(StoreScenario store, DateTime harvestedAt)
    {
        var product = store.NewProduct("sportm", "JH6206", "adidas", "JH6206") with
        {
            HarvestedAt = harvestedAt,
            SourceUrl = CatalogUrl,
            Name = "adidas Runfalcon",
            Images = [TileImage],
            Fields = new ShoeFields { Colourway = "grey" },
        };
        var listing = store.NewListing("sportm", "JH6206") with
        {
            HarvestedAt = harvestedAt,
            SourceUrl = CatalogUrl,
            Title = "adidas Runfalcon",
            Price = 4990m,
            Currency = "MKD",
            RefreshInterval = TimeSpan.FromHours(6),
        };
        return new Observation(product, listing);
    }

    private static Observation Page(StoreScenario store, DateTime harvestedAt)
    {
        var product = store.NewProduct("sportm", "JH6206", "adidas", "JH6206") with
        {
            HarvestedAt = harvestedAt,
            SourceUrl = PageUrl,
            Name = "adidas Runfalcon 5",
            CategoryId = CategoryId,
            Images = [.. PageImages],
            Fields = new ShoeFields { ModelCode = "RUNFALCON" },
        };
        var listing = store.NewListing("sportm", "JH6206") with
        {
            HarvestedAt = harvestedAt,
            SourceUrl = PageUrl,
            Title = "adidas Runfalcon 5",
            Description = "Mesh upper",
            Price = 4490m,
            OldPrice = 4990m,
            Currency = "MKD",
            Availability = Availability.InStock,
            Sizes = [new Size { Label = "42", InStock = true }],
            InfoCheckedAt = harvestedAt,
            StockCheckedAt = harvestedAt,
        };
        return new Observation(product, listing);
    }

    private static Guid CanonicalId(StoreScenario store)
    {
        return ProductIdentity.Canonical(store.Project.Seed, "adidas", "JH6206")!.Value;
    }

    private static string GalleryImage(int index)
    {
        return index == 0 ? TileImage : $"https://sportm.example/img/JH6206_{index}.webp";
    }

    private static BsonDocument Comparable(BsonDocument document)
    {
        var elements = document.Where(element => !SeededElements.Contains(element.Name));
        return Sorted(new BsonDocument(elements));
    }

    private static BsonDocument Sorted(BsonDocument document)
    {
        var sorted = new BsonDocument();
        foreach (var element in document.OrderBy(element => element.Name, StringComparer.Ordinal))
        {
            sorted[element.Name] = element.Value is BsonDocument inner ? Sorted(inner) : element.Value;
        }

        return sorted;
    }
}
