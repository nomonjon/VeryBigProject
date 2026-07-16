using AutoFixture;
using AutoFixture.AutoMoq;
using GrpcServer.ApiServices;
using GrpcServer.Dtos;
using GrpcServer.Interfaces;
using GrpcServer.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace GrpcServer.Tests
{
    public class ProductRuleServiceTests
    {
        private readonly IFixture _fixture;
        private readonly Mock<IProductRuleRepository> _ruleRepositoryMock;
        private readonly Mock<IProductRepository> _productRepositoryMock;
        private readonly ProductRuleService _sut;

        public ProductRuleServiceTests()
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _ruleRepositoryMock = _fixture.Freeze<Mock<IProductRuleRepository>>();
            _productRepositoryMock = _fixture.Freeze<Mock<IProductRepository>>();
            _sut = new ProductRuleService(_ruleRepositoryMock.Object, _productRepositoryMock.Object);
        }

        [Fact]
        public async Task CreateRuleAsync_ShouldCreateRule_WhenExpressionIsValid()
        {
            // Arrange
            var dto = new CreateUpdateProductRuleDto
            {
                Name = "Expensive products",
                Expression = "Price > 100 && Quantity < 5", // = string query => contex.product.query;
                IsActive = true
            };

            _ruleRepositoryMock
                .Setup(r => r.CreateAsync(It.IsAny<ProductRule>()))
                .ReturnsAsync((ProductRule rule) => rule);

            // Act
            var result = await _sut.CreateRuleAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(dto.Name, result.Name);
            Assert.Equal(dto.Expression, result.Expression);
            Assert.True(result.IsActive);
            _ruleRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<ProductRule>()), Times.Once);
        }

        [Theory]
        [InlineData("Price >>> 100")]
        [InlineData("NonExistingProperty == 5")]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateRuleAsync_ShouldThrowArgumentException_WhenExpressionIsInvalid(string expression)
        {
            // Arrange
            var dto = new CreateUpdateProductRuleDto
            {
                Name = "Broken rule",
                Expression = expression
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateRuleAsync(dto));
            _ruleRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<ProductRule>()), Times.Never);
        }

        [Fact]
        public async Task UpdateRuleAsync_ShouldThrowArgumentException_WhenExpressionIsInvalid()
        {
            // Arrange
            var dto = new CreateUpdateProductRuleDto
            {
                Name = "Broken rule",
                Expression = "Price >"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateRuleAsync(Guid.NewGuid(), dto));
            _ruleRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ProductRule>()), Times.Never);
        }

        [Fact]
        public async Task GetMatchingProductsAsync_ShouldReturnNull_WhenRuleDoesNotExist()
        {
            // Arrange
            var ruleId = Guid.NewGuid();
            _ruleRepositoryMock
                .Setup(r => r.GetByIdAsync(ruleId))
                .ReturnsAsync((ProductRule?)null);

            // Act
            var result = await _sut.GetMatchingProductsAsync(ruleId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetMatchingProductsAsync_ShouldReturnProducts_WhenRuleExists()
        {
            // Arrange
            var rule = _fixture.Build<ProductRule>()
                               .With(r => r.Expression, "Price > 100")
                               .Create();

            var products = _fixture.CreateMany<Product>(3).ToList();

            _ruleRepositoryMock
                .Setup(r => r.GetByIdAsync(rule.Id))
                .ReturnsAsync(rule);

            _productRepositoryMock
                .Setup(r => r.GetWhereAsync(It.IsAny<Expression<Func<Product, bool>>>()))
                .ReturnsAsync(products);

            // Act
            var result = await _sut.GetMatchingProductsAsync(rule.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result!.Count);
            _productRepositoryMock.Verify(r => r.GetWhereAsync(It.IsAny<Expression<Func<Product, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task EvaluateProductAsync_ShouldReturnNull_WhenProductDoesNotExist()
        {
            // Arrange
            var productId = Guid.NewGuid();
            _productRepositoryMock
                .Setup(r => r.GetByIdAsync(productId))
                .ReturnsAsync((Product?)null);

            // Act
            var result = await _sut.EvaluateProductAsync(productId);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("Price > 100", true)]
        [InlineData("Price < 100", false)]
        [InlineData("Quantity == 10 && Price >= 150", true)]
        [InlineData("Name.Contains(\"Lap\")", true)]
        [InlineData("Name.StartsWith(\"Phone\")", false)]
        public async Task EvaluateProductAsync_ShouldEvaluateExpressionAgainstProduct(string expression, bool expectedMatch)
        {
            // Arrange
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Laptop",
                Quantity = 10,
                Price = 150m
            };

            var rule = _fixture.Build<ProductRule>()
                               .With(r => r.Expression, expression)
                               .With(r => r.IsActive, true)
                               .Create();

            _productRepositoryMock
                .Setup(r => r.GetByIdAsync(product.Id))
                .ReturnsAsync(product);

            _ruleRepositoryMock
                .Setup(r => r.GetActiveAsync())
                .ReturnsAsync(new List<ProductRule> { rule });

            // Act
            var result = await _sut.EvaluateProductAsync(product.Id);

            // Assert
            Assert.NotNull(result);
            var match = Assert.Single(result!);
            Assert.Equal(rule.Id, match.RuleId);
            Assert.Equal(expectedMatch, match.IsMatch);
        }

        [Fact]
        public async Task ApplyActiveRulesAsync_ShouldPaintMatchingProduct_WithRuleColor()
        {
            // Arrange
            var rule = _fixture.Build<ProductRule>()
                               .With(r => r.Expression, "Quantity < 5")
                               .With(r => r.Color, ProductColors.Red)
                               .With(r => r.IsActive, true)
                               .Create();

            var lowStock = new Product { Id = Guid.NewGuid(), Name = "Apple", Quantity = 2, Price = 20m, StatusColor = ProductColors.Green };
            var healthy = new Product { Id = Guid.NewGuid(), Name = "Keyboard", Quantity = 20, Price = 50m, StatusColor = ProductColors.Green };

            _ruleRepositoryMock
                .Setup(r => r.GetActiveAsync())
                .ReturnsAsync(new List<ProductRule> { rule });

            _productRepositoryMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Product> { lowStock, healthy });

            _productRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .ReturnsAsync((Product p) => p);

            // Act
            var changed = await _sut.ApplyActiveRulesAsync();

            // Assert
            Assert.Equal(1, changed);
            Assert.Equal(ProductColors.Red, lowStock.StatusColor);
            Assert.Equal(ProductColors.Green, healthy.StatusColor);
            _productRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Product>(p => p.Id == lowStock.Id)), Times.Once);
            _productRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Product>(p => p.Id == healthy.Id)), Times.Never);
        }

        [Fact]
        public async Task ApplyActiveRulesAsync_ShouldUseMostSevereColor_WhenMultipleRulesMatch()
        {
            // Arrange
            var warnRule = _fixture.Build<ProductRule>()
                               .With(r => r.Expression, "Quantity < 10")
                               .With(r => r.Color, ProductColors.Orange)
                               .With(r => r.IsActive, true)
                               .Create();
            var criticalRule = _fixture.Build<ProductRule>()
                               .With(r => r.Expression, "Quantity == 0")
                               .With(r => r.Color, ProductColors.Red)
                               .With(r => r.IsActive, true)
                               .Create();

            var product = new Product { Id = Guid.NewGuid(), Name = "Apple", Quantity = 0, Price = 20m, StatusColor = ProductColors.Green };

            _ruleRepositoryMock
                .Setup(r => r.GetActiveAsync())
                .ReturnsAsync(new List<ProductRule> { warnRule, criticalRule });

            _productRepositoryMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Product> { product });

            _productRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .ReturnsAsync((Product p) => p);

            // Act
            var changed = await _sut.ApplyActiveRulesAsync();

            // Assert
            Assert.Equal(1, changed);
            Assert.Equal(ProductColors.Red, product.StatusColor);
        }

        [Fact]
        public async Task ApplyActiveRulesAsync_ShouldResetToDefaultColor_WhenNoRuleMatches()
        {
            // Arrange — product was previously painted red, no active rules remain.
            var product = new Product { Id = Guid.NewGuid(), Name = "Mouse", Quantity = 5, Price = 20m, StatusColor = ProductColors.Red };

            _ruleRepositoryMock
                .Setup(r => r.GetActiveAsync())
                .ReturnsAsync(new List<ProductRule>());

            _productRepositoryMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Product> { product });

            _productRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .ReturnsAsync((Product p) => p);

            // Act
            var changed = await _sut.ApplyActiveRulesAsync();

            // Assert
            Assert.Equal(1, changed);
            Assert.Equal(ProductColors.Green, product.StatusColor);
        }
    }
}
