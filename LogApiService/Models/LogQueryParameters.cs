namespace LogApiService.Models;

public class LogQueryParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Level { get; set; }
    public string? ServiceName { get; set; }
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string SortBy { get; set; } = "createdAt";
    public string SortOrder { get; set; } = "desc";
}
