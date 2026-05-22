using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using KeyNotFoundException = System.Collections.Generic.KeyNotFoundException;

namespace TaskTracker.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProjectController(IProjectService projectService) : ControllerBase
{
    [Authorize(Roles = "User")]
    [HttpGet]
    public async Task<IActionResult> GetProjects(CancellationToken cancellationToken)
    {
        var projects = await projectService.GetProjectsAsync(cancellationToken);
        return Ok(projects);
    }
    [HttpGet("withId")]
    public async Task<IActionResult> GetProjectsWithId(CancellationToken cancellationToken)
    {
        var projects = await projectService.GetProjectsWithIdAsync(cancellationToken);
        return Ok(projects);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProject(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var project = await projectService.GetProjectByIdAsync(id, cancellationToken);
            return Ok(project);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("WithTasks{id:guid}")]
    public async Task<IActionResult> GetProjectWithTasks(Guid id, CancellationToken cancellationToken)
    { 
            var project = await projectService.GetProjectByIdWithTasksAsync(id, cancellationToken);
        return project.ToResponse();
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateUpdateProjectDto newProject, CancellationToken cancellationToken)
    {
        try
        {
            var createdProject = await projectService.CreateProjectAsync(newProject, cancellationToken);
            return Ok(createdProject);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProject([FromRoute] Guid id, [FromBody] CreateUpdateProjectDto updatedProject, CancellationToken cancellationToken)
    {
        try
        {
            var project = await projectService.UpdateProjectAsync(id, updatedProject, cancellationToken);
            return Ok(project);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken cancellationToken)
    {
        var isDeleted = await projectService.DeleteProjectAsync(id, cancellationToken);

        if (!isDeleted.IsSuccess)
            return NotFound($"Project with id {id} not found");

        return NoContent();
    }
}
