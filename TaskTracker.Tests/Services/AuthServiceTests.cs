using Microsoft.Extensions.Options;
using Moq;
using TaskTracker.Interfaces;
using TaskTracker.Models;
using TaskTracker.MyOptions;
using TaskTracker.Services;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Services;

/// <summary>
/// AuthService uses a real <see cref="TokenService"/> rather than a mocked one.
/// TokenService has no I/O, so substituting it would only add setup while removing
/// the guarantee that registration and login actually hand back a usable token.
///
/// BCrypt is also real. It is slow by design (~100ms per hash), which is the price of
/// testing the thing that matters: that the password is hashed and verified, not stored.
/// </summary>
public class AuthServiceTests : TestBase
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var tokenService = new TokenService(Options.Create(new JwtSettings
        {
            SecretKey = "this-is-a-test-signing-key-at-least-32-bytes-long",
            Issuer = "TaskTracker",
            Audience = "TaskTrackerClients",
            ExpiresInMinutes = 60
        }));

        _sut = new AuthService(_users.Object, tokenService);
    }

    // ---------- RegisterAsync ----------

    [Fact]
    public async Task RegisterAsync_CreatesTheUser_AndReturnsAToken()
    {
        ArrangeNoExistingUser();

        var result = await _sut.RegisterAsync(Make.RegisterDto(fullName: "Ada", email: "ada@example.com"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada", result.Value!.FullName);
        Assert.Equal("ada@example.com", result.Value.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Token));
    }

    [Fact]
    public async Task RegisterAsync_HashesThePassword()
    {
        var persisted = ArrangeNoExistingUser();

        await _sut.RegisterAsync(Make.RegisterDto(password: "correct horse battery staple"), default);

        Assert.NotEqual("correct horse battery staple", persisted.Captured!.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("correct horse battery staple", persisted.Captured.PasswordHash));
    }

    [Fact]
    public async Task RegisterAsync_AlwaysAssignsTheUserRole()
    {
        var persisted = ArrangeNoExistingUser();

        await _sut.RegisterAsync(Make.RegisterDto(), default);

        // Self-service signup must never mint an Admin, whatever the request body said.
        Assert.Equal(Roles.User, persisted.Captured!.Role);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsEmailTaken_AndWritesNothing_WhenTheEmailIsAlreadyRegistered()
    {
        _users.Setup(r => r.GetByEmail("ada@example.com", It.IsAny<CancellationToken>()))
              .ReturnsAsync(Make.User(email: "ada@example.com"));

        var result = await _sut.RegisterAsync(Make.RegisterDto(email: "ada@example.com"), default);

        Assert.Same(Error.EmailTaken, result.Error);
        _users.Verify(r => r.Create(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_LooksTheEmailUpBeforeCreating()
    {
        ArrangeNoExistingUser();

        await _sut.RegisterAsync(Make.RegisterDto(email: "ada@example.com"), default);

        _users.Verify(r => r.GetByEmail("ada@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- LoginAsync ----------

    [Fact]
    public async Task LoginAsync_ReturnsAToken_ForTheRightPassword()
    {
        ArrangeRegisteredUser("ada@example.com", "s3cret");

        var result = await _sut.LoginAsync(Make.LoginDto(email: "ada@example.com", password: "s3cret"), default);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.Token));
        Assert.Equal("ada@example.com", result.Value.Email);
    }

    [Fact]
    public async Task LoginAsync_ReturnsInvalidCredentials_ForTheWrongPassword()
    {
        ArrangeRegisteredUser("ada@example.com", "s3cret");

        var result = await _sut.LoginAsync(Make.LoginDto(email: "ada@example.com", password: "guess"), default);

        Assert.Same(Error.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task LoginAsync_ReturnsInvalidCredentials_ForAnUnknownEmail()
    {
        _users.Setup(r => r.GetByEmail(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _sut.LoginAsync(Make.LoginDto(), default);

        Assert.Same(Error.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task LoginAsync_UsesTheSameErrorForUnknownEmailAndWrongPassword()
    {
        _users.Setup(r => r.GetByEmail(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var unknownEmail = await _sut.LoginAsync(Make.LoginDto(), default);

        ArrangeRegisteredUser("ada@example.com", "s3cret");
        var wrongPassword = await _sut.LoginAsync(Make.LoginDto(email: "ada@example.com", password: "guess"), default);

        // Distinguishing the two would let an attacker enumerate registered accounts.
        Assert.Same(unknownEmail.Error, wrongPassword.Error);
    }

    [Fact]
    public async Task LoginAsync_CarriesTheStoredRoleIntoTheResponse()
    {
        ArrangeRegisteredUser("ada@example.com", "s3cret", Roles.Admin);

        var result = await _sut.LoginAsync(Make.LoginDto(email: "ada@example.com", password: "s3cret"), default);

        Assert.Equal(Roles.Admin, result.Value!.Role);
    }

    // ---------- helpers ----------

    private sealed class CreatedUser { public User? Captured { get; set; } }

    private CreatedUser ArrangeNoExistingUser()
    {
        var created = new CreatedUser();
        _users.Setup(r => r.GetByEmail(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _users.Setup(r => r.Create(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => created.Captured = u)
              .ReturnsAsync((User u, CancellationToken _) => u);
        return created;
    }

    private void ArrangeRegisteredUser(string email, string password, string role = Roles.User)
        => _users.Setup(r => r.GetByEmail(email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Make.User(email: email, role: role, passwordHash: BCrypt.Net.BCrypt.HashPassword(password)));
}
