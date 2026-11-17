using Dapper;

using KAM.Common.Data;
using KAM.Common.DependencyInjection;

using Microsoft.AspNetCore.OpenApi;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// Library-feature DI: the SQLite connection factory, the unit of work, the repository and
/// service, schema migration at startup, and the database readiness check. Discovered via <see cref="IDependency"/>.
/// </summary>
internal sealed class LibraryDependencies : IDependency
{
    public void RegisterServices(IServiceCollection services)
    {
        // Process-wide Dapper settings; re-adding replaces the same handler, so this is idempotent.
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
        SqlMapper.AddTypeHandler(new IdTypeHandler<BookId>(value => new BookId(value), id => id.Value));
        SqlMapper.AddTypeHandler(new IdTypeHandler<LoanId>(value => new LoanId(value), id => id.Value));

        services.TryAddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddUnitOfWork();
        services.AddScoped<ILibraryRepository, LibraryRepository>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddHostedService<LibraryDatabaseInitializer>();

        // Every OpenAPI document (one per API version): document the typed ids as plain integers.
        services.ConfigureAll<OpenApiOptions>(options => options.AddSchemaTransformer<IdOpenApiSchemaTransformer>());

        // Unhealthy, not Degraded (contrast WeatherProviderHealthCheck): this is the app's own
        // database, and a down database means the instance can't serve its writes.
        services.AddHealthChecks().AddCheck<LibraryDatabaseHealthCheck>(
            "library-database",
            failureStatus: HealthStatus.Unhealthy,
            tags: [KAM.Common.HealthChecks.HealthCheckTag.Ready]);
    }
}
