using Api.Extensions;
using Api.Interfaces;
using Api.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;

namespace Api.Controllers;

[ApiController]
[Route("api/connections")]
[Authorize]
public sealed class ConnectionsController(IConnectionService connectionService, SmsGateProvider smsGateProvider, IDeviceStatusService deviceStatusService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await connectionService.GetAllForUserAsync(User.GetUserId()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConnectionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Connection name is required.");

        var dto = await connectionService.CreateAsync(User.GetUserId(), req);
        return Ok(dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConnectionRequest req)
    {
        var updated = await connectionService.UpdateAsync(User.GetUserId(), id, req);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await connectionService.DeleteAsync(User.GetUserId(), id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/revalidate-webhook")]
    public async Task<IActionResult> RevalidateWebhook(Guid id)
    {
        var result = await connectionService.RevalidateWebhookAsync(User.GetUserId(), id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/webhooks")]
    public async Task<IActionResult> GetWebhooks(Guid id)
    {
        var webhooks = await connectionService.GetRegisteredWebhooksAsync(User.GetUserId(), id);
        return webhooks is null ? NotFound() : Ok(webhooks);
    }

    [HttpGet("{id:guid}/device-status")]
    public async Task<IActionResult> GetDeviceStatus(Guid id)
    {
        if (!await connectionService.UserOwnsConnectionAsync(User.GetUserId(), id))
            return NotFound();
        return Ok(deviceStatusService.Get(id));
    }

    [HttpPost("smsgate-devices")]
    public async Task<IActionResult> GetSmsGateDevices([FromBody] SmsGateConnectionConfig config)
    {
        var devices = await smsGateProvider.GetDevicesAsync(config);
        return Ok(devices.Select(d => new SmsGateDeviceDto(d.Id ?? "", d.Name ?? "", d.LastSeen)));
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<Guid> orderedIds)
    {
        var reordered = await connectionService.ReorderAsync(User.GetUserId(), orderedIds);
        return reordered ? Ok() : BadRequest("Invalid connection IDs.");
    }

    [HttpPut("{id:guid}/retention-days")]
    public async Task<IActionResult> UpdateRetentionDays(Guid id, [FromBody] int days)
    {
        if (days < 1 || days > 3650)
            return BadRequest("Retention days must be between 1 and 3650.");

        var updated = await connectionService.UpdateMessageRetentionDaysAsync(User.GetUserId(), id, days);
        return updated ? NoContent() : NotFound();
    }
}
