using System.Threading;
using System.Threading.Tasks;

namespace SIKOPY.Database;

public static class PostgresHealthCheck
{
    public static async Task<bool> CanConnectAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result switch
        {
            int value => value == 1,
            long value => value == 1,
            _ => false
        };
    }
}
