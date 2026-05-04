using System.Net;
using System.Text;

namespace FlipKit.Core.Tests.Infrastructure;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/>. Pass to a real <c>new HttpClient(handler)</c>
/// and the handler will return the configured response(s) without making real network calls.
/// Captures every request for assertion (URL, headers, body).
///
/// Usage — single response:
/// <code>
///   var handler = new StubHttpMessageHandler(HttpStatusCode.OK, jsonBody);
///   var client = new HttpClient(handler);
/// </code>
///
/// Usage — different response per call (fallback chain testing):
/// <code>
///   var responses = new Queue&lt;HttpResponseMessage&gt;([
///     new HttpResponseMessage(HttpStatusCode.NotFound),
///     new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) },
///   ]);
///   var handler = new StubHttpMessageHandler(_ => responses.Dequeue());
/// </code>
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    /// <summary>Every request the handler observed, in call order.</summary>
    public List<HttpRequestMessage> Requests { get; } = new();

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public StubHttpMessageHandler(HttpStatusCode status, string body, string contentType = "application/json")
        : this(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        })
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}
