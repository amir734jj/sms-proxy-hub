# SMS Proxy Hub

Multi-provider SMS proxy with webhook callbacks. Send SMS through SmsGate or Twilio, get replies forwarded to your app via webhooks with payload echo.

## Projects

- **Api** – ASP.NET Core backend, PostgreSQL, FluentMigrator, JWT + API token auth
- **UI** – Blazor WASM frontend with Havit Bootstrap
- **Shared** – DTOs, Refit interfaces, phone normalization
- **Client** – NuGet package (`SmsProxyHub.Client`) for consuming apps
- **Migrations** – FluentMigrator migrations

## Providers

Providers implement `ISmsProvider`:

- **SmsGate** – private Android SMS gateway (Basic auth → JWT → REST). Uses NSwag-generated client from the [OpenAPI spec](https://docs.sms-gate.app/integration/api/). Supports device selection for multi-user setups.
- **Twilio** – standard Twilio REST API

Adding a new provider:
1. Implement `ISmsProvider`
2. Add a `SmsConnectionConfig` subtype with `[JsonSubtypes.KnownSubType]`
3. Register in `Program.cs`
4. No DB migration needed — config is stored as polymorphic JSON

## Payload echo

Include an optional `payload` (any JSON string) when sending. When the recipient replies, the webhook to your URL includes the original `payload` back — so you can correlate replies without storing state.

## Client NuGet usage

```csharp
services.AddSmsProxyHub("https://sms-proxy-hub.example.com", "your-api-token");

var response = await smsClient.SendSmsAsync(
    connectionId: null, // null = try all connections by priority
    phoneNumber: "+15551234567",
    message: "Hello!",
    payload: new { clinicId = "abc", patientId = 123 });
```

## Webhook callbacks

When someone replies to an SMS, sms-proxy-hub forwards the reply to all webhook subscriptions registered for that connection. Your app receives a POST like:

```json
{
  "fromPhone": "+15551234567",
  "message": "Yes, confirmed",
  "originalPayload": "{\"clinicId\":\"abc\",\"patientId\":123}",
  "connectionId": "a1b2c3d4-...",
  "receivedAt": "2026-06-12T20:30:00Z"
}
```

If a webhook secret is configured, the request includes HMAC-SHA256 signature headers:
- `X-Signature` — hex-encoded HMAC of `body + timestamp`
- `X-Timestamp` — unix timestamp (seconds)

Verify in your app:
```csharp
var expected = HMACSHA256(Encoding.UTF8.GetBytes(secret))
    .ComputeHash(Encoding.UTF8.GetBytes(body + timestamp));
// compare hex-encoded expected with X-Signature header
```

### How replies are matched

sms-proxy-hub finds the most recent outbound SMS to the replying phone number on the same connection that hasn't been replied to yet. The `originalPayload` from that message is echoed back. After matching, the outbound message is marked as replied so subsequent replies match older messages.

### Webhook auto-registration

When you create a SmsGate connection, sms-proxy-hub automatically registers a device-specific webhook with the SMS Gate server. No manual `curl` needed — just create the connection in the UI and it's ready.

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