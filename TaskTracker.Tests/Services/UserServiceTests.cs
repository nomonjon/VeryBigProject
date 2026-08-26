using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Models;
using TaskTracker.MyOptions;
using TaskTracker.Services;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Services;

public class UserServiceTests : TestBase
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly UserService _sut;

    public UserServiceTests()
        => _sut = new UserService(
            _users.Object,
            Options.Create(new DbConection { Default = "Host=localhost" }),
            NullLogger<UserService>.Instance);

    // ---------- CreateUserAsync ----------

    [Fact]
    public async Task CreateUserAsync_PersistsTheMappedUser()
    {
        User? persisted = null;
        _users.Setup(r => r.Create(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => persisted = u)
              .ReturnsAsync((User u, CancellationToken _) => u);

        var result = await _sut.CreateUserAsync(Make.UserDto(fullName: "Ada", email: "ada@example.com"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada", persisted!.FullName);
        Assert.Equal("ada@example.com", persisted.Email);
    }

    [Fact]
    public async Task CreateUserAsync_ReturnsTheStoredUser_NotTheSubmittedOne()
    {
        // The repository is what assigns the id and (elsewhere) the role.
        _users.Setup(r => r.Create(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Make.User(fullName: "Stored", role: Roles.Admin));

        var result = await _sut.CreateUserAsync(Make.UserDto(fullName: "Submitted"), default);

        Assert.Equal("Stored", result.Value!.FullName);
        Assert.Equal(Roles.Admin, result.Value.Role);
    }

    [Fact]
    public async Task CreateUserAsync_ReturnsBadRequest_AndWritesNothing_WhenTheBodyIsMissing()
    {
        var result = await _sut.CreateUserAsync(null!, default);

        Assert.False(result.IsSuccess);
        Assert.Same(Error.BadRequest, result.Error);
        _users.Verify(r => r.Create(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- GetUserByIdAsync ----------

    [Fact]
    public async Task GetUserByIdAsync_ReturnsTheUser_WhenItExists()
    {
        var user = Make.User(fullName: "Ada");
        _users.Setup(r => r.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _sut.GetUserByIdAsync(user.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada", result.Value!.FullName);
    }

    [Fact]
    public async Task GetUserByIdAsync_ReturnsNotFound_WhenTheUserIsMissing()
    {
        _users.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _sut.GetUserByIdAsync(Guid.NewGuid(), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    // ---------- GetUsersAsync / GetUsersWithIdAsync ----------

    [Fact]
    public async Task GetUsersAsync_MapsEveryUser()
    {
        _users.Setup(r => r.GetAll(It.IsAny<CancellationToken>()))
              .ReturnsAsync([Make.User(fullName: "Ada"), Make.User(fullName: "Grace")]);

        var result = await _sut.GetUsersAsync(default);

        Assert.Equal(["Ada", "Grace"], result.Select(u => u.FullName));
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsAnEmptyList_WhenThereAreNoUsers()
    {
        _users.Setup(r => r.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        Assert.Empty(await _sut.GetUsersAsync(default));
    }

    [Fact]
    public async Task GetUsersWithIdAsync_IncludesTheId()
    {
        var user = Make.User();
        _users.Setup(r => r.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync([user]);

        var result = await _sut.GetUsersWithIdAsync(default);

        Assert.Equal(user.Id, Assert.Single(result).Id);
    }

    // ---------- UpdateUserAsync (full replace) ----------

    [Fact]
    public async Task UpdateUserAsync_OverwritesEveryProfileField()
    {
        var user = Make.User(fullName: "Old", email: "old@example.com", position: "Old");
        _users.Setup(r => r.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.Update(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.UpdateUserAsync(
            user.Id, Make.UserDto(fullName: "New", email: "new@example.com", position: "New"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", result.Value!.FullName);
        Assert.Equal("new@example.com", result.Value.Email);
        Assert.Equal("New", result.Value.Position);
    }

    [Fact]
    public async Task UpdateUserAsync_NeverChangesTheRole()
    {
        // Role changes go through UpdateRoleAsync, which the controller guards
        // separately. A profile edit must not be a privilege escalation path.
        var user = Make.User(role: Roles.User);
        _users.Setup(r => r.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.Update(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.UpdateUserAsync(user.Id, Make.UserDto(), default);

        Assert.Equal(Roles.User, result.Value!.Role);
    }

    [Fact]
    public async Task UpdateUserAsync_ReturnsNotFound_AndWritesNothing_WhenTheUserIsMissing()
    {
        _users.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _sut.UpdateUserAsync(Guid.NewGuid(), Make.UserDto(), default);

        Assert.Same(Error.NotFound, result.Error);
        _users.Verify(r => r.Update(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_ReturnsNotModified_WhenTheSaveChangedNothing()
    {
        _users.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Make.User());
        _users.Setup(r => r.Update(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _sut.UpdateUserAsync(Guid.NewGuid(), Make.UserDto(), default);

        Assert.Same(Error.NotModified, result.Error);
    }

    // ---------- UpdatePartly (PATCH) ----------

    [Fact]
    public async Task UpdatePartly_ChangesOnlyTheFieldsThatWereSupplied()
    {
        var user = Make.User(fullName: "Ada", email: "ada@example.com", position: "Engineer");
        _users.Setup(r => r.GetByIdWithTraking(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.Update(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.UpdatePartly(user.Id, new CreateUpdateUserDto(null!, "new@example.com", null!), default);

        Assert.Equal("new@example.com", result.Value!.Email);
        Assert.Equal("Ada", result.Value.FullName);       // untouched
        Assert.Equal("Engineer", result.Value.Position);  // untouched
    }

    [Fact]
    public async Task UpdatePartly_CanChangeEveryFieldAtOnce()
    {
        var user = Make.User(fullName: "Ada", email: "ada@example.com", position: "Engineer");
        _users.Setup(r => r.GetByIdWithTraking(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.Update(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.UpdatePartly(user.Id, Make.UserDto(fullName: "Grace", email: "grace@example.com", position: "Admiral"), default);

        Assert.Equal("Grace", result.Value!.FullName);
        Assert.Equal("grace@example.com", result.Value.Email);
        Assert.Equal("Admiral", result.Value.Position);
    }

    [Fact]
    public async Task UpdatePartly_LoadsTheUserTracked_SoEfSeesTheEdit()
    {
        var user = Make.User();
        _users.Setup(r => r.GetByIdWithTraking(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.Update(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _sut.UpdatePartly(user.Id, Make.UserDto(), default);

        // A no-tracking read would leave the change detached and silently unsaved.
        _users.Verify(r => r.GetByIdWithTraking(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePartly_ReturnsNotFound_WhenTheUserIsMissing()
    {
        _users.Setup(r => r.GetByIdWithTraking(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _sut.UpdatePartly(Guid.NewGuid(), Make.UserDto(), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdatePartly_ReturnsNotModified_WhenTheSaveChangedNothing()
    {
        _users.Setup(r => r.GetByIdWithTraking(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Make.User());
        _users.Setup(r => r.Update(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _sut.UpdatePartly(Guid.NewGuid(), Make.UserDto(), default);

        Assert.Same(Error.NotModified, result.Error);
    }

    // ---------- UpdateRoleAsync ----------

    [Fact]
    public async Task UpdateRoleAsync_SetsTheNewRole()
    {
        var user = Make.User(role: Roles.User);
        _users.Setup(r => r.GetByIdWithTraking(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.Update(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.UpdateRoleAsync(user.Id, new UpdateUserRoleDto(Roles.Admin), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(Roles.Admin, result.Value!.Role);
    }

    [Fact]
    public async Task UpdateRoleAsync_LeavesTheProfileAlone()
    {
        var user = Make.User(fullName: "Ada", email: "ada@example.com");
        _users.Setup(r => r.GetByIdWithTraking(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.Update(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.UpdateRoleAsync(user.Id, new UpdateUserRoleDto(Roles.Admin), default);

        Assert.Equal("Ada", result.Value!.FullName);
        Assert.Equal("ada@example.com", result.Value.Email);
    }

    [Fact]
    public async Task UpdateRoleAsync_ReturnsNotFound_WhenTheUserIsMissing()
    {
        _users.Setup(r => r.GetByIdWithTraking(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _sut.UpdateRoleAsync(Guid.NewGuid(), new UpdateUserRoleDto(Roles.Admin), default);

        Assert.Same(Error.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdateRoleAsync_ReturnsNotModified_WhenTheSaveChangedNothing()
    {
        _users.Setup(r => r.GetByIdWithTraking(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Make.User());
        _users.Setup(r => r.Update(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _sut.UpdateRoleAsync(Guid.NewGuid(), new UpdateUserRoleDto(Roles.Admin), default);

        Assert.Same(Error.NotModified, result.Error);
    }

    // ---------- DeleteUserAsync ----------

    [Fact]
    public async Task DeleteUserAsync_ReturnsSuccessTrue_WhenTheUserWasDeleted()
    {
        _users.Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.DeleteUserAsync(Guid.NewGuid(), default);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task DeleteUserAsync_ReturnsSuccessFalse_WhenThereWasNothingToDelete()
    {
        _users.Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _sut.DeleteUserAsync(Guid.NewGuid(), default);

        // Documenting today's behaviour, not endorsing it: the controller does
        // `result.ToResponse()`, so deleting a nonexistent user answers 200 with
        // `false` instead of 404. ProjectService.DeleteProjectAsync returns
        // Failure(NotFound) for the same situation — the two are inconsistent.
        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task DeleteUserAsync_ForwardsTheIdToTheRepository()
    {
        var id = Guid.NewGuid();
        _users.Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _sut.DeleteUserAsync(id, default);

        _users.Verify(r => r.Delete(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
