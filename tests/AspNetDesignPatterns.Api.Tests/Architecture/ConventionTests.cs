using FluentValidation;

using KAM.Common.DependencyInjection;
using KAM.Common.Pipeline;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NetArchTest.Rules;

namespace AspNetDesignPatterns.Api.Tests.Architecture;

/// <summary>
/// "Copy the shape of <c>Features/Weather/</c>" only works if the shapes are consistent. Each
/// role type is named for its role and sealed; endpoints stay thin HTTP adapters.
/// </summary>
[TestFixture]
public class ConventionTests
{
    [Test]
    public void Endpoints_are_named_Endpoint_sealed_and_internal()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().ImplementInterface(typeof(IEndpoint))
            .Should().HaveNameEndingWith("Endpoint", StringComparison.Ordinal)
            .And().BeSealed()
            .And().NotBePublic()
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, "an endpoint is an internal, sealed IEndpoint named …Endpoint");
    }

    [Test]
    public void Request_handlers_are_named_Handler_and_sealed()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().ImplementInterface(typeof(IRequestHandler<,>))
            .Should().HaveNameEndingWith("Handler", StringComparison.Ordinal)
            .And().BeSealed()
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, "a handler implements IRequestHandler<,> and is named …Handler");
    }

    [Test]
    public void Pipeline_steps_are_named_Step()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().ImplementInterface(typeof(IPipelineStep<>))
            .Should().HaveNameEndingWith("Step", StringComparison.Ordinal)
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, "a pipeline step implements IPipelineStep<> and is named …Step");
    }

    [Test]
    public void Settings_classes_are_named_Options()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().Inherit(typeof(SettingsBase<>))
            .Should().HaveNameEndingWith("Options", StringComparison.Ordinal)
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, "a settings class inherits SettingsBase<T> and is named …Options");
    }

    [Test]
    public void Validators_are_named_Validator()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().Inherit(typeof(AbstractValidator<>))
            .Should().HaveNameEndingWith("Validator", StringComparison.Ordinal)
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, "a FluentValidation validator is named …Validator");
    }

    [Test]
    public void Health_checks_are_named_HealthCheck()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().ImplementInterface(typeof(IHealthCheck))
            .Should().HaveNameEndingWith("HealthCheck", StringComparison.Ordinal)
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, "an IHealthCheck is named …HealthCheck");
    }

    [Test]
    public void Dependency_modules_are_named_Dependencies()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().ImplementInterface(typeof(IDependency))
            .Should().HaveNameEndingWith("Dependencies", StringComparison.Ordinal)
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, "a DI module implements IDependency and is named …Dependencies");
    }

    [Test]
    public void Endpoints_do_not_reach_past_the_handler_into_services_clients_or_repositories()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().ImplementInterface(typeof(IEndpoint))
            .ShouldNot().MeetCustomRule(CustomRules.DependsOnALayerBelowTheHandler)
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, "an endpoint binds + validates + delegates to IRequestHandler — nothing lower");
    }

    [Test]
    public void Handlers_and_pipeline_steps_reach_the_gateway_only_through_a_service()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().ImplementInterface(typeof(IRequestHandler<,>))
            .Or().ImplementInterface(typeof(IPipelineStep<>))
            .ShouldNot().MeetCustomRule(CustomRules.DependsOnTheGatewayLayer)
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(
            result,
            "a client, repository or unit of work is the service's to use — that's where its exceptions become a Result");
    }

    [Test]
    public void The_gateway_rule_can_see_a_gateway_dependency()
    {
        // Not a rule — a check that the rule above can fail: LibraryService uses the repository
        // and the unit of work, so applying the rule to services must find it.
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().HaveNameEndingWith("Service", StringComparison.Ordinal)
            .ShouldNot().MeetCustomRule(CustomRules.DependsOnTheGatewayLayer)
            .GetResult();

        // Assert
        result.FailingTypeNames.Should().Contain(name => name.EndsWith(".LibraryService", StringComparison.Ordinal));
    }
}
