using Microsoft.AspNetCore.Mvc;
using Moq;
using TaskTracker.Controllers;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Models;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Controllers;

public class WorkTaskControllerTests
{
    private readonly Mock<IWorkTaskService> _service = new();
    private readonly WorkTaskController _sut;

    public WorkTaskControllerTests() => _sut = new WorkTaskController(_service.Object);

    private static WorkTaskDto SampleTask(string name = "Write docs")
        => new(Guid.NewGuid(), name, "Describe the API", Priority.Medium, Status.InProgress, Guid.NewGuid(), null, [], []);

    // ---------- reads ----------

    [Fact]
    public async Task GetTasks_Returns200_WithTheList()
    {
        var tasks = new List<WorkTaskDto> { SampleTask() };
        _service.Setup(s => s.GetWorkTasksAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tasks);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetTasks(default));

        Assert.Same(tasks, ok.Value);
    }

    [Fact]
    public async Task GetTasksWithId_MapsTheEntitiesToDtos()
    {
        _service.Setup(s => s.GetWorkTasksWithIdAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([Make.WorkTask(name: "Write docs")]);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetTasksWithId(default));

        var dtos = Assert.IsAssignableFrom<IEnumerable<WorkTaskDto>>(ok.Value);
        Assert.Equal("Write docs", Assert.Single(dtos).Name);
    }

    [Fact]
    public async Task GetTaskById_Returns200_WhenTheTaskExists()
    {
        _service.Setup(s => s.GetWorkTaskByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<WorkTaskDto?>.Success(SampleTask()));

        Assert.IsType<OkObjectResult>(await _sut.GetTaskById(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task GetTaskById_Returns404_WhenTheTaskIsMissing()
    {
        _service.Setup(s => s.GetWorkTaskByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<WorkTaskDto?>.Failure(Error.NotFound));

        Assert.IsType<NotFoundObjectResult>(await _sut.GetTaskById(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task GetTaskById_Returns403_WhenTheCallerIsNotAProjectMember()
    {
        _service.Setup(s => s.GetWorkTaskByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<WorkTaskDto?>.Failure(Error.Forbidden));

        var result = Assert.IsType<ObjectResult>(await _sut.GetTaskById(Guid.NewGuid(), default));

        Assert.Equal(403, result.StatusCode);
    }

    // ---------- CreateTask ----------

    [Fact]
    public async Task CreateTask_Returns200_WhenTheTaskWasCreated()
    {
        _service.Setup(s => s.CreateWorkTaskAsync(It.IsAny<CreateUpdateWorkTaskDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<WorkTaskDto>.Success(SampleTask()));

        Assert.IsType<OkObjectResult>(await _sut.CreateTask(Make.WorkTaskDto(), default));
    }

    [Fact]
    public async Task CreateTask_Returns404_WhenTheProjectIsMissing()
    {
        _service.Setup(s => s.CreateWorkTaskAsync(It.IsAny<CreateUpdateWorkTaskDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<WorkTaskDto>.Failure(Error.NotFound));

        Assert.IsType<NotFoundObjectResult>(await _sut.CreateTask(Make.WorkTaskDto(), default));
    }

    [Fact]
    public async Task CreateTask_Returns403_ForANonMember()
    {
        _service.Setup(s => s.CreateWorkTaskAsync(It.IsAny<CreateUpdateWorkTaskDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<WorkTaskDto>.Failure(Error.Forbidden));

        var result = Assert.IsType<ObjectResult>(await _sut.CreateTask(Make.WorkTaskDto(), default));

        Assert.Equal(403, result.StatusCode);
    }

    // ---------- UpdateTask / UpdateStatus ----------

    [Fact]
    public async Task UpdateTask_Returns200_WhenTheTaskWasUpdated()
    {
        _service.Setup(s => s.UpdateWorkTaskAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateWorkTaskDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<WorkTaskDto>.Success(SampleTask()));

        Assert.IsType<OkObjectResult>(await _sut.UpdateTask(Guid.NewGuid(), Make.WorkTaskDto(), default));
    }

    [Fact]
    public async Task UpdateTask_Returns400_WhenTheSaveFailed()
    {
        _service.Setup(s => s.UpdateWorkTaskAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateWorkTaskDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<WorkTaskDto>.Failure(Error.BadRequest));

        Assert.IsType<BadRequestObjectResult>(await _sut.UpdateTask(Guid.NewGuid(), Make.WorkTaskDto(), default));
    }

    [Fact]
    public async Task UpdateStatus_ForwardsTheStatusFromTheBody()
    {
        _service.Setup(s => s.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<Status>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<WorkTaskDto>.Success(SampleTask()));

        var id = Guid.NewGuid();
        await _sut.UpdateStatus(id, new UpdateWorkTaskStatusDto(Status.Done), default);

        _service.Verify(s => s.UpdateStatusAsync(id, Status.Done, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_Returns404_WhenTheTaskIsMissing()
    {
        _service.Setup(s => s.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<Status>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<WorkTaskDto>.Failure(Error.NotFound));

        Assert.IsType<NotFoundObjectResult>(
            await _sut.UpdateStatus(Guid.NewGuid(), new UpdateWorkTaskStatusDto(Status.Done), default));
    }

    // ---------- DeleteTask ----------

    [Fact]
    public async Task DeleteTask_Returns200_WhenTheTaskWasDeleted()
    {
        _service.Setup(s => s.DeleteWorkTaskAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Success(true));

        Assert.IsType<OkObjectResult>(await _sut.DeleteTask(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task DeleteTask_Returns404_WhenTheTaskIsMissing()
    {
        _service.Setup(s => s.DeleteWorkTaskAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Failure(Error.NotFound));

        Assert.IsType<NotFoundObjectResult>(await _sut.DeleteTask(Guid.NewGuid(), default));
    }

    // ---------- AddComment ----------

    [Fact]
    public async Task AddComment_Returns200_WithTheStoredComment()
    {
        var comment = new TaskCommentDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Looks good", DateTime.UtcNow);
        _service.Setup(s => s.AddCommentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TaskCommentDto>.Success(comment));

        var ok = Assert.IsType<OkObjectResult>(await _sut.AddComment(Guid.NewGuid(), "Looks good", default));

        Assert.Same(comment, ok.Value);
    }

    [Fact]
    public async Task AddComment_Returns401_ForAnUnauthenticatedCaller()
    {
        _service.Setup(s => s.AddCommentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TaskCommentDto>.Failure(Error.Unauthorized));

        Assert.IsType<UnauthorizedObjectResult>(await _sut.AddComment(Guid.NewGuid(), "hi", default));
    }

    [Fact]
    public async Task AddComment_Returns403_ForANonMember()
    {
        _service.Setup(s => s.AddCommentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TaskCommentDto>.Failure(Error.Forbidden));

        var result = Assert.IsType<ObjectResult>(await _sut.AddComment(Guid.NewGuid(), "hi", default));

        Assert.Equal(403, result.StatusCode);
    }
}
