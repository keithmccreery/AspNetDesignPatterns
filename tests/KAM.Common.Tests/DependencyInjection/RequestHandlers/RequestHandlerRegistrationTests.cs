using KAM.Common.Handlers;
using KAM.Common.Results;

using Microsoft.Extensions.DependencyInjection;

namespace KAM.Common.Tests.DependencyInjection.RequestHandlers;

[TestFixture]
public class RequestHandlerRegistrationTests
{
    public sealed record Ping(int N);

    public sealed class PingHandler : IRequestHandler<Ping, int>
    {
        public Task<Result<int>> HandleAsync(Ping request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(request.N));
    }

    private static readonly System.Reflection.Assembly ThisAssembly = typeof(RequestHandlerRegistrationTests).Assembly;

    [Test]
    public void AddRequestHandlers_registers_closed_handler_interfaces_as_scoped()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddRequestHandlers(ThisAssembly);

        // Assert
        using (new AssertionScope())
        {
            IRequestHandler<Ping, int>? handler = services.BuildServiceProvider().GetService<IRequestHandler<Ping, int>>();
            handler.Should().BeOfType<PingHandler>();
            services.First(d => d.ServiceType == typeof(IRequestHandler<Ping, int>)).Lifetime
                .Should().Be(ServiceLifetime.Scoped);
        }
    }

    [Test]
    public void AddRequestHandlers_with_no_assemblies_falls_back_to_the_calling_assembly()
    {
        // Arrange — see DependencyRegistrationTests's equivalent test for why this matters.
        ServiceCollection services = new();

        // Act
        services.AddRequestHandlers();

        // Assert
        services.BuildServiceProvider().GetService<IRequestHandler<Ping, int>>().Should().BeOfType<PingHandler>();
    }
}
