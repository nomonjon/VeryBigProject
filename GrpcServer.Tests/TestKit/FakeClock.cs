using Microsoft.Extensions.Internal;

namespace GrpcServer.Tests.TestKit;

/// <summary>
/// Controllable clock for <see cref="Microsoft.Extensions.Caching.Memory.MemoryCacheOptions.Clock"/>.
///
/// The alternative — <c>await Task.Delay(...)</c> until a TTL lapses — makes the suite
/// slower and flaky on a loaded CI box. Injecting time turns "wait 61 seconds" into
/// "advance the clock 61 seconds", which is instant and exact.
/// </summary>
public sealed class FakeClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
