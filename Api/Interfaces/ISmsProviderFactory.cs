namespace Api.Interfaces;

public interface ISmsProviderFactory
{
    ISmsProvider GetProvider(string providerType);
}
