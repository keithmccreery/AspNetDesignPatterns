using AspNetDesignPatterns.Api.Shared.Handlers;
using AspNetDesignPatterns.Api.Shared.Results;

using Microsoft.Extensions.DependencyInjection;

namespace AspNetDesignPatterns.Api.Shared.Tests.DependencyInjection.RequestHandlers;

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
}
