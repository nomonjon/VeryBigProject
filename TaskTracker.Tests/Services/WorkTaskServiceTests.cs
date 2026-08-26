using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaskTracker.Data;
using TaskTracker.Interfaces;
using TaskTracker.Models;
using TaskTracker.Services;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Services;

/// <summary>
/// The widest class in the codebase: authorization, persistence through two different
/// paths (repository *and* DbContext), audit history and a RabbitMQ publish.
///
/// The dependencies are split deliberately:
///   - repositories  -> Moq, because tests care about which call happened;
///   - AppDbContext  -> real in-memory store, because tests care about which rows exist;
///   - publisher     -> recording fake, because tests care about what was published.
/// Picking the double per *question being asked* is what keeps these tests readable.
/// </summary>
public class WorkTaskServiceTests : TestBase, IDisposable
{
    private readonly Mock<IWorkTaskRepository> _tasks = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IProjectRepository> _projects = new();
    private readonly RecordingPublisher _publisher = new();
    private readonly AppDbContext _db = InMemoryDb.Create();

    public void Dispose() => _db.Dispose();

    private WorkTaskService CreateSut(IHttpContextAccessor caller)
        => new(_tasks.Object, _users.Object, _projects.Object, _publisher, caller, _db,
               NullLogger<WorkTaskService>.Instance);

    // ================= CreateWorkTaskAsync =================

    [Fact]
    public async Task CreateWorkTaskAsync_ReturnsBadRequest_WhenTheBodyIsMissing()
    {
        var result = await CreateSut(FakeHttpContext.ForAdmin()).CreateWorkTaskAsync(null!, default);

        Assert.Same(Error.BadRequest, result.Error);
        _tasks.Verify(r => r.Create(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateWorkTaskAsync_ReturnsNotFound_WhenTheProjectDoesNotExist()
    {
        _projects.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Project?)null);
        _users.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await CreateSut(FakeHttpContext.ForAdmin())
            .CreateWorkTaskAsync(Make.WorkTaskDto(projectId: Guid.NewGuid()), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    [Fact]
    public async Task CreateWorkTaskAsync_Succeeds_ForAnAdmin_WithoutCheckingMembership()
    {
        var project = Make.Project();
        ArrangeCreate(project);

        var result = await CreateSut(FakeHttpContext.ForAdmin())
            .CreateWorkTaskAsync(Make.WorkTaskDto(projectId: project.Id), default);

        Assert.True(result.IsSuccess);
        _projects.Verify(r => r.GetByIdWithUsers(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateWorkTaskAsync_Succeeds_ForAProjectMember()
    {
        var member = Make.User();
        var project = Make.ProjectWithMember(member);
        ArrangeCreate(project);
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await CreateSut(FakeHttpContext.ForUser(member.Id))
            .CreateWorkTaskAsync(Make.WorkTaskDto(projectId: project.Id), default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateWorkTaskAsync_ReturnsForbidden_ForANonMember()
    {
        var project = Make.Project();
        ArrangeCreate(project);
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await CreateSut(FakeHttpContext.ForUser(Guid.NewGuid()))
            .CreateWorkTaskAsync(Make.WorkTaskDto(projectId: project.Id), default);

        Assert.Same(Error.Forbidden, result.Error);
        _tasks.Verify(r => r.Create(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateWorkTaskAsync_UsesTheResolvedProjectAndAssignee()
    {
        var assignee = Make.User();
        var project = Make.Project();
        var created = ArrangeCreate(project, assignee);

        await CreateSut(FakeHttpContext.ForAdmin())
            .CreateWorkTaskAsync(Make.WorkTaskDto(projectId: project.Id, assigneeId: assignee.Id), default);

        Assert.Equal(project.Id, created.Captured!.ProjectId);
        Assert.Equal(assignee.Id, created.Captured.AssigneeId);
    }

    [Fact]
    public async Task CreateWorkTaskAsync_LeavesTheTaskUnassigned_WhenTheAssigneeDoesNotExist()
    {
        var project = Make.Project();
        var created = ArrangeCreate(project);

        await CreateSut(FakeHttpContext.ForAdmin())
            .CreateWorkTaskAsync(Make.WorkTaskDto(projectId: project.Id, assigneeId: Guid.NewGuid()), default);

        // An unknown assignee is not an error — the task is simply created unassigned.
        Assert.Null(created.Captured!.AssigneeId);
    }

    [Fact]
    public async Task CreateWorkTaskAsync_ReturnsTheStoredTask()
    {
        var project = Make.Project();
        ArrangeCreate(project);

        var result = await CreateSut(FakeHttpContext.ForAdmin())
            .CreateWorkTaskAsync(Make.WorkTaskDto(name: "Write docs", projectId: project.Id), default);

        Assert.Equal("Write docs", result.Value!.Name);
    }

    // ================= DeleteWorkTaskAsync =================

    [Fact]
    public async Task DeleteWorkTaskAsync_ReturnsNotFound_WhenTheTaskDoesNotExist()
    {
        _tasks.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((WorkTask?)null);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).DeleteWorkTaskAsync(Guid.NewGuid(), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    [Fact]
    public async Task DeleteWorkTaskAsync_ReturnsForbidden_ForANonMember()
    {
        var task = Make.WorkTask(project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForUser(Guid.NewGuid())).DeleteWorkTaskAsync(task.Id, default);

        Assert.Same(Error.Forbidden, result.Error);
        _tasks.Verify(r => r.Delete(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteWorkTaskAsync_Succeeds_ForAProjectMember()
    {
        var member = Make.User();
        var task = Make.WorkTask(project: Make.ProjectWithMember(member));
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(r => r.Delete(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut(FakeHttpContext.ForUser(member.Id)).DeleteWorkTaskAsync(task.Id, default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteWorkTaskAsync_Succeeds_ForAnAdminWhoIsNotAMember()
    {
        var task = Make.WorkTask(project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(r => r.Delete(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).DeleteWorkTaskAsync(task.Id, default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteWorkTaskAsync_ReturnsNotFound_WhenTheDeleteAffectedNoRows()
    {
        var task = Make.WorkTask(project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(r => r.Delete(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).DeleteWorkTaskAsync(task.Id, default);

        Assert.Same(Error.NotFound, result.Error);
    }

    // ================= GetWorkTaskByIdAsync =================

    [Fact]
    public async Task GetWorkTaskByIdAsync_ReturnsNotFound_WhenTheTaskDoesNotExist()
    {
        _tasks.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((WorkTask?)null);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).GetWorkTaskByIdAsync(Guid.NewGuid(), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    [Fact]
    public async Task GetWorkTaskByIdAsync_ReturnsForbidden_ForANonMember()
    {
        var task = Make.WorkTask(project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForUser(Guid.NewGuid())).GetWorkTaskByIdAsync(task.Id, default);

        Assert.Same(Error.Forbidden, result.Error);
    }

    [Fact]
    public async Task GetWorkTaskByIdAsync_ReturnsTheTask_ForAProjectMember()
    {
        var member = Make.User();
        var task = Make.WorkTask(name: "Write docs", project: Make.ProjectWithMember(member));
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForUser(member.Id)).GetWorkTaskByIdAsync(task.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Write docs", result.Value!.Name);
    }

    [Fact]
    public async Task GetWorkTaskByIdAsync_ReturnsForbidden_WhenTheProjectNavigationWasNotLoaded()
    {
        var task = Make.WorkTask(project: null);
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForUser(Guid.NewGuid())).GetWorkTaskByIdAsync(task.Id, default);

        // Membership cannot be proven without the Project graph, so access is denied.
        // Failing closed here is the right default — a missing Include must never
        // widen access.
        Assert.Same(Error.Forbidden, result.Error);
    }

    [Fact]
    public async Task GetWorkTaskByIdAsync_ReturnsForbidden_WhenTheProjectHasNoUsersLoaded()
    {
        var project = Make.Project();
        project.Users = null!;
        var task = Make.WorkTask(project: project);
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForUser(Guid.NewGuid())).GetWorkTaskByIdAsync(task.Id, default);

        Assert.Same(Error.Forbidden, result.Error);
    }

    [Fact]
    public async Task GetWorkTaskByIdAsync_ReturnsTheTask_ForAnAdminWhoIsNotAMember()
    {
        var task = Make.WorkTask(project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).GetWorkTaskByIdAsync(task.Id, default);

        Assert.True(result.IsSuccess);
    }

    // ================= GetWorkTasksAsync =================

    [Fact]
    public async Task GetWorkTasksAsync_ReturnsNothing_WhenTheCallerHasNoUserId()
    {
        _tasks.Setup(r => r.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync([Make.WorkTask()]);

        var result = await CreateSut(FakeHttpContext.Anonymous()).GetWorkTasksAsync(default);

        Assert.Empty(result);
        _tasks.Verify(r => r.GetAll(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetWorkTasksAsync_ReturnsEveryTask_ForAnAdmin()
    {
        _tasks.Setup(r => r.GetAll(It.IsAny<CancellationToken>()))
              .ReturnsAsync([Make.WorkTask(name: "First"), Make.WorkTask(name: "Second")]);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).GetWorkTasksAsync(default);

        Assert.Equal(["First", "Second"], result.Select(t => t.Name));
    }

    [Fact]
    public async Task GetWorkTasksAsync_ReturnsOnlyTasksInTheCallersProjects()
    {
        var member = Make.User();
        _tasks.Setup(r => r.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            Make.WorkTask(name: "Mine", project: Make.ProjectWithMember(member)),
            Make.WorkTask(name: "Someone else's", project: Make.Project())
        ]);

        var result = await CreateSut(FakeHttpContext.ForUser(member.Id)).GetWorkTasksAsync(default);

        Assert.Equal("Mine", Assert.Single(result).Name);
    }

    [Fact]
    public async Task GetWorkTasksWithIdAsync_AppliesTheSameFilter_ButReturnsEntities()
    {
        var member = Make.User();
        var mine = Make.WorkTask(name: "Mine", project: Make.ProjectWithMember(member));
        _tasks.Setup(r => r.GetAll(It.IsAny<CancellationToken>()))
              .ReturnsAsync([mine, Make.WorkTask(name: "Someone else's", project: Make.Project())]);

        var result = await CreateSut(FakeHttpContext.ForUser(member.Id)).GetWorkTasksWithIdAsync(default);

        Assert.Same(mine, Assert.Single(result));
    }

    // ================= UpdateWorkTaskAsync =================

    [Fact]
    public async Task UpdateWorkTaskAsync_ReturnsNotFound_WhenTheTaskDoesNotExist()
    {
        _tasks.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((WorkTask?)null);

        var result = await CreateSut(FakeHttpContext.ForAdmin())
            .UpdateWorkTaskAsync(Guid.NewGuid(), Make.WorkTaskDto(), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_ReturnsForbidden_ForANonMember()
    {
        var task = Make.WorkTask(project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForUser(Guid.NewGuid()))
            .UpdateWorkTaskAsync(task.Id, Make.WorkTaskDto(), default);

        Assert.Same(Error.Forbidden, result.Error);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_ReturnsNotFound_WhenTheTargetProjectDoesNotExist()
    {
        var task = Make.WorkTask(project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _projects.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Project?)null);
        _users.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await CreateSut(FakeHttpContext.ForAdmin())
            .UpdateWorkTaskAsync(task.Id, Make.WorkTaskDto(projectId: Guid.NewGuid()), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_ReturnsForbidden_WhenMovingATaskIntoAProjectTheCallerIsNotIn()
    {
        var member = Make.User();
        var source = Make.ProjectWithMember(member);
        var destination = Make.Project(name: "Somewhere else");
        var task = Make.WorkTask(project: source, projectId: source.Id);
        ArrangeUpdate(task, destination);
        _projects.Setup(r => r.GetByIdWithUsers(destination.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destination);

        var result = await CreateSut(FakeHttpContext.ForUser(member.Id))
            .UpdateWorkTaskAsync(task.Id, Make.WorkTaskDto(projectId: destination.Id), default);

        // Being a member of the source project is not enough to push work into another.
        Assert.Same(Error.Forbidden, result.Error);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_AllowsMovingATask_BetweenProjectsTheCallerBelongsTo()
    {
        var member = Make.User();
        var source = Make.ProjectWithMember(member);
        var destination = Make.ProjectWithMember(member, name: "Also mine");
        var task = Make.WorkTask(project: source, projectId: source.Id);
        ArrangeUpdate(task, destination);
        _projects.Setup(r => r.GetByIdWithUsers(destination.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destination);

        var result = await CreateSut(FakeHttpContext.ForUser(member.Id))
            .UpdateWorkTaskAsync(task.Id, Make.WorkTaskDto(projectId: destination.Id), default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_ReturnsBadRequest_WhenTheSaveFailed()
    {
        var project = Make.Project();
        var task = Make.WorkTask(project: project, projectId: project.Id);
        ArrangeUpdate(task, project, saveSucceeds: false);

        var result = await CreateSut(FakeHttpContext.ForAdmin())
            .UpdateWorkTaskAsync(task.Id, Make.WorkTaskDto(projectId: project.Id), default);

        Assert.Same(Error.BadRequest, result.Error);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_KeepsTheRouteIdOnTheReplacementTask()
    {
        var project = Make.Project();
        var task = Make.WorkTask(project: project, projectId: project.Id);
        var saved = ArrangeUpdate(task, project);

        await CreateSut(FakeHttpContext.ForAdmin())
            .UpdateWorkTaskAsync(task.Id, Make.WorkTaskDto(name: "Renamed", projectId: project.Id), default);

        Assert.Equal(task.Id, saved.Captured!.Id);
        Assert.Equal("Renamed", saved.Captured.Name);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_RecordsHistory_OnlyForTheFieldsThatChanged()
    {
        var caller = Make.User();
        var project = Make.ProjectWithMember(caller);
        var task = Make.WorkTask(name: "Old name", description: "Same", priority: Priority.Low,
                                 status: Status.InProgress, project: project, projectId: project.Id);
        ArrangeUpdate(task, project);
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        await CreateSut(FakeHttpContext.ForUser(caller.Id)).UpdateWorkTaskAsync(
            task.Id,
            Make.WorkTaskDto(name: "New name", description: "Same", priority: Priority.Low,
                             status: Status.Done, projectId: project.Id),
            default);

        var history = await _db.TaskHistories.ToListAsync();
        Assert.Equal(["Name", "Status"], history.Select(h => h.FieldName).Order());
        Assert.All(history, h => Assert.Equal(caller.Id, h.UserId));
        Assert.All(history, h => Assert.Equal(task.Id, h.WorkTaskId));
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_RecordsTheOldAndNewValue()
    {
        var caller = Make.User();
        var project = Make.ProjectWithMember(caller);
        var task = Make.WorkTask(status: Status.InProgress, project: project, projectId: project.Id);
        ArrangeUpdate(task, project);
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        await CreateSut(FakeHttpContext.ForUser(caller.Id)).UpdateWorkTaskAsync(
            task.Id, Make.WorkTaskDto(name: task.Name, description: task.Description,
                                      priority: task.Priority, status: Status.Done, projectId: project.Id), default);

        var entry = Assert.Single(await _db.TaskHistories.ToListAsync());
        Assert.Equal("Status", entry.FieldName);
        Assert.Equal(nameof(Status.InProgress), entry.OldValue);
        Assert.Equal(nameof(Status.Done), entry.NewValue);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_RecordsHistory_ForDescriptionPriorityAndAssignee()
    {
        var caller = Make.User();
        var newAssignee = Make.User(fullName: "Grace");
        var project = Make.ProjectWithMember(caller);
        var task = Make.WorkTask(description: "Old description", priority: Priority.Low,
                                 assigneeId: null, project: project, projectId: project.Id);
        ArrangeUpdate(task, project);
        _users.Setup(r => r.GetById(newAssignee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(newAssignee);
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        await CreateSut(FakeHttpContext.ForUser(caller.Id)).UpdateWorkTaskAsync(
            task.Id,
            Make.WorkTaskDto(name: task.Name, description: "New description", priority: Priority.High,
                             status: task.Status, projectId: project.Id, assigneeId: newAssignee.Id),
            default);

        var history = (await _db.TaskHistories.ToListAsync()).ToDictionary(h => h.FieldName);
        Assert.Equal(["Assignee", "Description", "Priority"], history.Keys.Order());
        Assert.Equal("Unassigned", history["Assignee"].OldValue);
        Assert.Equal(newAssignee.Id.ToString(), history["Assignee"].NewValue);
        Assert.Equal("Old description", history["Description"].OldValue);
        Assert.Equal(nameof(Priority.Low), history["Priority"].OldValue);
        Assert.Equal(nameof(Priority.High), history["Priority"].NewValue);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_RecordsUnassigned_WhenAnAssigneeIsRemoved()
    {
        var caller = Make.User();
        var previous = Make.User();
        var project = Make.ProjectWithMember(caller);
        var task = Make.WorkTask(assignee: previous, project: project, projectId: project.Id);
        ArrangeUpdate(task, project);   // user lookup returns null -> no assignee
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        await CreateSut(FakeHttpContext.ForUser(caller.Id)).UpdateWorkTaskAsync(
            task.Id,
            Make.WorkTaskDto(name: task.Name, description: task.Description, priority: task.Priority,
                             status: task.Status, projectId: project.Id, assigneeId: null),
            default);

        var entry = Assert.Single(await _db.TaskHistories.ToListAsync());
        Assert.Equal("Assignee", entry.FieldName);
        Assert.Equal(previous.Id.ToString(), entry.OldValue);
        Assert.Equal("Unassigned", entry.NewValue);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_RecordsNoHistory_WhenNothingChanged()
    {
        var project = Make.Project();
        var task = Make.WorkTask(project: project, projectId: project.Id);
        ArrangeUpdate(task, project);

        await CreateSut(FakeHttpContext.ForAdmin()).UpdateWorkTaskAsync(
            task.Id, Make.WorkTaskDto(name: task.Name, description: task.Description,
                                      priority: task.Priority, status: task.Status, projectId: project.Id), default);

        Assert.Empty(await _db.TaskHistories.ToListAsync());
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_RecordsNoHistory_WhenTheCallerHasNoUserId()
    {
        var project = Make.Project();
        var task = Make.WorkTask(name: "Old", project: project, projectId: project.Id);
        ArrangeUpdate(task, project);

        // Admin role but no subject claim: authorized, yet there is nobody to attribute
        // the change to, so history is skipped rather than written with an empty user.
        await CreateSut(FakeHttpContext.WithRoleOnly(Roles.Admin)).UpdateWorkTaskAsync(
            task.Id, Make.WorkTaskDto(name: "New", projectId: project.Id), default);

        Assert.Empty(await _db.TaskHistories.ToListAsync());
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_PublishesAnEvent_WhenTheStatusChanged()
    {
        var project = Make.Project();
        var task = Make.WorkTask(status: Status.InProgress, project: project, projectId: project.Id);
        ArrangeUpdate(task, project);

        await CreateSut(FakeHttpContext.ForAdmin()).UpdateWorkTaskAsync(
            task.Id, Make.WorkTaskDto(status: Status.Done, projectId: project.Id), default);

        var published = Assert.Single(_publisher.Published);
        Assert.Equal(Status.InProgress, published.OldStatus);
        Assert.Equal(Status.Done, published.NewStatus);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_PublishesNothing_WhenTheStatusIsUnchanged()
    {
        var project = Make.Project();
        var task = Make.WorkTask(name: "Old", status: Status.InProgress, project: project, projectId: project.Id);
        ArrangeUpdate(task, project);

        await CreateSut(FakeHttpContext.ForAdmin()).UpdateWorkTaskAsync(
            task.Id, Make.WorkTaskDto(name: "New", status: Status.InProgress, projectId: project.Id), default);

        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task UpdateWorkTaskAsync_PublishesTheProjectNameAndEveryMemberEmail()
    {
        var alice = Make.User(email: "alice@example.com");
        var bob = Make.User(email: "bob@example.com");
        var project = Make.Project(name: "Apollo", users: [alice, bob]);
        var task = Make.WorkTask(status: Status.InProgress, project: project, projectId: project.Id);
        ArrangeUpdate(task, project);

        await CreateSut(FakeHttpContext.ForAdmin()).UpdateWorkTaskAsync(
            task.Id, Make.WorkTaskDto(status: Status.Done, projectId: project.Id), default);

        var published = Assert.Single(_publisher.Published);
        Assert.Equal("Apollo", published.ProjectName);
        Assert.Equal(["alice@example.com", "bob@example.com"], published.ProjectUserEmails);
    }

    // ================= UpdateStatusAsync =================

    [Fact]
    public async Task UpdateStatusAsync_ReturnsNotFound_WhenTheTaskDoesNotExist()
    {
        _tasks.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((WorkTask?)null);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).UpdateStatusAsync(Guid.NewGuid(), Status.Done, default);

        Assert.Same(Error.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsForbidden_ForANonMember()
    {
        var task = Make.WorkTask(project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForUser(Guid.NewGuid()))
            .UpdateStatusAsync(task.Id, Status.Done, default);

        Assert.Same(Error.Forbidden, result.Error);
    }

    [Fact]
    public async Task UpdateStatusAsync_IsANoOp_WhenTheStatusIsAlreadyTheRequestedOne()
    {
        var task = Make.WorkTask(status: Status.Done, project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).UpdateStatusAsync(task.Id, Status.Done, default);

        Assert.True(result.IsSuccess);
        _tasks.Verify(r => r.Update(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_publisher.Published);
        Assert.Empty(await _db.TaskHistories.ToListAsync());
    }

    [Fact]
    public async Task UpdateStatusAsync_SavesTheNewStatus_AndKeepsEverythingElse()
    {
        var project = Make.Project();
        var task = Make.WorkTask(name: "Write docs", priority: Priority.High,
                                 status: Status.InProgress, project: project, projectId: project.Id);
        WorkTask? saved = null;
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(r => r.Update(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()))
              .Callback<WorkTask, CancellationToken>((t, _) => saved = t)
              .ReturnsAsync(true);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).UpdateStatusAsync(task.Id, Status.Review, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(Status.Review, saved!.Status);
        Assert.Equal("Write docs", saved.Name);
        Assert.Equal(Priority.High, saved.Priority);
        Assert.Equal(project.Id, saved.ProjectId);
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsBadRequest_WhenTheSaveFailed()
    {
        var task = Make.WorkTask(status: Status.InProgress, project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(r => r.Update(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).UpdateStatusAsync(task.Id, Status.Done, default);

        Assert.Same(Error.BadRequest, result.Error);
    }

    [Fact]
    public async Task UpdateStatusAsync_RecordsAStatusHistoryRow()
    {
        var caller = Make.User();
        var task = Make.WorkTask(status: Status.InProgress, project: Make.ProjectWithMember(caller));
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(r => r.Update(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await CreateSut(FakeHttpContext.ForUser(caller.Id)).UpdateStatusAsync(task.Id, Status.Done, default);

        var entry = Assert.Single(await _db.TaskHistories.ToListAsync());
        Assert.Equal("Status", entry.FieldName);
        Assert.Equal(nameof(Status.InProgress), entry.OldValue);
        Assert.Equal(nameof(Status.Done), entry.NewValue);
        Assert.Equal(caller.Id, entry.UserId);
    }

    [Fact]
    public async Task UpdateStatusAsync_RecordsNoHistory_WhenTheCallerHasNoUserId()
    {
        var task = Make.WorkTask(status: Status.InProgress, project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(r => r.Update(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await CreateSut(FakeHttpContext.WithRoleOnly(Roles.Admin)).UpdateStatusAsync(task.Id, Status.Done, default);

        Assert.Empty(await _db.TaskHistories.ToListAsync());
    }

    [Fact]
    public async Task UpdateStatusAsync_PublishesTheTransition()
    {
        var assignee = Make.User(email: "ada@example.com");
        var project = Make.Project(name: "Apollo");
        var task = Make.WorkTask(name: "Write docs", status: Status.InProgress, project: project, assignee: assignee);
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(r => r.Update(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await CreateSut(FakeHttpContext.ForAdmin()).UpdateStatusAsync(task.Id, Status.Done, default);

        var published = Assert.Single(_publisher.Published);
        Assert.Equal(task.Id, published.Id);
        Assert.Equal("Write docs", published.Name);
        Assert.Equal(Status.InProgress, published.OldStatus);
        Assert.Equal(Status.Done, published.NewStatus);
        Assert.Equal(assignee.Id, published.AssigneeId);
        Assert.Equal("ada@example.com", published.AssigneeEmail);
        Assert.Equal("Apollo", published.ProjectName);
    }

    // ================= AddCommentAsync =================

    [Fact]
    public async Task AddCommentAsync_ReturnsUnauthorized_WhenTheCallerHasNoUserId()
    {
        var result = await CreateSut(FakeHttpContext.Anonymous()).AddCommentAsync(Guid.NewGuid(), "hi", default);

        Assert.Same(Error.Unauthorized, result.Error);
        _tasks.Verify(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddCommentAsync_ReturnsNotFound_WhenTheTaskDoesNotExist()
    {
        _tasks.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((WorkTask?)null);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).AddCommentAsync(Guid.NewGuid(), "hi", default);

        Assert.Same(Error.NotFound, result.Error);
    }

    [Fact]
    public async Task AddCommentAsync_ReturnsForbidden_ForANonMember()
    {
        var task = Make.WorkTask(project: Make.Project());
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForUser(Guid.NewGuid())).AddCommentAsync(task.Id, "hi", default);

        Assert.Same(Error.Forbidden, result.Error);
        Assert.Empty(await _db.TaskComments.ToListAsync());
    }

    [Fact]
    public async Task AddCommentAsync_PersistsTheComment_AttributedToTheCaller()
    {
        var caller = Make.User();
        var task = Make.WorkTask(project: Make.ProjectWithMember(caller));
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForUser(caller.Id))
            .AddCommentAsync(task.Id, "Looks good to me", default);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(await _db.TaskComments.ToListAsync());
        Assert.Equal("Looks good to me", stored.Content);
        Assert.Equal(task.Id, stored.WorkTaskId);
        Assert.Equal(caller.Id, stored.UserId);
    }

    [Fact]
    public async Task AddCommentAsync_ReturnsTheStoredCommentIncludingItsGeneratedId()
    {
        var caller = Make.User();
        var task = Make.WorkTask(project: Make.ProjectWithMember(caller));
        _tasks.Setup(r => r.GetById(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut(FakeHttpContext.ForUser(caller.Id)).AddCommentAsync(task.Id, "hi", default);

        Assert.NotEqual(Guid.Empty, result.Value!.Id);
        Assert.Equal(task.Id, result.Value.WorkTaskId);
    }

    // ================= GetProjectAndUserAsync =================

    [Fact]
    public async Task GetProjectAndUserAsync_ReturnsBothNulls_WhenTheProjectIsMissing()
    {
        _projects.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Project?)null);
        _users.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Make.User());

        var (project, user) = await CreateSut(FakeHttpContext.ForAdmin())
            .GetProjectAndUserAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        // No project means the request is rejected outright, so the user is not
        // reported either — callers only ever check the project first.
        Assert.Null(project);
        Assert.Null(user);
    }

    [Fact]
    public async Task GetProjectAndUserAsync_ReturnsTheProjectAlone_WhenTheUserIsMissing()
    {
        var existing = Make.Project();
        _projects.Setup(r => r.GetById(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _users.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var (project, user) = await CreateSut(FakeHttpContext.ForAdmin())
            .GetProjectAndUserAsync(existing.Id, Guid.NewGuid(), default);

        Assert.Same(existing, project);
        Assert.Null(user);
    }

    [Fact]
    public async Task GetProjectAndUserAsync_ReturnsBoth_WhenBothExist()
    {
        var existingProject = Make.Project();
        var existingUser = Make.User();
        _projects.Setup(r => r.GetById(existingProject.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingProject);
        _users.Setup(r => r.GetById(existingUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingUser);

        var (project, user) = await CreateSut(FakeHttpContext.ForAdmin())
            .GetProjectAndUserAsync(existingProject.Id, existingUser.Id, default);

        Assert.Same(existingProject, project);
        Assert.Same(existingUser, user);
    }

    // ================= arrange helpers =================

    /// <summary>Holds whatever a Moq callback saw, so a test can assert on it afterwards.</summary>
    private sealed class Recorded<T> { public T? Captured { get; set; } }

    private Recorded<WorkTask> ArrangeCreate(Project project, User? assignee = null)
    {
        var created = new Recorded<WorkTask>();
        _projects.Setup(r => r.GetById(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _users.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        if (assignee is not null)
            _users.Setup(r => r.GetById(assignee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assignee);
        _tasks.Setup(r => r.Create(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()))
              .Callback<WorkTask, CancellationToken>((t, _) => created.Captured = t)
              .ReturnsAsync((WorkTask t, CancellationToken _) => t);
        return created;
    }

    private Recorded<WorkTask> ArrangeUpdate(WorkTask existing, Project destination, bool saveSucceeds = true)
    {
        var saved = new Recorded<WorkTask>();
        _tasks.Setup(r => r.GetById(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _projects.Setup(r => r.GetById(destination.Id, It.IsAny<CancellationToken>())).ReturnsAsync(destination);
        _users.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _tasks.Setup(r => r.Update(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()))
              .Callback<WorkTask, CancellationToken>((t, _) => saved.Captured = t)
              .ReturnsAsync(saveSucceeds);
        return saved;
    }
}
