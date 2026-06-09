using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using TaskTracker.Models;
using TaskTracker.Services;
using Xunit;

namespace TaskTracker.Tests
{
    public class ProjectServiceTests
    {
        private readonly IFixture _fixture;
        private readonly Mock<IProjectRepository> _projectRepoMock;
        private readonly Mock<ILogger<ProjectService>> _loggerMock;
        private readonly ProjectService _sut;

        public ProjectServiceTests()
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            _projectRepoMock = _fixture.Freeze<Mock<IProjectRepository>>();
            _loggerMock = _fixture.Freeze<Mock<ILogger<ProjectService>>>();
            _sut = new ProjectService(_projectRepoMock.Object, _loggerMock.Object);
        }

        [Theory]
        [InlineData("Project A")]
        [InlineData("Project B")]
        public async Task CreateProjectAsync_ShouldReturnSuccess_WhenValidData(string name)
        {
            // Arrange
            var createDto = _fixture.Build<CreateUpdateProjectDto>().With(x => x.Name, name).Create();
            var savedProject = _fixture.Build<Project>().With(x => x.Name, name).Create();
            
            _projectRepoMock
                .Setup(repo => repo.Create(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(savedProject);

            // Act
            var result = await _sut.CreateProjectAsync(createDto, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal(name, result.Value.Name);
            _projectRepoMock.Verify(repo => repo.Create(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateProjectAsync_ShouldReturnFailure_WhenDtoIsNull()
        {
            // Arrange
            CreateUpdateProjectDto createDto = null;

            // Act
            var result = await _sut.CreateProjectAsync(createDto, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Equal(Error.BadRequest, result.Error);
        }

        [Theory]
        [InlineData("d18f5e92-7f99-4a0b-8d8a-36b0c26eb390")]
        public async Task GetProjectByIdAsync_ShouldReturnSuccess_WhenProjectExists(string idStr)
        {
            // Arrange
            var id = Guid.Parse(idStr);
            var project = _fixture.Build<Project>().With(x => x.Id, id).Create();
            
            _projectRepoMock
                .Setup(repo => repo.GetById(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(project);

            // Act
            var result = await _sut.GetProjectByIdAsync(id, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal(project.Name, result.Value.Name);
            _projectRepoMock.Verify(repo => repo.GetById(id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("d18f5e92-7f99-4a0b-8d8a-36b0c26eb390", true)]
        [InlineData("d18f5e92-7f99-4a0b-8d8a-36b0c26eb390", false)]
        public async Task DeleteProjectAsync_ShouldReturnExpectedResult_BasedOnRepoDelete(string idStr, bool isDeleted)
        {
            // Arrange
            var id = Guid.Parse(idStr);
            _projectRepoMock
                .Setup(repo => repo.Delete(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(isDeleted);

            // Act
            var result = await _sut.DeleteProjectAsync(id, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(isDeleted, result.IsSuccess);
            _projectRepoMock.Verify(repo => repo.Delete(id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetProjectByIdWithTasksAsync_ShouldReturnProject_WhenProjectExists()
        {
            // Arrange
            var id = Guid.NewGuid();
            var project = _fixture.Build<Project>().With(x => x.Id, id).Create();
            _projectRepoMock
                .Setup(repo => repo.GetByIdWithTasks(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(project);

            // Act
            var result = await _sut.GetProjectByIdWithTasksAsync(id, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal(project.Name, result.Value.Name);
            _projectRepoMock.Verify(repo => repo.GetByIdWithTasks(id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetProjectsAsync_ShouldReturnListOfProjectDtos()
        {
            // Arrange
            var projects = _fixture.CreateMany<Project>(3).ToList();
            _projectRepoMock
                .Setup(repo => repo.GetAll(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);

            // Act
            var result = await _sut.GetProjectsAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            _projectRepoMock.Verify(repo => repo.GetAll(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetProjectsWithIdAsync_ShouldReturnListOfProjects()
        {
            // Arrange
            var projects = _fixture.CreateMany<Project>(3).ToList();
            _projectRepoMock
                .Setup(repo => repo.GetAll(It.IsAny<CancellationToken>()))
                .ReturnsAsync(projects);

            // Act
            var result = await _sut.GetProjectsWithIdAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            _projectRepoMock.Verify(repo => repo.GetAll(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateProjectAsync_ShouldReturnSuccess_WhenProjectIsUpdated()
        {
            // Arrange
            var id = Guid.NewGuid();
            var existingProject = _fixture.Build<Project>().With(x => x.Id, id).Create();
            var updateDto = _fixture.Create<CreateUpdateProjectDto>();
            
            _projectRepoMock
                .Setup(repo => repo.GetById(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingProject);
                
            _projectRepoMock
                .Setup(repo => repo.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.UpdateProjectAsync(id, updateDto, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal(updateDto.Name, result.Value.Name);
            _projectRepoMock.Verify(repo => repo.Update(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
