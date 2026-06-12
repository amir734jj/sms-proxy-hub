using System.Text;

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

        if (connectionStringUrl.StartsWith("Host=") || connectionStringUrl.StartsWith("Server="))
        {
            return connectionStringUrl;
        }

        var uri = new Uri(connectionStringUrl);
        var userInfo = uri.UserInfo.Split(':');
        var sb = new StringBuilder();
        sb.Append($"Host={uri.Host};");
        if (uri.Port > 0) sb.Append($"Port={uri.Port};");
        sb.Append($"Username={userInfo[0]};");
        if (userInfo.Length > 1) sb.Append($"Password={userInfo[1]};");
        sb.Append($"Database={uri.AbsolutePath.TrimStart('/')};");
        sb.Append("SSL Mode=Prefer;Trust Server Certificate=true;");
        return sb.ToString();
    }
}
