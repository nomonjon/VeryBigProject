using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Interfaces;
using TaskTracker.Models;

namespace TaskTracker.Repository;

public class ProjectRepository(AppDbContext context) : BaseRepository<Project>(context), IProjectRepository
{

    //public async Task<Project> CreateProject(Project newProject, CancellationToken cancellationToken)
    //{
    //    await context.Projects.AddAsync(newProject, cancellationToken);
    //    await context.SaveChangesAsync(cancellationToken);

    //    return newProject;
    //}

    //public async Task<bool> DeleteProject(Guid id, CancellationToken cancellationToken)
    //{
    //    var project = await context.Projects.FindAsync(id, cancellationToken);

    //    if (project is null)
    //        return false;

    //    context.Projects.Remove(project);
    //    await context.SaveChangesAsync(cancellationToken);

    //    return true;
    //}

    //public async Task<Project?> GetProjectById(Guid id, CancellationToken cancellationToken)
    //{
    //    return await context.Projects
    //        .AsNoTracking()
    //        .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    //}

    public async Task<Project?> GetByIdWithTasks(Guid id, CancellationToken cancellationToken)
    {
        return await context.Projects
            .AsNoTracking()
            .Include(p => p.WorkTasks)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Project?> GetByIdWithUsers(Guid id, CancellationToken cancellationToken)
    {
        return await context.Projects
            .Include(p => p.Users)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<List<Project>> GetAllWithUsers(CancellationToken cancellationToken)
    {
        return await context.Projects
            .AsNoTracking()
            .Include(p => p.Users)
            .Include(p => p.WorkTasks)
            .ToListAsync(cancellationToken);
    }

    //public async Task<List<Project>> GetProjects(CancellationToken cancellationToken)
    //{
    //    return await context.Projects
    //        .AsNoTracking()
    //        .ToListAsync(cancellationToken);
    //}

    //public async Task<Project> UpdateProject(Project updatedProject, CancellationToken cancellationToken)
    //{

    //    context.Projects.Update(updatedProject);
    //    await context.SaveChangesAsync(cancellationToken);

    //    return updatedProject;
    //}
}
