using Microsoft.Extensions.Options;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Mappers;
using TaskTracker.Models;
using TaskTracker.MyOptions;
using KeyNotFoundException = System.Collections.Generic.KeyNotFoundException;

namespace TaskTracker.Services;

public class UserService(IUserRepository userRepo, IOptions<DbConection> _conection, ILogger<UserService> logger) : IUserService
{
    private readonly DbConection conection = _conection.Value;
    public async Task<Result<UserDto>> CreateUserAsync(CreateUpdateUserDto newUser, CancellationToken cancellationToken)
    {
        if (newUser is null)
            return Result<UserDto>.Failure(Error.BadRequest);


        var user = newUser.ToUser();

        var savedUser = await userRepo.Create(user, cancellationToken);
        return Result<UserDto>.Success(savedUser.ToUserDto());
    }

    public async Task<Result<bool>> DeleteUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var isDeleted = await userRepo.Delete(id, cancellationToken);

        return Result<bool>.Success(isDeleted);
    }

    public async Task<Result<UserDto>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userRepo.GetById(id, cancellationToken);

        if (user is null)
            return Result<UserDto>.Failure(Error.NotFound);

        return Result<UserDto>.Success(user.ToUserDto());
    }

    

    public async Task<List<UserDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning(conection.Default);
        var users = await userRepo.GetAll(cancellationToken);
        return users.Select(u => u.ToUserDto()).ToList();

    }

    public async Task<List<User>> GetUsersWithIdAsync(CancellationToken cancellationToken)
    {
        return await userRepo.GetAll(cancellationToken);
    }

    public async Task<Result<UserDto>> UpdateUserAsync(Guid id, CreateUpdateUserDto updatedUser, CancellationToken cancellationToken)
    {
        var user = await userRepo.GetById(id, cancellationToken);

        if (user is null)
            return Result<UserDto>.Failure(Error.NotFound);

        user.FullName = updatedUser.FullName;
        user.Email = updatedUser.Email;
        user.Position = updatedUser.Position;

        var saved = await userRepo.Update(user, cancellationToken);

        if (saved is false)
            return Result<UserDto>.Failure(Error.NotModified);

        return Result<UserDto>.Success(user.ToUserDto());
    }

    public async Task<Result<UserDto>> UpdatePartly(Guid id, CreateUpdateUserDto updatedUser, CancellationToken cancellationToken)
    {
        var user = await userRepo.GetByIdWithTraking(id, cancellationToken);
        
        if(user is null)
            return Result<UserDto>.Failure(Error.NotFound);

        if(updatedUser.FullName is not null)
            user.FullName = updatedUser.FullName;
        if(updatedUser.Email is not null)
            user.Email = updatedUser.Email;
        if(updatedUser.Position is not null)
            user.Position = updatedUser.Position;
        var SavedUser = await userRepo.Update(user, cancellationToken);

        if (SavedUser is false)
            return Result<UserDto>.Failure(Error.NotModified);

        return Result<UserDto>.Success(user.ToUserDto());
    }


}
