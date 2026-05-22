using HotChocolate.Subscriptions;
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
        CancellationToken cancellationToken)
    {
        if(newTask is null)
        {
            throw new ArgumentNullException("Invalid task data");
        }
        var task = newTask.ToWorkTask(newTask.ProjectId, newTask.AssigneeId);

        await context.WorkTasks.AddAsync(task, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return task;
    }

    public async Task<Project> CreateProject(
        CreateUpdateProjectDto newProject,
        [Service] AppDbContext context,
        CancellationToken cancellationToken)
    {
        if (newProject is null)
        {
            throw new ArgumentNullException("Invalid project data");
        }
        var project = newProject.ToProject();

        await context.Projects.AddAsync(project, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return project;
    }

    public async Task<User> CreateUser(
        CreateUpdateUserDto newUser,
        [Service] AppDbContext context,
        CancellationToken cancellationToken,
        ITopicEventSender topicEventSender)
    {
        if (newUser is null)
        {
            throw new ArgumentNullException("Invalid user data");
        }
        var user = newUser.ToUser();                    
        await context.Users.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await topicEventSender.SendAsync(nameof(Subscription.CreateUser), user);

        return user;
    }

    public async Task<User> UpdateUser(
        Guid id,
        CreateUpdateUserDto updateUser,
        [Service] AppDbContext context,
        CancellationToken cancellationToken,
        ITopicEventSender topicEventSender)
    {
        if (updateUser is null)
        {
            throw new ArgumentNullException("Invalid user data");
        }

        var user = updateUser.ToUser();
        user.Id = id;

        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);

        await topicEventSender.SendAsync($"{user.Id}_{nameof(Subscription.UpdateUser)}", user);

        return user;
    }
}