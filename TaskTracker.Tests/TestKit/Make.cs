using TaskTracker.Dtos;
using TaskTracker.Models;

namespace TaskTracker.Tests.TestKit;

/// <summary>
/// Object mother for the TaskTracker domain.
///
/// Authorization here depends on graph shape — "is this user in this project's Users
/// collection" — so tests need to wire objects together deliberately. AutoFixture
/// cannot express that; these helpers can, in one line.
/// </summary>
public static class Make
{
    public static User User(
        Guid? id = null,
        string fullName = "Ada Lovelace",
        string email = "ada@example.com",
        string position = "Engineer",
        string role = Roles.User,
        string passwordHash = "hash") => new()
        {
            Id = id ?? Guid.NewGuid(),
            FullName = fullName,
            Email = email,
            Position = position,
            Role = role,
            PasswordHash = passwordHash
        };

    public static Project Project(
        Guid? id = null,
        string name = "Apollo",
        string description = "Moon landing",
        List<User>? users = null,
        List<WorkTask>? workTasks = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Description = description,
            Users = users ?? [],
            WorkTasks = workTasks ?? []
        };

    public static WorkTask WorkTask(
        Guid? id = null,
        string name = "Write docs",
        string description = "Describe the API",
        Priority priority = Priority.Medium,
        Status status = Status.InProgress,
        Guid? projectId = null,
        Project? project = null,
        Guid? assigneeId = null,
        User? assignee = null,
        List<TaskComment>? comments = null,
        List<TaskHistory>? history = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Description = description,
            Priority = priority,
            Status = status,
            ProjectId = projectId ?? project?.Id ?? Guid.NewGuid(),
            Project = project,
            AssigneeId = assigneeId ?? assignee?.Id,
            Assignee = assignee,
            Comments = comments ?? [],
            History = history ?? []
        };

    /// <summary>A project that <paramref name="member"/> belongs to, with the back-reference set.</summary>
    public static Project ProjectWithMember(User member, Guid? id = null, string name = "Apollo")
        => Project(id: id, name: name, users: [member]);

    public static CreateUpdateProjectDto ProjectDto(string name = "Apollo", string description = "Moon landing")
        => new(name, description);

    public static CreateUpdateUserDto UserDto(
        string fullName = "Ada Lovelace",
        string email = "ada@example.com",
        string position = "Engineer")
        => new(fullName, email, position);

    public static CreateUpdateWorkTaskDto WorkTaskDto(
        string name = "Write docs",
        string description = "Describe the API",
        Priority priority = Priority.Medium,
        Status status = Status.InProgress,
        Guid projectId = default,
        Guid? assigneeId = null)
        => new(name, description, priority, status, projectId, assigneeId);

    public static RegisterDto RegisterDto(
        string fullName = "Ada Lovelace",
        string email = "ada@example.com",
        string password = "correct horse battery staple",
        string position = "Engineer")
        => new(fullName, email, password, position);

    public static LoginDto LoginDto(
        string email = "ada@example.com",
        string password = "correct horse battery staple")
        => new(email, password);
}
