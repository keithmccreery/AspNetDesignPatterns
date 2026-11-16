using System.Data.Common;

using KAM.Common.Data;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// Readiness check for the library database: opens a connection and runs <c>SELECT 1</c>. Cheap
/// and local, so — unlike <c>WeatherProviderHealthCheck</c> — it isn't cached.
/// </summary>
internal sealed class LibraryDatabaseHealthCheck : IHealthCheck
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LibraryDatabaseHealthCheck(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("The library database is reachable.");
        }
        catch (DbException ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "The library database is unreachable.", ex);
        }
    }
}
