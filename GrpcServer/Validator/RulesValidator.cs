using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;
using System.Linq.Expressions;
using System.Xml.Linq;
using GrpcServer.Models;

namespace GrpcServer.Validator;

public static class RulesValidator
{
    // Stored expression string is only transport; this turns it into the
    // typed expression function everything else works with.
    public static Expression<Func<Product, bool>> ToPredicate(string expression)
    {
        return DynamicExpressionParser.ParseLambda<Product, bool>(
            ParsingConfig.Default, createParameterCtor: false, expression);
    }

    public static void ValidateColor(string color)
    {
        if (!ProductColors.IsValid(color))
            throw new ArgumentException(
                $"Invalid color '{color}'. Allowed: {string.Join(", ", ProductColors.All)}.");
    }

    public static void ValidateExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Expression must not be empty.");

        try
        {
            ToPredicate(expression);
        }
        catch (ParseException ex)
        {
            throw new ArgumentException($"Invalid rule expression: {ex.Message}", ex);
        }
    }
}