using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace SIKOPY.Database;

public static class PostgresConnectionFactory
{
    private const string ConnectionStringEnvironmentVariable =
        "SIKOPY_DATABASE_CONNECTION_STRING";

    public static NpgsqlConnection Create()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The {ConnectionStringEnvironmentVariable} environment variable is not configured.");
        }

        return new NpgsqlConnection(connectionString);
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
