using System.Reflection.Metadata.Ecma335;
using TaskTracker.Models;

namespace TaskTracker.Dtos;

public record ProjectDto(string Name, string Description, List<WorkTaskDto> WorkTasks);

public record CreateUpdateProjectDto(string Name, string Description);