using System.Diagnostics;
using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GrpcServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductRuleController(IProductRuleService ruleService, ILogger<ProductRuleController> logger) : ControllerBase
{
    private readonly Stopwatch stopwatch = new();
    [HttpGet]
    public async Task<IActionResult> GetRules()
    {
        stopwatch.Start();
        var rules = await ruleService.GetAllRulesAsync();

        logger.LogInformation($"GetRules executed in {stopwatch.ElapsedMilliseconds} ms");

        return Ok(rules);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRule(Guid id)
    {
        stopwatch.Start();
        var rule = await ruleService.GetRuleByIdAsync(id);
        if (rule is null)
            return NotFound();

        logger.LogInformation($"GetRule executed in {stopwatch.ElapsedMilliseconds} ms");
        return Ok(rule);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRule([FromBody] CreateUpdateProductRuleDto newRule)
    {
        try
        {
            var createdRule = await ruleService.CreateRuleAsync(newRule);
            return Ok(createdRule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] CreateUpdateProductRuleDto updatedRule)
    {
        try
        {
            var rule = await ruleService.UpdateRuleAsync(id, updatedRule);
            if (rule is null)
                return NotFound();
            return Ok(rule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        var result = await ruleService.DeleteRuleAsync(id);
        if (!result)
            return NotFound();
        return NoContent();
    }

    [HttpGet("{id:guid}/products")]
    public async Task<IActionResult> GetMatchingProducts(Guid id)
    {
        stopwatch.Start();
        var products = await ruleService.GetMatchingProductsAsync(id);
        if (products is null)
            return NotFound();
        logger.LogInformation($"GetMatchingProducts executed in {stopwatch.ElapsedMilliseconds} ms");
        return Ok(products);
    }

    [HttpGet("evaluate/{productId:guid}")]
    public async Task<IActionResult> EvaluateProduct(Guid productId)
    {
        stopwatch.Start();  
        var matches = await ruleService.EvaluateProductAsync(productId);
        if (matches is null)
            return NotFound();

        logger.LogInformation($"EvaluateProduct executed in {stopwatch.ElapsedMilliseconds} ms");
        return Ok(matches);
    }
}
