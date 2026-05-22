using TaskTracker.Dtos;
using TaskTracker.Models;

namespace TaskTracker.Mappers;

public static class WorkTaskMapper
{
    public static WorkTaskDto ToWorkTaskDto(this WorkTask workTask)
    {
        return new WorkTaskDto
        (
            workTask.Name,
            workTask.Description,
            workTask.Priority,
            workTask.Status,
            workTask.Id,
            workTask.AssigneeId
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
