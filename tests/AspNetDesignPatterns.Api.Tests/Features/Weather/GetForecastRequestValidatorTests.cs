using AspNetDesignPatterns.Api.Features.Weather;

using FluentValidation.TestHelper;

namespace AspNetDesignPatterns.Api.Tests.Features.Weather;

[TestFixture]
public class GetForecastRequestValidatorTests
{
    private readonly GetForecastRequestValidator _validator = new();

    [Test]
    public void Accepts_a_request_within_range()
    {
        TestValidationResult<GetForecastRequest> result = _validator.TestValidate(new GetForecastRequest(52.5, 13.4, 5));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestCase(-91)]
    [TestCase(91)]
    public void Rejects_out_of_range_latitude(double latitude)
    {
        TestValidationResult<GetForecastRequest> result = _validator.TestValidate(new GetForecastRequest(latitude, 0, 3));

        result.ShouldHaveValidationErrorFor(x => x.Latitude);
    }

    [TestCase(-181)]
    [TestCase(181)]
    public void Rejects_out_of_range_longitude(double longitude)
    {
        TestValidationResult<GetForecastRequest> result = _validator.TestValidate(new GetForecastRequest(0, longitude, 3));

        result.ShouldHaveValidationErrorFor(x => x.Longitude);
    }

    [TestCase(0)]
    [TestCase(17)]
    public void Rejects_out_of_range_days(int days)
    {
        TestValidationResult<GetForecastRequest> result = _validator.TestValidate(new GetForecastRequest(0, 0, days));

        result.ShouldHaveValidationErrorFor(x => x.Days);
    }
}
