using TaskTracker.Mappers;
using TaskTracker.Models;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Mappers;

public class UserMapperTests
{
    [Fact]
    public void ToUserDto_CopiesTheProfileFields()
    {
        var user = Make.User(fullName: "Ada", email: "ada@example.com", position: "Engineer", role: Roles.Admin);

        var dto = user.ToUserDto();

        Assert.Equal("Ada", dto.FullName);
        Assert.Equal("ada@example.com", dto.Email);
        Assert.Equal("Engineer", dto.Position);
        Assert.Equal(Roles.Admin, dto.Role);
    }

    [Fact]
    public void ToUserDto_NeverExposesThePasswordHash()
    {
        // The DTO is what leaves the process. A hash added here by accident would be
        // a credential leak, so assert on the shape, not just on today's values.
        Assert.Null(typeof(Dtos.UserDto).GetProperty("PasswordHash"));
        Assert.Null(typeof(Dtos.UserWithIdDto).GetProperty("PasswordHash"));
    }

    [Fact]
    public void ToUserDto_OmitsTheId_UnlikeToUserWithIdDto()
    {
        var user = Make.User();

        Assert.Null(typeof(Dtos.UserDto).GetProperty("Id"));
        Assert.Equal(user.Id, user.ToUserWithIdDto().Id);
    }

    [Fact]
    public void ToUserDto_MapsTheAssignedTasks()
    {
        var user = Make.User();
        user.WorkTasks = [Make.WorkTask(name: "First"), Make.WorkTask(name: "Second")];

        var dto = user.ToUserDto();

        Assert.Equal(["First", "Second"], dto.WorkTasks.Select(t => t.Name));
    }

    [Fact]
    public void ToUserDto_ReturnsAnEmptyTaskList_WhenTasksWereNotLoaded()
    {
        var user = Make.User();
        user.WorkTasks = null!;   // what EF leaves behind without an Include

        var dto = user.ToUserDto();

        Assert.Empty(dto.WorkTasks);
    }

    [Fact]
    public void ToUserWithIdDto_CopiesEverythingToUserDtoDoes_PlusTheId()
    {
        var user = Make.User(fullName: "Ada", role: Roles.Admin);
        user.WorkTasks = [Make.WorkTask(name: "First")];

        var dto = user.ToUserWithIdDto();

        Assert.Equal(user.Id, dto.Id);
        Assert.Equal("Ada", dto.FullName);
        Assert.Equal(Roles.Admin, dto.Role);
        Assert.Single(dto.WorkTasks);
    }

    [Fact]
    public void ToUserWithIdDto_ReturnsAnEmptyTaskList_WhenTasksWereNotLoaded()
    {
        var user = Make.User();
        user.WorkTasks = null!;

        Assert.Empty(user.ToUserWithIdDto().WorkTasks);
    }

    [Fact]
    public void ToUser_CopiesOnlyTheClientSuppliedFields()
    {
        var user = Make.UserDto(fullName: "Ada", email: "ada@example.com", position: "Engineer").ToUser();

        Assert.Equal("Ada", user.FullName);
        Assert.Equal("ada@example.com", user.Email);
        Assert.Equal("Engineer", user.Position);
    }

    [Fact]
    public void ToUser_LeavesRoleAndPasswordUnset_BecauseAClientMustNotChooseThem()
    {
        var user = Make.UserDto().ToUser();

        Assert.Equal(string.Empty, user.Role);
        Assert.Equal(string.Empty, user.PasswordHash);
    }

    [Fact]
    public void ToUser_LeavesIdEmpty_SoTheDatabaseAssignsIt()
        => Assert.Equal(Guid.Empty, Make.UserDto().ToUser().Id);
}
