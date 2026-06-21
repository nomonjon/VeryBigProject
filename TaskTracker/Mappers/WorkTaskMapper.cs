using TaskTracker.Dtos;
using TaskTracker.Models;

namespace TaskTracker.Mappers;

public static class WorkTaskMapper
{
    public static WorkTaskDto ToWorkTaskDto(this WorkTask workTask)
    {
        var comments = workTask.Comments?.Select(c => new TaskCommentDto(c.Id, c.WorkTaskId, c.UserId, c.Content, c.CreatedAt)).ToList() ?? new List<TaskCommentDto>();
        var history = workTask.History?.Select(h => new TaskHistoryDto(h.Id, h.WorkTaskId, h.UserId, h.FieldName, h.OldValue, h.NewValue, h.ChangedAt)).ToList() ?? new List<TaskHistoryDto>();

        return new WorkTaskDto
        (
            workTask.Id,
            workTask.Name,
            workTask.Description,
            workTask.Priority,
            workTask.Status,
            workTask.ProjectId,
            workTask.AssigneeId,
            comments,
            history
        );
    }
    public static WorkTask ToWorkTask(this CreateUpdateWorkTaskDto workTaskDto, Guid projectId, Guid? assigneeId)
    {
        return new WorkTask
        {
            Name = workTaskDto.Name,
            Description = workTaskDto.Description,
            Priority = workTaskDto.Priority,
            Status = workTaskDto.Status,
            ProjectId = projectId,
            AssigneeId = assigneeId
        };
    }
}
