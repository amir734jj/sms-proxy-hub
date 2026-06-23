using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Api.Logging;

/// <summary>
/// Serilog enricher that masks sensitive data in every log event — phone numbers,
/// bearer/JWT tokens, and known free-text PII fields (message text, patient name,
/// patient phone, payload) — keeping only the last 4 characters. Runs purely inside
/// the Serilog pipeline, so all sinks receive already-redacted values.
/// </summary>
public sealed class RedactingEnricher : ILogEventEnricher
{
    // Property values that are atomic PII on their own — mask the whole value.
    private static readonly HashSet<string> WholeValueKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "PatientName", "Patient", "PatientPhone", "Phone", "To"
    };

    // "field":"value" pairs whose value is sensitive (e.g. inside a logged JSON body).
    private static readonly Regex JsonPiiRegex = new(
        @"(?<pre>""(?:message|patientName|patientPhone|payload|to)""\s*:\s*"")(?<val>(?:\\.|[^""\\])*)(?<post>"")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex JwtRegex = new(
        @"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+",
        RegexOptions.Compiled);

    private static readonly Regex BearerRegex = new(
        @"Bearer\s+(?<token>[A-Za-z0-9._\-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Phone-like sequences; an additional digit-count guard avoids masking dates/ids.
    private static readonly Regex PhoneRegex = new(
        @"\+?\d[\d\s().\-]{8,}\d",
        RegexOptions.Compiled);

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToArray())
        {
            if (property.Value is not ScalarValue { Value: string text } || text.Length == 0)
            {
                continue;
            }

            var redacted = WholeValueKeys.Contains(property.Key)
                ? MaskKeepLast4(text)
                : RedactContent(text);

            if (redacted != text)
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, new ScalarValue(redacted)));
            }
        }
    }

    private static string RedactContent(string input)
    {
        var result = JsonPiiRegex.Replace(input, m =>
            m.Groups["pre"].Value + MaskKeepLast4(m.Groups["val"].Value) + m.Groups["post"].Value);

        result = JwtRegex.Replace(result, m => MaskKeepLast4(m.Value));

        result = BearerRegex.Replace(result, m => "Bearer " + MaskKeepLast4(m.Groups["token"].Value));

        result = PhoneRegex.Replace(result, m =>
        {
            var digits = 0;
            foreach (var c in m.Value)
            {
                if (char.IsDigit(c)) digits++;
            }

            return digits >= 10 ? MaskKeepLast4(m.Value) : m.Value;
        });

        return result;
    }

    private static string MaskKeepLast4(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        var stars = Math.Min(value.Length - 4, 6);
        return new string('*', stars) + value[^4..];
    }
}
