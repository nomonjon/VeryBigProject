using Microsoft.AspNetCore.Mvc;
using TaskTracker.Services;

namespace TaskTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductRuleController(ProductRuleApiService ruleService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rules = await ruleService.GetAllAsync();
        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUpdateProductRuleDto dto)
    {
        try
        {
            var rule = await ruleService.CreateAsync(dto);
            return Ok(rule);
        }
        catch (ProductRuleApiException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await ruleService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
