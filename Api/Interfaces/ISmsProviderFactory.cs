using Shared.Contracts;

namespace Api.Interfaces;

public interface ISmsProviderFactory
{
    ISmsProvider GetProvider(SmsProviderType providerType);
}
