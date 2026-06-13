using Refit;
using Shared.Contracts;
using Shared.Contracts.Interfaces;

namespace UI.Services;

public sealed class ApiService(
    IAuthApi authApi,
    IUsersApi usersApi,
    IProfileApi profileApi,
    IConnectionsApi connectionsApi,
    IMessagesApi messagesApi,
    IWebhooksApi webhooksApi,
    IApiTokensApi apiTokensApi,
    AuthService auth)
{
    // Auth
    public async Task<string?> LoginAsync(string email, string password)
    {
        try
        {
            var response = await authApi.LoginAsync(new LoginRequest(email, password));
            await auth.SetTokenAsync(response.Token, response.Role, response.UserId);
            try
            {
                var me = await authApi.MeAsync();
                await auth.SetDisplayNameAsync(me.DisplayName);
            }
            catch { /* non-critical */ }
            return null;
        }
        catch (ApiException ex) when ((int)ex.StatusCode == 403)
        {
            return "Your account is pending activation by an administrator.";
        }
        catch (ApiException)
        {
            return "Invalid email or password.";
        }
    }

    public async Task<(bool Success, bool IsActive, string? Error)> RegisterAsync(
        string email, string password, string passwordConfirm)
    {
        try
        {
            var response = await authApi.RegisterAsync(new RegisterRequest(email, password, passwordConfirm));
            return (true, response.IsActive, null);
        }
        catch (ApiException)
        {
            return (false, false, "Registration failed. Password must be at least 8 characters.");
        }
    }

    public Task<MeResponse> GetProfileAsync() => authApi.MeAsync();

    public async Task UpdateProfileAsync(string? displayName)
    {
        await profileApi.UpdateAsync(new UpdateProfileRequest(displayName));
        await auth.SetDisplayNameAsync(displayName);
    }

    public Task ChangePasswordAsync(string current, string newPw, string confirm)
        => profileApi.ChangePasswordAsync(new ChangePasswordRequest(current, newPw, confirm));

    // Users (Admin)
    public Task<List<UserDto>> GetUsersAsync() => usersApi.GetAllAsync();
    public Task ActivateUserAsync(Guid id) => usersApi.ActivateAsync(id);
    public Task DeactivateUserAsync(Guid id) => usersApi.DeactivateAsync(id);
    public Task DeleteUserAsync(Guid id) => usersApi.DeleteAsync(id);

    // Connections
    public Task<List<SmsConnectionDto>> GetConnectionsAsync() => connectionsApi.GetAllAsync();
    public Task<SmsConnectionDto> CreateConnectionAsync(CreateConnectionRequest req) => connectionsApi.CreateAsync(req);
    public Task UpdateConnectionAsync(Guid id, UpdateConnectionRequest req) => connectionsApi.UpdateAsync(id, req);
    public Task DeleteConnectionAsync(Guid id) => connectionsApi.DeleteAsync(id);
    public Task<List<SmsGateDeviceDto>> GetSmsGateDevicesAsync(SmsGateConnectionConfig config) => connectionsApi.GetSmsGateDevicesAsync(config);
    public Task ReorderConnectionsAsync(List<Guid> orderedIds) => connectionsApi.ReorderAsync(orderedIds);

    // Messages
    public Task<BulkSendSmsResponse> SendSmsAsync(SendSmsRequest req) => messagesApi.SendAsync(req);
    public Task<List<SmsMessageDto>> GetMessagesAsync() => messagesApi.GetAllAsync();
    public Task<List<SmsMessageDto>> GetMessagesByConnectionAsync(Guid connId) => messagesApi.GetByConnectionAsync(connId);
    public Task<UsageStatsDto> GetUsageAsync(int days = 30) => messagesApi.GetUsageAsync(days);

    // Webhooks
    public Task<List<WebhookSubscriptionDto>> GetWebhooksAsync() => webhooksApi.GetAllAsync();
    public Task<WebhookSubscriptionDto> CreateWebhookAsync(CreateWebhookRequest req) => webhooksApi.CreateAsync(req);
    public Task DeleteWebhookAsync(Guid id) => webhooksApi.DeleteAsync(id);
    public Task<List<WebhookDeliveryDto>> GetWebhookDeliveriesAsync(int limit = 50) => webhooksApi.GetDeliveriesAsync(limit);
    public Task ReplayWebhookAsync(Guid deliveryId) => webhooksApi.ReplayAsync(deliveryId);

    // API Tokens
    public Task<List<ApiTokenDto>> GetApiTokensAsync() => apiTokensApi.GetAllAsync();
    public Task<ApiTokenDto> CreateApiTokenAsync(string name) => apiTokensApi.CreateAsync(new CreateApiTokenRequest(name));
    public Task DeleteApiTokenAsync(Guid id) => apiTokensApi.DeleteAsync(id);
}
