using KAM.Common.DependencyInjection;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

using NSubstitute;
using NSubstitute.ClearExtensions;

namespace KAM.Common.Tests.DependencyInjection.ExceptionHandling;

[TestFixture]
public class GlobalExceptionHandlerTests
{
    private readonly IProblemDetailsService _problemDetails = Substitute.For<IProblemDetailsService>();

    private GlobalExceptionHandler CreateHandler() =>
        new(_problemDetails, Microsoft.Extensions.Logging.Abstractions.NullLogger<GlobalExceptionHandler>.Instance);

    [SetUp]
    public void Reset() => _problemDetails.ClearSubstitute();

    [Test]
    public async Task Sets_a_500_status_and_writes_a_ProblemDetails()
    {
        // Arrange
        _problemDetails.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(true);
        DefaultHttpContext httpContext = new();

        // Act
        bool handled = await CreateHandler().TryHandleAsync(
            httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            handled.Should().BeTrue();
            httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            await _problemDetails.Received(1).TryWriteAsync(Arg.Is<ProblemDetailsContext>(c =>
                c.ProblemDetails.Status == 500 && c.Exception!.Message == "boom"));
        }
    }

    [Test]
    public async Task Reports_not_handled_when_the_ProblemDetails_service_declines()
    {
        // Arrange
        _problemDetails.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(false);

        // Act
        bool handled = await CreateHandler().TryHandleAsync(
            new DefaultHttpContext(), new InvalidOperationException("x"), CancellationToken.None);

        // Assert
        handled.Should().BeFalse();
    }
}
