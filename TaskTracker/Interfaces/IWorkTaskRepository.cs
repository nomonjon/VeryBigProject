using TaskTracker.Models;

namespace TaskTracker.Interfaces;

public interface IWorkTaskRepository
{
    Task<List<WorkTask>> GetAll(CancellationToken cancellationToken);
    Task<WorkTask?> GetById(Guid id, CancellationToken cancellationToken);
    Task<WorkTask> Create(WorkTask newTask, CancellationToken cancellationToken);
    Task<bool> Update(WorkTask updatedTask, CancellationToken cancellationToken);
    Task<bool> Delete(Guid id, CancellationToken cancellationToken);
}