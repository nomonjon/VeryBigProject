using System.Net;
using System.Text;
using System.Text.Json;

namespace GrpcServer.Tests.TestKit;

/// <summary>
/// Replaces the network underneath an <see cref="HttpClient"/>.
///
/// <c>HttpClient</c> is a concrete, mostly non-virtual class, so it cannot be mocked
/// directly. The seam is <see cref="HttpMessageHandler.SendAsync"/> — swap the handler
/// and the client is fully under test control with no sockets involved.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    /// <summary>Every request the client sent, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => _responder = responder;

    public static StubHttpMessageHandler ReturningJson<T>(T payload, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        });

    public static StubHttpMessageHandler ReturningStatus(HttpStatusCode status, string body = "")
        => new(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

    public static StubHttpMessageHandler Throwing(Exception exception)
        => new(_ => throw exception);

    public HttpClient CreateClient(string baseAddress = "http://randomprice:5059/")
        => new(this) { BaseAddress = new Uri(baseAddress) };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}
