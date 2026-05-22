using GrpcServer.Dtos;
using GrpcServer.Models;

namespace GrpcServer.Mapper;

public static class ProductMapper
{
    public static Product ToProduct(this CreateUpdateProductDto productDto, Guid id = default)
    {
        return new Product
        {
            Id = id,
            Name = productDto.Name,
            Quantity = productDto.Quantity,
            Price = productDto.Price
        };
    }

    public static Product ToProduct(this CreateUpdateProductDto productDto)
    {
        return new Product
        {
            Name = productDto.Name,
            Quantity = productDto.Quantity,
            Price = productDto.Price
        };
    }

    public static ProductDto ToProductDto(this Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Quantity = product.Quantity,
            Price = product.Price
        };
    }
}
