using TaskTracker.Dtos;
using TaskTracker.Models;

namespace TaskTracker.Interfaces;

public interface IWorkTaskService
{
    Task<List<WorkTaskDto>> GetWorkTasksAsync(CancellationToken cancellationToken);
    Task<List<WorkTask>> GetWorkTasksWithIdAsync(CancellationToken cancellationToken);
    Task<Result<WorkTaskDto?>> GetWorkTaskByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<(Project?, User?)> GetProjectAndUserAsync(Guid projectId,Guid userId, CancellationToken cancellationToken);
    Task<Result<WorkTaskDto>> CreateWorkTaskAsync(CreateUpdateWorkTaskDto newWorkTask, CancellationToken cancellationToken);
    Task<Result<WorkTaskDto>> UpdateWorkTaskAsync(Guid id, CreateUpdateWorkTaskDto updatedWorkTask, CancellationToken cancellationToken);
    Task<Result<WorkTaskDto>> UpdateStatusAsync(Guid id, Status newStatus, CancellationToken cancellationToken);
    Task<Result<bool>> DeleteWorkTaskAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<TaskCommentDto>> AddCommentAsync(Guid taskId, string content, CancellationToken cancellationToken);
}
