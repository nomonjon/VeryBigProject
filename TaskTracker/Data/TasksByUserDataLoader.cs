using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskTracker.Models;

namespace TaskTracker.Data;

public class TasksByUserDataLoader(
    IDbContextFactory<AppDbContext> factory,
    IBatchScheduler scheduler,
    DataLoaderOptions options) : GroupedDataLoader<Guid, WorkTask>(scheduler, options)
{
    protected override async Task<ILookup<Guid, WorkTask>> LoadGroupedBatchAsync(
        IReadOnlyList<Guid> assigneeIds,
        CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);

        var tasks = await context.WorkTasks
            .Where(t => t.AssigneeId.HasValue &&
                        assigneeIds.Contains(t.AssigneeId.Value))
            .ToListAsync(ct);

        return tasks.ToLookup(t => t.AssigneeId!.Value);
    }
}