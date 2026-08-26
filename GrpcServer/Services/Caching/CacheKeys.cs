namespace GrpcServer.Services.Caching;

// Redis has no cheap key scan, so every cached list keeps a fixed key that
// writers invalidate explicitly. Keep this the single source of key names.
public static class CacheKeys
{
    public const string ProductList = "products:all";
    public static string Product(Guid id) => $"product:{id}";

    public const string RuleList = "rules:all";
    public const string ActiveRuleList = "rules:active";
    public static string Rule(Guid id) => $"rule:{id}";
}
