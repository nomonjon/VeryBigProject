using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Mappers;
using TaskTracker.Models;

namespace TaskTracker.Services;

public class ProjectService(IProjectRepository projectRepo, ILogger<ProjectService> logger) : IProjectService
{
    public async Task<Result<ProjectDto>> CreateProjectAsync(CreateUpdateProjectDto newProject, CancellationToken cancellationToken)
    {
        
        if (newProject is null)
        {
            logger.LogWarning("CreateProjectAsync failed: newProject is null");
            return Result<ProjectDto>.Failure(Error.BadRequest);
        }

        var project = newProject.ToProject();

        var savedProject = await projectRepo
            .Create(project, cancellationToken);
        
        logger.LogInformation("Project created successfully with ID: {ProjectId}", savedProject.Id);
        return Result<ProjectDto>.Success(savedProject.ToProjectDto());
    }

    public async Task<Result<bool>> DeleteProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        
        var isDeleted = await projectRepo.Delete(id, cancellationToken);

        if (isDeleted)
        {
            logger.LogInformation("Project deleted successfully with ID: {ProjectId}", id);
            return Result<bool>.Success(true);
        }
        
        logger.LogWarning("Project not found for deletion with ID: {ProjectId}", id);
        return Result<bool>.Failure(Error.NotFound);
    }

    public async Task<Result<ProjectDto?>> GetProjectByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        
        var project = await projectRepo.GetById(id, cancellationToken);

        if (project is null)
        {
            logger.LogWarning("Project not found with ID: {ProjectId}", id);
            return Result<ProjectDto?>.Failure(Error.NotFound);
        }

        logger.LogInformation("Project retrieved successfully with ID: {ProjectId}", id);
        return Result<ProjectDto?>.Success(project.ToProjectDto());
    }

    public async Task<Result<ProjectDto?>> GetProjectByIdWithTasksAsync(Guid id, CancellationToken cancellationToken)
    {
        
        var project = await projectRepo.GetByIdWithTasks(id, cancellationToken);

        if (project is null)
        {
            logger.LogWarning("Project not found with ID: {ProjectId}", id);
            return Result<ProjectDto?>.Failure(Error.NotFound);
        }

        logger.LogInformation("Project with tasks retrieved successfully with ID: {ProjectId}", id);
        return Result<ProjectDto?>.Success(project.ToProjectDto());
    }

    public async Task<List<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        
        var projects = await projectRepo.GetAll(cancellationToken);
        var result = projects.Select(p => p.ToProjectDto()).ToList();
        
        logger.LogInformation("Retrieved {ProjectCount} projects", result.Count);
        return result;
    }

    public async Task<List<Project>> GetProjectsWithIdAsync(CancellationToken cancellationToken)
    {
        
        var projects = await projectRepo.GetAll(cancellationToken);
        logger.LogInformation("Retrieved {ProjectCount} projects with IDs", projects.Count);
        
        return projects;
    }

    public async Task<Result<ProjectDto>> UpdateProjectAsync(Guid id, CreateUpdateProjectDto updatedProject, CancellationToken cancellationToken)
    {
        
        var project = await projectRepo.GetById(id, cancellationToken);

        if (project is null)
        {
            logger.LogWarning("Project not found for update with ID: {ProjectId}", id);
            return Result<ProjectDto>.Failure(Error.NotFound);
        }

        var newProject = updatedProject.ToProject();
        newProject.Id = id;

        var projectSaved = await projectRepo.Update(newProject, cancellationToken);

        if (!projectSaved)
        {
            logger.LogError("Failed to save updated project with ID: {ProjectId}", id);
            return Result<ProjectDto>.Failure(Error.BadRequest);
        }

        logger.LogInformation("Project updated successfully with ID: {ProjectId}", id);
        return Result<ProjectDto>.Success(newProject.ToProjectDto());
    }
}