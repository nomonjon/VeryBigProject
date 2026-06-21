namespace NotificationService.Contracts;

public class TaskStatusChangedEvent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Status OldStatus { get; set; }
    public Status NewStatus { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid? AssigneeId { get; set; }
    public string AssigneeEmail { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public List<string> ProjectUserEmails { get; set; } = new();
}
