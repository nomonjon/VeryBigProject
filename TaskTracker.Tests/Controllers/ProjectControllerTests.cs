using Microsoft.AspNetCore.Mvc;
using Moq;
using TaskTracker.Controllers;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Controllers;

public class ProjectControllerTests
{
    private readonly Mock<IProjectService> _service = new();
    private readonly ProjectController _sut;

    public ProjectControllerTests() => _sut = new ProjectController(_service.Object);

    private static ProjectDto SampleProject(string name = "Apollo") => new(name, "Moon landing", []);

    // ---------- reads ----------

    [Fact]
    public async Task GetProjects_Returns200_WithTheList()
    {
        var projects = new List<ProjectDto> { SampleProject() };
        _service.Setup(s => s.GetProjectsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(projects);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetProjects(default));

        Assert.Same(projects, ok.Value);
    }

    [Fact]
    public async Task GetProjectsWithId_Returns200_WithTheList()
    {
        var projects = new List<ProjectWithIdDto> { new(Guid.NewGuid(), "Apollo", "Moon landing", [], []) };
        _service.Setup(s => s.GetProjectsWithIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(projects);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetProjectsWithId(default));

        Assert.Same(projects, ok.Value);
    }

    [Fact]
    public async Task GetProject_Returns200_WithTheResult()
    {
        _service.Setup(s => s.ValidateProjectAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Success(true));
        _service.Setup(s => s.GetProjectByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ProjectDto?>.Success(SampleProject()));

        Assert.IsType<OkObjectResult>(await _sut.GetProject(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task GetProject_Returns404_WhenValidationThrowsKeyNotFound()
    {
        _service.Setup(s => s.ValidateProjectAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("Project not found"));

        var notFound = Assert.IsType<NotFoundObjectResult>(await _sut.GetProject(Guid.NewGuid(), default));

        Assert.Equal("Project not found", notFound.Value);
    }

    [Fact]
    public async Task GetProjectWithTasks_Returns200_WhenTheProjectExists()
    {
        _service.Setup(s => s.GetProjectByIdWithTasksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ProjectDto?>.Success(SampleProject()));

        Assert.IsType<OkObjectResult>(await _sut.GetProjectWithTasks(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task GetProjectWithTasks_Returns404_WhenTheProjectIsMissing()
    {
        _service.Setup(s => s.GetProjectByIdWithTasksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ProjectDto?>.Failure(Error.NotFound));

        Assert.IsType<NotFoundObjectResult>(await _sut.GetProjectWithTasks(Guid.NewGuid(), default));
    }

    // ---------- CreateProject ----------

    [Fact]
    public async Task CreateProject_Returns200_WithTheResult()
    {
        _service.Setup(s => s.CreateProjectAsync(It.IsAny<CreateUpdateProjectDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ProjectDto>.Success(SampleProject()));

        Assert.IsType<OkObjectResult>(await _sut.CreateProject(Make.ProjectDto(), default));
    }

    [Fact]
    public async Task CreateProject_Returns400_WhenTheServiceRejectsTheArguments()
    {
        _service.Setup(s => s.CreateProjectAsync(It.IsAny<CreateUpdateProjectDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Name is required"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(await _sut.CreateProject(Make.ProjectDto(), default));

        Assert.Equal("Name is required", badRequest.Value);
    }

    // ---------- UpdateProject ----------

    [Fact]
    public async Task UpdateProject_Returns200_WithTheResult()
    {
        _service.Setup(s => s.UpdateProjectAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateProjectDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ProjectDto>.Success(SampleProject()));

        Assert.IsType<OkObjectResult>(await _sut.UpdateProject(Guid.NewGuid(), Make.ProjectDto(), default));
    }

    [Fact]
    public async Task UpdateProject_Returns404_WhenTheServiceThrowsKeyNotFound()
    {
        _service.Setup(s => s.UpdateProjectAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateProjectDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("Project not found"));

        Assert.IsType<NotFoundObjectResult>(await _sut.UpdateProject(Guid.NewGuid(), Make.ProjectDto(), default));
    }

    [Fact]
    public async Task UpdateProject_Returns400_ForAnyOtherException()
    {
        _service.Setup(s => s.UpdateProjectAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateProjectDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

        Assert.IsType<BadRequestObjectResult>(await _sut.UpdateProject(Guid.NewGuid(), Make.ProjectDto(), default));
    }

    // ---------- DeleteProject ----------

    [Fact]
    public async Task DeleteProject_Returns204_WhenTheProjectWasDeleted()
    {
        _service.Setup(s => s.DeleteProjectAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Success(true));

        Assert.IsType<NoContentResult>(await _sut.DeleteProject(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task DeleteProject_Returns404_WhenThereWasNothingToDelete()
    {
        _service.Setup(s => s.DeleteProjectAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Failure(Error.NotFound));

        Assert.IsType<NotFoundObjectResult>(await _sut.DeleteProject(Guid.NewGuid(), default));
    }

    // ---------- membership ----------

    [Fact]
    public async Task AddUserToProject_Returns200_OnSuccess()
    {
        _service.Setup(s => s.AddUserToProjectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Success(true));

        Assert.IsType<OkResult>(await _sut.AddUserToProject(Guid.NewGuid(), Guid.NewGuid(), default));
    }

    [Fact]
    public async Task AddUserToProject_Returns404_WhenTheProjectOrUserIsMissing()
    {
        _service.Setup(s => s.AddUserToProjectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Failure(Error.NotFound));

        Assert.IsType<NotFoundResult>(await _sut.AddUserToProject(Guid.NewGuid(), Guid.NewGuid(), default));
    }

    [Fact]
    public async Task RemoveUserFromProject_Returns204_OnSuccess()
    {
        _service.Setup(s => s.RemoveUserFromProjectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Success(true));

        Assert.IsType<NoContentResult>(await _sut.RemoveUserFromProject(Guid.NewGuid(), Guid.NewGuid(), default));
    }

    [Fact]
    public async Task RemoveUserFromProject_Returns404_WhenTheProjectIsMissing()
    {
        _service.Setup(s => s.RemoveUserFromProjectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Failure(Error.NotFound));

        Assert.IsType<NotFoundResult>(await _sut.RemoveUserFromProject(Guid.NewGuid(), Guid.NewGuid(), default));
    }
}
