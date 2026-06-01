using LogPlatform.Models;
using LogPlatform.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly LogService _logService;

    public LogsController(LogService logService)
    {
        _logService = logService;
    }

    /// <summary>
    /// Query paginated log entries with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<LogEntry>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? level = null,
        [FromQuery] string? serviceName = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _logService.GetLogsAsync(
            page, pageSize, level, serviceName, search, startDate, endDate);

        return Ok(result);
    }

    /// <summary>
    /// Returns the list of known service names derived from MongoDB collection names.
    /// </summary>
    [HttpGet("services")]
    public async Task<ActionResult<IEnumerable<string>>> GetServices()
    {
        var services = await _logService.GetServiceNamesAsync();
        return Ok(services);
    }
}
