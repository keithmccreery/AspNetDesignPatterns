using AspNetDesignPatterns.Api.Shared.FluentValidations;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AspNetDesignPatterns.Api.Shared.Tests.FluentValidations;

[TestFixture]
public class ValidationFilterTests
{
    private sealed record Command(string Value);

    private sealed class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.Value).NotEmpty().WithMessage("Value is required.");
    }

    private static DefaultEndpointFilterInvocationContext ContextWith(params object[] arguments) =>
        new(new DefaultHttpContext(), arguments);

    [Test]
    public async Task Calls_the_handler_when_the_request_is_valid()
    {
        // Arrange
        ValidationFilter<Command> filter = new(new CommandValidator());
        bool handlerRan = false;

        // Act
        object? result = await filter.InvokeAsync(
            ContextWith(new Command("ok")),
            _ => { handlerRan = true; return ValueTask.FromResult<object?>("handled"); });

        // Assert
        using (new AssertionScope())
        {
            handlerRan.Should().BeTrue();
            result.Should().Be("handled");
        }
    }

    [Test]
    public async Task Short_circuits_to_a_validation_problem_when_the_request_is_invalid()
    {
        // Arrange
        ValidationFilter<Command> filter = new(new CommandValidator());
        bool handlerRan = false;

        // Act
        object? result = await filter.InvokeAsync(
            ContextWith(new Command("")),
            _ => { handlerRan = true; return ValueTask.FromResult<object?>("handled"); });

        // Assert
        using (new AssertionScope())
        {
            handlerRan.Should().BeFalse();
            result.Should().BeOfType<ValidationProblem>()
                .Which.ProblemDetails.Errors.Should().ContainKey("Value");
        }
    }

    [Test]
    public async Task Passes_through_when_no_validator_is_registered()
    {
        // Arrange
        ValidationFilter<Command> filter = new(validator: null);

        // Act
        object? result = await filter.InvokeAsync(
            ContextWith(new Command("")),
            _ => ValueTask.FromResult<object?>("handled"));

        // Assert
        result.Should().Be("handled");
    }

    [Test]
    public async Task Passes_through_when_the_argument_of_the_expected_type_is_absent()
    {
        // Arrange
        ValidationFilter<Command> filter = new(new CommandValidator());

        // Act
        object? result = await filter.InvokeAsync(
            ContextWith("not-a-command", 42),
            _ => ValueTask.FromResult<object?>("handled"));

        // Assert
        result.Should().Be("handled");
    }
}
