using MongoDB.Driver;
using LogApiService.Models;
using LogApiService.Settings;
using Microsoft.Extensions.Options;

namespace LogApiService.Services;

public class LogService
{
    private readonly IMongoCollection<LogEntry> _logs;

    public LogService(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        var database = client.GetDatabase(settings.Value.DatabaseName);
        _logs = database.GetCollection<LogEntry>(settings.Value.CollectionName);
    }

    public async Task<PagedResult<LogEntry>> GetLogsAsync(LogQueryParameters query)
    {
        var filterBuilder = Builders<LogEntry>.Filter;
        var filters = new List<FilterDefinition<LogEntry>>();

        if (!string.IsNullOrEmpty(query.Level))
            filters.Add(filterBuilder.Eq(x => x.Level, query.Level));

        if (!string.IsNullOrEmpty(query.ServiceName))
            filters.Add(filterBuilder.Eq(x => x.ServiceName, query.ServiceName));

        if (!string.IsNullOrEmpty(query.Search))
            filters.Add(filterBuilder.Regex(x => x.Message, new MongoDB.Bson.BsonRegularExpression(query.Search, "i")));

        if (query.From.HasValue)
            filters.Add(filterBuilder.Gte(x => x.CreatedAt, query.From.Value));

        if (query.To.HasValue)
            filters.Add(filterBuilder.Lte(x => x.CreatedAt, query.To.Value));

        var filter = filters.Count > 0 ? filterBuilder.And(filters) : filterBuilder.Empty;

        var sortDefinition = query.SortOrder.ToLower() == "asc"
            ? Builders<LogEntry>.Sort.Ascending(x => x.CreatedAt)
            : Builders<LogEntry>.Sort.Descending(x => x.CreatedAt);

        var totalCount = await _logs.CountDocumentsAsync(filter);
        var items = await _logs.Find(filter)
            .Sort(sortDefinition)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync();

        return new PagedResult<LogEntry>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<List<string>> GetDistinctServicesAsync()
    {
        return await _logs.Distinct<string>("ServiceName", Builders<LogEntry>.Filter.Empty).ToListAsync();
    }

    public async Task<List<string>> GetDistinctLevelsAsync()
    {
        return await _logs.Distinct<string>("Level", Builders<LogEntry>.Filter.Empty).ToListAsync();
    }
}
