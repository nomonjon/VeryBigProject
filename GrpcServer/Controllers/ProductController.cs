using System.Diagnostics;
using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrpcServer.Controllers;


[ApiController]
[Route("api/[controller]")]
public class ProductController(IProductService productService, ILogger<ProductController> logger) : ControllerBase
{
    private readonly Stopwatch stopwatch = new();
    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        stopwatch.Start();
        var products = await productService.GetAllProductsAsync();
        
        logger.LogInformation($"GetProducts executed in {stopwatch.ElapsedMilliseconds} ms");

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        stopwatch.Start();
        var product = await productService.GetProductByIdAsync(id);
        if (product is null)
            return NotFound();

        logger.LogInformation($"GetProduct executed in {stopwatch.ElapsedMilliseconds} ms");
        return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] CreateUpdateProductDto newProduct)
    {
        var createdProduct = await productService.CreateProductAsync(newProduct);
        return Ok(createdProduct);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] CreateUpdateProductDto updatedProduct)
    {
        var product = await productService.UpdateProductAsync(id, updatedProduct);
        if (product is null)
            return NotFound();
        return Ok(product);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var result = await productService.DeleteProductAsync(id);
        if (!result)
            return NotFound();
        return NoContent();
    }
}
