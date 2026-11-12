using Collector.Core.Ids;

namespace Collector.Tests;

public class DeterministicIdTests
{
    private static readonly Guid DnsNamespace = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
    private static readonly Guid ProjectA = Guid.Parse("0b7c1f8e-2d41-4c55-9f3a-1e2d3c4b5a69");
    private static readonly Guid ProjectB = Guid.Parse("7f1e2d3c-4b5a-4968-8776-655443322110");

    [Fact]
    public void Matches_the_rfc_4122_version_5_reference_value()
    {
        var id = DeterministicId.Create(DnsNamespace, "python.org");

        Assert.Equal(Guid.Parse("886313e1-3b8a-5372-9b90-0c9aee199e5d"), id);
    }

    [Fact]
    public void Same_project_and_key_give_the_same_id()
    {
        var first = DeterministicId.For(ProjectA, "sportvision", "listing", "12345");
        var second = DeterministicId.For(ProjectA, "sportvision", "listing", "12345");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Same_key_in_two_projects_gives_two_ids()
    {
        var inA = DeterministicId.For(ProjectA, "sportvision", "listing", "12345");
        var inB = DeterministicId.For(ProjectB, "sportvision", "listing", "12345");

        Assert.NotEqual(inA, inB);
    }

    [Theory]
    [InlineData("sportm", "listing", "12345")]
    [InlineData("sportvision", "product", "12345")]
    [InlineData("sportvision", "listing", "12346")]
    public void Any_part_of_the_key_changes_the_id(string provider, string kind, string providerId)
    {
        var reference = DeterministicId.For(ProjectA, "sportvision", "listing", "12345");

        Assert.NotEqual(reference, DeterministicId.For(ProjectA, provider, kind, providerId));
    }

    [Fact]
    public void Ids_are_version_5_with_the_rfc_variant()
    {
        var text = DeterministicId.For(ProjectA, "sportm", "listing", "JQ5211").ToString();

        Assert.Equal('5', text[14]);
        Assert.Contains(text[19], "89ab");
    }

    [Fact]
    public void Empty_key_parts_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => DeterministicId.For(ProjectA, "", "listing", "1"));
    }
}
