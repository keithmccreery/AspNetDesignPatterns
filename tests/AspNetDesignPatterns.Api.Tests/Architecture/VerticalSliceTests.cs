using NetArchTest.Rules;

namespace AspNetDesignPatterns.Api.Tests.Architecture;

/// <summary>
/// The one remaining slice boundary this suite needs to enforce with a test: a feature is
/// self-contained. "Shared/ and the DependencyInjection plumbing never depend on a feature"
/// used to be two more rules here, checked the same way — but since both moved to
/// <c>KAM.Common</c> (a separate project with no reference back to this one), that dependency
/// is now a compile error, not just a test failure. A rule this suite
/// can't even express anymore is a stronger guarantee than one it enforces at runtime, so it
/// was deleted rather than left in place checking nothing (see the Architecture/ README).
/// </summary>
[TestFixture]
public class VerticalSliceTests
{
    private static IEnumerable<string> Features => ArchitectureRules.FeatureNamespaces;

    [TestCaseSource(nameof(Features))]
    public void A_feature_does_not_depend_on_another_feature(string feature)
    {
        // Arrange
        string[] otherFeatures = [.. Features.Where(other => !string.Equals(other, feature, StringComparison.Ordinal))];

        if (otherFeatures.Length == 0)
        {
            Assert.Pass($"{feature} is the only slice; the rule is armed for when a second one lands.");
        }

        // Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().ResideInNamespaceStartingWith(feature)
            .ShouldNot().HaveDependencyOnAny(otherFeatures)
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, $"{feature} must not reach into another feature slice");
    }
}
