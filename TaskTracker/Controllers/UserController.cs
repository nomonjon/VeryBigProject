using Microsoft.AspNetCore.Mvc;
using TaskTracker.Dtos;
using TaskTracker.Interfaces;
using KeyNotFoundException = System.Collections.Generic.KeyNotFoundException;

namespace TaskTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await userService.GetUsersAsync(cancellationToken);
        return Ok(users);
    }
    [HttpGet("withId")]
    public async Task<IActionResult> GetUsersWithId(CancellationToken cancellationToken)
    {

        var users = await userService.GetUsersWithIdAsync(cancellationToken);
        return Ok(users);

    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken)
    {

        var user = await userService.GetUserByIdAsync(id, cancellationToken);
        
        return user.ToResponse();

    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUpdateUserDto newUser, CancellationToken cancellationToken)
    {
        try
        {
            var createdUser = await userService.CreateUserAsync(newUser, cancellationToken);
            return Ok(createdUser);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser([FromRoute] Guid id, [FromBody] CreateUpdateUserDto updatedUser, CancellationToken cancellationToken)
    {
        try
        {
            var user = await userService.UpdateUserAsync(id, updatedUser, cancellationToken);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateUserPartly([FromRoute] Guid id, [FromBody] CreateUpdateUserDto updatedUser, CancellationToken cancellationToken)
    {
        try
        {
            var user = await userService.UpdatePartly(id, updatedUser, cancellationToken);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var isDeleted = await userService.DeleteUserAsync(id, cancellationToken);

        return isDeleted.ToResponse();
    }
}
