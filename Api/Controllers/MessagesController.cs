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
        if (req.PhoneNumbers is null || req.PhoneNumbers.Length == 0 || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest("Phone numbers and message are required.");

        if (req.Message.Length > 1600)
            return BadRequest("Message too long (max 1600 characters).");

        var userId = User.GetUserId();

        if (req.ConnectionId is not null &&
            !await connectionService.UserOwnsConnectionAsync(userId, req.ConnectionId.Value))
        {
            return NotFound("Connection not found.");
        }

        var results = new List<SendSmsResponse>();
        foreach (var phone in req.PhoneNumbers)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                results.Add(new SendSmsResponse(null, "failed"));
                continue;
            }

            var (messageId, success, usedConnectionId) = await messageService.SendAsync(
                userId, req.ConnectionId, phone, req.Message, req.Payload);

            results.Add(success
                ? new SendSmsResponse(messageId, "sent", usedConnectionId)
                : new SendSmsResponse(null, "failed"));
        }

        return Ok(new BulkSendSmsResponse(results));
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

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage([FromQuery] int days = 30)
    {
        if (days < 1) days = 1;
        if (days > 365) days = 365;

        return Ok(await messageService.GetUsageForUserAsync(User.GetUserId(), days));
    }
}
