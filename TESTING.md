# Unit testing in this solution

Two parts. **Part 1** is what was done to `GrpcServer.Tests` and `TaskTracker.Tests` and why.
**Part 2** is the lesson — every technique explained against a real test from this repo, so you
can open the file and read the thing being described.

---

# Part 1 — What was done

## 1.1 Starting state

| | Before | After |
|---|---|---|
| GrpcServer.Tests | 3 files, **did not compile** | 191 tests across 18 files |
| TaskTracker.Tests | 1 file, 8 tests | 274 tests across 25 files |
| TaskTracker (app) | **did not compile** | builds |

Three things were already broken before any test was written:

1. `GrpcServer.Tests/ProductGrpcServiceTests.cs:34` called a 3-argument `ProductGrpcService`
   constructor. The class had been refactored to 2 arguments. The test project had not
   compiled since then, so *nothing* in it had run.
2. `ProductRuleServiceTests` stubbed `IProductRepository.GetAllAsync()` for the rule sweep,
   but `ApplyActiveRulesAsync` had moved to `GetWhereAsync2(...)`. Those tests were passing
   against an API the code no longer used.
3. `TaskTracker/Repository/BaseRepository.cs` had an uncommitted change —
   `GetAll` returned `Task<IQueryable<T>>` instead of `Task<List<T>>` — which broke
   `UserRepository` and `ProjectRepository` against their interfaces (`CS0738`).

That is the first lesson of the whole document, free of charge: **a test suite that does not
run in CI decays into fiction.** Both of the "passing" test files were lying.

## 1.2 Structure

Test folders mirror source folders, one test class per production class, named `<Class>Tests`:

```
GrpcServer.Tests/                       TaskTracker.Tests/
├── TestKit/          shared doubles     ├── TestKit/
│   ├── TestBase.cs                      │   ├── TestBase.cs
│   ├── Make.cs        object mother     │   ├── Make.cs
│   ├── FakeCacheService.cs              │   ├── FakeHttpContext.cs
│   ├── FakeClock.cs                     │   ├── InMemoryDb.cs
│   ├── TestServerCallContext.cs         │   ├── RecordingPublisher.cs
│   └── StubHttpMessageHandler.cs        │   ├── StubHttpMessageHandler.cs
├── ApiServices/                         │   └── GrpcCall.cs
├── Services/  (+ Caching/)              ├── Services/
├── Controllers/                         ├── Controllers/
├── Mapper/                              ├── Mappers/
├── Validator/                           └── Common/     Result<T> + Error
└── Models/
```

Why mirroring: finding the tests for a class is never a search, and a production file with no
matching test file is visible at a glance.

## 1.3 Decisions and their cost

| Decision | Why | Cost |
|---|---|---|
| Deleted the 3 old test files | Two did not compile, one duplicated new coverage | Lost nothing that ran |
| `FrameworkReference Microsoft.AspNetCore.App` in both test csproj | Needed for `ControllerBase`, `IActionResult`, `IHttpContextAccessor` | None |
| `Microsoft.EntityFrameworkCore.InMemory` 8.0.0 → TaskTracker.Tests | `WorkTaskService` takes a concrete `AppDbContext` and writes comments/history through it | New dependency; provider is not relational (see §2.11) |
| `Microsoft.Extensions.Caching.Memory` 8.0.1 → GrpcServer.Tests | Testing `MemoryCacheService` against a real `MemoryCache` | None |
| `RabbitMQ.Client` 7.0.0 → TaskTracker.Tests | Only to name `IConnection` in `RecordingPublisher`'s base constructor call | None |
| `NoWarn=MSB3277` | Test projects are `net10.0`, apps are `net8.0`; assembly unification warnings were drowning real output | Hides a class of warning — remove it if the TFMs are ever aligned |

### Two production files were edited

Both were required to make the code testable at all; neither changes behaviour.

**`TaskTracker/Services/RabbitMqPublisher.cs`** — `PublishAsync` and `PublishLogAsync` are now
`virtual`. `WorkTaskService` takes this concrete class, and every publish opens an AMQP channel.
Without `virtual` there is no way to test `WorkTaskService` without a live broker.

**`TaskTracker/Repository/{User,Project}Repository.cs`** — added `new ... GetAll(...)` overrides
returning `Task<List<T>>`. This repairs the pre-existing `CS0738` break from the `BaseRepository`
change, using the same shadowing pattern `WorkTaskRepository` already used. Nothing consumed the
`IQueryable` form.

## 1.4 Coverage

`dotnet test --collect:"XPlat Code Coverage"`. Every class in the agreed scope
(services, mappers, validators, controllers):

| | Line | Branch |
|---|---|---|
| All GrpcServer in-scope classes | **100%** | **100%** |
| All TaskTracker in-scope classes | **100%** | 100%, except ↓ |
| `TaskTracker.Services.ProjectService` | 100% | 80% |
| `TaskTracker.Services.WorkTaskService` | 100% | 79% |

The two branch gaps are null-conditional chains — `claims?.FindFirst(...)?.Value`,
`project?.Users?.Any(...)` — where the compiler emits a branch per `?.`. Several are already
covered (`FakeHttpContext.NoContext()`, `Anonymous()`, a task with `Project == null`, a project
with `Users == null`); the rest are combinations that cannot occur together. Chasing them would
add tests that assert nothing new. See §2.13.

Deliberately **out of scope** and therefore at 0%: repositories, `ProductRuleWorker`,
`RabbitMqLogSink`, `RedisCacheService`, GraphQL resolvers, middleware, `Program.cs`, migrations,
generated proto code.

## 1.5 What the tests found

Real issues, pinned by tests that document today's behaviour rather than silently passing:

1. **The rule sweep never touches new products.**
   `ProductRuleService.ApplyActiveRulesAsync` filters on `p.LastCheckedTime <= cutoff`.
   `LastCheckedTime` is `DateTime?`, and a null comparison is `false` in both C# and SQL, so a
   freshly created product is never swept and keeps the default colour forever.
   → `ApplyActiveRulesAsync_NeverPicksUpProductsThatWereNeverChecked`
   *(the sweep also never writes `LastCheckedTime`, so the same 10 rows are reprocessed every tick)*

2. **`DELETE /api/User/{id}` answers 200 for a user that does not exist.**
   `UserService.DeleteUserAsync` returns `Success(false)`; `ProjectService.DeleteProjectAsync`
   returns `Failure(NotFound)` for the identical situation.
   → `DeleteUserAsync_ReturnsSuccessFalse_WhenThereWasNothingToDelete`

3. **`POST /api/User` answers 200 on failure.** The action wraps the `Result` in `Ok(...)`
   instead of calling `.ToResponse()`, so a `BadRequest` failure is serialized inside a 200.
   → `CreateUser_Returns200_EvenWhenTheServiceFailed`

4. **A missing product escapes `ProductsController` as an `RpcException`.**
   `ProductApiService.GetByIdAsync` never returns null, so the controller's `NotFound` branch
   is unreachable and a gRPC `NotFound` propagates uncaught.
   → `GetById_PropagatesAnRpcException_WhenTheProductIsMissing`

None of these were fixed — that was not the task. Each has a test that will fail the moment
someone does fix it, which is the correct place for that decision.

---

# Part 2 — The lesson

## 2.1 What "unit" means

A unit test exercises **one class**, with every dependency that crosses a process boundary
replaced by something you control. No database, no HTTP, no broker, no clock, no filesystem.

That is why they run in seconds:

```
GrpcServer.Tests   191 tests   0.8 s
TaskTracker.Tests  274 tests   5 s      (BCrypt hashing is the 5 s)
```

Speed is not a nice-to-have. A suite you run after every save changes how you write code; a
suite you run before lunch does not.

**What a unit test cannot tell you:** that your SQL is valid, that your EF mappings match the
schema, that DI is wired, that two services agree on a JSON contract. Those need integration
tests. Do not fake your way to a green suite that proves none of it.

## 2.2 Arrange – Act – Assert

Every test in this suite has three parts, usually with blank lines instead of comments:

```csharp
[Fact]
public async Task DeleteProductAsync_ReturnsTrue_AndInvalidatesBothKeys()
{
    var id = Guid.NewGuid();                                          // Arrange
    _repository.Setup(r => r.DeleteAsync(id)).ReturnsAsync(true);

    var result = await _sut.DeleteProductAsync(id);                   // Act

    Assert.True(result);                                              // Assert
    Assert.Equal([CacheKeys.ProductList, CacheKeys.Product(id)], _cache.RemovedKeys);
}
```

`_sut` = *system under test*. One name, every file, so the thing being tested is never in doubt.

**One act per test.** If you need two `await`s on the SUT to reach the assertion, you are
testing a scenario, not a unit — split it, or the failure message will not tell you which call
broke.

## 2.3 Naming

`MethodName_ExpectedBehaviour_Condition`. Written so the test list reads as a specification:

```
CreateRuleAsync_Rejects_InvalidExpressions
UpdateProductAsync_DoesNotInvalidateAnything_WhenTheUpdateMissed
GetWorkTaskByIdAsync_ReturnsForbidden_WhenTheProjectNavigationWasNotLoaded
LoginAsync_UsesTheSameErrorForUnknownEmailAndWrongPassword
```

Name the **behaviour**, never the mechanism. `Should_Call_Repository_Once` tells a future reader
nothing about why it matters; `DoesNotInvalidateAnything_WhenTheUpdateMissed` tells them exactly
what breaks if they delete it.

## 2.4 The five kinds of test double

Everyone says "mock" for all of them. They are different, and picking wrong is the most common
way to write brittle tests. All five are in this repo.

| Kind | Purpose | In this repo |
|---|---|---|
| **Dummy** | Fills a parameter, never used | `NullLogger<ProductController>.Instance` |
| **Stub** | Returns canned answers | `_repository.Setup(r => r.GetAllAsync()).ReturnsAsync([...])` |
| **Spy** | Records what happened | `StubHttpMessageHandler.Requests` |
| **Mock** | Asserts an interaction happened | `_rules.Verify(r => r.CreateAsync(...), Times.Never)` |
| **Fake** | Working lightweight implementation | `FakeCacheService`, `InMemoryDb`, `RecordingPublisher` |

**Dummy — use `NullLogger`, not `Mock<ILogger<T>>`:**

```csharp
_sut = new ProductController(_service.Object, NullLogger<ProductController>.Instance);
```
Nothing asserts on log output here, so a mock would be setup noise pretending to be intent.

**Fake — when a mock would be unreadable.** `ICacheService.GetOrSetAsync` takes a factory that
the SUT expects to be invoked. Expressed as a mock, once per generic `T`:

```csharp
_cache.Setup(c => c.GetOrSetAsync(It.IsAny<string>(), It.IsAny<Func<Task<List<ProductDto>?>>>(), It.IsAny<TimeSpan?>()))
      .Returns((string _, Func<Task<List<ProductDto>?>> f, TimeSpan? _) => f());
```

Expressed as a fake, once, forever (`TestKit/FakeCacheService.cs`):

```csharp
public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? ttl = null)
{
    if (_seeded.TryGetValue(key, out var hit)) return (T?)hit;
    FactoryCalls.Add(key);
    return await factory();
}
```

The fake also turned into the *assertion surface*. `RemovedKeys` and `FactoryCalls` are ordinary
lists, so cache-invalidation tests read like prose:

```csharp
Assert.Equal([CacheKeys.RuleList, CacheKeys.ActiveRuleList, CacheKeys.Rule(id)], _cache.RemovedKeys);
```

## 2.5 Choosing the double — the actual decision rule

**Pick the double from the question you are asking.** `WorkTaskServiceTests` uses three
different kinds in one class, on purpose:

| Dependency | Double | Because the question is |
|---|---|---|
| `IWorkTaskRepository` etc. | Moq | *Was `Update` called with the right task?* |
| `AppDbContext` | real in-memory EF | *Does a `TaskHistory` row exist with these values?* |
| `RabbitMqPublisher` | `RecordingPublisher` fake | *What event was published?* |

The history assertion is the clearest case. With a mocked `DbSet` you would end up asserting
`AddRangeAsync` was called — a statement about EF's API. With the in-memory context you assert
what a support engineer would actually check:

```csharp
var history = await _db.TaskHistories.ToListAsync();
Assert.Equal(["Name", "Status"], history.Select(h => h.FieldName).Order());
Assert.All(history, h => Assert.Equal(caller.Id, h.UserId));
```

## 2.6 Test data: AutoFixture *or* an object mother

Both are in the kit; they answer different needs.

**AutoFixture** (`TestBase`) when the values are irrelevant — "give me *a* product". It fills
every property with anonymous data, which also stops you accidentally depending on a default.
Recursive domain graphs (`User → WorkTasks → Project → Users`) need one line of setup:

```csharp
Fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => Fixture.Behaviors.Remove(b));
Fixture.Behaviors.Add(new OmitOnRecursionBehavior());
```

**An object mother** (`Make`) when the values *are* the test. A rule that fires on
`Quantity < 5` is meaningless unless the reader can see the quantity:

```csharp
var lowStock = Make.Product(name: "Apple", quantity: 2);
var healthy  = Make.Product(name: "Keyboard", quantity: 20);
```

Every parameter has a default, so a test names only the fields it reasons about. The
signal-to-noise ratio of the whole suite depends on this one habit.

`Make` also encodes relationships that AutoFixture cannot express — and TaskTracker's entire
authorization model is a relationship:

```csharp
public static Project ProjectWithMember(User member, Guid? id = null, string name = "Apollo")
    => Project(id: id, name: name, users: [member]);
```

## 2.7 `[Fact]` vs `[Theory]` — and when a Theory is a lie

`[Theory]` is for **the same behaviour over different inputs**:

```csharp
[Theory]
[InlineData("Price > 100", true)]
[InlineData("Price < 100", false)]
[InlineData("Quantity == 10 && Price >= 150", true)]
[InlineData("Name.StartsWith(\"Phone\")", false)]
public async Task EvaluateProductAsync_ReportsWhetherEachRuleMatches(string expression, bool expectedMatch)
```

Seven expression-parser cases in `RulesValidatorTests` are twelve lines instead of ninety, and
adding the eighth is one line.

It is **not** a way to cram unrelated cases together. If the arrange step needs an `if` on the
parameter, you have two tests wearing one coat. Each of these is its own `[Fact]`:

```csharp
UpdateRuleAsync_ReturnsNull_AndInvalidatesNothing_WhenTheRuleDoesNotExist
UpdateRuleAsync_InvalidatesBothListsAndTheRuleItself
```

## 2.8 Assert on behaviour, not on implementation

The single biggest cause of a suite that everyone hates: tests that fail on every refactor while
catching no bugs. Two rules.

**Compile the thing, do not just check it was passed.** Asserting "a predicate reached the
repository" proves nothing — the expression could be `p => true`:

```csharp
await _sut.GetMatchingProductsAsync(rule.Id);

var predicate = captured!.Compile();
Assert.True(predicate(Make.Product(price: 150m)));   // the rule was "Price > 100"
Assert.False(predicate(Make.Product(price: 50m)));
```

That test survives any rewrite of how the predicate is built and fails the moment the rule stops
meaning what it says.

**Use `Verify` for absence, not presence.** A `Verify(..., Times.Once)` that just restates the
line above it is dead weight. `Times.Never` is different — it is the only way to state a
security or correctness property:

```csharp
await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateRuleAsync(Make.RuleDto(expression: expression)));

_rules.Verify(r => r.CreateAsync(It.IsAny<ProductRule>()), Times.Never);   // nothing was written
Assert.Empty(_cache.RemovedKeys);                                          // nothing was evicted
```

"Validation happens *before* the write" is not observable in the return value. This is what
`Verify` is for.

## 2.9 Test the sad paths first

`ProductRuleService.CreateRuleAsync` has one happy path and roughly six ways to fail. The failure
paths are where the bugs live, and they are the paths nobody exercises by hand.

For anything with authorization, cover **three roles, always**:

```csharp
CreateWorkTaskAsync_Succeeds_ForAnAdmin_WithoutCheckingMembership
CreateWorkTaskAsync_Succeeds_ForAProjectMember
CreateWorkTaskAsync_ReturnsForbidden_ForANonMember
```

`FakeHttpContext` exists so that switching role is one word. Make the awkward case cheap and it
gets tested; make it fiddly and it silently does not.

Some assertions only make sense as a *pair*:

```csharp
[Fact]
public async Task LoginAsync_UsesTheSameErrorForUnknownEmailAndWrongPassword()
{
    // ... unknown email, then wrong password ...
    Assert.Same(unknownEmail.Error, wrongPassword.Error);
}
```

Neither call is wrong on its own. Distinguishing them is a user-enumeration vulnerability, and
only a test comparing the two can say so.

## 2.10 Control time — never sleep

`MemoryCacheService` applies a TTL. The lazy test is `await Task.Delay(...)`: slow, and flaky on
a loaded CI box. Inject the clock instead (`TestKit/FakeClock.cs`):

```csharp
var clock = new FakeClock();
var cache = new MemoryCache(new MemoryCacheOptions { Clock = clock });

await sut.GetOrSetAsync("key", factory, TimeSpan.FromSeconds(60));
clock.Advance(TimeSpan.FromSeconds(59));
await sut.GetOrSetAsync("key", factory);      // hit
Assert.Equal(1, calls);

clock.Advance(TimeSpan.FromSeconds(2));
await sut.GetOrSetAsync("key", factory);      // miss
Assert.Equal(2, calls);
```

Instant, exact, and it can test a 24-hour TTL. Same principle for randomness, GUIDs and `Now`:
**if it is nondeterministic, it is a dependency — inject it.**

Where the code owns the timestamp and cannot be injected, assert a *window*, never equality:

```csharp
var before = DateTime.UtcNow;
var result = await _sut.CreateRuleAsync(Make.RuleDto());
Assert.InRange(result.CreatedAt, before, DateTime.UtcNow);
```

## 2.11 Seams: what to do when a class cannot be mocked

Moq can only substitute interfaces and virtual members. Four of this codebase's dependencies are
neither, and each needed a different answer. **Recognising the seam is the skill.**

| Blocked by | Seam used | File |
|---|---|---|
| `HttpClient` (concrete, non-virtual) | swap the `HttpMessageHandler` under it | `StubHttpMessageHandler.cs` |
| `AppDbContext` (concrete) | real EF in-memory provider | `InMemoryDb.cs` |
| `RabbitMqPublisher` (concrete) | made 2 methods `virtual`, subclassed | `RecordingPublisher.cs` |
| `ProductServiceClient` (generated) | already virtual — mock it, wrap the reply | `GrpcCall.cs` |

The gRPC one is worth a look. Generated clients are virtual *by design*, but return
`AsyncUnaryCall<T>`, which is not a `Task`, so `ReturnsAsync` does not apply. One adapter fixes it
for every call in the suite:

```csharp
public static AsyncUnaryCall<T> Returning<T>(T response) => new(
    Task.FromResult(response), Task.FromResult(new Metadata()),
    () => Status.DefaultSuccess, () => [], () => { });
```

And when a class has *no* seam at all — `AuthController` takes a concrete `AuthService` with
non-virtual methods — **move one layer down**: mock `IUserRepository` and let the real service
run. `AuthControllerTests` does this, and as a bonus it verifies controller and service still
agree.

> **In-memory EF caveat.** It is not a relational database: no foreign keys, no constraints, no
> SQL translation. It is fine for "was this row written". It proves *nothing* about your schema
> or your queries. For that, use SQLite in-memory or a real Postgres in a container.

## 2.12 Pinning tests: encode reality, not wishes

When you find a bug mid-suite you have three options. Fixing it may be out of scope; deleting
the test is dishonest; **pinning** it is the third:

```csharp
[Fact]
public async Task ApplyActiveRulesAsync_NeverPicksUpProductsThatWereNeverChecked()
{
    // KNOWN GAP, pinned deliberately: LastCheckedTime is nullable and the sweep filters on
    // `p.LastCheckedTime <= cutoff`. In both C# and SQL a NULL comparison is false, so a
    // freshly created product is never swept and keeps the default colour forever.
    // Flip this to Assert.True the day the filter is fixed.
    Assert.False(captured!.Compile()(Make.Product(lastCheckedTime: null)));
}
```

The suite stays green, the bug stays visible, and whoever fixes it gets a failing test pointing
at the decision. All four findings in §1.5 are pinned this way.

## 2.13 What coverage does and does not tell you

100% line coverage on `WorkTaskService` means every line executed. It does **not** mean every
line was checked — a test with no assertions would score identically.

Use coverage as a **gap finder, never as a goal**. Concretely, in this session it found:

- `ToProjectWithIdDto` mapping tasks (only the null branch was covered) → one test added
- Three `TaskHistory` field branches — Assignee, Description, Priority → two tests added

And it correctly stopped being useful at the null-conditional branches in §1.4. Tests written to
move that number would assert nothing. **When the only way to raise coverage is a test whose
name you cannot write as a sentence, stop.**

## 2.14 The anti-patterns this suite avoids

| Anti-pattern | Why it hurts | Instead |
|---|---|---|
| Logic in the test (`if`/`foreach` deciding the expectation) | You now have untested code testing your code | Use `[Theory]` with explicit expected values |
| Asserting on log messages | Logs are not the contract; wording changes break tests | `NullLogger`. *(Exception: `OrderServiceTests` — there the logging genuinely **is** the behaviour)* |
| `Thread.Sleep` / `Task.Delay` | Slow and flaky | Inject the clock (§2.10) |
| Shared mutable state between tests | Order-dependent failures | xUnit builds a new class instance per test; keep fields `readonly`, and see `InMemoryDb`'s per-call unique database name |
| One test asserting eight things | The failure message names one; you learn nothing about the rest | One behaviour per test |
| Mirroring the implementation step by step | Every refactor is a red suite | Assert on outcomes (§2.8) |
| Mocking types you do not own | You are testing your assumptions about someone else's library | Real `MemoryCache`, real BCrypt, real `TokenService` |

That last row deserves emphasis. `AuthServiceTests` uses real BCrypt — it costs ~100 ms per hash
and it is worth every millisecond, because it is the only way to assert the thing that matters:

```csharp
Assert.NotEqual("correct horse battery staple", persisted.Captured!.PasswordHash);
Assert.True(BCrypt.Net.BCrypt.Verify("correct horse battery staple", persisted.Captured.PasswordHash));
```

A mocked hasher would have proven that you called a method.

## 2.15 A checklist for the next class you test

1. List the **public methods**. One `#region`-style comment block per method in the test file.
2. For each: what is the happy path? Write it first — it forces the constructor and the doubles.
3. For each: enumerate the **early returns and throws**. One test per branch. These are ~70% of
   the file and where the bugs are.
4. Any **authorization**? Three tests: privileged, permitted, denied.
5. Any **write**? Assert both that it happened with the right values *and* that it does **not**
   happen on the rejection paths (`Times.Never`).
6. Any **side effect** — cache eviction, published event, audit row? Assert it explicitly. That
   is what the fakes in `TestKit` are for.
7. Anything **nondeterministic** — time, GUIDs, network? Inject or fake it.
8. Run coverage **last**, to find what you forgot. Not to hit a number.

## 2.16 Running it

```bash
dotnet test GrpcServer.Tests
dotnet test TaskTracker.Tests

dotnet test TaskTracker.Tests --filter "FullyQualifiedName~WorkTaskServiceTests"
dotnet test TaskTracker.Tests --filter "FullyQualifiedName~UpdateStatusAsync"

dotnet test --collect:"XPlat Code Coverage"
```

## 2.17 Exercises

Working from what is here, in rough order of difficulty:

1. `GrpcServer/Repository/ProductRepository.DeleteAsync` calls `GetByIdAsync` then
   `Remove(product!)`. Write the test for a missing id. Watch it throw. Decide what it should do.
2. Add `TaskTracker.Middlewares.GlobalExceptionHandler` tests. Seam: `RequestDelegate` is a
   delegate — just pass a lambda that throws.
3. Add `GrpcServer.Workers.ProductRuleWorker` tests. Seam: `IHostedService.StartAsync` plus a
   `CancellationTokenSource` you cancel after the first tick.
4. Take finding #2 in §1.5 — make `DeleteUserAsync` return `Failure(NotFound)`. Exactly one test
   should go red. Update it. That is the workflow the whole suite exists to give you.
5. Add repository tests against **SQLite in-memory** rather than the EF in-memory provider, and
   compare: SQLite enforces the foreign keys that §2.11 warns the in-memory provider ignores.
