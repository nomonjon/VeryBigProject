using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrpcServer.Controllers;


[ApiController]
[Route("api/[controller]")]
public class ProductController(IProductService productService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        var products = await productService.GetAllProductsAsync();
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        var product = await productService.GetProductByIdAsync(id);
        if (product is null)
            return NotFound();
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
