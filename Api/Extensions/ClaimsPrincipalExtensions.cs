using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal user)
    {
        public Guid GetUserId()
        {
            var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                        ?? throw new InvalidOperationException("User ID claim missing.");
            return Guid.Parse(value);
        }

        public string GetEmail()
        {
            return user.FindFirstValue(JwtRegisteredClaimNames.Email)
                   ?? throw new InvalidOperationException("Email claim missing.");
        }

        public string? TryGetEmail()
        {
            return user.FindFirstValue(JwtRegisteredClaimNames.Email);
        }
    }
}
