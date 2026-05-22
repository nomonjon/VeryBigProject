namespace TaskTracker.MyOptions;

public class DbConection
{
    public const string SectionName = "ConnectionStrings";
    public string Default { get; set; } = string.Empty;
}
