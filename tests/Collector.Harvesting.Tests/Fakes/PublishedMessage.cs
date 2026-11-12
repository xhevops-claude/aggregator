using Collector.Core.Messages;

namespace Collector.Harvesting.Tests.Fakes;

internal sealed record PublishedMessage(string Name, IMessage Message);
