using TaskTracker.Models;

namespace TaskTracker.Dtos;

public record TaskCommentDto(Guid Id, Guid WorkTaskId, Guid UserId, string Content, DateTime CreatedAt);
public record TaskHistoryDto(Guid Id, Guid WorkTaskId, Guid UserId, string FieldName, string OldValue, string NewValue, DateTime ChangedAt);

public record WorkTaskDto(Guid Id, string Name, string Description, Priority Priority, Status Status, Guid ProjectId, Guid? AssigneeId, List<TaskCommentDto>? Comments, List<TaskHistoryDto>? History);

public record CreateUpdateWorkTaskDto(string Name, string Description, Priority Priority, Status Status, Guid ProjectId, Guid? AssigneeId);

public record UpdateWorkTaskStatusDto(Status Status);
