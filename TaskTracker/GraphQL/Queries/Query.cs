using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.GraphQL.Queries;

public class Query
{
    [UseFiltering]
    [UseSorting]
    public IQueryable<Project> GetProjects([Service] AppDbContext context)
    {
        return context.Projects.Include(p => p.WorkTasks);
    }

    public Project? GetProjectById(Guid id,[Service]  AppDbContext context)
    {
        return context.Projects.Include(p => p.WorkTasks).FirstOrDefault(p => p.Id == id);
    }

    
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<WorkTask> GetWorkTasks([Service] AppDbContext context)
    {
        return context.WorkTasks
                    .Include(wt => wt.Project)
                    .Include(wt => wt.Assignee);
    }

    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public WorkTask? GetWorkTasksById(Guid id, [Service] AppDbContext context)
    {
        return context.WorkTasks
            .Include(wt => wt.Project)
            .Include(wt => wt.Assignee)
            .FirstOrDefault(p => p.Id == id);
    }

    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<User> GetUsers([Service] AppDbContext db)
    {
        return db.Users.Include(u => u.WorkTasks);
    }

    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public User? GetUser(Guid id, [Service] AppDbContext db)
    {
        return db.Users.Include(u => u.WorkTasks).FirstOrDefault(u => u.Id == id);
    }
}
