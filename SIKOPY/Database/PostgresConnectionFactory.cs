using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace SIKOPY.Database;

/// <summary>
/// Opens PostgreSQL connections. The connection string is read from, in order:
/// <list type="number">
/// <item>the <c>SIKOPY_DATABASE_CONNECTION_STRING</c> environment variable;</item>
/// <item><c>database.local.json</c> next to the application
/// (<c>{ "ConnectionString": "..." }</c>), which is git-ignored.</item>
/// </list>
/// </summary>
public static class PostgresConnectionFactory
{
    public const string ConnectionStringEnvironmentVariable =
        "SIKOPY_DATABASE_CONNECTION_STRING";

    public const string LocalSettingsFileName = "database.local.json";

    public static NpgsqlConnection Create() => new(GetConnectionString());

    private static string GetConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        var settingsPath = Path.Combine(AppContext.BaseDirectory, LocalSettingsFileName);
        if (File.Exists(settingsPath))
        {
            try
            {
                using var stream = File.OpenRead(settingsPath);
                using var document = JsonDocument.Parse(stream);
                if (document.RootElement.TryGetProperty("ConnectionString", out var value) &&
                    value.GetString() is { Length: > 0 } fromFile &&
                    !string.IsNullOrWhiteSpace(fromFile))
                {
                    return fromFile;
                }
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    $"{LocalSettingsFileName} is not valid JSON: {exception.Message}", exception);
            }
        }

        throw new InvalidOperationException(
            $"No database connection is configured. Set the {ConnectionStringEnvironmentVariable} " +
            $"environment variable or create {LocalSettingsFileName}.");
    }

    public static async Task<NpgsqlConnection> OpenAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = Create();

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
