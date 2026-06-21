using Microsoft.AspNetCore.Mvc;
using TaskTracker.Services;

namespace TaskTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(ProductApiService productService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await productService.GetAllAsync();
        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await productService.GetByIdAsync(id);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUpdateProductDto dto)
    {
        var product = await productService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, CreateUpdateProductDto dto)
    {
        var product = await productService.UpdateAsync(id, dto);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await productService.DeleteAsync(id);
        return result ? NoContent() : NotFound();
    }
}