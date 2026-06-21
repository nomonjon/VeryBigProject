using TaskTracker.Models;

namespace TaskTracker.Interfaces;

public interface IProjectRepository
{
    Task<List<Project>> GetAll(CancellationToken cancellationToken);
    Task<List<Project>> GetAllWithUsers(CancellationToken cancellationToken);
    Task<Project?> GetById(Guid id, CancellationToken cancellationToken);
    Task<Project?> GetByIdWithTasks(Guid id, CancellationToken cancellationToken);
    Task<Project?> GetByIdWithUsers(Guid id, CancellationToken cancellationToken);
    Task<Project> Create(Project newProject, CancellationToken cancellationToken);
    Task<bool> Update(Project updatedProject, CancellationToken cancellationToken);
    Task<bool> Delete(Guid id, CancellationToken cancellationToken);
}