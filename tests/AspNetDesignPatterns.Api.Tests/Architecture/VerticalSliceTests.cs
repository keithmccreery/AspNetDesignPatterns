using NetArchTest.Rules;

namespace AspNetDesignPatterns.Api.Tests.Architecture;

/// <summary>
/// The slice boundaries: a feature is self-contained, and the cross-cutting layers below it
/// never reach back up into a feature.
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

    [Test]
    public void Shared_does_not_depend_on_any_feature()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().ResideInNamespaceStartingWith(ArchitectureRules.SharedNamespace)
            .ShouldNot().HaveDependencyOnAny(ArchitectureRules.FeaturesNamespace)
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, "Shared/ is a building block — it cannot know about a feature");
    }

    [Test]
    public void DependencyInjection_plumbing_does_not_depend_on_any_feature()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().ResideInNamespace(ArchitectureRules.DependencyInjectionNamespace)
            .ShouldNot().HaveDependencyOnAny(ArchitectureRules.FeaturesNamespace)
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, "the reflection-registration plumbing is generic — it cannot know about a feature");
    }
}
