using Grpc.Core;

namespace TaskTracker.Tests.TestKit;

/// <summary>
/// Wraps a plain response object in the <see cref="AsyncUnaryCall{T}"/> that a
/// generated gRPC client method returns.
///
/// Generated clients declare their methods virtual and expose a protected parameterless
/// constructor precisely so they can be mocked — but the return type is not a Task, so
/// <c>ReturnsAsync</c> does not apply. This helper is the missing adapter.
/// </summary>
public static class GrpcCall
{
    public static AsyncUnaryCall<T> Returning<T>(T response) => new(
        Task.FromResult(response),
        Task.FromResult(new Metadata()),
        () => Status.DefaultSuccess,
        () => [],
        () => { });
}
