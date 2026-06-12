using Api.Extensions;
using Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;

namespace Api.Controllers;

[ApiController]
[Route("api/tokens")]
[Authorize]
public sealed class ApiTokensController(IApiTokenService apiTokenService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await apiTokenService.GetAllForUserAsync(User.GetUserId()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiTokenRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Token name is required.");

        var dto = await apiTokenService.CreateAsync(User.GetUserId(), req.Name);
        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await apiTokenService.DeleteAsync(User.GetUserId(), id);
        return deleted ? NoContent() : NotFound();
    }
}
