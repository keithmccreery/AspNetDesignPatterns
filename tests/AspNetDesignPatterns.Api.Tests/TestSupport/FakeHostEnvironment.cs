using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AspNetDesignPatterns.Api.Tests.TestSupport;

/// <summary>Minimal <see cref="IHostEnvironment"/> for unit tests that only care about the environment name.</summary>
public sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } =
        new PhysicalFileProvider(AppContext.BaseDirectory);
}
