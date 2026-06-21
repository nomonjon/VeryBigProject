namespace TaskTracker.Models;

public class TaskComment : BaseEntity
{
    public Guid WorkTaskId { get; set; }
    public WorkTask? WorkTask { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
