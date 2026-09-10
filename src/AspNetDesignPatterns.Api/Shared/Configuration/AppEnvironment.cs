namespace AspNetDesignPatterns.Api.Shared.Configuration;

/// <summary>
/// A small, eagerly-evaluated view of "where and how am I running", derived from the hosting
/// environment and a couple of well-known environment variables. Built once in
/// <c>Program.cs</c> before the container is configured, then registered as a singleton, so
/// startup code (Kestrel, Scalar gating) and services can branch on it without re-reading
/// environment state.
/// </summary>
/// <remarks>
/// A trimmed version of the multi-environment model used in production apps (which also carry
/// account/region/ephemeral-slot identity). Kept minimal here on purpose.
/// </remarks>
public sealed class AppEnvironment
{
    public AppEnvironment(IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        EnvironmentName = hostEnvironment.EnvironmentName;
        IsDevelopment = hostEnvironment.IsDevelopment();
        IsProduction = hostEnvironment.IsProduction();

        // Set by the .NET base images; the canonical "am I in a container" signal.
        IsContainerized =
            string.Equals(
                Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
                "true",
                StringComparison.OrdinalIgnoreCase);
    }

    public string EnvironmentName { get; }

    public bool IsDevelopment { get; }

    public bool IsProduction { get; }

    public bool IsContainerized { get; }
}
