using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Interfaces;
using TaskTracker.Models;
using KeyNotFoundException = System.Collections.Generic.KeyNotFoundException;

namespace TaskTracker.Repository;

public class UserRepository(AppDbContext context) : BaseRepository<User>(context), IUserRepository
{
    // BaseRepository.GetAll now hands back an IQueryable so callers can compose on it.
    // IUserRepository still promises a materialised list, so shadow it here — same
    // pattern WorkTaskRepository already uses for its Include-heavy override.
    public new async Task<List<User>> GetAll(CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    //public async Task<User> CreateUser(User newUser, CancellationToken cancellationToken)
    //{

    //    await context.Users.AddAsync(newUser, cancellationToken);
    //    await context.SaveChangesAsync(cancellationToken);

    //    return newUser;
    //}

    //public async Task<bool> DeleteUser(Guid id, CancellationToken cancellationToken)
    //{
    //    var user = await context.Users.FindAsync(id, cancellationToken);

    //    if (user is null)
    //        return false;

    //    context.Users.Remove(user);
    //    await context.SaveChangesAsync(cancellationToken);

    //    return true;
    //}

    //public async Task<User?> GetUserById(Guid id, CancellationToken cancellationToken)
    //{
    //    return await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    //}

    //public async Task<List<User>> GetUsers(CancellationToken cancellationToken)
    //{
    //    return await context.Users.AsNoTracking().ToListAsync(cancellationToken);
    //}

    //public async Task<User> UpdateUser(Guid id, User updatedUser, CancellationToken cancellationToken)
    //{
    //    var user = await context.Users.FindAsync(id, cancellationToken);

    //    if (user is null)
    //        throw new KeyNotFoundException($"User with id {id} not found");

    //    context.Users.Update(updatedUser);
    //    await context.SaveChangesAsync(cancellationToken);

    //    return updatedUser;
    //}

    //public async Task<User> UpdateUser(User updatedUser, CancellationToken cancellationToken)
    //{

    //    context.Users.Update(updatedUser);
    //    await context.SaveChangesAsync(cancellationToken);

    //    return updatedUser;
    //}
}
