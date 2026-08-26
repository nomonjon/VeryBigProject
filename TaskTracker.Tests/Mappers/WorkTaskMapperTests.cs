using TaskTracker.Mappers;
using TaskTracker.Models;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Mappers;

public class WorkTaskMapperTests
{
    [Fact]
    public void ToWorkTaskDto_CopiesEveryScalarField()
    {
        var projectId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var task = Make.WorkTask(
            name: "Write docs",
            description: "Describe the API",
            priority: Priority.High,
            status: Status.Review,
            projectId: projectId,
            assigneeId: assigneeId);

        var dto = task.ToWorkTaskDto();

        Assert.Equal(task.Id, dto.Id);
        Assert.Equal("Write docs", dto.Name);
        Assert.Equal("Describe the API", dto.Description);
        Assert.Equal(Priority.High, dto.Priority);
        Assert.Equal(Status.Review, dto.Status);
        Assert.Equal(projectId, dto.ProjectId);
        Assert.Equal(assigneeId, dto.AssigneeId);
    }

    [Fact]
    public void ToWorkTaskDto_KeepsAssigneeIdNull_WhenTheTaskIsUnassigned()
        => Assert.Null(Make.WorkTask(assigneeId: null).ToWorkTaskDto().AssigneeId);

    [Fact]
    public void ToWorkTaskDto_MapsComments()
    {
        var task = Make.WorkTask();
        var createdAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        task.Comments =
        [
            new TaskComment { Id = Guid.NewGuid(), WorkTaskId = task.Id, UserId = Guid.NewGuid(), Content = "Looks good", CreatedAt = createdAt }
        ];

        var comment = Assert.Single(task.ToWorkTaskDto().Comments!);

        Assert.Equal("Looks good", comment.Content);
        Assert.Equal(task.Id, comment.WorkTaskId);
        Assert.Equal(createdAt, comment.CreatedAt);
    }

    [Fact]
    public void ToWorkTaskDto_MapsHistory()
    {
        var task = Make.WorkTask();
        task.History =
        [
            new TaskHistory { Id = Guid.NewGuid(), WorkTaskId = task.Id, FieldName = "Status", OldValue = "InProgress", NewValue = "Done" }
        ];

        var entry = Assert.Single(task.ToWorkTaskDto().History!);

        Assert.Equal("Status", entry.FieldName);
        Assert.Equal("InProgress", entry.OldValue);
        Assert.Equal("Done", entry.NewValue);
    }

    [Fact]
    public void ToWorkTaskDto_ReturnsEmptyCollections_WhenNavigationsWereNotLoaded()
    {
        var task = Make.WorkTask();
        task.Comments = null!;
        task.History = null!;

        var dto = task.ToWorkTaskDto();

        // Without an .Include() EF leaves these null. Callers project straight onto the
        // DTO, so mapping must absorb that rather than throwing at serialization time.
        Assert.Empty(dto.Comments!);
        Assert.Empty(dto.History!);
    }

    [Fact]
    public void ToWorkTask_CopiesTheClientSuppliedFields()
    {
        var dto = Make.WorkTaskDto(name: "Write docs", description: "Describe", priority: Priority.Low, status: Status.Done);

        var task = dto.ToWorkTask(Guid.NewGuid(), null);

        Assert.Equal("Write docs", task.Name);
        Assert.Equal("Describe", task.Description);
        Assert.Equal(Priority.Low, task.Priority);
        Assert.Equal(Status.Done, task.Status);
    }

    [Fact]
    public void ToWorkTask_TakesProjectAndAssignee_FromTheArguments_NotTheDto()
    {
        var resolvedProject = Guid.NewGuid();
        var resolvedAssignee = Guid.NewGuid();
        // The service resolves and validates these ids before mapping, so whatever the
        // client put in the body must be ignored.
        var dto = Make.WorkTaskDto(projectId: Guid.NewGuid(), assigneeId: Guid.NewGuid());

        var task = dto.ToWorkTask(resolvedProject, resolvedAssignee);

        Assert.Equal(resolvedProject, task.ProjectId);
        Assert.Equal(resolvedAssignee, task.AssigneeId);
    }

    [Fact]
    public void ToWorkTask_AcceptsANullAssignee()
        => Assert.Null(Make.WorkTaskDto().ToWorkTask(Guid.NewGuid(), null).AssigneeId);

    [Fact]
    public void ToWorkTask_LeavesIdEmpty_SoTheDatabaseAssignsIt()
        => Assert.Equal(Guid.Empty, Make.WorkTaskDto().ToWorkTask(Guid.NewGuid(), null).Id);
}
