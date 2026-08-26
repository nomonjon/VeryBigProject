using GrpcServer.Controllers;
using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Mapper;
using GrpcServer.Tests.TestKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GrpcServer.Tests.Controllers;

public class ProductRuleControllerTests
{
    private readonly Mock<IProductRuleService> _service = new();
    private readonly ProductRuleController _sut;

    public ProductRuleControllerTests()
        => _sut = new ProductRuleController(_service.Object, NullLogger<ProductRuleController>.Instance);

    // ---------- GetRules / GetRule ----------

    [Fact]
    public async Task GetRules_Returns200_WithTheList()
    {
        var rules = new List<ProductRuleDto> { Make.Rule().ToProductRuleDto() };
        _service.Setup(s => s.GetAllRulesAsync()).ReturnsAsync(rules);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetRules());

        Assert.Same(rules, ok.Value);
    }

    [Fact]
    public async Task GetRule_Returns200_WhenTheRuleExists()
    {
        var rule = Make.Rule().ToProductRuleDto();
        _service.Setup(s => s.GetRuleByIdAsync(rule.Id)).ReturnsAsync(rule);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetRule(rule.Id));

        Assert.Same(rule, ok.Value);
    }

    [Fact]
    public async Task GetRule_Returns404_WhenTheRuleIsMissing()
    {
        _service.Setup(s => s.GetRuleByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ProductRuleDto?)null);

        Assert.IsType<NotFoundResult>(await _sut.GetRule(Guid.NewGuid()));
    }

    // ---------- CreateRule ----------

    [Fact]
    public async Task CreateRule_Returns200_WithTheCreatedRule()
    {
        var created = Make.Rule().ToProductRuleDto();
        _service.Setup(s => s.CreateRuleAsync(It.IsAny<CreateUpdateProductRuleDto>())).ReturnsAsync(created);

        var ok = Assert.IsType<OkObjectResult>(await _sut.CreateRule(Make.RuleDto()));

        Assert.Same(created, ok.Value);
    }

    [Fact]
    public async Task CreateRule_Returns400_WithTheValidationMessage_WhenTheRuleIsRejected()
    {
        _service.Setup(s => s.CreateRuleAsync(It.IsAny<CreateUpdateProductRuleDto>()))
                .ThrowsAsync(new ArgumentException("Invalid rule expression: bad token"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(await _sut.CreateRule(Make.RuleDto()));

        // The message is the only feedback the API gives a client that wrote a bad
        // expression, so losing it (e.g. returning a bare BadRequest()) is a real bug.
        Assert.Equal("Invalid rule expression: bad token", badRequest.Value);
    }

    // ---------- UpdateRule ----------

    [Fact]
    public async Task UpdateRule_Returns200_WhenTheRuleExists()
    {
        var id = Guid.NewGuid();
        var updated = Make.Rule(id: id).ToProductRuleDto();
        _service.Setup(s => s.UpdateRuleAsync(id, It.IsAny<CreateUpdateProductRuleDto>())).ReturnsAsync(updated);

        var ok = Assert.IsType<OkObjectResult>(await _sut.UpdateRule(id, Make.RuleDto()));

        Assert.Same(updated, ok.Value);
    }

    [Fact]
    public async Task UpdateRule_Returns404_WhenTheRuleIsMissing()
    {
        _service.Setup(s => s.UpdateRuleAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateProductRuleDto>()))
                .ReturnsAsync((ProductRuleDto?)null);

        Assert.IsType<NotFoundResult>(await _sut.UpdateRule(Guid.NewGuid(), Make.RuleDto()));
    }

    [Fact]
    public async Task UpdateRule_Returns400_WhenTheRuleIsRejected()
    {
        _service.Setup(s => s.UpdateRuleAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateProductRuleDto>()))
                .ThrowsAsync(new ArgumentException("Invalid color 'purple'."));

        var badRequest = Assert.IsType<BadRequestObjectResult>(await _sut.UpdateRule(Guid.NewGuid(), Make.RuleDto()));

        Assert.Equal("Invalid color 'purple'.", badRequest.Value);
    }

    // ---------- DeleteRule ----------

    [Fact]
    public async Task DeleteRule_Returns204_WhenTheRuleWasDeleted()
    {
        _service.Setup(s => s.DeleteRuleAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        Assert.IsType<NoContentResult>(await _sut.DeleteRule(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteRule_Returns404_WhenThereWasNothingToDelete()
    {
        _service.Setup(s => s.DeleteRuleAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        Assert.IsType<NotFoundResult>(await _sut.DeleteRule(Guid.NewGuid()));
    }

    // ---------- GetMatchingProducts / EvaluateProduct ----------

    [Fact]
    public async Task GetMatchingProducts_Returns200_WithTheMatches()
    {
        var products = new List<ProductDto> { Make.Product().ToProductDto() };
        _service.Setup(s => s.GetMatchingProductsAsync(It.IsAny<Guid>())).ReturnsAsync(products);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetMatchingProducts(Guid.NewGuid()));

        Assert.Same(products, ok.Value);
    }

    [Fact]
    public async Task GetMatchingProducts_Returns404_WhenTheRuleIsMissing()
    {
        _service.Setup(s => s.GetMatchingProductsAsync(It.IsAny<Guid>())).ReturnsAsync((List<ProductDto>?)null);

        Assert.IsType<NotFoundResult>(await _sut.GetMatchingProducts(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetMatchingProducts_Returns200_WithAnEmptyList_WhenNothingMatched()
    {
        _service.Setup(s => s.GetMatchingProductsAsync(It.IsAny<Guid>())).ReturnsAsync([]);

        // Empty is not the same as missing: a rule that matches nothing still exists.
        var ok = Assert.IsType<OkObjectResult>(await _sut.GetMatchingProducts(Guid.NewGuid()));

        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ProductDto>>(ok.Value));
    }

    [Fact]
    public async Task EvaluateProduct_Returns200_WithTheRuleMatches()
    {
        var matches = new List<ProductRuleMatchDto> { new() { RuleName = "Low stock", IsMatch = true } };
        _service.Setup(s => s.EvaluateProductAsync(It.IsAny<Guid>())).ReturnsAsync(matches);

        var ok = Assert.IsType<OkObjectResult>(await _sut.EvaluateProduct(Guid.NewGuid()));

        Assert.Same(matches, ok.Value);
    }

    [Fact]
    public async Task EvaluateProduct_Returns404_WhenTheProductIsMissing()
    {
        _service.Setup(s => s.EvaluateProductAsync(It.IsAny<Guid>())).ReturnsAsync((List<ProductRuleMatchDto>?)null);

        Assert.IsType<NotFoundResult>(await _sut.EvaluateProduct(Guid.NewGuid()));
    }
}
