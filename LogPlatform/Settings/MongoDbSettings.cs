namespace LogPlatform.Settings;

public class MongoDbSettings
{
    public const string SectionName = "MongoDB";

    public string ConnectionString { get; set; } = "mongodb://admin:admin@localhost:27017";
    public string DatabaseName { get; set; } = "LogsDb";
    public string CollectionName { get; set; } = "Logs";
}
