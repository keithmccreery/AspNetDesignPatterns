using FluentValidation;

using KAM.Common.DependencyInjection;
using KAM.Common.Handlers;
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
    public void Endpoints_do_not_reach_past_the_handler_into_services_or_clients()
    {
        // Arrange & Act
        TestResult result = ArchitectureRules.InProductionCode()
            .That().ImplementInterface(typeof(IEndpoint))
            .ShouldNot().MeetCustomRule(CustomRules.DependsOnAServiceOrClient)
            .GetResult();

        // Assert
        ArchitectureRules.AssertRuleHolds(result, "an endpoint binds + validates + delegates to IRequestHandler — nothing lower");
    }
}
