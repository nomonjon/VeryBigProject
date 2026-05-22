using TaskTracker.Models;

namespace TaskTracker.Interfaces;

public interface IUserRepository
{
    Task<List<User>> GetAll(CancellationToken cancellationToken);
    Task<User?> GetById(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByIdWithTraking(Guid id, CancellationToken cancellationToken);
    Task<User> Create(User newUser, CancellationToken cancellationToken);
    Task<bool> Update(User updatedUser, CancellationToken cancellationToken);
    Task<bool> Delete(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByEmail(string email, CancellationToken cancellationToken);
}
