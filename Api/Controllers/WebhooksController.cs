using Api.Extensions;
using Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;

namespace Api.Controllers;

[ApiController]
[Route("api/webhooks")]
[Authorize]
public sealed class WebhooksController(IWebhookService webhookService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await webhookService.GetAllForUserAsync(User.GetUserId()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWebhookRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Url))
            return BadRequest("Webhook URL is required.");

        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            return BadRequest("Invalid webhook URL.");
        }

        try
        {
            var dto = await webhookService.CreateAsync(User.GetUserId(), req);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await webhookService.DeleteAsync(User.GetUserId(), id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("deliveries")]
    public async Task<IActionResult> GetDeliveries([FromQuery] int limit = 50)
    {
        return Ok(await webhookService.GetDeliveriesForUserAsync(User.GetUserId(), limit));
    }
}
