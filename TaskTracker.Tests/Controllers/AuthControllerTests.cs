using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using TaskTracker.Controllers;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Models;
using TaskTracker.MyOptions;
using TaskTracker.Services;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Controllers;

/// <summary>
/// AuthController takes a concrete <see cref="AuthService"/> with non-virtual methods,
/// so there is nothing to mock at that level. The seam moves one layer down: mock
/// <see cref="IUserRepository"/> and let the real service run.
///
/// That is the general rule — when a class cannot be substituted, substitute its
/// dependencies. It also means these tests double as a check that controller and
/// service still agree.
/// </summary>
public class AuthControllerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        var tokenService = new TokenService(Options.Create(new JwtSettings
        {
            SecretKey = "this-is-a-test-signing-key-at-least-32-bytes-long",
            Issuer = "TaskTracker",
            Audience = "TaskTrackerClients",
            ExpiresInMinutes = 60
        }));

        _sut = new AuthController(new AuthService(_users.Object, tokenService));
    }

    [Fact]
    public async Task Register_Returns200_WithTheTokenPayload()
    {
        _users.Setup(r => r.GetByEmail(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _users.Setup(r => r.Create(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((User u, CancellationToken _) => u);

        var ok = Assert.IsType<OkObjectResult>(await _sut.Register(Make.RegisterDto(), default));

        var body = Assert.IsType<AuthResponseDto>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
    }

    [Fact]
    public async Task Register_Returns409_WhenTheEmailIsTaken()
    {
        _users.Setup(r => r.GetByEmail(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Make.User());

        Assert.IsType<ConflictObjectResult>(await _sut.Register(Make.RegisterDto(), default));
    }

    [Fact]
    public async Task Login_Returns200_ForValidCredentials()
    {
        _users.Setup(r => r.GetByEmail("ada@example.com", It.IsAny<CancellationToken>()))
              .ReturnsAsync(Make.User(email: "ada@example.com", passwordHash: BCrypt.Net.BCrypt.HashPassword("s3cret")));

        var ok = Assert.IsType<OkObjectResult>(
            await _sut.Login(Make.LoginDto(email: "ada@example.com", password: "s3cret"), default));

        Assert.IsType<AuthResponseDto>(ok.Value);
    }

    [Fact]
    public async Task Login_Returns401_ForTheWrongPassword()
    {
        _users.Setup(r => r.GetByEmail("ada@example.com", It.IsAny<CancellationToken>()))
              .ReturnsAsync(Make.User(email: "ada@example.com", passwordHash: BCrypt.Net.BCrypt.HashPassword("s3cret")));

        Assert.IsType<UnauthorizedObjectResult>(
            await _sut.Login(Make.LoginDto(email: "ada@example.com", password: "guess"), default));
    }

    [Fact]
    public async Task Login_Returns401_ForAnUnknownEmail()
    {
        _users.Setup(r => r.GetByEmail(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Assert.IsType<UnauthorizedObjectResult>(await _sut.Login(Make.LoginDto(), default));
    }
}
