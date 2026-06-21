namespace TaskTracker.Models;

public class TaskHistory : BaseEntity
{
    public Guid WorkTaskId { get; set; }
    public WorkTask? WorkTask { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string FieldName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
