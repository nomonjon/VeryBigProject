using Microsoft.AspNetCore.Mvc;
using Moq;
using TaskTracker.Controllers;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Models;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Controllers;

public class UserControllerTests
{
    private readonly Mock<IUserService> _service = new();
    private readonly UserController _sut;

    public UserControllerTests() => _sut = new UserController(_service.Object);

    // ---------- reads ----------

    [Fact]
    public async Task GetUsers_Returns200_WithTheList()
    {
        var users = new List<UserDto> { new("Ada", "ada@example.com", "Engineer", Roles.User, []) };
        _service.Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetUsers(default));

        Assert.Same(users, ok.Value);
    }

    [Fact]
    public async Task GetUsersWithId_Returns200_WithTheList()
    {
        var users = new List<UserWithIdDto> { new(Guid.NewGuid(), "Ada", "ada@example.com", "Engineer", Roles.User, []) };
        _service.Setup(s => s.GetUsersWithIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetUsersWithId(default));

        Assert.Same(users, ok.Value);
    }

    [Fact]
    public async Task GetUser_Returns200_WhenTheUserExists()
    {
        _service.Setup(s => s.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<UserDto>.Success(new UserDto("Ada", "ada@example.com", "Engineer", Roles.User, [])));

        Assert.IsType<OkObjectResult>(await _sut.GetUser(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task GetUser_Returns404_WhenTheUserIsMissing()
    {
        _service.Setup(s => s.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<UserDto>.Failure(Error.NotFound));

        Assert.IsType<NotFoundObjectResult>(await _sut.GetUser(Guid.NewGuid(), default));
    }

    // ---------- CreateUser ----------

    [Fact]
    public async Task CreateUser_Returns200_WithTheResult()
    {
        _service.Setup(s => s.CreateUserAsync(It.IsAny<CreateUpdateUserDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<UserDto>.Success(new UserDto("Ada", "ada@example.com", "Engineer", Roles.User, [])));

        Assert.IsType<OkObjectResult>(await _sut.CreateUser(Make.UserDto(), default));
    }

    [Fact]
    public async Task CreateUser_Returns400_WhenTheServiceRejectsTheArguments()
    {
        _service.Setup(s => s.CreateUserAsync(It.IsAny<CreateUpdateUserDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Email is required"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(await _sut.CreateUser(Make.UserDto(), default));

        Assert.Equal("Email is required", badRequest.Value);
    }

    [Fact]
    public async Task CreateUser_Returns200_EvenWhenTheServiceFailed()
    {
        // Documenting a real inconsistency: this action wraps the Result in Ok()
        // instead of calling ToResponse(), so a BadRequest failure still answers 200
        // with a serialized failure object. GetUser and DeleteUser do it correctly.
        _service.Setup(s => s.CreateUserAsync(It.IsAny<CreateUpdateUserDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<UserDto>.Failure(Error.BadRequest));

        var ok = Assert.IsType<OkObjectResult>(await _sut.CreateUser(Make.UserDto(), default));

        Assert.False(Assert.IsType<Result<UserDto>>(ok.Value).IsSuccess);
    }

    // ---------- UpdateUser ----------

    [Fact]
    public async Task UpdateUser_Returns200_WithTheResult()
    {
        _service.Setup(s => s.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateUserDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<UserDto>.Success(new UserDto("Ada", "ada@example.com", "Engineer", Roles.User, [])));

        Assert.IsType<OkObjectResult>(await _sut.UpdateUser(Guid.NewGuid(), Make.UserDto(), default));
    }

    [Fact]
    public async Task UpdateUser_Returns404_WhenTheServiceThrowsKeyNotFound()
    {
        _service.Setup(s => s.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateUserDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("User not found"));

        var notFound = Assert.IsType<NotFoundObjectResult>(await _sut.UpdateUser(Guid.NewGuid(), Make.UserDto(), default));

        Assert.Equal("User not found", notFound.Value);
    }

    [Fact]
    public async Task UpdateUser_Returns400_ForAnyOtherException()
    {
        _service.Setup(s => s.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<CreateUpdateUserDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

        Assert.IsType<BadRequestObjectResult>(await _sut.UpdateUser(Guid.NewGuid(), Make.UserDto(), default));
    }

    // ---------- UpdateUserPartly ----------

    [Fact]
    public async Task UpdateUserPartly_Returns200_WithTheResult()
    {
        _service.Setup(s => s.UpdatePartly(It.IsAny<Guid>(), It.IsAny<CreateUpdateUserDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<UserDto>.Success(new UserDto("Ada", "ada@example.com", "Engineer", Roles.User, [])));

        Assert.IsType<OkObjectResult>(await _sut.UpdateUserPartly(Guid.NewGuid(), Make.UserDto(), default));
    }

    [Fact]
    public async Task UpdateUserPartly_Returns404_WhenTheServiceThrowsKeyNotFound()
    {
        _service.Setup(s => s.UpdatePartly(It.IsAny<Guid>(), It.IsAny<CreateUpdateUserDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("User not found"));

        Assert.IsType<NotFoundObjectResult>(await _sut.UpdateUserPartly(Guid.NewGuid(), Make.UserDto(), default));
    }

    [Fact]
    public async Task UpdateUserPartly_Returns400_ForAnyOtherException()
    {
        _service.Setup(s => s.UpdatePartly(It.IsAny<Guid>(), It.IsAny<CreateUpdateUserDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

        Assert.IsType<BadRequestObjectResult>(await _sut.UpdateUserPartly(Guid.NewGuid(), Make.UserDto(), default));
    }

    // ---------- UpdateUserRole ----------

    [Fact]
    public async Task UpdateUserRole_Returns200_WhenTheRoleWasChanged()
    {
        _service.Setup(s => s.UpdateRoleAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserRoleDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<UserDto>.Success(new UserDto("Ada", "ada@example.com", "Engineer", Roles.Admin, [])));

        Assert.IsType<OkObjectResult>(await _sut.UpdateUserRole(Guid.NewGuid(), new UpdateUserRoleDto(Roles.Admin), default));
    }

    [Fact]
    public async Task UpdateUserRole_Returns404_WhenTheUserIsMissing()
    {
        _service.Setup(s => s.UpdateRoleAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserRoleDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<UserDto>.Failure(Error.NotFound));

        Assert.IsType<NotFoundObjectResult>(await _sut.UpdateUserRole(Guid.NewGuid(), new UpdateUserRoleDto(Roles.Admin), default));
    }

    [Fact]
    public async Task UpdateUserRole_Returns304_WhenNothingChanged()
    {
        _service.Setup(s => s.UpdateRoleAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserRoleDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<UserDto>.Failure(Error.NotModified));

        var result = Assert.IsType<StatusCodeResult>(
            await _sut.UpdateUserRole(Guid.NewGuid(), new UpdateUserRoleDto(Roles.Admin), default));

        Assert.Equal(304, result.StatusCode);
    }

    // ---------- DeleteUser ----------

    [Fact]
    public async Task DeleteUser_Returns200_WithTheOutcomeFlag()
    {
        _service.Setup(s => s.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Success(true));

        var ok = Assert.IsType<OkObjectResult>(await _sut.DeleteUser(Guid.NewGuid(), default));

        Assert.Equal(true, ok.Value);
    }

    [Fact]
    public async Task DeleteUser_Returns200WithFalse_WhenThereWasNothingToDelete()
    {
        // See UserServiceTests: the service reports "nothing deleted" as Success(false),
        // so the endpoint answers 200, not 404.
        _service.Setup(s => s.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Success(false));

        var ok = Assert.IsType<OkObjectResult>(await _sut.DeleteUser(Guid.NewGuid(), default));

        Assert.Equal(false, ok.Value);
    }
}
