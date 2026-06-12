using Api.Interfaces;

namespace Api.Providers;

public sealed class SmsProviderFactory(IEnumerable<ISmsProvider> providers) : ISmsProviderFactory
{
    private readonly Dictionary<string, ISmsProvider> _providers =
        providers.ToDictionary(p => p.ProviderType, StringComparer.OrdinalIgnoreCase);

    public ISmsProvider GetProvider(string providerType)
    {
        if (_providers.TryGetValue(providerType, out var provider))
            return provider;

        throw new InvalidOperationException($"No SMS provider registered for type '{providerType}'.");
    }
}
