using Microsoft.AspNetCore.Mvc;
using LogApiService.Models;
using LogApiService.Services;

namespace LogApiService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly LogService _logService;

    public LogsController(LogService logService)
    {
        _logService = logService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<LogEntry>>> GetLogs([FromQuery] LogQueryParameters query)
    {
        var result = await _logService.GetLogsAsync(query);
        return Ok(result);
    }

    [HttpGet("services")]
    public async Task<ActionResult<List<string>>> GetServices()
    {
        var services = await _logService.GetDistinctServicesAsync();
        return Ok(services);
    }

    [HttpGet("levels")]
    public async Task<ActionResult<List<string>>> GetLevels()
    {
        var levels = await _logService.GetDistinctLevelsAsync();
        return Ok(levels);
    }
}
