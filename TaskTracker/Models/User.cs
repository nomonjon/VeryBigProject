
namespace TaskTracker.Models;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public List<WorkTask> WorkTasks { get; set; } = [];
    public List<Project> Projects { get; set; } = [];
}
