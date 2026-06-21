using TaskTracker.Dtos;
using TaskTracker.Models;

namespace TaskTracker.Mappers;

public static class UserMapper
{
    public static UserDto ToUserDto(this User user)
    {
        return new UserDto
        (
            user.FullName,
            user.Email,
            user.Position,
            user.Role,
            user.WorkTasks?.Select(wt => wt.ToWorkTaskDto()).ToList() ?? new List<WorkTaskDto>()
        );
    }

    public static UserWithIdDto ToUserWithIdDto(this User user)
    {
        return new UserWithIdDto
        (
            user.Id,
            user.FullName,
            user.Email,
            user.Position,
            user.Role,
            user.WorkTasks?.Select(wt => wt.ToWorkTaskDto()).ToList() ?? new List<WorkTaskDto>()
        );
    }

    public static User ToUser(this CreateUpdateUserDto userDto)
    {
        return new User
        {
            FullName = userDto.FullName,
            Email = userDto.Email,
            Position = userDto.Position
        };
    }
}
