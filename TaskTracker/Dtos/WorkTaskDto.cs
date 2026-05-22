using TaskTracker.Models;

namespace TaskTracker.Dtos;



public record WorkTaskDto(string Name, string Description, Priority Priority, Status Status, Guid ProjectId, Guid? AssigneeId);



public record CreateUpdateWorkTaskDto(string Name, string Description, Priority Priority, Status Status, Guid ProjectId, Guid? AssigneeId);



