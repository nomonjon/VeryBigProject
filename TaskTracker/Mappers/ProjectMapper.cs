using TaskTracker.Dtos;
using TaskTracker.Models;

namespace TaskTracker.Mappers;

public static class ProjectMapper
{
    public static ProjectDto ToProjectDto(this Project project)
    {
        return new ProjectDto(
            project.Name,
            project.Description,
            project.WorkTasks is null
                ? new List<WorkTaskDto>()
                : project.WorkTasks.Select(wt => wt.ToWorkTaskDto()).ToList()
        );
    }

    public static ProjectWithIdDto ToProjectWithIdDto(this Project project)
    {
        return new ProjectWithIdDto(
            project.Id,
            project.Name,
            project.Description,
            project.WorkTasks is null
                ? new List<WorkTaskDto>()
                : project.WorkTasks.Select(wt => wt.ToWorkTaskDto()).ToList(),
            project.Users is null
                ? new List<Guid>()
                : project.Users.Select(u => u.Id).ToList()
        );
    }

    public static Project ToProject(this CreateUpdateProjectDto projectDto)
    {
        return new Project
        {
            Name = projectDto.Name,
            Description = projectDto.Description
        };
    }
}