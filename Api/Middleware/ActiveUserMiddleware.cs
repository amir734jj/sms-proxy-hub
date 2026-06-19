using Api.Data.Entities;
using Api.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Serilog.Context;

namespace Api.Middleware;

public sealed class ActiveUserMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    public async Task InvokeAsync(HttpContext context, UserManager<User> userManager, IMemoryCache cache)
    {
        if (context.User.Identity is not { IsAuthenticated: true })
        {
            await next(context);
            return;
        }

        var userId = context.User.GetUserId().ToString();
        var cacheKey = $"active_user:{userId}";

        if (!cache.TryGetValue(cacheKey, out CachedUser? cached) || cached is null)
        {
            var user = await userManager.FindByIdAsync(userId);
            cached = new CachedUser(user is { IsActive: true }, user?.UserName);
            cache.Set(cacheKey, cached, CacheDuration);
        }

        if (!cached.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Account has been deactivated." });
            return;
        }

        using (LogContext.PushProperty("UserName", cached.UserName))
        {
            await next(context);
        }
    }

    private sealed record CachedUser(bool IsActive, string? UserName);
}
