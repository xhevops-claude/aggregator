using System.Text.Json;
using Collector.Core.Messages;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Collector.Messaging.Tests;

[Trait("Category", "Integration")]
public class PublishTests : RabbitTestBase
{
    [Fact]
    public async Task Published_message_carries_its_name_id_headers_and_camel_case_json()
    {
        var command = TestMessages.HarvestCommand();
        await using var publisher = await CreatePublisherAsync();

        await publisher.PublishAsync(MessageNames.HarvestCommand, command, Cancellation);

        var queue = QueueName(QueueNames.HarvesterHarvestCommand);
        var result = await Channel.BasicGetAsync(queue, autoAck: true, Cancellation);
        Assert.NotNull(result);
        var properties = result.BasicProperties;
        Assert.Equal(MessageJson.ContentType, properties.ContentType);
        Assert.Equal(DeliveryModes.Persistent, properties.DeliveryMode);
        Assert.Equal(MessageNames.HarvestCommand, properties.Type);
        Assert.True(Guid.TryParse(properties.MessageId, out _));
        Assert.Equal(command.ProjectId.ToString(), MessageHeaders.ReadText(properties.Headers, "projectId"));
        Assert.Equal(command.RunId.ToString(), MessageHeaders.ReadText(properties.Headers, "runId"));
        Assert.Equal(command.ItemId.ToString(), MessageHeaders.ReadText(properties.Headers, "itemId"));

        using var json = JsonDocument.Parse(result.Body);
        var root = json.RootElement;
        Assert.Equal(command.ProjectId, root.GetProperty("projectId").GetGuid());
        Assert.Equal("Browser", root.GetProperty("mechanism").GetString());
        Assert.Equal("Jitter", root.GetProperty("pacing").GetProperty("mode").GetString());
        Assert.Equal("4711", root.GetProperty("targets")[0].GetProperty("providerId").GetString());
    }

    [Fact]
    public async Task Consumer_receives_what_was_published()
    {
        var command = TestMessages.HarvestCommand();
        var received = new TaskCompletionSource<HarvestCommand>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Handle(HarvestCommand message, CancellationToken token)
        {
            received.TrySetResult(message);
            return Task.CompletedTask;
        }

        var options = new ConsumerOptions { Queue = QueueNames.HarvesterHarvestCommand };
        await using var consumer = Consumer<HarvestCommand>(options, Handle);
        await consumer.StartAsync(Cancellation);
        await using var publisher = await CreatePublisherAsync();

        await publisher.PublishAsync(MessageNames.HarvestCommand, command, Cancellation);

        var message = await received.Task.WaitAsync(Cancellation);
        Assert.Equal(command.ItemId, message.ItemId);
        Assert.Equal(command.Namespace, message.Namespace);
        Assert.Equal(command.Targets, message.Targets);
        Assert.Equal(command.Parameters, message.Parameters);
        Assert.Equal(command.Pacing, message.Pacing);
        Assert.Equal(command with { Targets = message.Targets, Parameters = message.Parameters }, message);
        await WaitForCountAsync(QueueName(QueueNames.HarvesterHarvestCommand), 0);
    }

    [Fact]
    public async Task Message_nobody_listens_to_is_refused()
    {
        await using var publisher = await CreatePublisherAsync();

        var publish = publisher.PublishAsync("nobody.listens", TestMessages.AdaptCommand(), Cancellation);

        await Assert.ThrowsAnyAsync<PublishException>(() => publish);
    }

    [Fact]
    public async Task Message_without_a_project_is_refused()
    {
        await using var publisher = await CreatePublisherAsync();
        var message = TestMessages.AdaptCommand() with { ProjectId = Guid.Empty };

        var publish = publisher.PublishAsync(MessageNames.AdaptCommand, message, Cancellation);

        await Assert.ThrowsAsync<ArgumentException>(() => publish);
    }
}
