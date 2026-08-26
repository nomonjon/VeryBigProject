using System.Linq.Dynamic.Core.Exceptions;
using GrpcServer.Models;
using GrpcServer.Tests.TestKit;
using GrpcServer.Validator;

namespace GrpcServer.Tests.Validator;

public class RulesValidatorTests
{
    // ---------- ToPredicate ----------

    [Theory]
    [InlineData("Price > 100", true)]
    [InlineData("Price < 100", false)]
    [InlineData("Quantity == 10 && Price >= 150", true)]
    [InlineData("Quantity < 5 || Price > 100", true)]
    [InlineData("Name.Contains(\"Lap\")", true)]
    [InlineData("Name.StartsWith(\"Phone\")", false)]
    [InlineData("StatusColor == \"green\"", true)]
    public void ToPredicate_CompilesExpression_ThatEvaluatesAgainstAProduct(string expression, bool expectedMatch)
    {
        var product = Make.Product(name: "Laptop", quantity: 10, price: 150m, statusColor: ProductColors.Green);

        var predicate = RulesValidator.ToPredicate(expression).Compile();

        Assert.Equal(expectedMatch, predicate(product));
    }

    [Fact]
    public void ToPredicate_ThrowsParseException_ForUnknownProperty()
        => Assert.Throws<ParseException>(() => RulesValidator.ToPredicate("NoSuchProperty == 5"));

    [Fact]
    public void ToPredicate_ThrowsParseException_ForBrokenSyntax()
        => Assert.Throws<ParseException>(() => RulesValidator.ToPredicate("Price >"));

    // ---------- ValidateColor ----------

    [Theory]
    [InlineData(ProductColors.Green)]
    [InlineData(ProductColors.Orange)]
    [InlineData(ProductColors.Red)]
    public void ValidateColor_Accepts_KnownColors(string color)
    {
        var exception = Record.Exception(() => RulesValidator.ValidateColor(color));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("purple")]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateColor_Throws_ForUnknownColor(string color)
        => Assert.Throws<ArgumentException>(() => RulesValidator.ValidateColor(color));

    [Fact]
    public void ValidateColor_Message_ListsTheAllowedColors()
    {
        var exception = Assert.Throws<ArgumentException>(() => RulesValidator.ValidateColor("purple"));

        Assert.Contains("purple", exception.Message);
        foreach (var allowed in ProductColors.All)
            Assert.Contains(allowed, exception.Message);
    }

    // ---------- ValidateExpression ----------

    [Theory]
    [InlineData("Price > 100")]
    [InlineData("Quantity < 5 && Name != null")]
    public void ValidateExpression_Accepts_ValidExpressions(string expression)
    {
        var exception = Record.Exception(() => RulesValidator.ValidateExpression(expression));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateExpression_Throws_WhenExpressionIsBlank(string? expression)
    {
        var exception = Assert.Throws<ArgumentException>(() => RulesValidator.ValidateExpression(expression!));

        Assert.Equal("Expression must not be empty.", exception.Message);
    }

    [Theory]
    [InlineData("Price >>> 100")]
    [InlineData("NonExistingProperty == 5")]
    [InlineData("Price >")]
    public void ValidateExpression_WrapsParseErrors_InArgumentException(string expression)
    {
        var exception = Assert.Throws<ArgumentException>(() => RulesValidator.ValidateExpression(expression));

        // The caller (the REST controller) catches ArgumentException to return 400.
        // The original parser error must survive as InnerException for the logs.
        Assert.StartsWith("Invalid rule expression:", exception.Message);
        Assert.IsType<ParseException>(exception.InnerException);
    }
}
