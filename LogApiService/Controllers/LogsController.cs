using LogApiService.Models;
using LogApiService.Services;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<ActionResult<PaginatedResult<LogEntry>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? level = null,
        [FromQuery] string? serviceName = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _logService.GetLogsAsync(page, pageSize, level, serviceName, search, startDate, endDate);
        return Ok(result);
    }
}
