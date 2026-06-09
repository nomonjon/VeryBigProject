using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using TaskTracker.Models;

namespace TaskTracker.Dtos;


public record UserDto(string FullName, string Email, string Position, string Role, List<WorkTaskDto> WorkTasks);

public record CreateUpdateUserDto(string FullName, string Email, string Position);

public record UpdateUserRoleDto(string Role);
