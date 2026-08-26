using AutoFixture;
using AutoFixture.AutoMoq;

namespace TaskTracker.Tests.TestKit;

/// <summary>
/// Shared AutoFixture setup for every test class in this project.
///
/// xUnit builds a fresh instance of the test class per test method, so this fixture
/// is never shared between tests and no state can leak across them.
/// </summary>
public abstract class TestBase
{
    protected readonly IFixture Fixture;

    protected TestBase()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization());

        // User -> WorkTasks -> Project -> Users is a cycle. AutoFixture throws on
        // cycles by default; tell it to stop and leave the looping property null.
        Fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => Fixture.Behaviors.Remove(b));
        Fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }
}
