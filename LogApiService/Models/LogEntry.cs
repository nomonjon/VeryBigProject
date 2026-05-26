using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LogApiService.Models;

public class LogEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Timestamp { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SourceContext { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
