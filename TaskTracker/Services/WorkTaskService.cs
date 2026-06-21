using TaskTracker.Contracts;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Mappers;
using TaskTracker.Models;
using KeyNotFoundException = System.Collections.Generic.KeyNotFoundException;

using TaskTracker.Data;

namespace TaskTracker.Services;

public class WorkTaskService(IWorkTaskRepository workTaskRepo,
    IUserRepository userRepo,
    IProjectRepository projectRepo,
    RabbitMqPublisher publisher,
    IHttpContextAccessor httpContextAccessor,
    AppDbContext dbContext,
    ILogger<WorkTaskService> logger) : IWorkTaskService
{
    private (Guid userId, string? role) GetCurrentUser()
    {
        var claims = httpContextAccessor.HttpContext?.User;
        var role = claims?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        Guid.TryParse(claims?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId);
        return (userId, role);
    }

    private static bool IsProjectMember(Project? project, Guid userId)
        => project?.Users?.Any(u => u.Id == userId) == true;

    public async Task<Result<WorkTaskDto>> CreateWorkTaskAsync(CreateUpdateWorkTaskDto newWorkTask, CancellationToken cancellationToken)
    {
        if (newWorkTask is null)
        {
            logger.LogWarning("Attempt to create work task with null data");
            return Result<WorkTaskDto>.Failure(Error.BadRequest);
        }

        var (currentUserId, role) = GetCurrentUser();

        var (project, user) = await GetProjectAndUserAsync(newWorkTask.ProjectId, newWorkTask.AssigneeId ?? Guid.Empty, cancellationToken);

        if (project is null)
            return Result<WorkTaskDto>.Failure(Error.NotFound);

        if (role != Roles.Admin)
        {
            var projectWithUsers = await projectRepo.GetByIdWithUsers(project.Id, cancellationToken);
            if (!IsProjectMember(projectWithUsers, currentUserId))
                return Result<WorkTaskDto>.Failure(Error.Forbidden);
        }

        var workTask = newWorkTask.ToWorkTask(project.Id, user?.Id);

        var savedTask = await workTaskRepo.Create(workTask, cancellationToken);
        logger.LogInformation("Successfully created work task with ID: {TaskId}", savedTask.Id);
        return Result<WorkTaskDto>.Success(savedTask.ToWorkTaskDto());
    }

    public async Task<Result<bool>> DeleteWorkTaskAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = await workTaskRepo.GetById(id, cancellationToken);
        if (task is null)
        {
            logger.LogWarning("Failed to delete work task - not found. ID: {TaskId}", id);
            return Result<bool>.Failure(Error.NotFound);
        }

        var (currentUserId, role) = GetCurrentUser();
        if (role != Roles.Admin && !IsProjectMember(task.Project, currentUserId))
            return Result<bool>.Failure(Error.Forbidden);

        var isDeleted = await workTaskRepo.Delete(id, cancellationToken);

        if (isDeleted is false)
        {
            logger.LogWarning("Failed to delete work task - not found. ID: {TaskId}", id);
            return Result<bool>.Failure(Error.NotFound);
        }

        logger.LogInformation("Successfully deleted work task with ID: {TaskId}", id);
        return Result<bool>.Success(true);
    }

    public async Task<(Project?, User?)> GetProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await projectRepo.GetById(projectId, cancellationToken);
        var user = await userRepo.GetById(userId, cancellationToken);

        if (project is null)
            return (null, null);

        if (user is null)
            return (project, null);

        return (project, user);
    }

    public async Task<Result<WorkTaskDto?>> GetWorkTaskByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var workTask = await workTaskRepo.GetById(id, cancellationToken);

        if (workTask is null)
        {
            logger.LogWarning("Work task not found. ID: {TaskId}", id);
            return Result<WorkTaskDto?>.Failure(Error.NotFound);
        }

        var (currentUserId, role) = GetCurrentUser();

        if (role != Roles.Admin && !IsProjectMember(workTask.Project, currentUserId))
            return Result<WorkTaskDto?>.Failure(Error.Forbidden);

        logger.LogInformation("Successfully retrieved work task with ID: {TaskId}", id);
        return Result<WorkTaskDto?>.Success(workTask.ToWorkTaskDto());
    }

    public async Task<List<WorkTaskDto>> GetWorkTasksAsync(CancellationToken cancellationToken)
    {
        var workTasks = await GetFilteredWorkTasksAsync(cancellationToken);
        return workTasks.Select(wt => wt.ToWorkTaskDto()).ToList();
    }

    public async Task<List<WorkTask>> GetWorkTasksWithIdAsync(CancellationToken cancellationToken)
    {
        return await GetFilteredWorkTasksAsync(cancellationToken);
    }

    private async Task<List<WorkTask>> GetFilteredWorkTasksAsync(CancellationToken cancellationToken)
    {
        var (currentUserId, role) = GetCurrentUser();
        if (currentUserId == Guid.Empty) return new List<WorkTask>();

        var allTasks = await workTaskRepo.GetAll(cancellationToken);

        if (role == Roles.Admin)
            return allTasks;

        var filtered = allTasks.Where(wt => IsProjectMember(wt.Project, currentUserId)).ToList();
        logger.LogInformation("Retrieved {Count} work tasks for user", filtered.Count);
        return filtered;
    }

    public async Task<Result<WorkTaskDto>> UpdateWorkTaskAsync(Guid id, CreateUpdateWorkTaskDto updatedWorkTask, CancellationToken cancellationToken)
    {
        var workTask = await workTaskRepo.GetById(id, cancellationToken);

        if (workTask is null)
        {
            logger.LogWarning("Work task not found for update. ID: {TaskId}", id);
            return Result<WorkTaskDto>.Failure(Error.NotFound);
        }

        var (currentUserId, role) = GetCurrentUser();
        if (role != Roles.Admin && !IsProjectMember(workTask.Project, currentUserId))
            return Result<WorkTaskDto>.Failure(Error.Forbidden);

        var (project, user) = await GetProjectAndUserAsync(
            updatedWorkTask.ProjectId,
            updatedWorkTask.AssigneeId ?? Guid.Empty,
            cancellationToken);

        if (project is null)
        {
            logger.LogWarning("Project not found for work task update. ProjectId: {ProjectId}", updatedWorkTask.ProjectId);
            return Result<WorkTaskDto>.Failure(Error.NotFound);
        }

        // If reassigning to a different project, also require membership in destination
        if (role != Roles.Admin && project.Id != workTask.ProjectId)
        {
            var destProject = await projectRepo.GetByIdWithUsers(project.Id, cancellationToken);
            if (!IsProjectMember(destProject, currentUserId))
                return Result<WorkTaskDto>.Failure(Error.Forbidden);
        }

        var oldStatus = workTask.Status;
        var oldName = workTask.Name;
        var oldAssigneeId = workTask.AssigneeId;
        var oldDesc = workTask.Description;
        var oldPriority = workTask.Priority;

        var newTask = updatedWorkTask.ToWorkTask(project.Id, user?.Id);
        newTask.Id = id;

        var saved = await workTaskRepo.Update(newTask, cancellationToken);
        if (!saved)
        {
            logger.LogError("Failed to update work task with ID: {TaskId}", id);
            return Result<WorkTaskDto>.Failure(Error.BadRequest);
        }

        if (currentUserId != Guid.Empty)
        {
            var histories = new List<TaskHistory>();
            if (oldStatus != newTask.Status)
                histories.Add(new TaskHistory { WorkTaskId = id, UserId = currentUserId, FieldName = "Status", OldValue = oldStatus.ToString(), NewValue = newTask.Status.ToString() });
            if (oldName != newTask.Name)
                histories.Add(new TaskHistory { WorkTaskId = id, UserId = currentUserId, FieldName = "Name", OldValue = oldName, NewValue = newTask.Name });
            if (oldAssigneeId != newTask.AssigneeId)
                histories.Add(new TaskHistory { WorkTaskId = id, UserId = currentUserId, FieldName = "Assignee", OldValue = oldAssigneeId?.ToString() ?? "Unassigned", NewValue = newTask.AssigneeId?.ToString() ?? "Unassigned" });
            if (oldDesc != newTask.Description)
                histories.Add(new TaskHistory { WorkTaskId = id, UserId = currentUserId, FieldName = "Description", OldValue = oldDesc ?? "", NewValue = newTask.Description ?? "" });
            if (oldPriority != newTask.Priority)
                histories.Add(new TaskHistory { WorkTaskId = id, UserId = currentUserId, FieldName = "Priority", OldValue = oldPriority.ToString(), NewValue = newTask.Priority.ToString() });

            if (histories.Any())
            {
                await dbContext.TaskHistories.AddRangeAsync(histories, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        if (oldStatus != newTask.Status)
        {
            await PublishStatusChangeAsync(workTask, newTask.Name, oldStatus, newTask.Status, user);
        }

        logger.LogInformation("Successfully updated work task with ID: {TaskId}", id);
        return Result<WorkTaskDto>.Success(newTask.ToWorkTaskDto());
    }

    public async Task<Result<WorkTaskDto>> UpdateStatusAsync(Guid id, Status newStatus, CancellationToken cancellationToken)
    {
        var workTask = await workTaskRepo.GetById(id, cancellationToken);
        if (workTask is null)
            return Result<WorkTaskDto>.Failure(Error.NotFound);

        var (currentUserId, role) = GetCurrentUser();
        if (role != Roles.Admin && !IsProjectMember(workTask.Project, currentUserId))
            return Result<WorkTaskDto>.Failure(Error.Forbidden);

        if (workTask.Status == newStatus)
            return Result<WorkTaskDto>.Success(workTask.ToWorkTaskDto());

        var oldStatus = workTask.Status;
        var updated = new WorkTask
        {
            Id = workTask.Id,
            Name = workTask.Name,
            Description = workTask.Description,
            Priority = workTask.Priority,
            Status = newStatus,
            ProjectId = workTask.ProjectId,
            AssigneeId = workTask.AssigneeId
        };

        var saved = await workTaskRepo.Update(updated, cancellationToken);
        if (!saved)
            return Result<WorkTaskDto>.Failure(Error.BadRequest);

        if (currentUserId != Guid.Empty)
        {
            await dbContext.TaskHistories.AddAsync(new TaskHistory
            {
                WorkTaskId = id,
                UserId = currentUserId,
                FieldName = "Status",
                OldValue = oldStatus.ToString(),
                NewValue = newStatus.ToString()
            }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await PublishStatusChangeAsync(workTask, workTask.Name, oldStatus, newStatus, workTask.Assignee);

        logger.LogInformation("Task {TaskId} status changed: {OldStatus} → {NewStatus}", id, oldStatus, newStatus);
        return Result<WorkTaskDto>.Success(updated.ToWorkTaskDto());
    }

    private async Task PublishStatusChangeAsync(WorkTask workTask, string taskName, Status oldStatus, Status newStatus, User? assignee)
    {
        var projectUserEmails = workTask.Project?.Users?.Select(u => u.Email).ToList() ?? new List<string>();
        var projectName = workTask.Project?.Name ?? string.Empty;

        await publisher.PublishAsync(new TaskStatusChangedEvent
        {
            Id = workTask.Id,
            Name = taskName,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            AssigneeId = assignee?.Id,
            AssigneeEmail = assignee?.Email ?? string.Empty,
            ProjectName = projectName,
            ProjectUserEmails = projectUserEmails
        });
    }

    public async Task<Result<TaskCommentDto>> AddCommentAsync(Guid taskId, string content, CancellationToken cancellationToken)
    {
        var (currentUserId, role) = GetCurrentUser();
        if (currentUserId == Guid.Empty)
            return Result<TaskCommentDto>.Failure(Error.Unauthorized);

        var task = await workTaskRepo.GetById(taskId, cancellationToken);
        if (task == null) return Result<TaskCommentDto>.Failure(Error.NotFound);

        if (role != Roles.Admin && !IsProjectMember(task.Project, currentUserId))
            return Result<TaskCommentDto>.Failure(Error.Forbidden);

        var comment = new TaskComment
        {
            WorkTaskId = taskId,
            UserId = currentUserId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.TaskComments.AddAsync(comment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<TaskCommentDto>.Success(new TaskCommentDto(comment.Id, comment.WorkTaskId, comment.UserId, comment.Content, comment.CreatedAt));
    }
}
