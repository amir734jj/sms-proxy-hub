using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Api.Middleware;

/// <summary>
/// Allows API token authentication as an alternative to JWT.
/// Clients send: Authorization: Bearer {api-token}
/// If the token is not a valid JWT, we check if it's an API token.
/// </summary>
public sealed class ApiTokenAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IApiTokenService apiTokenService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "ApiToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authHeader["Bearer ".Length..].Trim();

        // Skip if it looks like a JWT (contains dots)
        if (token.Contains('.'))
        {
            return AuthenticateResult.NoResult();
        }

        var result = await apiTokenService.ValidateTokenAsync(token);
        if (result is null)
        {
            return AuthenticateResult.NoResult();
        }

        var (userId, _) = result.Value;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, Shared.Roles.User)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
