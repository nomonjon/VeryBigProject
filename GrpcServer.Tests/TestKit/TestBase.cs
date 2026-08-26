using AutoFixture;
using AutoFixture.AutoMoq;

namespace GrpcServer.Tests.TestKit;

/// <summary>
/// Shared AutoFixture setup for every test class in this project.
///
/// xUnit creates a new instance of the test class for each test method, so the
/// fixture built here is never shared between tests — no state can leak from one
/// test into the next.
/// </summary>
public abstract class TestBase
{
    protected readonly IFixture Fixture;

    protected TestBase()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization());

        // Domain objects reference each other (Product -> ProductRule -> ...).
        // By default AutoFixture throws on such loops; tell it to leave the
        // looping property null instead so Build<T>() stays usable.
        Fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => Fixture.Behaviors.Remove(b));
        Fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }
}
