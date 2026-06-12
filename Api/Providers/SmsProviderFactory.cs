using Api.Interfaces;
using Shared.Contracts;

namespace Api.Providers;

public sealed class SmsProviderFactory(IEnumerable<ISmsProvider> providers) : ISmsProviderFactory
{
    private readonly Dictionary<SmsProviderType, ISmsProvider> _providers =
        providers.ToDictionary(p => p.ProviderType);

    public ISmsProvider GetProvider(SmsProviderType providerType)
    {
        if (_providers.TryGetValue(providerType, out var provider))
            return provider;

        throw new InvalidOperationException($"No SMS provider registered for type '{providerType}'.");
    }
}
