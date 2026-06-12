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
    payload: "{\"clinicId\":\"abc\",\"patientId\":123}");
```

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

```bash
cd Api && nswag run nswag.json
```
