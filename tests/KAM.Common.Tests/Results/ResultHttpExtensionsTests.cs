using System.Net;

using KAM.Common.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace KAM.Common.Tests.Results;

[TestFixture]
public class ResultHttpExtensionsTests
{
    [TestCase(ErrorType.Validation, HttpStatusCode.BadRequest)]
    [TestCase(ErrorType.Unauthorized, HttpStatusCode.Unauthorized)]
    [TestCase(ErrorType.Forbidden, HttpStatusCode.Forbidden)]
    [TestCase(ErrorType.NotFound, HttpStatusCode.NotFound)]
    [TestCase(ErrorType.Conflict, HttpStatusCode.Conflict)]
    [TestCase(ErrorType.Upstream, HttpStatusCode.BadGateway)]
    [TestCase(ErrorType.Failure, HttpStatusCode.InternalServerError)]
    public void Maps_each_error_type_to_its_status_code(ErrorType type, HttpStatusCode expected)
    {
        // Act & Assert
        type.ToStatusCode().Should().Be((int) expected);
    }

    [TestCase(ErrorType.Validation, "One or more validation errors occurred.")]
    [TestCase(ErrorType.Unauthorized, "Authentication is required.")]
    [TestCase(ErrorType.Forbidden, "You do not have access to this resource.")]
    [TestCase(ErrorType.NotFound, "The requested resource was not found.")]
    [TestCase(ErrorType.Conflict, "The request conflicts with the current state.")]
    [TestCase(ErrorType.Upstream, "An upstream dependency failed.")]
    [TestCase(ErrorType.Failure, "An unexpected error occurred.")]
    public void Maps_each_error_type_to_its_problem_title(ErrorType type, string expectedTitle)
    {
        // Arrange — only Validation/NotFound/Upstream ever actually occur in the sample app, so
        // the rest of this mapping was untested; every ErrorType has its own factory to drive it.
        Error error = type switch
        {
            ErrorType.Validation => Error.Validation("X", "x"),
            ErrorType.Unauthorized => Error.Unauthorized("X", "x"),
            ErrorType.Forbidden => Error.Forbidden("X", "x"),
            ErrorType.NotFound => Error.NotFound("X", "x"),
            ErrorType.Conflict => Error.Conflict("X", "x"),
            ErrorType.Upstream => Error.Upstream("X", "x"),
            _ => Error.Failure("X", "x"),
        };

        // Act
        ProblemHttpResult problem = error.ToProblem().Should().BeOfType<ProblemHttpResult>().Subject;

        // Assert
        problem.ProblemDetails.Title.Should().Be(expectedTitle);
    }

    [Test]
    public void Failure_result_becomes_a_ProblemDetails_carrying_the_error_code()
    {
        // Arrange & Act
        IResult httpResult = Result.Failure<int>(Error.NotFound("Widget.Missing", "gone")).ToHttpResult();

        // Assert
        ProblemHttpResult problem = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;

        using (new AssertionScope())
        {
            problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            problem.ProblemDetails.Detail.Should().Be("gone");
            problem.ProblemDetails.Extensions.Should().ContainKey("errorCode")
                .WhoseValue.Should().Be("Widget.Missing");
        }
    }

    [Test]
    public void ValidationError_becomes_a_400_validation_problem_with_the_fields()
    {
        // Arrange
        Dictionary<string, string[]> failures = new() { ["Age"] = ["must be positive"] };

        // Act
        IResult httpResult = Result.Failure<int>(new ValidationError(failures)).ToHttpResult();

        // Assert
        ValidationProblem problem = httpResult.Should().BeOfType<ValidationProblem>().Subject;

        using (new AssertionScope())
        {
            problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            problem.ProblemDetails.Errors.Should().ContainKey("Age");
        }
    }

    [Test]
    public void Success_result_becomes_200_with_the_value()
    {
        // Arrange & Act & Assert
        Result.Success(7).ToHttpResult().Should().BeOfType<Ok<int>>().Which.Value.Should().Be(7);
    }

    [Test]
    public void Non_generic_success_becomes_204()
    {
        // Arrange & Act & Assert
        Result.Success().ToHttpResult().Should().BeOfType<NoContent>();
    }

    [Test]
    public void A_custom_onSuccess_projection_is_used_when_supplied()
    {
        // Arrange & Act & Assert
        Result.Success(5).ToHttpResult(v => TypedResults.Text($"value={v}"))
            .Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ContentHttpResult>();
    }
}
