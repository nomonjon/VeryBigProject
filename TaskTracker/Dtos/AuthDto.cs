namespace TaskTracker.Dtos;

public record RegisterDto(
    string FullName,
    string Email,
    string Password,
    string Position
);

public record LoginDto(
    string Email,
    string Password
);

public record AuthResponseDto(
    string Token,
    string Email,
    string FullName,
    string Role
);