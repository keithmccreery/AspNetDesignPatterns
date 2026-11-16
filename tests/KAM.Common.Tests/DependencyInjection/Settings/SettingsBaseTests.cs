using FluentValidation;

using KAM.Common.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KAM.Common.Tests.DependencyInjection.Settings;

/// <summary>
/// <see cref="SettingsBase{T}"/> binds a settings class to its <c>Section</c> and validates
/// on startup — FluentValidation when a validator is registered, DataAnnotations otherwise.
/// (A class with no <c>Section</c> doesn't compile, so there's no test for that.)
/// </summary>
[TestFixture]
public class SettingsBaseTests
{
    // Has a FluentValidation validator -> validated via FluentValidation.
    private sealed class FluentSettings : SettingsBase<FluentSettings>, ISettingsSection
    {
        public static string Section => "Fluent";

        public string Name { get; init; } = string.Empty;
    }

    private sealed class FluentSettingsValidator : AbstractValidator<FluentSettings>
    {
        public FluentSettingsValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    // No validator -> validated via DataAnnotations.
    private sealed class DataAnnotationsSettings : SettingsBase<DataAnnotationsSettings>, ISettingsSection
    {
        public static string Section => "DataAnnotations";

        [System.ComponentModel.DataAnnotations.Range(1, 10)]
        public int Count { get; init; }
    }

    // Section compiles but is empty -> RegisterSettings must fail fast. Internal (not private)
    // so SettingsRegistrationTests can name it.
    internal sealed class EmptySectionSettings : SettingsBase<EmptySectionSettings>, ISettingsSection
    {
        public static string Section => string.Empty;
    }

    // Names a different (valid) settings type -> compiles, so RegisterSettings must catch it.
    private sealed class MisnamedSettings : SettingsBase<DataAnnotationsSettings>, ISettingsSection
    {
        public static string Section => "Misnamed";
    }

    private static IOptions<T> Resolve<T>(
        Dictionary<string, string?> config,
        bool withValidator,
        bool validatorRegisteredAfterSettings = false)
        where T : class, ISettings, new()
    {
        ServiceCollection services = new();
        if (withValidator && !validatorRegisteredAfterSettings)
        {
            services.AddScoped<IValidator<FluentSettings>, FluentSettingsValidator>();
        }

        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        services.AddSingleton<IConfiguration>(configuration);

        new T().RegisterSettings(services);

        if (withValidator && validatorRegisteredAfterSettings)
        {
            services.AddScoped<IValidator<FluentSettings>, FluentSettingsValidator>();
        }

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
    public void FluentValidation_path_is_used_even_when_the_validator_is_registered_after_the_settings()
    {
        // Arrange & Act — the validator is looked up when validation runs, so registering it
        // last still selects FluentValidation (DataAnnotations has no rule for Name and would
        // accept the empty value).
        IOptions<FluentSettings> options = Resolve<FluentSettings>(
            new() { ["Fluent:Name"] = "" }, withValidator: true, validatorRegisteredAfterSettings: true);

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
    public void A_settings_class_with_an_empty_Section_fails_fast_on_registration()
    {
        // Arrange
        Action act = () => new EmptySectionSettings().RegisterSettings(new ServiceCollection());

        // Act & Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*EmptySectionSettings.Section*");
    }

    [Test]
    public void A_settings_class_naming_another_type_as_T_fails_fast_on_registration()
    {
        // Arrange
        Action act = () => new MisnamedSettings().RegisterSettings(new ServiceCollection());

        // Act & Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*SettingsBase<MisnamedSettings>*");
    }

    [Test]
    public void Registering_the_same_settings_twice_adds_one_validator()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        new DataAnnotationsSettings().RegisterSettings(services);
        new DataAnnotationsSettings().RegisterSettings(services);

        // Assert
        services.Count(d => d.ServiceType == typeof(IValidateOptions<DataAnnotationsSettings>)).Should().Be(1);
    }
}
