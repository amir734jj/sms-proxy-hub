using Microsoft.Extensions.DependencyInjection;

namespace SmsProxyHub.Client;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="SmsProxyHubClient"/> with the DI container.
    /// </summary>
    public static IServiceCollection AddSmsProxyHub(
        this IServiceCollection services, string baseUrl, string apiToken)
    {
        services.AddHttpClient<SmsProxyHubClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddTypedClient((httpClient, _) => new SmsProxyHubClient(httpClient, apiToken));

        return services;
    }
}
