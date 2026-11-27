using System.Collections.Concurrent;
using System.Net;

namespace ApiHost.Tests;

// Stands in for the internet when the app sends webhooks (registered by AppFixture as the
// "webhooks" HTTP client's handler): records every request, and answers 200 unless told to fail.
public class FakeWebhookReceiver
{
    public sealed record Received(Uri Url, IReadOnlyDictionary<string, string> Headers, string Body);

    private readonly ConcurrentQueue<Received> _received = new();
    private readonly ConcurrentDictionary<string, HttpStatusCode> _answers = new(StringComparer.Ordinal);

    public IReadOnlyList<Received> To(string url) => [.. _received.Where(r => r.Url.ToString() == url)];

    // How that URL answers from now on.
    public void Answer(string url, HttpStatusCode status) => _answers[url] = status;

    public HttpMessageHandler CreateHandler() => new Handler(this);

    private sealed class Handler(FakeWebhookReceiver receiver) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
            receiver._received.Enqueue(new Received(request.RequestUri!, headers, await request.Content!.ReadAsStringAsync(cancellationToken)));
            var status = receiver._answers.GetValueOrDefault(request.RequestUri!.ToString(), HttpStatusCode.OK);
            return new HttpResponseMessage(status) { Content = new StringContent(status == HttpStatusCode.OK ? "thanks" : "receiver is down") };
        }
    }
}
