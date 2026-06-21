using Microsoft.AspNetCore.Mvc;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Mappers;

using Microsoft.AspNetCore.Authorization;

namespace TaskTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkTaskController : ControllerBase
{
    private readonly IWorkTaskService _workTaskService;

    public WorkTaskController(IWorkTaskService workTaskService)
    {
        _workTaskService = workTaskService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks(CancellationToken cancellationToken)
    {
        var tasks = await _workTaskService.GetWorkTasksAsync(cancellationToken);
        return Ok(tasks);
    }

    [HttpGet("WithId")]
    public async Task<IActionResult> GetTasksWithId(CancellationToken cancellationToken)
    {
        var tasks = await _workTaskService.GetWorkTasksWithIdAsync(cancellationToken);
        return Ok(tasks.Select(t => t.ToWorkTaskDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTaskById(Guid id, CancellationToken cancellationToken)
    {
        var task = await _workTaskService.GetWorkTaskByIdAsync(id, cancellationToken);
        return task.ToResponse();
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask(CreateUpdateWorkTaskDto newTask, CancellationToken cancellationToken)
    {
        var createdTask = await _workTaskService.CreateWorkTaskAsync(newTask, cancellationToken);
        return createdTask.ToResponse();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTask(Guid id, CreateUpdateWorkTaskDto updatedTask, CancellationToken cancellationToken)
    {
        var task = await _workTaskService.UpdateWorkTaskAsync(id, updatedTask, cancellationToken);
        return task.ToResponse();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateWorkTaskStatusDto statusDto, CancellationToken cancellationToken)
    {
        var task = await _workTaskService.UpdateStatusAsync(id, statusDto.Status, cancellationToken);
        return task.ToResponse();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _workTaskService.DeleteWorkTaskAsync(id, cancellationToken);
        return deleted.ToResponse();
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] string content, CancellationToken cancellationToken)
    {
        var result = await _workTaskService.AddCommentAsync(id, content, cancellationToken);
        return result.ToResponse();
    }
}
