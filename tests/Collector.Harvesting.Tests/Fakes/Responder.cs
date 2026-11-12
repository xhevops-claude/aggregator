namespace Collector.Harvesting.Tests.Fakes;

internal delegate Task<HttpResponseMessage> Responder(HttpRequestMessage request, CancellationToken cancellationToken);
