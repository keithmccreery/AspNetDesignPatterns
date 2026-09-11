using AspNetDesignPatterns.Api.DependencyInjection;

using FluentValidation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Shared.Tests.DependencyInjection.Settings;

/// <summary>
/// <see cref="SettingsBase{T}"/> binds a settings class to its <c>Section</c> and validates
/// on startup — FluentValidation when a validator is registered, DataAnnotations otherwise.
/// </summary>
[TestFixture]
public class SettingsBaseTests
{
    // Has a FluentValidation validator -> validated via FluentValidation.
    private sealed class FluentSettings : SettingsBase<FluentSettings>
    {
        public static string Section => "Fluent";

        public string Name { get; init; } = string.Empty;
    }

    private sealed class FluentSettingsValidator : AbstractValidator<FluentSettings>
    {
        public FluentSettingsValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    // No validator -> validated via DataAnnotations.
    private sealed class DataAnnotationsSettings : SettingsBase<DataAnnotationsSettings>
    {
        public static string Section => "DataAnnotations";

        [System.ComponentModel.DataAnnotations.Range(1, 10)]
        public int Count { get; init; }
    }

    // Declares no Section -> RegisterSettings must fail fast.
    private sealed class NoSectionSettings : SettingsBase<NoSectionSettings>;

    private static IOptions<T> Resolve<T>(Dictionary<string, string?> config, bool withValidator)
        where T : class, ISettings, new()
    {
        ServiceCollection services = new();
        if (withValidator)
        {
            services.AddSingleton<IValidator<FluentSettings>, FluentSettingsValidator>();
        }

        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        services.AddSingleton<IConfiguration>(configuration);

        new T().RegisterSettings(services);

        return services.BuildServiceProvider().GetRequiredService<IOptions<T>>();
    }

    [Test]
    public void FluentValidation_path_accepts_valid_settings()
    {
        // Arrange & Act
        IOptions<FluentSettings> options = Resolve<FluentSettings>(new() { ["Fluent:Name"] = "ok" }, withValidator: true);

        // Assert
        options.Value.Name.Should().Be("ok");
    }

    [Test]
    public void FluentValidation_path_rejects_invalid_settings_with_a_detailed_message()
    {
        // Arrange & Act
        IOptions<FluentSettings> options = Resolve<FluentSettings>(new() { ["Fluent:Name"] = "" }, withValidator: true);

        // Assert
        options.Invoking(o => o.Value).Should().Throw<OptionsValidationException>()
            .WithMessage("*FluentSettings.Name*");
    }

    [Test]
    public void DataAnnotations_path_is_used_when_no_validator_is_registered()
    {
        // Arrange & Act
        IOptions<DataAnnotationsSettings> options = Resolve<DataAnnotationsSettings>(
            new() { ["DataAnnotations:Count"] = "99" }, withValidator: false);

        // Assert
        options.Invoking(o => o.Value).Should().Throw<OptionsValidationException>()
            .WithMessage("*Count*");
    }

    [Test]
    public void DataAnnotations_path_accepts_valid_settings()
    {
        // Arrange & Act
        IOptions<DataAnnotationsSettings> options = Resolve<DataAnnotationsSettings>(
            new() { ["DataAnnotations:Count"] = "5" }, withValidator: false);

        // Assert
        options.Value.Count.Should().Be(5);
    }

    [Test]
    public void A_settings_class_without_a_Section_fails_fast_on_registration()
    {
        // Arrange
        Action act = () => new NoSectionSettings().RegisterSettings(new ServiceCollection());

        // Act & Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Section*");
    }
}
