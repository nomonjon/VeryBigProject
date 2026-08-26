using System.Net;
using System.Text;
using System.Text.Json;

namespace TaskTracker.Tests.TestKit;

/// <summary>
/// Replaces the network underneath an <see cref="HttpClient"/>.
///
/// <c>HttpClient</c> is concrete and mostly non-virtual, so it cannot be mocked. The
/// designed seam is <see cref="HttpMessageHandler.SendAsync"/>: swap the handler and
/// the typed client is fully under test control with no sockets involved.
/// </summary>
public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    : HttpMessageHandler
{
    /// <summary>Every request the client sent, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttpMessageHandler ReturningJson<T>(T payload, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        });

    public static StubHttpMessageHandler ReturningRawJson(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    public static StubHttpMessageHandler ReturningStatus(HttpStatusCode status, string body = "")
        => new(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

    public HttpClient CreateClient(string baseAddress = "http://grpcserver:5002/")
        => new(this) { BaseAddress = new Uri(baseAddress) };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(responder(request));
    }
}
