using Api.Data.Entities;
using EfCoreRepository;

namespace Api.Data.Profiles;

public class SmsConnectionProfile : EntityProfile<SmsConnection>
{
    public SmsConnectionProfile() { MapAll(); }
}

public class SmsMessageProfile : EntityProfile<SmsMessage>
{
    public SmsMessageProfile() { MapAll(); }
}

public class WebhookSubscriptionProfile : EntityProfile<WebhookSubscription>
{
    public WebhookSubscriptionProfile() { MapAll(); }
}

public class ApiTokenProfile : EntityProfile<ApiToken>
{
    public ApiTokenProfile() { MapAll(); }
}

public class WebhookDeliveryProfile : EntityProfile<WebhookDelivery>
{
    public WebhookDeliveryProfile() { MapAll(); }
}
