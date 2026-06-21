using HotChocolate.Authorization;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Dtos;
using TaskTracker.Mappers;
using TaskTracker.Models;

namespace TaskTracker.GraphQL.Queries;

public class Mutation
{
    public async Task<WorkTask> CreateTask(
        CreateUpdateWorkTaskDto newTask,
        [Service] AppDbContext context,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        if (newTask is null)
            throw new ArgumentNullException(nameof(newTask), "Invalid task data");

        var claims = httpContextAccessor.HttpContext?.User;
        var role = claims?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (!Guid.TryParse(claims?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
            throw new UnauthorizedAccessException("Must be logged in to create a task.");

        if (role != Roles.Admin)
        {
            var isMember = await context.Projects
                .Where(p => p.Id == newTask.ProjectId)
                .AnyAsync(p => p.Users.Any(u => u.Id == userId), cancellationToken);
            if (!isMember)
                throw new UnauthorizedAccessException("Not a member of the project.");
        }

        var task = newTask.ToWorkTask(newTask.ProjectId, newTask.AssigneeId);

        await context.WorkTasks.AddAsync(task, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return task;
    }

    [Authorize(Roles = new[] { Roles.Admin })]
    public async Task<Project> CreateProject(
        CreateUpdateProjectDto newProject,
        [Service] AppDbContext context,
        CancellationToken cancellationToken)
    {
        if (newProject is null)
            throw new ArgumentNullException(nameof(newProject), "Invalid project data");

        var project = newProject.ToProject();

        await context.Projects.AddAsync(project, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return project;
    }

    [Authorize(Roles = new[] { Roles.Admin })]
    public async Task<User> CreateUser(
        CreateUpdateUserDto newUser,
        [Service] AppDbContext context,
        CancellationToken cancellationToken,
        ITopicEventSender topicEventSender)
    {
        if (newUser is null)
            throw new ArgumentNullException(nameof(newUser), "Invalid user data");

        var user = newUser.ToUser();
        await context.Users.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await topicEventSender.SendAsync(nameof(Subscription.CreateUser), user);

        return user;
    }

    [Authorize(Roles = new[] { Roles.Admin })]
    public async Task<User> UpdateUser(
        Guid id,
        CreateUpdateUserDto updateUser,
        [Service] AppDbContext context,
        CancellationToken cancellationToken,
        ITopicEventSender topicEventSender)
    {
        if (updateUser is null)
            throw new ArgumentNullException(nameof(updateUser), "Invalid user data");

        var user = updateUser.ToUser();
        user.Id = id;

        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);

        await topicEventSender.SendAsync($"{user.Id}_{nameof(Subscription.UpdateUser)}", user);

        return user;
    }

    public async Task<TaskComment> AddComment(
        Guid taskId,
        string content,
        [Service] AppDbContext context,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var claims = httpContextAccessor.HttpContext?.User;
        var role = claims?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (!Guid.TryParse(claims?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
            throw new UnauthorizedAccessException("Must be logged in to comment.");

        if (role != Roles.Admin)
        {
            var hasAccess = await context.WorkTasks
                .Where(t => t.Id == taskId)
                .AnyAsync(t => t.Project != null && t.Project.Users.Any(u => u.Id == userId), cancellationToken);
            if (!hasAccess)
                throw new UnauthorizedAccessException("Not a member of the project.");
        }

        var comment = new TaskComment
        {
            WorkTaskId = taskId,
            UserId = userId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        await context.TaskComments.AddAsync(comment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return comment;
    }
}
