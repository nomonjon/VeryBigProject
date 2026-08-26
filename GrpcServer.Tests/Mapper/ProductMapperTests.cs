using GrpcServer.Mapper;
using GrpcServer.Models;
using GrpcServer.Tests.TestKit;

namespace GrpcServer.Tests.Mapper;

public class ProductMapperTests
{
    [Fact]
    public void ToProduct_CopiesEveryEditableField()
    {
        var dto = Make.ProductDto(name: "Keyboard", quantity: 42, price: 19.99m);

        var product = dto.ToProduct();

        Assert.Equal("Keyboard", product.Name);
        Assert.Equal(42, product.Quantity);
        Assert.Equal(19.99m, product.Price);
    }

    [Fact]
    public void ToProduct_LeavesIdEmpty_SoTheDatabaseAssignsIt()
    {
        var product = Make.ProductDto().ToProduct();

        Assert.Equal(Guid.Empty, product.Id);
    }

    [Fact]
    public void ToProduct_UsesTheDefaultStatusColor_BecauseOnlyTheRuleSweepPaintsProducts()
    {
        var product = Make.ProductDto().ToProduct();

        Assert.Equal(ProductColors.Default, product.StatusColor);
    }

    [Fact]
    public void ToProduct_WithId_StampsTheSuppliedId()
    {
        var id = Guid.NewGuid();

        var product = Make.ProductDto().ToProduct(id);

        Assert.Equal(id, product.Id);
    }

    [Fact]
    public void ToProductDto_CopiesEveryFieldIncludingStatusColor()
    {
        var product = Make.Product(name: "Mouse", quantity: 3, price: 25m, statusColor: ProductColors.Red);

        var dto = product.ToProductDto();

        Assert.Equal(product.Id, dto.Id);
        Assert.Equal("Mouse", dto.Name);
        Assert.Equal(3, dto.Quantity);
        Assert.Equal(25m, dto.Price);
        Assert.Equal(ProductColors.Red, dto.StatusColor);
    }

    [Fact]
    public void ToProductDto_DoesNotLeakLastCheckedTime_WhichIsInternalToTheSweep()
    {
        var dto = Make.Product(lastCheckedTime: DateTime.UtcNow).ToProductDto();

        // ProductDto has no LastCheckedTime member at all — this test exists so that
        // adding one is a deliberate decision, not an accident of copy-paste.
        Assert.Null(typeof(GrpcServer.Dtos.ProductDto).GetProperty("LastCheckedTime"));
        Assert.NotNull(dto);
    }

    [Fact]
    public void ToProduct_ThenToProductDto_RoundTripsTheEditableFields()
    {
        var id = Guid.NewGuid();
        var dto = Make.ProductDto(name: "Monitor", quantity: 7, price: 300m);

        var result = dto.ToProduct(id).ToProductDto();

        Assert.Equal(id, result.Id);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Quantity, result.Quantity);
        Assert.Equal(dto.Price, result.Price);
    }
}
