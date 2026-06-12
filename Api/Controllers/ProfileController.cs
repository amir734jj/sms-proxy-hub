using Api.Data.Entities;
using Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;

namespace Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController(UserManager<User> userManager) : ControllerBase
{
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest req)
    {
        var user = await userManager.FindByIdAsync(User.GetUserId().ToString());
        if (user is null) return NotFound();

        user.DisplayName = req.DisplayName?.Trim();
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (req.NewPassword != req.NewPasswordConfirm)
            return BadRequest("Passwords do not match.");

        var user = await userManager.FindByIdAsync(User.GetUserId().ToString());
        if (user is null) return NotFound();

        var result = await userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return NoContent();
    }
}
