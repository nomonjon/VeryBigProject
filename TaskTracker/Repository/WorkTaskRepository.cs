using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Interfaces;
using TaskTracker.Models;

namespace TaskTracker.Repository;

public class WorkTaskRepository(AppDbContext context) : BaseRepository<WorkTask>(context), IWorkTaskRepository
{
    public new async Task<WorkTask?> GetById(Guid id, CancellationToken cancellationToken)
    {
        return await context.WorkTasks
            .Include(wt => wt.Project).ThenInclude(p => p.Users)
            .Include(wt => wt.Assignee)
            .Include(wt => wt.Comments)
            .Include(wt => wt.History)
            .AsNoTracking()
            .FirstOrDefaultAsync(wt => wt.Id == id, cancellationToken);
    }

    public new async Task<List<WorkTask>> GetAll(CancellationToken cancellationToken)
    {
        return await context.WorkTasks
            .Include(wt => wt.Project).ThenInclude(p => p.Users)
            .Include(wt => wt.Assignee)
            .Include(wt => wt.Comments)
            .Include(wt => wt.History)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
    //public async Task<WorkTask> CreateTask(WorkTask newTask, CancellationToken cancellationToken)
    //{
    //    await context.WorkTasks.AddAsync(newTask, cancellationToken);
    //    await context.SaveChangesAsync();

    //    return newTask;
    //}

    //public async Task<bool> DeleteTask(Guid id, CancellationToken cancellationToken)
    //{
    //    var task = await context.WorkTasks.FindAsync(id, cancellationToken);

    //    if (task is null)
    //        return false;

    //    context.WorkTasks.Remove(task);
    //    await context.SaveChangesAsync(cancellationToken);

    //    return true;
    //}

    //public async Task<WorkTask?> GetTaskById(Guid id, CancellationToken cancellationToken)
    //{
    //    return await context.WorkTasks
    //        .AsNoTracking()
    //        .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    //}

    //public async Task<List<WorkTask>> GetTasks(CancellationToken cancellationToken)
    //{
    //    return await context.WorkTasks
    //        .AsNoTracking()
    //        .ToListAsync(cancellationToken);
    //}

    //public async Task<WorkTask> UpdateTask( WorkTask updatedTask, CancellationToken cancellationToken)
    //{

    //    context.WorkTasks.Update(updatedTask);
    //    await context.SaveChangesAsync(cancellationToken);

    //    return updatedTask;
    //}
}