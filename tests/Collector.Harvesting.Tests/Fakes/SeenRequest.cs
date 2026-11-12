namespace Collector.Harvesting.Tests.Fakes;

internal sealed record SeenRequest(Uri Url, TimeSpan At, string UserAgent, string AcceptLanguage);
