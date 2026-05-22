using TaskTracker.Dtos;
using TaskTracker.Models;

namespace TaskTracker.Interfaces;

public interface IUserService
{
    Task<List<UserDto>> GetUsersAsync(CancellationToken cancellationToken);
    Task<List<User>> GetUsersWithIdAsync(CancellationToken cancellationToken);
    Task<Result<UserDto>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<UserDto>> CreateUserAsync(CreateUpdateUserDto newUser, CancellationToken cancellationToken);
    Task<Result<UserDto>> UpdateUserAsync(Guid id, CreateUpdateUserDto updatedUser, CancellationToken cancellationToken);
    Task<Result<UserDto>> UpdatePartly(Guid id, CreateUpdateUserDto updatedUser, CancellationToken cancellationToken);
    Task<Result<bool>> DeleteUserAsync(Guid id, CancellationToken cancellationToken);
}
