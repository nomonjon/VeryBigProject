using System.Net;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Controllers;
using TaskTracker.Services;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Controllers;

/// <summary>
/// Another concrete, non-mockable dependency (<see cref="ProductRuleApiService"/>), so
/// the seam is the HTTP handler underneath its typed client.
/// </summary>
public class ProductRuleControllerTests
{
    private static ProductRuleController CreateSut(StubHttpMessageHandler handler)
        => new(new ProductRuleApiService(handler.CreateClient()));

    [Fact]
    public async Task GetAll_Returns200_WithTheRules()
    {
        var handler = StubHttpMessageHandler.ReturningJson(new List<ProductRuleDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Low stock", Expression = "Quantity < 5" }
        });

        var ok = Assert.IsType<OkObjectResult>(await CreateSut(handler).GetAll());

        var rules = Assert.IsAssignableFrom<IEnumerable<ProductRuleDto>>(ok.Value);
        Assert.Equal("Low stock", Assert.Single(rules).Name);
    }

    [Fact]
    public async Task GetAll_Returns200_WithAnEmptyList_WhenThereAreNoRules()
    {
        var handler = StubHttpMessageHandler.ReturningJson(new List<ProductRuleDto>());

        var ok = Assert.IsType<OkObjectResult>(await CreateSut(handler).GetAll());

        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ProductRuleDto>>(ok.Value));
    }

    [Fact]
    public async Task Create_Returns200_WithTheCreatedRule()
    {
        var handler = StubHttpMessageHandler.ReturningJson(new ProductRuleDto { Id = Guid.NewGuid(), Name = "Low stock" });

        var ok = Assert.IsType<OkObjectResult>(
            await CreateSut(handler).Create(new CreateUpdateProductRuleDto { Name = "Low stock", Expression = "Quantity < 5" }));

        Assert.Equal("Low stock", Assert.IsType<ProductRuleDto>(ok.Value).Name);
    }

    [Fact]
    public async Task Create_Returns400_CarryingTheUpstreamValidationMessage()
    {
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.BadRequest, "Invalid rule expression: bad token");

        var badRequest = Assert.IsType<BadRequestObjectResult>(
            await CreateSut(handler).Create(new CreateUpdateProductRuleDto()));

        // GrpcServer owns expression validation; this endpoint must relay its reason
        // rather than replacing it with a generic 400.
        Assert.Equal("Invalid rule expression: bad token", badRequest.Value);
    }

    [Fact]
    public async Task Delete_Returns204_WhenTheRuleWasDeleted()
    {
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.NoContent);

        Assert.IsType<NoContentResult>(await CreateSut(handler).Delete(Guid.NewGuid()));
    }

    [Fact]
    public async Task Delete_Returns404_WhenTheRuleWasNotThere()
    {
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.NotFound);

        Assert.IsType<NotFoundResult>(await CreateSut(handler).Delete(Guid.NewGuid()));
    }
}
