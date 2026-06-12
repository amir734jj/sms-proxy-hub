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