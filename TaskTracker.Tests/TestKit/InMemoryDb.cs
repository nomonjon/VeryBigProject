using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;

namespace TaskTracker.Tests.TestKit;

/// <summary>
/// Creates an <see cref="AppDbContext"/> backed by the in-memory provider.
///
/// WorkTaskService writes TaskComment and TaskHistory rows through the DbContext
/// directly rather than through a repository interface, so there is no seam to mock.
/// Mocking DbSet&lt;T&gt; is possible but miserable (async query providers, ChangeTracker),
/// and it verifies the wrong thing: that certain EF methods were called, not that a
/// row was written. A real in-memory context lets the assertion be about rows.
///
/// Caveat worth knowing: the in-memory provider is not a relational database. It has
/// no foreign keys, no constraints and no SQL translation, so it proves persistence
/// logic — never schema correctness.
/// </summary>
public static class InMemoryDb
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            // A unique name per call keeps parallel test classes from sharing a store.
            .UseInMemoryDatabase($"tasktracker-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContext(options);
    }
}
