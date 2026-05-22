using TaskTracker.Contracts;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Mappers;
using TaskTracker.Models;
using KeyNotFoundException = System.Collections.Generic.KeyNotFoundException;

namespace TaskTracker.Services;

public class WorkTaskService(IWorkTaskRepository workTaskRepo,
    IUserRepository userRepo,
    IProjectRepository projectRepo,
    RabbitMqPublisher publisher,
    ILogger<WorkTaskService> logger) : IWorkTaskService
{

    public async Task<Result<WorkTaskDto>> CreateWorkTaskAsync(CreateUpdateWorkTaskDto newWorkTask, CancellationToken cancellationToken)
    {

        if (newWorkTask is null)
        {
            logger.LogWarning("Attempt to create work task with null data");
            return Result<WorkTaskDto>.Failure(Error.BadRequest);
        }

        var (project, user) = await GetProjectAndUserAsync(newWorkTask.ProjectId, newWorkTask.AssigneeId ?? Guid.Empty, cancellationToken);

        var workTask = newWorkTask.ToWorkTask(project!.Id, user?.Id);

        var savedTask = await workTaskRepo.Create(workTask, cancellationToken);
        logger.LogInformation("Successfully created work task with ID: {TaskId}", savedTask.Id);
        return Result<WorkTaskDto>.Success(savedTask.ToWorkTaskDto());
    }

    public async Task<Result<bool>> DeleteWorkTaskAsync(Guid id, CancellationToken cancellationToken)
    {
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

        logger.LogInformation("Successfully retrieved work task with ID: {TaskId}", id);
        return Result<WorkTaskDto?>.Success(workTask.ToWorkTaskDto());
    }

    public async Task<List<WorkTaskDto>> GetWorkTasksAsync(CancellationToken cancellationToken)
    {
        var workTasks = await workTaskRepo.GetAll(cancellationToken);
        logger.LogInformation("Retrieved {Count} work tasks", workTasks.Count);
        return workTasks.Select(wt => wt.ToWorkTaskDto()).ToList();
    }

    public async Task<List<WorkTask>> GetWorkTasksWithIdAsync(CancellationToken cancellationToken)
    {

        logger.LogInformation("Retrieved work tasks");
        return await workTaskRepo.GetAll(cancellationToken);
    }

    public async Task<Result<WorkTaskDto>> UpdateWorkTaskAsync(Guid id, CreateUpdateWorkTaskDto updatedWorkTask, CancellationToken cancellationToken)
    {
        var workTask = await workTaskRepo.GetById(id, cancellationToken);

        if (workTask is null)
        {
            logger.LogWarning("Work task not found for update. ID: {TaskId}", id);
            return Result<WorkTaskDto>.Failure(Error.NotFound);
        }

        var (project, user) = await GetProjectAndUserAsync(
            updatedWorkTask.ProjectId,
            updatedWorkTask.AssigneeId ?? Guid.Empty,
            cancellationToken);

        if (project is null)
        {
            logger.LogWarning("Project not found for work task update. ProjectId: {ProjectId}", updatedWorkTask.ProjectId);
            return Result<WorkTaskDto>.Failure(Error.NotFound);
        }

        var oldStatus = workTask.Status;  // capture before overwrite

        var newTask = updatedWorkTask.ToWorkTask(project.Id, user?.Id);
        newTask.Id = id;

        var saved = await workTaskRepo.Update(newTask, cancellationToken);
        if (!saved)
        {
            logger.LogError("Failed to update work task with ID: {TaskId}", id);
            return Result<WorkTaskDto>.Failure(Error.BadRequest);
        }

        if (oldStatus != newTask.Status)
        {
            await publisher.PublishAsync(new TaskStatusChangedEvent
            {
                Id = id,
                Name = newTask.Name,
                OldStatus = oldStatus,
                NewStatus = newTask.Status,
                ChangedAt = DateTime.UtcNow,
            });

            logger.LogInformation("Task {TaskId} status changed: {OldStatus} → {NewStatus}", id, oldStatus, newTask.Status);
        }

        logger.LogInformation("Successfully updated work task with ID: {TaskId}", id);
        return Result<WorkTaskDto>.Success(newTask.ToWorkTaskDto());
    }

}
