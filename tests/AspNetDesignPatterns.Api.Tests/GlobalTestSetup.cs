using AspNetDesignPatterns.Api.Tests.Integration;

namespace AspNetDesignPatterns.Api.Tests;

/// <summary>
/// Owns the single in-memory <see cref="WeatherApiFactory"/> shared by every integration test.
/// One host keeps Serilog's two-stage initialization (which freezes the static logger on
/// build) deterministic, and keeps the suite fast.
/// </summary>
[SetUpFixture]
public sealed class GlobalTestSetup
{
    public static WeatherApiFactory Factory { get; private set; } = null!;

    [OneTimeSetUp]
    public void StartHost() => Factory = new WeatherApiFactory();

    [OneTimeTearDown]
    public void StopHost() => Factory.Dispose();
}
