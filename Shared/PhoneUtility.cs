using PhoneNumbers;

namespace Shared;

public static class PhoneUtility
{
    private static readonly PhoneNumberUtil PhoneNumberUtil = PhoneNumberUtil.GetInstance();

    public static string? NormalizePhoneNumber(string? phoneNumberRaw)
    {
        if (string.IsNullOrWhiteSpace(phoneNumberRaw)) return phoneNumberRaw;

        try
        {
            var phoneNumber = PhoneNumberUtil.Parse(phoneNumberRaw, "US");
            return PhoneNumberUtil.Format(phoneNumber, PhoneNumberFormat.E164);
        }
        catch
        {
            return phoneNumberRaw;
        }
    }
}
