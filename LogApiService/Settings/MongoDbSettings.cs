namespace LogApiService.Settings;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "LogsDb";
    public string CollectionName { get; set; } = "Logs";
}
