using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.Repository;

public class BaseRepository<T>(AppDbContext context) where T : BaseEntity
{
    public async Task<IQueryable<T>> GetAll(CancellationToken cancellationToken)
    {
        return context.Set<T>().AsNoTracking();
    }

    public async Task<T?> GetById(Guid id, CancellationToken cancellationToken)
    {
        return await context.Set<T>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }


    public async Task<T?> GetByIdWithTraking(Guid id, CancellationToken cancellationToken)
    {
        return await context.Set<T>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<T> Create(T newEntity, CancellationToken cancellationToken)
    {

        await context.Set<T>().AddAsync(newEntity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return newEntity;
    }
    public async Task<bool> Delete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await context.Set<T>().FindAsync(new object[] { id }, cancellationToken);

        if (entity is null)
            return false;

        context.Set<T>().Remove(entity);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }
    public async Task<bool> Update(T updatedEntity, CancellationToken cancellationToken)
    {
        var existing = await context.Set<T>().FindAsync(new object[] { updatedEntity.Id }, cancellationToken);

        if (existing is null)
            return false;

        if (!ReferenceEquals(existing, updatedEntity))
        {
            context.Entry(existing).CurrentValues.SetValues(updatedEntity);
        }

        await context.SaveChangesAsync(cancellationToken);

        return true;
    }
    public async Task<User?> GetByEmail(string email, CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }
}