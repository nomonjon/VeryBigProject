using LogPlatform.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace LogPlatform.Services;

public class LogService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<LogService> _logger;

    public LogService(IMongoDatabase database, ILogger<LogService> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<PaginatedResult<LogEntry>> GetLogsAsync(
        int page,
        int pageSize,
        string? level,
        string? serviceName,
        string? search,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            // Query a specific service collection
            var safe = NormaliseCollectionName(serviceName);
            var collection = _database.GetCollection<LogEntry>($"logs_{safe.ToLower()}");
            return await QuerySingleCollectionAsync(collection, page, pageSize, level, search, startDate, endDate);
        }

        // No service filter → aggregate across ALL logs_* collections
        return await QueryAllCollectionsAsync(page, pageSize, level, search, startDate, endDate);
    }

    /// <summary>
    /// Query a single collection with filters, sorting and pagination.
    /// </summary>
    private async Task<PaginatedResult<LogEntry>> QuerySingleCollectionAsync(
        IMongoCollection<LogEntry> collection,
        int page, int pageSize,
        string? level, string? search,
        DateTime? startDate, DateTime? endDate)
    {
        var filter = BuildFilter(level, search, startDate, endDate);

        var totalCount = await collection.CountDocumentsAsync(filter);

        var items = await collection
            .Find(filter)
            .SortByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        _logger.LogDebug("Query returned {Count}/{Total} log entries", items.Count, totalCount);

        return new PaginatedResult<LogEntry>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Aggregate across all logs_* collections using $unionWith, then filter, sort and paginate.
    /// </summary>
    private async Task<PaginatedResult<LogEntry>> QueryAllCollectionsAsync(
        int page, int pageSize,
        string? level, string? search,
        DateTime? startDate, DateTime? endDate)
    {
        var collectionNames = await GetLogCollectionNamesAsync();

        if (collectionNames.Count == 0)
        {
            return new PaginatedResult<LogEntry>
            {
                Items = new List<LogEntry>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            };
        }

        // Build a BSON match filter
        var matchFilter = BuildBsonMatchFilter(level, search, startDate, endDate);

        // Use the first collection as the base, $unionWith the rest
        var baseCollection = _database.GetCollection<BsonDocument>(collectionNames[0]);

        var pipeline = new List<BsonDocument>();

        for (int i = 1; i < collectionNames.Count; i++)
        {
            pipeline.Add(new BsonDocument("$unionWith", collectionNames[i]));
        }

        if (matchFilter.ElementCount > 0)
        {
            pipeline.Add(new BsonDocument("$match", matchFilter));
        }

        // Use $facet to get both count and paginated data in one round-trip
        pipeline.Add(new BsonDocument("$facet", new BsonDocument
        {
            { "metadata", new BsonArray { new BsonDocument("$count", "total") } },
            { "data", new BsonArray
                {
                    new BsonDocument("$sort", new BsonDocument("CreatedAt", -1)),
                    new BsonDocument("$skip", (page - 1) * pageSize),
                    new BsonDocument("$limit", pageSize)
                }
            }
        }));

        var result = await baseCollection
            .Aggregate<BsonDocument>(pipeline)
            .FirstOrDefaultAsync();

        long totalCount = 0;
        var items = new List<LogEntry>();

        if (result != null)
        {
            var metadata = result["metadata"].AsBsonArray;
            if (metadata.Count > 0)
                totalCount = metadata[0].AsBsonDocument["total"].ToInt64();

            var data = result["data"].AsBsonArray;
            foreach (var doc in data)
            {
                items.Add(BsonSerializer.Deserialize<LogEntry>(doc.AsBsonDocument));
            }
        }

        _logger.LogDebug("Aggregated query returned {Count}/{Total} log entries across {Collections} collections",
            items.Count, totalCount, collectionNames.Count);

        return new PaginatedResult<LogEntry>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static FilterDefinition<LogEntry> BuildFilter(
        string? level, string? search,
        DateTime? startDate, DateTime? endDate)
    {
        var builder = Builders<LogEntry>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(level))
            filter &= builder.Regex(e => e.Level, new BsonRegularExpression(level, "i"));

        if (!string.IsNullOrWhiteSpace(search))
        {
            filter &= builder.Or(
                builder.Regex(e => e.Message, new BsonRegularExpression(search, "i")),
                builder.Regex(e => e.SourceContext, new BsonRegularExpression(search, "i"))
            );
        }

        if (startDate.HasValue)
            filter &= builder.Gte(e => e.CreatedAt, startDate.Value);

        if (endDate.HasValue)
            filter &= builder.Lte(e => e.CreatedAt, endDate.Value);

        return filter;
    }

    private static BsonDocument BuildBsonMatchFilter(
        string? level, string? search,
        DateTime? startDate, DateTime? endDate)
    {
        var conditions = new List<BsonDocument>();

        if (!string.IsNullOrWhiteSpace(level))
            conditions.Add(new BsonDocument("Level",
                new BsonDocument("$regex", new BsonRegularExpression(level, "i"))));

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add(new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("Message",
                    new BsonDocument("$regex", new BsonRegularExpression(search, "i"))),
                new BsonDocument("SourceContext",
                    new BsonDocument("$regex", new BsonRegularExpression(search, "i")))
            }));
        }

        if (startDate.HasValue)
            conditions.Add(new BsonDocument("CreatedAt",
                new BsonDocument("$gte", startDate.Value)));

        if (endDate.HasValue)
            conditions.Add(new BsonDocument("CreatedAt",
                new BsonDocument("$lte", endDate.Value)));

        return conditions.Count > 0
            ? new BsonDocument("$and", new BsonArray(conditions))
            : new BsonDocument();
    }

    private async Task<List<string>> GetLogCollectionNamesAsync()
    {
        var names = new List<string>();
        using var cursor = await _database.ListCollectionNamesAsync();
        await cursor.ForEachAsync(name =>
        {
            if (name.StartsWith("logs_", StringComparison.OrdinalIgnoreCase))
                names.Add(name);
        });
        return names;
    }

    /// <summary>
    /// Returns all distinct service names by listing collection names that start with "logs_".
    /// </summary>
    public async Task<IEnumerable<string>> GetServiceNamesAsync()
    {
        var names = new List<string>();
        using var cursor = await _database.ListCollectionNamesAsync();
        await cursor.ForEachAsync(name =>
        {
            if (name.StartsWith("logs_", StringComparison.OrdinalIgnoreCase))
                names.Add(name["logs_".Length..]);
        });
        return names;
    }

    private static string NormaliseCollectionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unknown";
        var safe = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "unknown" : safe;
    }
}
