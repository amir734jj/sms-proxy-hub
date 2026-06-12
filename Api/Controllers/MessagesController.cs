using Api.Extensions;
using Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;

namespace Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public sealed class MessagesController(
    IMessageService messageService,
    IConnectionService connectionService) : ControllerBase
{
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendSmsRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PhoneNumber) || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest("Phone number and message are required.");

        if (!await connectionService.UserOwnsConnectionAsync(User.GetUserId(), req.ConnectionId))
            return NotFound("Connection not found.");

        var (messageId, success) = await messageService.SendAsync(
            req.ConnectionId, req.PhoneNumber, req.Message, req.Payload);

        return success
            ? Ok(new SendSmsResponse(messageId, "sent"))
            : StatusCode(502, new SendSmsResponse(null, "failed"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await messageService.GetAllForUserAsync(User.GetUserId()));
    }

    [HttpGet("connection/{connectionId:guid}")]
    public async Task<IActionResult> GetByConnection(Guid connectionId)
    {
        if (!await connectionService.UserOwnsConnectionAsync(User.GetUserId(), connectionId))
            return NotFound();

        return Ok(await messageService.GetByConnectionAsync(connectionId));
    }
}
