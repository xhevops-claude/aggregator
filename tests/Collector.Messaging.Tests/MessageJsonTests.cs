using System.Text;
using System.Text.Json;
using Collector.Core.Messages;
using Collector.Core.Runs;

namespace Collector.Messaging.Tests;

public class MessageJsonTests
{
    [Fact]
    public void Messages_are_camel_case_with_enums_as_strings_and_no_nulls()
    {
        var message = new ItemCompleted
        {
            ProjectId = TestMessages.ProjectId,
            RunId = TestMessages.RunId,
            ItemId = Guid.NewGuid(),
            State = RunItemState.WaitingForDecision,
        };

        using var json = JsonDocument.Parse(MessageJson.Serialize(message));

        Assert.Equal("WaitingForDecision", json.RootElement.GetProperty("state").GetString());
        Assert.Equal(TestMessages.ProjectId, json.RootElement.GetProperty("projectId").GetGuid());
        Assert.False(json.RootElement.TryGetProperty("errorText", out _));
    }

    [Fact]
    public void Message_round_trips()
    {
        var command = TestMessages.HarvestCommand();

        var ok = MessageJson.TryDeserialize<HarvestCommand>(MessageJson.Serialize(command), out var back);

        Assert.True(ok);
        Assert.Equal(command with { Targets = back!.Targets, Parameters = back.Parameters }, back);
        Assert.Equal(command.Targets, back.Targets);
        Assert.Equal(command.Parameters, back.Parameters);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("{}")]
    [InlineData("{\"projectId\":\"00000000-0000-0000-0000-000000000000\"}")]
    public void Malformed_or_projectless_bodies_are_refused(string body)
    {
        var ok = MessageJson.TryDeserialize<AdaptCommand>(Encoding.UTF8.GetBytes(body), out var message);

        Assert.False(ok);
        Assert.Null(message);
    }
}
