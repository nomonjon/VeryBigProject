using Grpc.Core;

namespace GrpcServer.Tests.TestKit;

/// <summary>
/// Minimal <see cref="ServerCallContext"/> for calling gRPC service methods directly.
///
/// <c>ServerCallContext</c> is abstract with protected members, so mocking it works but
/// produces a mock that throws or returns null in unpredictable places. A concrete stub
/// with real values is smaller and never surprises you.
/// </summary>
public sealed class TestServerCallContext : ServerCallContext
{
    private readonly Metadata _requestHeaders = [];
    private readonly Metadata _responseTrailers = [];
    private readonly AuthContext _authContext = new(string.Empty, new Dictionary<string, List<AuthProperty>>());

    public static TestServerCallContext Create() => new();

    protected override string MethodCore => "/test.Service/TestMethod";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "ipv4:127.0.0.1:0";
    protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
    protected override Metadata RequestHeadersCore => _requestHeaders;
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore => _responseTrailers;
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => _authContext;

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => Task.CompletedTask;
}
