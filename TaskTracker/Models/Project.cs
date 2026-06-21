namespace TaskTracker.Models;

public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public List<WorkTask> WorkTasks { get; set; } = [];
    public List<User> Users { get; set; } = [];
}