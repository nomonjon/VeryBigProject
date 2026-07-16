namespace GrpcServer.Models;

// The colors a rule can paint a product with, ordered by severity.
// Product.StatusColor holds the color of the worst matching active rule.
public static class ProductColors
{
    public const string Green = "green";    // normal — no rule matched
    public const string Orange = "orange";  // warning (e.g. low stock)
    public const string Red = "red";        // critical (e.g. out of stock)

    // What a product shows when no active rule matches it.
    public const string Default = Green;

    private static readonly Dictionary<string, int> Severity = new(StringComparer.OrdinalIgnoreCase)
    {
        [Green] = 0,
        [Orange] = 1,
        [Red] = 2,
    };

    public static IReadOnlyCollection<string> All => Severity.Keys;

    public static bool IsValid(string color) =>
        !string.IsNullOrWhiteSpace(color) && Severity.ContainsKey(color);

    // Higher = more severe. Unknown colors rank just above normal so a custom
    // color still surfaces but never outranks an explicit red.
    public static int Rank(string color) =>
        Severity.TryGetValue(color ?? Default, out var rank) ? rank : 1;
}
