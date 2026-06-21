using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.GraphQL.Queries;

public class Query
{
    [UseFiltering]
    [UseSorting]
    public IQueryable<Project> GetProjects([Service] AppDbContext context, [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userIdStr = httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Enumerable.Empty<Project>().AsQueryable();

        return context.Projects.Include(p => p.WorkTasks).Where(p => p.Users.Any(u => u.Id == userId));
    }

    public Project? GetProjectById(Guid id,[Service]  AppDbContext context, [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userIdStr = httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return null;

        return context.Projects.Include(p => p.WorkTasks).FirstOrDefault(p => p.Id == id && p.Users.Any(u => u.Id == userId));
    }

    
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<WorkTask> GetWorkTasks([Service] AppDbContext context, [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userIdStr = httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Enumerable.Empty<WorkTask>().AsQueryable();

        return context.WorkTasks
                    .Include(wt => wt.Project)
                    .Include(wt => wt.Assignee)
                    .Where(wt => wt.Project != null && wt.Project.Users.Any(u => u.Id == userId));
    }

    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public WorkTask? GetWorkTasksById(Guid id, [Service] AppDbContext context, [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userIdStr = httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return null;

        return context.WorkTasks
            .Include(wt => wt.Project)
            .Include(wt => wt.Assignee)
            .FirstOrDefault(p => p.Id == id && p.Project != null && p.Project.Users.Any(u => u.Id == userId));
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
