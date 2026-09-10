using NetArchTest.Rules;

namespace AspNetDesignPatterns.Api.Tests.Architecture;

/// <summary>
/// The repo's first non-negotiable convention, enforced: everything above the client layer
/// speaks <c>Result</c>, and only the client throws.
/// </summary>
[TestFixture]
public class ResultPatternTests
{
    private static PredicateList FeatureClasses() =>
        ArchitectureRules.InProductionCode()
            .That().ResideInNamespaceStartingWith(ArchitectureRules.FeaturesNamespace).And().AreClasses();

    [Test]
    public void Services_return_a_Result_from_every_public_method()
    {
        // Arrange & Act
        TestResult result = FeatureClasses()
            .And().HaveNameEndingWith("Service", StringComparison.Ordinal)
            .Should().MeetCustomRule(CustomRules.ReturnResult)
            .GetResult();

        // Assert
        ArchitectureRules.Assert(result, "a service must not hand a bare value or a raw task upward");
    }

    [Test]
    public void Request_handlers_return_a_Result_from_every_public_method()
    {
        // Arrange & Act
        TestResult result = FeatureClasses()
            .And().HaveNameEndingWith("Handler", StringComparison.Ordinal)
            .Should().MeetCustomRule(CustomRules.ReturnResult)
            .GetResult();

        // Assert
        ArchitectureRules.Assert(result, "a handler is the edge of the Result world");
    }

    [Test]
    public void Services_and_handlers_never_throw()
    {
        // Arrange & Act
        TestResult result = FeatureClasses()
            .And().HaveNameEndingWith("Service", StringComparison.Ordinal)
            .Or().HaveNameEndingWith("Handler", StringComparison.Ordinal)
            .Should().MeetCustomRule(CustomRules.NeverThrow)
            .GetResult();

        // Assert
        ArchitectureRules.Assert(result, "above the client, a failure is a returned Error — not an exception");
    }

    [Test]
    public void Only_client_types_throw_the_feature_exceptions()
    {
        // Arrange & Act — the exception type itself is fine; what matters is who raises it.
        TestResult result = FeatureClasses()
            .And().DoNotHaveNameEndingWith("Client", StringComparison.Ordinal)
            .And().DoNotHaveNameEndingWith("Exception", StringComparison.Ordinal)
            .Should().MeetCustomRule(CustomRules.NeverThrow)
            .GetResult();

        // Assert
        ArchitectureRules.Assert(result, "transport failures become exceptions in the client and nowhere else");
    }

    [Test]
    public void The_client_layer_is_where_throwing_happens()
    {
        // Not a rule — a check that the "never throw" rule can actually see a throw. The client
        // raises WeatherClientException, so applying the rule to *Client types must fail.
        // Arrange & Act
        TestResult result = FeatureClasses()
            .And().HaveNameEndingWith("Client", StringComparison.Ordinal)
            .Should().MeetCustomRule(CustomRules.NeverThrow)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeFalse("the client turns a transport failure into an exception");
    }
}
