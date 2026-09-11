using FluentValidation;

using KAM.Common.FluentValidations;
using KAM.Common.Results;

namespace KAM.Common.Tests.FluentValidations;

[TestFixture]
public class ValidationResultExtensionsTests
{
    private sealed record Person(string Name, int Age);

    private sealed class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Age).GreaterThan(0);
        }
    }

    private readonly PersonValidator _validator = new();

    [Test]
    public void A_valid_instance_becomes_a_success()
    {
        // Arrange & Act & Assert
        _validator.ValidateAsResult(new Person("Ada", 30)).IsSuccess.Should().BeTrue();
    }

    [Test]
    public void An_invalid_instance_becomes_a_ValidationError_keeping_per_field_messages()
    {
        // Arrange & Act
        Result result = _validator.ValidateAsResult(new Person("", -1));

        // Assert
        ValidationError error = result.Error.Should().BeOfType<ValidationError>().Subject;

        using (new AssertionScope())
        {
            result.IsFailure.Should().BeTrue();
            error.Type.Should().Be(ErrorType.Validation);
            error.Failures.Should().ContainKeys("Name", "Age");
        }
    }

    [Test]
    public async Task ValidateAsResultAsync_mirrors_the_sync_behaviour()
    {
        // Arrange & Act & Assert
        (await _validator.ValidateAsResultAsync(new Person("Ada", 30))).IsSuccess.Should().BeTrue();
        (await _validator.ValidateAsResultAsync(new Person("", 0))).IsFailure.Should().BeTrue();
    }
}
