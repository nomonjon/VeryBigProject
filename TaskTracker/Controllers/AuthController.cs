using Microsoft.AspNetCore.Mvc;
using TaskTracker.Dtos;
using TaskTracker.Services;

namespace TaskTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto registerDto, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(registerDto, cancellationToken);

        return result.ToResponse();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto loginDto, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(loginDto, cancellationToken);
        return result.ToResponse();
    }
}
