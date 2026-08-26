using System.Net;
using System.Text.Json;
using TaskTracker.Services;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Services;

/// <summary>
/// A typed HttpClient wrapper. Nothing here talks to a real GrpcServer: the handler is
/// stubbed, so the tests are about request shape and response handling — including the
/// error paths, which is where thin HTTP wrappers actually go wrong.
/// </summary>
public class ProductRuleApiServiceTests
{
    // ---------- GetAllAsync ----------

    [Fact]
    public async Task GetAllAsync_ReturnsTheDeserializedRules()
    {
        var handler = StubHttpMessageHandler.ReturningJson(new List<ProductRuleDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Low stock", Expression = "Quantity < 5", Color = "orange", IsActive = true }
        });

        var rules = await new ProductRuleApiService(handler.CreateClient()).GetAllAsync();

        var rule = Assert.Single(rules);
        Assert.Equal("Low stock", rule.Name);
        Assert.Equal("Quantity < 5", rule.Expression);
    }

    [Fact]
    public async Task GetAllAsync_CallsTheRulesEndpoint()
    {
        var handler = StubHttpMessageHandler.ReturningJson(new List<ProductRuleDto>());

        await new ProductRuleApiService(handler.CreateClient()).GetAllAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("api/ProductRule", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAnEmptyList_WhenTheBodyIsJsonNull()
    {
        var handler = StubHttpMessageHandler.ReturningRawJson("null");

        Assert.Empty(await new ProductRuleApiService(handler.CreateClient()).GetAllAsync());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAnEmptyList_WhenThereAreNoRules()
    {
        var handler = StubHttpMessageHandler.ReturningJson(new List<ProductRuleDto>());

        Assert.Empty(await new ProductRuleApiService(handler.CreateClient()).GetAllAsync());
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task CreateAsync_ReturnsTheCreatedRule()
    {
        var created = new ProductRuleDto { Id = Guid.NewGuid(), Name = "Low stock", Expression = "Quantity < 5" };
        var handler = StubHttpMessageHandler.ReturningJson(created);

        var result = await new ProductRuleApiService(handler.CreateClient())
            .CreateAsync(new CreateUpdateProductRuleDto { Name = "Low stock", Expression = "Quantity < 5" });

        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Low stock", result.Name);
    }

    [Fact]
    public async Task CreateAsync_PostsTheRuleAsJson()
    {
        var handler = StubHttpMessageHandler.ReturningJson(new ProductRuleDto());

        await new ProductRuleApiService(handler.CreateClient())
            .CreateAsync(new CreateUpdateProductRuleDto { Name = "Low stock", Expression = "Quantity < 5", Color = "red" });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("api/ProductRule", request.RequestUri!.ToString());

        // Read the raw JSON rather than deserializing into the DTO. PostAsJsonAsync
        // uses web defaults (camelCase), so a DTO round-trip would hide the actual
        // property names on the wire — which is exactly what the other service parses.
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        Assert.Equal("Low stock", body.RootElement.GetProperty("name").GetString());
        Assert.Equal("Quantity < 5", body.RootElement.GetProperty("expression").GetString());
        Assert.Equal("red", body.RootElement.GetProperty("color").GetString());
    }

    [Fact]
    public async Task CreateAsync_ThrowsWithTheUpstreamStatusAndMessage_WhenTheRuleIsRejected()
    {
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.BadRequest, "Invalid rule expression: bad token");

        var exception = await Assert.ThrowsAsync<ProductRuleApiException>(
            () => new ProductRuleApiService(handler.CreateClient()).CreateAsync(new CreateUpdateProductRuleDto()));

        // GrpcServer validates the expression. Swallowing its message here would leave
        // the TaskTracker client with a 400 and no idea what was wrong.
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("Invalid rule expression: bad token", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_ThrowsForAnyNonSuccessStatus_NotJustBadRequest()
    {
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.InternalServerError, "boom");

        var exception = await Assert.ThrowsAsync<ProductRuleApiException>(
            () => new ProductRuleApiService(handler.CreateClient()).CreateAsync(new CreateUpdateProductRuleDto()));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    // ---------- DeleteAsync ----------

    [Fact]
    public async Task DeleteAsync_ReturnsTrue_OnNoContent()
    {
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.NoContent);

        Assert.True(await new ProductRuleApiService(handler.CreateClient()).DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_OnNotFound()
    {
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.NotFound);

        // 404 is a normal answer here ("already gone"), not an exception.
        Assert.False(await new ProductRuleApiService(handler.CreateClient()).DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsOnOtherFailures()
    {
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => new ProductRuleApiService(handler.CreateClient()).DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_TargetsTheRuleById()
    {
        var id = Guid.NewGuid();
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.NoContent);

        await new ProductRuleApiService(handler.CreateClient()).DeleteAsync(id);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.EndsWith($"api/ProductRule/{id}", request.RequestUri!.ToString());
    }
}
