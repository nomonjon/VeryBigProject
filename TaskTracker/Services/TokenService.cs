using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Models;
using TaskTracker.MyOptions;

namespace TaskTracker.Services;

public class TokenService(IOptions<JwtSettings> jwtSettings)
{
    private readonly JwtSettings settings = jwtSettings.Value;

    public string GenerateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(CustomClaims.Position, user.Position)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var toker = new JwtSecurityToken
        (
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(settings.ExpiresInMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(toker);
    }
}

public class AuthService(IUserRepository userRepo, TokenService tokenService)
{
    public async Task<Result<AuthResponseDto>> RegisterAsync(RegisterDto registerDto, CancellationToken cancellationToken)
    {
        var existingUser = await userRepo.GetByEmail(registerDto.Email, cancellationToken);
        if (existingUser is not null)
            return Result<AuthResponseDto>.Failure(Error.EmailTaken);

        var user = new User
        {
            FullName = registerDto.FullName,
            Email = registerDto.Email,
            Position = registerDto.Position,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
            Role = Roles.User
        };

        await userRepo.Create(user, cancellationToken);

        var token = tokenService.GenerateToken(user);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            Id: user.Id,
            Token: token,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role
        ));
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginDto loginDto, CancellationToken cancellationToken)
    {
        var user = await userRepo.GetByEmail(loginDto.Email, cancellationToken);

        if (user is null)
            return Result<AuthResponseDto>.Failure(Error.InvalidCredentials);

        var isValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);

        if (isValid is false)
            return Result<AuthResponseDto>.Failure(Error.InvalidCredentials);

        var token = tokenService.GenerateToken(user);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            Id: user.Id,
            Token: token,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role
            ));
    }
}
