using Api.Data.Entities;
using Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Contracts;

namespace Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public sealed class UsersController(UserManager<User> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var allUsers = userManager.Users.ToList();
        var result = new List<UserDto>();

        foreach (var user in allUsers)
        {
            var userRoles = await userManager.GetRolesAsync(user);
            result.Add(new UserDto(user.Id, user.Email!, user.IsActive, userRoles.ToList(), user.LastLoginAt));
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        user.IsActive = true;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        if (id == User.GetUserId()) return BadRequest("Cannot deactivate yourself.");

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        user.IsActive = false;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (id == User.GetUserId()) return BadRequest("Cannot delete yourself.");

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        await userManager.DeleteAsync(user);
        return NoContent();
    }
}
