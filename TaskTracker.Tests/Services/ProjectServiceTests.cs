using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaskTracker.Interfaces;
using TaskTracker.Models;
using TaskTracker.Services;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Services;

/// <summary>
/// ProjectService mixes two concerns: CRUD, and "which projects may this caller see".
/// The second one is security, so the list methods are tested from three angles —
/// admin, member, outsider — instead of only the happy path.
/// </summary>
public class ProjectServiceTests : TestBase
{
    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IUserRepository> _users = new();

    private ProjectService CreateSut(IHttpContextAccessor? caller = null)
        => new(_projects.Object,
               _users.Object,
               caller ?? FakeHttpContext.ForAdmin(),
               NullLogger<ProjectService>.Instance);

    // ---------- CreateProjectAsync ----------

    [Fact]
    public async Task CreateProjectAsync_PersistsTheMappedProject()
    {
        Project? persisted = null;
        _projects.Setup(r => r.Create(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
                 .Callback<Project, CancellationToken>((p, _) => persisted = p)
                 .ReturnsAsync((Project p, CancellationToken _) => p);

        var result = await CreateSut().CreateProjectAsync(Make.ProjectDto(name: "Apollo", description: "Moon"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Apollo", persisted!.Name);
        Assert.Equal("Moon", persisted.Description);
    }

    [Fact]
    public async Task CreateProjectAsync_ReturnsTheStoredProject()
    {
        _projects.Setup(r => r.Create(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Make.Project(name: "Stored"));

        var result = await CreateSut().CreateProjectAsync(Make.ProjectDto(name: "Submitted"), default);

        Assert.Equal("Stored", result.Value!.Name);
    }

    [Fact]
    public async Task CreateProjectAsync_ReturnsBadRequest_AndWritesNothing_WhenTheBodyIsMissing()
    {
        var result = await CreateSut().CreateProjectAsync(null!, default);

        Assert.Same(Error.BadRequest, result.Error);
        _projects.Verify(r => r.Create(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- GetProjectByIdAsync / GetProjectByIdWithTasksAsync ----------

    [Fact]
    public async Task GetProjectByIdAsync_ReturnsTheProject_WhenItExists()
    {
        var project = Make.Project(name: "Apollo");
        _projects.Setup(r => r.GetById(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await CreateSut().GetProjectByIdAsync(project.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Apollo", result.Value!.Name);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ReturnsNotFound_WhenTheProjectIsMissing()
    {
        _projects.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Project?)null);

        var result = await CreateSut().GetProjectByIdAsync(Guid.NewGuid(), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    [Fact]
    public async Task GetProjectByIdWithTasksAsync_IncludesTheTasks()
    {
        var project = Make.Project(workTasks: [Make.WorkTask(name: "First")]);
        _projects.Setup(r => r.GetByIdWithTasks(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await CreateSut().GetProjectByIdWithTasksAsync(project.Id, default);

        Assert.Equal("First", Assert.Single(result.Value!.WorkTasks).Name);
    }

    [Fact]
    public async Task GetProjectByIdWithTasksAsync_ReturnsNotFound_WhenTheProjectIsMissing()
    {
        _projects.Setup(r => r.GetByIdWithTasks(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Project?)null);

        var result = await CreateSut().GetProjectByIdWithTasksAsync(Guid.NewGuid(), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    // ---------- GetProjectsAsync (visibility rules) ----------

    [Fact]
    public async Task GetProjectsAsync_ReturnsEveryProject_ForAnAdmin()
    {
        ArrangeProjects(Make.Project(name: "Apollo"), Make.Project(name: "Gemini"));

        var result = await CreateSut(FakeHttpContext.ForAdmin()).GetProjectsAsync(default);

        Assert.Equal(["Apollo", "Gemini"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsOnlyTheCallersProjects_ForANonAdmin()
    {
        var member = Make.User();
        ArrangeProjects(Make.ProjectWithMember(member, name: "Mine"), Make.Project(name: "Someone else's"));

        var result = await CreateSut(FakeHttpContext.ForUser(member.Id)).GetProjectsAsync(default);

        Assert.Equal("Mine", Assert.Single(result).Name);
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsNothing_ForANonAdminInNoProjects()
    {
        ArrangeProjects(Make.Project(name: "Apollo"));

        var result = await CreateSut(FakeHttpContext.ForUser(Guid.NewGuid())).GetProjectsAsync(default);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsNothing_WhenThereIsNoHttpContext()
    {
        // A background worker resolving IProjectService gets no ambient user. It must
        // not accidentally be treated as an admin.
        ArrangeProjects(Make.Project(name: "Apollo"));

        var result = await CreateSut(FakeHttpContext.NoContext()).GetProjectsAsync(default);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetProjectsWithIdAsync_AppliesTheSameVisibilityRules()
    {
        var member = Make.User();
        ArrangeProjects(Make.ProjectWithMember(member, name: "Mine"), Make.Project(name: "Someone else's"));

        var result = await CreateSut(FakeHttpContext.ForUser(member.Id)).GetProjectsWithIdAsync(default);

        Assert.Equal("Mine", Assert.Single(result).Name);
    }

    [Fact]
    public async Task GetProjectsWithIdAsync_IncludesTheProjectIdAndItsMembers()
    {
        var member = Make.User();
        var project = Make.ProjectWithMember(member);
        ArrangeProjects(project);

        var result = await CreateSut(FakeHttpContext.ForAdmin()).GetProjectsWithIdAsync(default);

        var dto = Assert.Single(result);
        Assert.Equal(project.Id, dto.Id);
        Assert.Equal([member.Id], dto.UserIds);
    }

    // ---------- UpdateProjectAsync ----------

    [Fact]
    public async Task UpdateProjectAsync_KeepsTheRouteId_OnTheReplacementProject()
    {
        var id = Guid.NewGuid();
        Project? saved = null;
        _projects.Setup(r => r.GetById(id, It.IsAny<CancellationToken>())).ReturnsAsync(Make.Project(id: id));
        _projects.Setup(r => r.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
                 .Callback<Project, CancellationToken>((p, _) => saved = p)
                 .ReturnsAsync(true);

        await CreateSut().UpdateProjectAsync(id, Make.ProjectDto(name: "Renamed"), default);

        Assert.Equal(id, saved!.Id);
        Assert.Equal("Renamed", saved.Name);
    }

    [Fact]
    public async Task UpdateProjectAsync_ReturnsTheUpdatedValues()
    {
        var id = Guid.NewGuid();
        _projects.Setup(r => r.GetById(id, It.IsAny<CancellationToken>())).ReturnsAsync(Make.Project(id: id, name: "Old"));
        _projects.Setup(r => r.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().UpdateProjectAsync(id, Make.ProjectDto(name: "New", description: "Fresh"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", result.Value!.Name);
        Assert.Equal("Fresh", result.Value.Description);
    }

    [Fact]
    public async Task UpdateProjectAsync_ReturnsNotFound_AndWritesNothing_WhenTheProjectIsMissing()
    {
        _projects.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Project?)null);

        var result = await CreateSut().UpdateProjectAsync(Guid.NewGuid(), Make.ProjectDto(), default);

        Assert.Same(Error.NotFound, result.Error);
        _projects.Verify(r => r.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProjectAsync_ReturnsBadRequest_WhenTheSaveFailed()
    {
        _projects.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Make.Project());
        _projects.Setup(r => r.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateSut().UpdateProjectAsync(Guid.NewGuid(), Make.ProjectDto(), default);

        Assert.Same(Error.BadRequest, result.Error);
    }

    // ---------- DeleteProjectAsync ----------

    [Fact]
    public async Task DeleteProjectAsync_ReturnsSuccess_WhenTheProjectWasDeleted()
    {
        _projects.Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().DeleteProjectAsync(Guid.NewGuid(), default);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task DeleteProjectAsync_ReturnsNotFound_WhenThereWasNothingToDelete()
    {
        _projects.Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateSut().DeleteProjectAsync(Guid.NewGuid(), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    // ---------- AddUserToProjectAsync ----------

    [Fact]
    public async Task AddUserToProjectAsync_AddsTheUser_AndSaves()
    {
        var user = Make.User();
        var project = Make.Project();
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _users.Setup(r => r.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _projects.Setup(r => r.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().AddUserToProjectAsync(project.Id, user.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Contains(user, project.Users);
        _projects.Verify(r => r.Update(project, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddUserToProjectAsync_IsIdempotent_WhenTheUserIsAlreadyAMember()
    {
        var user = Make.User();
        var project = Make.ProjectWithMember(user);
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await CreateSut().AddUserToProjectAsync(project.Id, user.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Single(project.Users);
        _projects.Verify(r => r.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddUserToProjectAsync_ReturnsNotFound_WhenTheProjectIsMissing()
    {
        _projects.Setup(r => r.GetByIdWithUsers(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Project?)null);

        var result = await CreateSut().AddUserToProjectAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    [Fact]
    public async Task AddUserToProjectAsync_ReturnsNotFound_WhenTheUserIsMissing()
    {
        var project = Make.Project();
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _users.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await CreateSut().AddUserToProjectAsync(project.Id, Guid.NewGuid(), default);

        Assert.Same(Error.NotFound, result.Error);
        _projects.Verify(r => r.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- RemoveUserFromProjectAsync ----------

    [Fact]
    public async Task RemoveUserFromProjectAsync_RemovesTheUser_AndSaves()
    {
        var user = Make.User();
        var project = Make.ProjectWithMember(user);
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _projects.Setup(r => r.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().RemoveUserFromProjectAsync(project.Id, user.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Empty(project.Users);
        _projects.Verify(r => r.Update(project, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveUserFromProjectAsync_IsIdempotent_WhenTheUserIsNotAMember()
    {
        var project = Make.Project();
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await CreateSut().RemoveUserFromProjectAsync(project.Id, Guid.NewGuid(), default);

        Assert.True(result.IsSuccess);
        _projects.Verify(r => r.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveUserFromProjectAsync_LeavesTheOtherMembersInPlace()
    {
        var stays = Make.User(fullName: "Stays");
        var goes = Make.User(fullName: "Goes");
        var project = Make.Project(users: [stays, goes]);
        _projects.Setup(r => r.GetByIdWithUsers(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _projects.Setup(r => r.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await CreateSut().RemoveUserFromProjectAsync(project.Id, goes.Id, default);

        Assert.Equal([stays], project.Users);
    }

    [Fact]
    public async Task RemoveUserFromProjectAsync_ReturnsNotFound_WhenTheProjectIsMissing()
    {
        _projects.Setup(r => r.GetByIdWithUsers(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Project?)null);

        var result = await CreateSut().RemoveUserFromProjectAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    // ---------- ValidateProjectAsync ----------

    [Fact]
    public async Task ValidateProjectAsync_AlwaysSucceeds()
    {
        // Placeholder in production code. Pinned so that turning it into real
        // validation is a deliberate change with a failing test to update.
        var result = await CreateSut().ValidateProjectAsync(Guid.NewGuid(), default);

        Assert.True(result.IsSuccess);
    }

    private void ArrangeProjects(params Project[] projects)
        => _projects.Setup(r => r.GetAllWithUsers(It.IsAny<CancellationToken>())).ReturnsAsync(projects.ToList());
}
