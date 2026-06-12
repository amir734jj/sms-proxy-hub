using Npgsql;

namespace Api.Utilities;

public static class ConnectionStringUtility
{
    /// <summary>
    /// Converts DATABASE_URL (Heroku-style) to a PostgreSQL connection string.
    /// </summary>
    public static string ConnectionStringUrlToPgResource(string? connectionStringUrl)
    {
        if (string.IsNullOrWhiteSpace(connectionStringUrl))
        {
            throw new InvalidOperationException("DATABASE_URL environment variable is not set.");
        }

        // Already a connection string
        if (connectionStringUrl.StartsWith("Host=") || connectionStringUrl.StartsWith("Server="))
        {
            return connectionStringUrl;
        }

        if (!Uri.TryCreate(connectionStringUrl, UriKind.Absolute, out var url))
        {
            throw new InvalidOperationException("DATABASE_URL is not a valid URL.");
        }

        var userInfo = url.UserInfo.Split(':');
        var port = url.Port > 0 ? url.Port : 5432;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = url.Host,
            Port = port,
            Username = userInfo.Length > 0 ? userInfo[0] : string.Empty,
            Password = userInfo.Length > 1 ? userInfo[1] : string.Empty,
            Database = url.AbsolutePath.TrimStart('/'),
            ApplicationName = "sms-proxy-hub",
            SslMode = SslMode.Prefer,
            Pooling = true,
            MaxPoolSize = 20,
            CommandTimeout = 0,
            Timeout = (int)TimeSpan.FromMinutes(1).TotalSeconds
        };

        return builder.ToString();
    }
}
