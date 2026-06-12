# SMS Proxy Hub

Multi-provider SMS proxy with webhook callbacks. Send SMS through SmsGate or Twilio, get replies forwarded to your app via webhooks with payload echo.

## Projects

- **Api** - ASP.NET Core backend, PostgreSQL, FluentMigrator, JWT + API token auth
- **UI** - Blazor WASM frontend with Havit Bootstrap
- **Shared** - DTOs, Refit interfaces, phone normalization
- **Client** - NuGet package (`SmsProxyHub.Client`) for consuming apps
- **Migrations** - FluentMigrator migrations

## Providers

Providers implement `ISmsProvider`:

- **SmsGate** - private Android SMS gateway (Basic auth -> JWT -> REST). Uses NSwag-generated client from the [OpenAPI spec](https://docs.sms-gate.app/integration/api/). Supports device selection for multi-user setups.
- **Twilio** - standard Twilio REST API

Adding a new provider:
1. Implement `ISmsProvider`
2. Add a `SmsConnectionConfig` subtype with `[JsonSubtypes.KnownSubType]`
3. Register in `Program.cs`
4. No DB migration needed -- config is stored as polymorphic JSON

## Payload echo

Include an optional `payload` (any JSON string) when sending. When the recipient replies, the webhook to your URL includes the original `payload` back -- so you can correlate replies without storing state.

## Client NuGet usage

```csharp
// Create per-request from IHttpClientFactory (token can change at runtime)
var httpClient = httpClientFactory.CreateClient();
httpClient.BaseAddress = new Uri("https://sms-proxy-hub.example.com");
var client = new SmsProxyHubClient(httpClient, apiToken);

var response = await client.SendSmsAsync(
    connectionId: Guid.Parse("..."),
    phoneNumber: "+15551234567",
    message: "Hello!",
    payload: new { clinicId = "abc", patientId = 123 });
```

## Webhook callbacks

sms-proxy-hub sends webhook POSTs to your registered URLs on these events:

| Event | When | Fields |
|-------|------|--------|
| `SmsSent` | SMS accepted by provider | `phone`, `message`, `originalPayload` |
| `SmsFailed` | Provider rejected the SMS | `phone`, `message`, `originalPayload`, `reason` |
| `SmsReply` | Someone replied to an SMS you sent | `phone`, `message`, `originalPayload` |

Payload format:

```json
{
  "event": "SmsReply",
  "phone": "+15551234567",
  "message": "Yes, confirmed",
  "originalPayload": "{\"clinicId\":\"abc\",\"patientId\":123}",
  "connectionId": "a1b2c3d4-...",
  "reason": null,
  "timestamp": "2026-06-12T20:30:00Z"
}
```

### How replies are matched

sms-proxy-hub finds the most recent outbound SMS to the replying phone number on the same connection that hasn't been replied to yet. The `originalPayload` from that message is echoed back. After matching, the outbound message is marked as replied so subsequent replies match older messages.

### Webhook auto-registration

When you create a connection, sms-proxy-hub automatically registers the webhook with the provider:
- **SmsGate** -- registers a device-specific `sms:received` webhook via the API
- **Twilio** -- updates the phone number's `SmsUrl` to point to the proxy

No manual setup needed -- just create the connection in the UI and it's ready.

## Docker

```bash
docker build -t sms-proxy-hub .
docker run -e DATABASE_URL=postgres://user:pass@host:5432/db \
           -e Jwt__Key=your-32-char-secret \
           -p 80:80 sms-proxy-hub
```

## NuGet publishing

Pushing to `master` triggers the GitHub Actions workflow that builds and publishes `SmsProxyHub.Client` to nuget.org.

## Re-generating the SmsGate client

NSwag runs automatically on build via MSBuild. To regenerate manually:

```bash
cd Api && nswag run nswag.json
```