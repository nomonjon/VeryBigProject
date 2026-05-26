using LogApiService.Models;
using MongoDB.Driver;
using MongoDB.Bson;

namespace LogApiService.Services;

public class LogService
{
    private readonly IMongoCollection<LogEntry> _logs;

    public LogService(IConfiguration configuration)
    {
        var client = new MongoClient(configuration.GetValue<string>("MongoDB:ConnectionString"));
        var database = client.GetDatabase(configuration.GetValue<string>("MongoDB:DatabaseName"));
        _logs = database.GetCollection<LogEntry>("Logs");
    }

    public async Task<PaginatedResult<LogEntry>> GetLogsAsync(
        int page, int pageSize, string? level, string? serviceName, string? search, DateTime? startDate, DateTime? endDate)
    {
        var filterBuilder = Builders<LogEntry>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrEmpty(level))
            filter &= filterBuilder.Eq(x => x.Level, level);

        if (!string.IsNullOrEmpty(serviceName))
            filter &= filterBuilder.Eq(x => x.ServiceName, serviceName);

        if (!string.IsNullOrEmpty(search))
            filter &= filterBuilder.Regex(x => x.Message, new BsonRegularExpression(search, "i"));

        if (startDate.HasValue)
            filter &= filterBuilder.Gte(x => x.CreatedAt, startDate.Value);

        if (endDate.HasValue)
            filter &= filterBuilder.Lte(x => x.CreatedAt, endDate.Value);

        var totalCount = await _logs.CountDocumentsAsync(filter);
        var items = await _logs.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PaginatedResult<LogEntry>
        {
            Items = items,
            TotalCount = (int)totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
