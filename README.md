# SMS Proxy Hub

A multi-provider SMS proxy with webhook callback support.

## Architecture

- **Api** – ASP.NET Core API with PostgreSQL, FluentMigrator, JWT + API token auth
- **UI** – Blazor WebAssembly + Havit Bootstrap components
- **Shared** – DTOs, Refit interfaces, phone normalization
- **Client** – NuGet package (`SmsProxyHub.Client`) for consumers
- **Migrations** – FluentMigrator database migrations

## SMS Providers

Providers implement `ISmsProvider`. Currently supported:

- **SmsGate** – Basic auth → JWT token → REST API
- **Twilio** – Account SID + Auth Token

To add a new provider:
1. Create a class implementing `ISmsProvider`
2. Add a new `SmsConnectionConfig` subtype with `[JsonSubtypes.KnownSubType]`
3. Register in `Program.cs` DI
4. No database migration needed — config is stored as polymorphic JSON

## Payload Echo

When sending an SMS, include an optional `payload` (any JSON string). When the recipient replies, the webhook callback to your registered URL will include the original `payload` — enabling stateless correlation.

## Consumer Usage (NuGet)

```csharp
services.AddSmsProxyHub("https://sms-proxy-hub.example.com", "your-api-token");

// Then inject and use:
var response = await smsClient.SendSmsAsync(
    connectionId: Guid.Parse("..."),
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

## NuGet Publishing

Push a tag `v1.0.0` to trigger the GitHub Actions workflow that builds and publishes `SmsProxyHub.Client` to nuget.org.
