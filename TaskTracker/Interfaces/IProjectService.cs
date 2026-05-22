using TaskTracker.Dtos;
using TaskTracker.Models;

namespace TaskTracker.Interfaces;

public interface IProjectService
{
    Task<List<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken);
    Task<List<Project>> GetProjectsWithIdAsync(CancellationToken cancellationToken);
    Task<Result<ProjectDto?>> GetProjectByIdWithTasksAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<ProjectDto?>> GetProjectByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<ProjectDto>> CreateProjectAsync(CreateUpdateProjectDto newProject, CancellationToken cancellationToken);
    Task<Result<ProjectDto>> UpdateProjectAsync(Guid id, CreateUpdateProjectDto updatedProject, CancellationToken cancellationToken);
    Task<Result<bool>> DeleteProjectAsync(Guid id, CancellationToken cancellationToken);
}
