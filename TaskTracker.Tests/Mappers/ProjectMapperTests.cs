using TaskTracker.Mappers;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Mappers;

public class ProjectMapperTests
{
    [Fact]
    public void ToProjectDto_CopiesNameAndDescription()
    {
        var dto = Make.Project(name: "Apollo", description: "Moon landing").ToProjectDto();

        Assert.Equal("Apollo", dto.Name);
        Assert.Equal("Moon landing", dto.Description);
    }

    [Fact]
    public void ToProjectDto_MapsTheTasks()
    {
        var project = Make.Project(workTasks: [Make.WorkTask(name: "First"), Make.WorkTask(name: "Second")]);

        Assert.Equal(["First", "Second"], project.ToProjectDto().WorkTasks.Select(t => t.Name));
    }

    [Fact]
    public void ToProjectDto_ReturnsAnEmptyTaskList_WhenTasksWereNotLoaded()
    {
        var project = Make.Project();
        project.WorkTasks = null!;

        Assert.Empty(project.ToProjectDto().WorkTasks);
    }

    [Fact]
    public void ToProjectDto_OmitsTheId()
        => Assert.Null(typeof(Dtos.ProjectDto).GetProperty("Id"));

    [Fact]
    public void ToProjectWithIdDto_IncludesTheIdAndTheMemberIds()
    {
        var alice = Make.User(fullName: "Alice");
        var bob = Make.User(fullName: "Bob");
        var project = Make.Project(users: [alice, bob]);

        var dto = project.ToProjectWithIdDto();

        Assert.Equal(project.Id, dto.Id);
        Assert.Equal([alice.Id, bob.Id], dto.UserIds);
    }

    [Fact]
    public void ToProjectWithIdDto_MapsTheTasks()
    {
        var project = Make.Project(workTasks: [Make.WorkTask(name: "First"), Make.WorkTask(name: "Second")]);

        Assert.Equal(["First", "Second"], project.ToProjectWithIdDto().WorkTasks.Select(t => t.Name));
    }

    [Fact]
    public void ToProjectWithIdDto_ExposesOnlyMemberIds_NotWholeUsers()
    {
        // Returning User entities here would drag password hashes into the response.
        var dto = Make.Project(users: [Make.User()]).ToProjectWithIdDto();

        Assert.IsType<List<Guid>>(dto.UserIds);
    }

    [Fact]
    public void ToProjectWithIdDto_ReturnsEmptyCollections_WhenNavigationsWereNotLoaded()
    {
        var project = Make.Project();
        project.WorkTasks = null!;
        project.Users = null!;

        var dto = project.ToProjectWithIdDto();

        Assert.Empty(dto.WorkTasks);
        Assert.Empty(dto.UserIds);
    }

    [Fact]
    public void ToProject_CopiesTheClientSuppliedFields()
    {
        var project = Make.ProjectDto(name: "Apollo", description: "Moon landing").ToProject();

        Assert.Equal("Apollo", project.Name);
        Assert.Equal("Moon landing", project.Description);
    }

    [Fact]
    public void ToProject_LeavesIdEmpty_SoTheDatabaseAssignsIt()
        => Assert.Equal(Guid.Empty, Make.ProjectDto().ToProject().Id);

    [Fact]
    public void ToProject_StartsWithNoMembersAndNoTasks()
    {
        var project = Make.ProjectDto().ToProject();

        Assert.Empty(project.Users);
        Assert.Empty(project.WorkTasks);
    }
}
