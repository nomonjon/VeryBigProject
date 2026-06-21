namespace TaskTracker.Models;

public class WorkTask : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Priority Priority { get; set; }
    public Status Status { get; set; } 

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public List<TaskComment> Comments { get; set; } = [];
    public List<TaskHistory> History { get; set; } = [];
}
public enum Status
{
    InProgress,
    Review,
    Canceled,
    Done
}