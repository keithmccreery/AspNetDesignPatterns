using FluentValidation;

using KAM.Common.DependencyInjection;
using KAM.Common.Handlers;
using KAM.Common.Pipeline;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NetArchTest.Rules;

namespace AspNetDesignPatterns.Api.Tests.Architecture;

/// <summary>
/// An architecture rule over an empty type set passes vacuously — a typo in a namespace
/// string or an interface NetArchTest cannot resolve would make a rule silently meaningless.
/// These tests assert the predicates the other fixtures rely on actually select something.
/// </summary>
[TestFixture]
public class SanityTests
{
    [Test]
    public void The_feature_slices_are_discovered()
    {
        // Arrange & Act & Assert
        ArchitectureRules.FeatureNamespaces.Should().Contain(ArchitectureRules.FeaturesNamespace + ".Weather");
    }

    [TestCase(typeof(IEndpoint))]
    [TestCase(typeof(IDependency))]
    [TestCase(typeof(IHealthCheck))]
    [TestCase(typeof(IRequestHandler<,>))]
    [TestCase(typeof(IPipelineStep<>))]
    public void The_role_interface_is_implemented_by_at_least_one_production_type(Type roleInterface)
    {
        // Arrange & Act
        IEnumerable<Type> matches = ArchitectureRules.InProductionCode()
            .That().ImplementInterface(roleInterface)
            .GetTypes();

        // Assert
        matches.Should().NotBeEmpty("the naming rule for {0} would otherwise pass vacuously", roleInterface.Name);
    }

    [TestCase(typeof(SettingsBase<>))]
    [TestCase(typeof(AbstractValidator<>))]
    public void The_role_base_class_is_inherited_by_at_least_one_production_type(Type roleBase)
    {
        // Arrange & Act
        IEnumerable<Type> matches = ArchitectureRules.InProductionCode()
            .That().Inherit(roleBase)
            .GetTypes();

        // Assert
        matches.Should().NotBeEmpty("the naming rule for {0} would otherwise pass vacuously", roleBase.Name);
    }

    [TestCase("Service")]
    [TestCase("Handler")]
    [TestCase("Client")]
    public void At_least_one_feature_type_is_named_with_each_role_suffix(string suffix)
    {
        // Arrange & Act
        IEnumerable<Type> matches = ArchitectureRules.InProductionCode()
            .That().ResideInNamespaceStartingWith(ArchitectureRules.FeaturesNamespace)
            .And().HaveNameEndingWith(suffix, StringComparison.Ordinal)
            .GetTypes();

        // Assert
        matches.Should().NotBeEmpty("the Result / throwing rules for '…{0}' would otherwise pass vacuously", suffix);
    }
}
